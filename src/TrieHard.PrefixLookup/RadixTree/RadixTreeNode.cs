using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using TrieHard.PrefixLookup;

namespace TrieHard.Collections;

/// <summary>
/// A typed Radix Tree Node containing a payload value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity stored within this node</typeparam>
internal class RadixTreeNode<T> : IDisposable
{
    public byte[] FullKey => KeySegment.Array;
    private ArraySegment<byte> KeySegment;
    public RadixTreeNode<T>[] childrenBuffer = EmptyNodes;
    private ReadOnlySpan<RadixTreeNode<T>> Children => childrenBuffer.AsSpan(0, ChildCount);
    private byte ChildCount = 0;
    public T? Value;
    public byte FirstChar;

    public static readonly RadixTreeNode<T>[] EmptyNodes = Array.Empty<RadixTreeNode<T>>();
    public static readonly byte[] EmptyBytes = [];

    public RadixTreeNode()
    {
        this.KeySegment = new ArraySegment<byte>(EmptyBytes);
    }

    public RadixTreeNode(byte[] key, int offset, int count)
    {
        // It would be good if we could pool this array, but its shared
        // non trivially with parents and children nodes created from the same key
        this.KeySegment = new ArraySegment<byte>(key, offset, count);
        this.FirstChar = KeySegment[0];
    }

    public RadixTreeNode(ArraySegment<byte> key)
    {
        this.KeySegment = key;
        this.FirstChar = KeySegment[0];
    }

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

    FakeTailRecursion:
        // Set on a child
        searchKey = key.Slice(keyOffset);
        var searchFirstChar = searchKey[0];

        int childIndex = 0;
        bool found = false;

        for (int i = 0; i < searchNode.Children.Length; i++)
        {
            RadixTreeNode<T> child = searchNode.Children[i];

            if (child.FirstChar == searchFirstChar)
            {
                childIndex = i;
                found = true;
                break;
            }
            if (child.FirstChar > searchFirstChar)
            {
                break;
            }
            childIndex = i + 1;
        }


        if (found)
        {
            ref RadixTreeNode<T> matchingChild = ref searchNode.childrenBuffer[childIndex]!;

            var childKeySegment = matchingChild.KeySegment.AsSpan();
            var matchingLength = searchKey.CommonPrefixLength(childKeySegment);
            if (matchingLength == searchKey.Length)
            {
                if (matchingLength == childKeySegment.Length)
                {
                    // We found a child node that matches our key exactly
                    // E.g. Key = "apple" and child key = "apple"
                    matchingChild.Value = value;
                }
                else
                {
                    // https://en.wikipedia.org/wiki/Radix_tree#/media/File:Inserting_the_word_'team'_into_a_Patricia_trie_with_a_split.png

                    // We matched the whole set key, but not the entire child key. We need to split the child key
                    searchNode.SplitChild(matchingLength, ref matchingChild);
                    //matchingChild = searchNode.Children[childIndex];
                    matchingChild.Value = value;
                }

            }
            else
            {
                // We matched part of the set key on a child
                if (matchingLength == childKeySegment.Length)
                {
                    // and the entire child key
                    keyOffset += matchingLength;
                    searchNode = ref matchingChild;
                    goto FakeTailRecursion;
                }
                else
                {
                    // and only part of the child key
                    searchNode.SplitChild(matchingLength, ref matchingChild);
                    //matchingChild = searchNode.Children[childIndex];
                    keyOffset += matchingLength;
                    searchNode = ref matchingChild;
                    goto FakeTailRecursion;
                }
            }
        }
        else
        {
            // There were no matching children. 
            // E.g. Key = "apple" and no child that even starts with 'a'. Add a new child node

            if (keyBytes is null) keyBytes = key.ToArray();
            var newChild = NodePool.Rent();
            newChild.KeySegment = new ArraySegment<byte>(keyBytes, keyOffset, searchKey.Length);
            newChild.FirstChar = newChild.KeySegment[0];
            newChild.Value = value;
            
            //var newChild = new RadixTreeNode<T>(keyBytes, keyOffset, searchKey.Length); //TODO: Pool these
            //newChild.Value = value;
            searchNode.AddChild(ref searchNode, newChild, childIndex);
        }
    }

    public RadixTreeNode<T> Clone()
    {
        var newNode = NodePool.Rent();
        newNode.KeySegment = KeySegment;
        newNode.FirstChar = FirstChar;
        newNode.Parent = Parent;
        newNode.ParentIndex = ParentIndex;
        newNode.Value = Value;
        newNode.childrenBuffer = childrenBuffer;
        newNode.ChildCount = ChildCount;
        return newNode;

        //return new RadixTreeNode<T>(KeySegment)
        //{
        //    newNode.Children = Children,
        //    Parent = Parent,
        //    ParentIndex = ParentIndex,
        //    Value = Value,
        //};
    }

    private void AddChild(ref RadixTreeNode<T> self, RadixTreeNode<T> newChild, int afterIndex)
    {
        if (Children.Length == 0)
        {
            newChild.ParentIndex = 0;
            newChild.Parent = this;
            var newChildrenBuffer = ArrayPool<RadixTreeNode<T>>.Shared.Rent(1);
            newChildrenBuffer[0] = newChild;
            childrenBuffer = newChildrenBuffer;
            ChildCount = 1;
        }
        else
        {
            // This operation is messy.
            // We are inserting the new child into the correct place for it
            // to be sorted in the array, but we need to copy and replace
            // any nodes that require patches to their Parent/ParentIndex values
            // in a concurrent safe way.

            // Right now this patches the references on clones of the children
            // onto a clone of 'this' then replaces the node reference in its 
            // parent with the clone of this (effectively replacing the current
            // node in the graph with a patched clone containing the changes).

            // Being done as a single assignment with a single writer allows this
            // to work and pass concurrency checks.

            //var keySegment = this.KeySegment;
            var newSelf = self.Clone();
            newChild.Parent = newSelf;
            byte newLength = Convert.ToByte(newSelf.ChildCount + 1);
            var newChildArray = ArrayPool<RadixTreeNode<T>>.Shared.Rent(newLength);

            // Poor man's sorted insert
            for(int i = 0; i < newLength; i++)
            {
                if (i == afterIndex)
                {
                    newChildArray[i] = newChild;
                }
                else
                {
                    int offset = i > afterIndex ? -1 : 0;
                    newChildArray[i] = newSelf.Children[i + offset].Clone();
                    newChildArray[i].ParentIndex = i;
                    foreach (var clonedChild in newChildArray[i].Children)
                    {
                        clonedChild.Parent = newChildArray[i];
                    }
                }
            }

            for(int i = 0; i < newLength; i++)
            {
                var child = newChildArray[i];
                child.ParentIndex = i;
                child.Parent = newSelf;
            }

            newSelf.childrenBuffer = newChildArray;
            newSelf.ChildCount = newLength;

            self = newSelf;

        }
    }


    private void SplitChild(int startingCharacter, ref RadixTreeNode<T> child)
    {
        // We are taking a child node and splitting it at a specific number of
        // characters in its key segment

        // E.g.
        // We have nodes A => BC => etc...
        // and we want A => B => C => etc..
        // A is the current 'this' node of this method
        // BC is the original child node
        // B is the splitParent, and gets a null value
        // C is the new splitChild, it retains the original value and children of the 'BC' node


        // We have to clone the child we split because we are changing its key size
        var splitChild = child.Clone();
        var newOffset = startingCharacter;
        var newCount = splitChild.KeySegment.Count - startingCharacter;
        splitChild.KeySegment =  splitChild.KeySegment.Slice(newOffset, newCount);
        splitChild.FirstChar = splitChild.KeySegment[0];

        var splitParentChildren = ArrayPool<RadixTreeNode<T>>.Shared.Rent(1);
        splitParentChildren[0] = splitChild;

        var splitParent = NodePool.Rent();
        splitParent.KeySegment = new ArraySegment<byte>(child.FullKey, child.KeySegment.Offset, startingCharacter);
        splitParent.FirstChar = splitParent.KeySegment[0];
        splitParent.childrenBuffer = splitParentChildren;
        splitParent.ChildCount = 1;

        //var splitParent = new RadixTreeNode<T>(
        //    child.FullKey,
        //    offset: child.KeySegment.Offset,  
        //    count: startingCharacter) { childrenBuffer = splitParentChildren, ChildCount = 1 };
        //splitParent.FirstChar = splitParent.KeySegment[0];

        splitParent.Parent = this;
        splitParent.ParentIndex = child.ParentIndex;

        splitChild.Parent = splitParent;
        splitChild.ParentIndex = 0;

        // The order we perform the above operations is very important for concurrency reasons

        child = splitParent;
    }

    private RadixTreeNode<T>? Parent;
    private int ParentIndex;

    public T? GetValue(ReadOnlySpan<byte> key)
    {
        var searchNode = this;
        var searchKey = key;
        byte searchFirstChar;

        FakeTailRecursion:
        var searchChildren = searchNode.Children;
        searchFirstChar = searchKey[0];

        for (int i = 0; i < searchChildren.Length; i++)
        {
            RadixTreeNode<T> child = searchChildren[i];
            if (searchFirstChar != child.FirstChar) continue;

            var childKeySegment = child.KeySegment.AsSpan();
            var matchingBytes = searchKey.CommonPrefixLength(childKeySegment);

            if (matchingBytes == searchKey.Length)
            {
                if (matchingBytes == childKeySegment.Length)
                {
                    // We found a key with an exact match
                    return child.Value;
                }
                else
                {
                    // We found a key that was longer than the
                    // one we were looking for that matched the length of the key

                    // In a radix tree, that means our key wasn't found, because if it
                    // existed, it would have been split at our length
                    return default;
                }
            }
            else if (matchingBytes < searchKey.Length)
            {
                searchKey = searchKey.Slice(matchingBytes);
                searchNode = child;
                goto FakeTailRecursion; // C# maintainers, please add tail call optimizations
            }
        }
        return default;

    }

    public void CollectKeyValues(ArrayPoolList<KeyValuePair<byte[], T?>> collector)
    {
        if (Value is not null)
        {
            collector.Add(new KeyValuePair<byte[], T?>(FullKey, Value));
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
            collector.Add(new KeyValuePair<string, T?>(System.Text.Encoding.UTF8.GetString(FullKey), Value));
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
        matchingNode = default!;

        if (Children.Length == 0) return false;
        var searchRoot = this;

    TailRecursionWhen:

        foreach (var child in searchRoot.Children)
        {
            if (key[0] != child.FirstChar) continue;
            var childKeySegment = child.KeySegment.AsSpan();
            var matchingCharacters = key.CommonPrefixLength(childKeySegment);

            if (matchingCharacters == key.Length)
            {
                // We found a key that matched the entire prefix search
                matchingNode = child;
                return true;
            }
            else
            {

                if (matchingCharacters == childKeySegment.Length)
                {
                    // We matched the whole child key, check the remainder of
                    // the prefix search against that child's children
                    key = key.Slice(matchingCharacters);
                    searchRoot = child;

                    // This could probably be rewritten as a while loop
                    goto TailRecursionWhen;
                }

                // We partial matched, but the remainder is a mismatch
                return false;

            }
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

    public void SearchPrefix(ReadOnlySpan<byte> key, ArrayPoolList<KeyValuePair<byte[], T?>> collector)
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
    internal void Reset()
    {
        this.Value = default;
        this.Parent = null;
        this.ChildCount = 0;
        this.FirstChar = 0;
        this.ParentIndex = 0;
        if (Children != null && Children.Length > 0)
        {
            ArrayPool<RadixTreeNode<T>>.Shared.Return(this.childrenBuffer, true);
        }
        KeySegment = default;
    }    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    ~RadixTreeNode()
    {
        if (NodePool.Return(this))
        {
            GC.ReRegisterForFinalize(this);
        }
    }

    private static class NodePool
    {
        //private static int Dropped = 0;
        public static RadixTreeNode<T> Rent()
        {
            if (Pool.Reader.TryRead(out var item))
            {
                //Rented++;
                //if (Rented % 10_000 == 0) Console.WriteLine("Rented: " + Rented);
                return item;
            }
            else
            {
                //Created++;
                //if (Created % 10_000 == 0) Console.WriteLine("Created: " + Created);
                return new RadixTreeNode<T>();
            }
        }

        public static bool Return(RadixTreeNode<T> node)
        {
            node.Reset();
            return Pool.Writer.TryWrite(node);
            //if (returned) Returned++;
            //if (Returned > 0 && Returned % 10_000 == 0) Console.WriteLine("Returned: " + Returned);
            //return returned;
        }

        internal static Channel<RadixTreeNode<T>> Pool =
            Channel.CreateBounded<RadixTreeNode<T>>(
            new BoundedChannelOptions(1_000_000)
            {
                FullMode = BoundedChannelFullMode.DropNewest
            }, x => {
                //Dropped++;
                //if (Dropped > 0 && Dropped % 1_000 == 0) Console.WriteLine("Dropped: " + Dropped);

                x.Dispose(); 
            }
        );

    }
}