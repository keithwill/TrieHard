using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TrieHard.PrefixLookup;

namespace TrieHard.Collections;

/// <summary>
/// A typed Radix Tree Node containing a payload value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity stored within this node</typeparam>
internal class RadixTreeNode<T> : IDisposable
{
    public byte[] FullKey => KeySegment.Array!;
    private ArraySegment<byte> KeySegment = ArraySegment<byte>.Empty;
    public RadixTreeNode<T>[] childrenBuffer = EmptyNodes;
    private Span<RadixTreeNode<T>> Children => childrenBuffer.AsSpan(0, ChildCount);
    private byte ChildCount = 0;
    public T? Value;

    private byte[] childrenFirstKeyBytesBuffer = EmptyBytes;
    private Span<byte> ChildrenFirstKeyBytes => childrenFirstKeyBytesBuffer.AsSpan(0, ChildCount);

    public static readonly RadixTreeNode<T>[] EmptyNodes = Array.Empty<RadixTreeNode<T>>();
    public static readonly byte[] EmptyBytes = [];

    /// <summary>
    /// Steps down the child nodes of this node, creating missing key segments as necessary before
    /// setting the value on the last matching child node.
    /// </summary>
    /// <param name="rootNode">A reference to the root node. Necessary for one fringe concurrency case</param>
    /// <param name="key">The full key to set the value on</param>
    /// <param name="value">The value to set on the matching node</param>
    /// <param name="atomic">
    /// Can be passed as false to skip expensive atomic operations if the caller of this method can ensure
    /// no other threads will be accessing the trie concurrently. It can corrupt the trie if passed as
    /// false when synchronized access is not assured.</param>
    public void SetValue(ref RadixTreeNode<T> rootNode, ReadOnlySpan<byte> key, T? value)
    {
        ref RadixTreeNode<T> searchNode = ref rootNode;
        var searchKey = key;
        int keyOffset = 0;
        byte[]? keyBytes = null;

        // Every branch of this while statement must either set a new descendant search node or return
        while (true)
        {
            ReadOnlySpan<RadixTreeNode<T>> searchChildren = searchNode.Children;
            searchKey = key.Slice(keyOffset);
            var searchFirstByte = searchKey[0];

            var matchingIndex = searchNode.ChildrenFirstKeyBytes.BinarySearch(searchFirstByte);

            if (matchingIndex > -1)
            {
                ref RadixTreeNode<T> matchingChild = ref searchNode.childrenBuffer[matchingIndex]!;

                int childKeySegmentLength = matchingChild.KeySegment.Count;

                var matchingLength = childKeySegmentLength == 1 ? 1 : searchKey.CommonPrefixLength(matchingChild.KeySegment);

                if (matchingLength == searchKey.Length)
                {
                    if (matchingLength == childKeySegmentLength)
                    {
                        // We found a child node that matches our key exactly
                        // E.g. Key = "apple" and child key = "apple"
                        matchingChild.Value = value;
                        return;
                    }
                    else
                    {
                        // We matched the whole set key, but not the entire child key. We need to split the child key
                        SplitNode(ref matchingChild, matchingLength);
                        matchingChild.Value = value;
                        return;
                    }

                }
                else // We matched part of the set key on a child
                {
                    if (matchingLength == childKeySegmentLength)
                    {
                        // and the entire child key
                        keyOffset += matchingLength;
                        searchNode = ref matchingChild;
                    }
                    else
                    {
                        // and only part of the child key
                        SplitNode(ref matchingChild, matchingLength);
                        keyOffset += matchingLength;
                        searchNode = ref matchingChild;
                    }
                }
            }
            else
            {
                // There were no matching children, lets add a new one.
                // E.g. Key = "apple" and no child that even starts with 'a'. Add a new child node

                // Binary search results returns bitwise complement of the index of the first
                // byte greater than the one we searched for (which is where we want to insert
                // our new child).
                int insertChildAtIndex = ~matchingIndex; 
                if (keyBytes is null) keyBytes = key.ToArray();
                var newChild = NodePool.Rent();
                newChild.KeySegment = new ArraySegment<byte>(keyBytes, keyOffset, searchKey.Length);
                newChild.Value = value;

                // We grabbed this search node ref variable from its parent's 
                // children collection, and that is the only way nodes are referenced in the graph.
                // In effect, this method call and assignment replaces the node with a clone of
                // itself that contains a new child
                searchNode = searchNode.CloneWithNewChild(newChild, insertChildAtIndex);
            }
        }

 
    }


    public RadixTreeNode<T> Clone(bool copyChildren)
    {
        var clone = NodePool.Rent();
        clone.KeySegment = KeySegment;
        clone.Value = Value;
        clone.ChildCount = ChildCount;
        clone.SetChildCapacity(ChildCount, copyChildren);
        if (copyChildren)
        {
            Children.CopyTo(clone.Children);
            ChildrenFirstKeyBytes.CopyTo(clone.childrenFirstKeyBytesBuffer);
        }
        return clone;
    }

    /// <summary>
    /// Resizes a node's child capacity if needed. Should only be used
    /// on nodes that are disconnected from the graph (e.g. clones before they
    /// are used to replace a node).
    /// </summary>
    /// <param name="capacity">The new capacity needed</param>
    /// <param name="copyChildren">If the buffer is resized, if the original values need to be kept</param>
    public void SetChildCapacity(int capacity, bool copyChildren = true)
    {
        if (childrenBuffer.Length < capacity)
        {
            var oldChildren = childrenBuffer;
            var oldChildrenFirstBytes = childrenFirstKeyBytesBuffer;
            childrenBuffer = ArrayPool<RadixTreeNode<T>>.Shared.Rent(capacity);
            childrenFirstKeyBytesBuffer = ArrayPool<byte>.Shared.Rent(capacity);

            if (copyChildren)
            {
                oldChildren.CopyTo(childrenBuffer, 0);
                oldChildrenFirstBytes.CopyTo(childrenFirstKeyBytesBuffer, 0);
            }
            ArrayPool<RadixTreeNode<T>>.Shared.Return(oldChildren);
            ArrayPool<byte>.Shared.Return(oldChildrenFirstBytes);
        }
        ChildCount = (byte)capacity;
    }

    public RadixTreeNode<T> CloneWithNewChild(RadixTreeNode<T> newChild, int atIndex)
    {
        var clone = Clone(false);
        clone.SetChildCapacity(ChildCount + 1, false);
        Children.CopyWithInsert(clone.Children, newChild, atIndex);
        ChildrenFirstKeyBytes.CopyWithInsert(clone.ChildrenFirstKeyBytes, newChild.KeySegment[0], atIndex);
        return clone;
    }

    private static void SplitNode(ref RadixTreeNode<T> child, int atKeyLength)
    {
        // We are taking a child node and splitting it at a specific number of
        // characters in its key segment

        // E.g.
        // We have nodes A => BC => ...
        // and we want A => B => C => etc..
        // A is the current 'this' node of this method
        // BC is the original child node
        // B is the splitParent, and gets a null value
        // C is the new splitChild, it retains the original value and children of the 'BC' node

        // We have to clone the child we split because we are changing its key size
        var splitChild = child.Clone(true);
        var newOffset = atKeyLength;
        var newCount = splitChild.KeySegment.Count - atKeyLength;
        splitChild.KeySegment =  splitChild.KeySegment.Slice(newOffset, newCount);

        var splitParent = NodePool.Rent();
        splitParent.SetChildCapacity(1, false);
        splitParent.childrenBuffer[0] = splitChild;
        splitParent.childrenFirstKeyBytesBuffer[0] = splitChild.KeySegment[0];

        splitParent.KeySegment = new ArraySegment<byte>(child.FullKey, child.KeySegment.Offset, atKeyLength);

        child = splitParent;
    }


    public T? Get(ReadOnlySpan<byte> key)
    {
        var searchNode = this;
        var matchingChildIndex = searchNode.ChildrenFirstKeyBytes.BinarySearch(key[0]);

        while (matchingChildIndex > -1)
        {
            var matchingChild = searchNode.Children[matchingChildIndex];
            int matchingChildKeyLength = matchingChild.KeySegment.Count;
            var matchingBytes = matchingChildKeyLength == 1 ? 1 : key.CommonPrefixLength(matchingChild.KeySegment);

            if (matchingBytes == key.Length)
            {
                return matchingBytes == matchingChildKeyLength ? matchingChild.Value : default;
            }
            key = key.Slice(matchingBytes);
            searchNode = matchingChild;

            matchingChildIndex = searchNode.ChildrenFirstKeyBytes.BinarySearch(key[0]);
        }
        return default;

    }

    public void CollectKeyValues(ArrayPoolList<KeyValuePair<ReadOnlyMemory<byte>, T?>> collector)
    {
        if (Value is not null)
        {
            collector.Add(new KeyValuePair<ReadOnlyMemory<byte>, T?>(FullKey.AsMemory(0, KeySegment.Offset + KeySegment.Count), Value));
        }
        foreach(var child in Children)
        {
            child.CollectKeyValues(collector);
        }
    }

    public void CollectKeyValueStrings(ArrayPoolList<KeyValuePair<string, T?>> collector)
    {
        if (Value is not null)
        {
            collector.Add(new KeyValuePair<string, T?>(System.Text.Encoding.UTF8.GetString(FullKey.AsSpan(0, KeySegment.Offset + KeySegment.Count)), Value));
        }
        foreach (var child in Children)
        {
            child.CollectKeyValueStrings(collector);
        }
    }

    public void CollectValues(ArrayPoolList<T?> collector)
    {
        if (Value is not null)
        {
            collector.Add(Value);
        }
        foreach (var child in Children)
        {
            child.CollectValues(collector);
        }
    }

    private bool TryFindFirstPrefixMatch(ReadOnlySpan<byte> key, out RadixTreeNode<T> matchingNode)
    {
        var searchNode = this;
        matchingNode = this;

        var matchingChildIndex = searchNode.ChildrenFirstKeyBytes.BinarySearch(key[0]);

        while (matchingChildIndex > -1)
        {
            matchingNode = searchNode.Children[matchingChildIndex];

            var matchingChildKey = matchingNode.KeySegment.AsSpan();

            var matchingBytes = key.CommonPrefixLength(matchingChildKey);

            if (matchingBytes == key.Length) { return true; }

            // We have to match the whole child key to be a match
            if (matchingBytes != matchingChildKey.Length) break;

            key = key.Slice(matchingBytes);
            searchNode = matchingNode;

            matchingChildIndex = searchNode.ChildrenFirstKeyBytes.BinarySearch(key[0]);
        }
        return false;
    }

    public void SearchPrefixValues(ReadOnlySpan<byte> key, ArrayPoolList<T?> collector)
    {
        if (TryFindFirstPrefixMatch(key, out var matchingNode))
        {
            matchingNode.CollectValues(collector);
        }
    }

    public void SearchPrefix(ReadOnlySpan<byte> key, ArrayPoolList<KeyValuePair<ReadOnlyMemory<byte>, T?>> collector)
    {
        if (TryFindFirstPrefixMatch(key, out var matchingNode))
        {
            matchingNode.CollectKeyValues(collector);
        }
    }

    public void SearchPrefixStrings(ReadOnlySpan<byte> key, ArrayPoolList<KeyValuePair<string, T?>> collector)
    {
        if (TryFindFirstPrefixMatch(key, out var matchingNode))
        {
            matchingNode.CollectKeyValueStrings(collector);
        }
    }

    public int GetValuesCount()
    {
        int runningCount = 0;
        GetValuesCountInternal(ref runningCount);
        return runningCount;
    }

    private void GetValuesCountInternal(ref int runningCount)
    {
        if (Value is not null) runningCount++;
        foreach (var child in Children)
        {
            child.GetValuesCountInternal(ref runningCount);
        }
    }

    public override string ToString()
    {
        return System.Text.Encoding.UTF8.GetString(KeySegment);
    }

    /// <summary>
    /// Preps the instance for reuse
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Reset()
    {
        this.Value = default;
        this.ChildCount = 0;
        Array.Clear(childrenBuffer);
        KeySegment = default;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (childrenBuffer != null && childrenBuffer.Length > 0)
        {
            ArrayPool<RadixTreeNode<T>>.Shared.Return(childrenBuffer, true);
            ArrayPool<byte>.Shared.Return(childrenFirstKeyBytesBuffer);
        }
    }

    ~RadixTreeNode()
    {
        if (NodePool.Return(this))
        {
            GC.ReRegisterForFinalize(this);
        }
    }

    internal static class NodePool
    {

        public static RadixTreeNode<T> Rent()
        {
            if (Reader.TryRead(out var item))
            {
                return item;
            }
            else
            {                
                return new RadixTreeNode<T>();
            }
        }

        public static bool Return(RadixTreeNode<T> node)
        {
            node.Reset();
            return Writer.TryWrite(node);
        }

        static NodePool()
        {
            Pool =
            Channel.CreateBounded<RadixTreeNode<T>>(
                new BoundedChannelOptions(10_500)
                {
                    FullMode = BoundedChannelFullMode.DropNewest,
                }, x => { x.Dispose(); }
            );
            Writer = Pool.Writer;
            Reader = Pool.Reader;
        }

        public static readonly Channel<RadixTreeNode<T>> Pool;
        internal static readonly ChannelWriter<RadixTreeNode<T>> Writer;
        internal static readonly ChannelReader<RadixTreeNode<T>> Reader;

    }
}