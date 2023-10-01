using System.Runtime.CompilerServices;
using TrieHard.PrefixLookup;

namespace TrieHard.Collections;

/// <summary>
/// A typed Radix Tree Node containing a payload value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity stored within this node</typeparam>
internal class RadixTreeNode<T>
{
    public byte[] FullKey => KeySegment.Array!;
    private ArraySegment<byte> KeySegment = ArraySegment<byte>.Empty;
    public RadixTreeNode<T>[] childrenBuffer = EmptyNodes;
    private Span<RadixTreeNode<T>> Children => childrenBuffer.AsSpan(0, ChildCount);
    private byte ChildCount = 0;
    public T? Value;

    public static readonly RadixTreeNode<T>[] EmptyNodes = Array.Empty<RadixTreeNode<T>>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindChildByFirstByte(RadixTreeNode<T> node, byte searchKeyByte)
    {
        for (int i = node.ChildCount - 1; i >= 0; i--)
        {
            var childFirstByte = node.childrenBuffer[i].KeySegment[0];
            if (childFirstByte == searchKeyByte) return i;
            if (childFirstByte < searchKeyByte) return ~(i + 1);
        }
        return -1;
    }

    /// <summary>
    /// Steps down the child nodes of this node, creating missing key segments as necessary before
    /// setting the value on the last matching child node.
    /// </summary>
    /// <param name="rootNode">A reference to the root node. Necessary for one fringe concurrency case</param>
    /// <param name="key">The full key to set the value on</param>
    /// <param name="value">The value to set on the matching node</param>
    public void SetValue(ref RadixTreeNode<T> rootNode, ReadOnlySpan<byte> key, T? value)
    {
        ref RadixTreeNode<T> searchNode = ref rootNode;
        var searchKey = key;
        int keyOffset = 0;
        byte[]? keyBytes = null;

        // Every branch of this while statement must either set a new descendant search node or return
        while (true)
        {
            searchKey = key.Slice(keyOffset);
            var searchKeyByte = searchKey[0];
                        
            int matchingIndex = FindChildByFirstByte(searchNode, searchKeyByte);

            if (matchingIndex > -1)
            {
                ref RadixTreeNode<T> matchingChild = ref searchNode.childrenBuffer[matchingIndex]!;

                int matchingLength = 1;
                int childKeySegmentLength = matchingChild.KeySegment.Count;

                if (childKeySegmentLength > 1)
                {
                    var matchingChildKeySegment = matchingChild.KeySegment;
                    ReadOnlySpan<byte> matchingChildKey = matchingChildKeySegment.AsSpan();
                    matchingLength = searchKey.CommonPrefixLength(matchingChildKey);
                }

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
                var newChild = new RadixTreeNode<T>();
                newChild.KeySegment = new ArraySegment<byte>(keyBytes, keyOffset, searchKey.Length);
                newChild.Value = value;

                // We can avoid cloning the searchNode when inserting a new child at the end of the 
                // children buffer. This happens often for sequential inserts, and is a common use
                // case for any trie being used to store generated keys.

                //sanity check insertChildAtIndex!
                // Some keys are being inserted twice, or as nonsense values 

                if (insertChildAtIndex == searchNode.ChildCount && searchNode.childrenBuffer.Length > insertChildAtIndex)
                {
                    searchNode.childrenBuffer[insertChildAtIndex] = newChild;
                    searchNode.ChildCount += 1;
                }
                else
                {
                    // For concurrency reasons, we replace the searchNode with a clone
                    // containing the new child inserted at the expected index
                    searchNode = searchNode.CloneWithNewChild(newChild, insertChildAtIndex);
                }
                return;
            }
        }
    }

    public RadixTreeNode<T> Clone(bool copyChildren)
    {
        var clone = new RadixTreeNode<T>();
        clone.KeySegment = KeySegment;
        clone.Value = Value;
        clone.ChildCount = ChildCount;
        if (copyChildren)
        {
            clone.childrenBuffer = childrenBuffer;
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
            int nextBufferSize = ChildBuffersSizeLUT[capacity];
            childrenBuffer = new RadixTreeNode<T>[nextBufferSize];
            if (copyChildren)
            {
                oldChildren.CopyTo(childrenBuffer, 0);
            }
        }
        ChildCount = (byte)capacity;
    }

    private static readonly int[] ChildBuffersSizeLUT = GetChildBufferCapacitySizes();
    private static int[] GetChildBufferCapacitySizes()
    {
        // The purpose of this lookup table is to assign child array sizes by bucketing
        // around the assumption that many keys will map to ASCII based usage patterns.
        int[] sizes = new int[256];
        for (int i = 0; i < sizes.Length; i++)
        {
            if (i == 0)
            {
                sizes[i] = 0;
            }
            else if (i == 1)
            {
                sizes[i] = 1;
            }
            else if (i < 4)
            {
                sizes[i] = 4;
            }
            else if (i < 11) // Decimals
            {
                // Including a space for an
                // arbitrary delimiter
                sizes[i] = 10;
            }
            else if (i < 16) // GUID
            {
                // While 17 characters are possible,
                // only 16 unique combinations can be at any
                // given location within a string representation of
                // a guid (as the hyphens are always at the same ordinals).
                sizes[i] = 16;
            }
            else if (i < 32) // Alpha characters
            {
                // Enough to hold either uppercase or lowercase
                // plus a couple delimiters, and added
                // a few extra to round up to nearest power of 2
                sizes[i] = 32;
            }
            else if (i < 65) // Base64
            {
                sizes[i] = 65;
            }
            else if (i < 95) // Printable ASCII
            {
                sizes[i] = 95;
            }
            else if (i < 128)
            {
                // Jump to full 128 or 256
                // If we are using more than 95 unique byte values
                // then there is a good chance we are storing something
                // that is not heavily ascii based.
                sizes[i] = 128;
            }
            else
            {
                sizes[i] = 256;
            }
        }
        return sizes;
    }

    public RadixTreeNode<T> CloneWithNewChild(RadixTreeNode<T> newChild, int atIndex)
    {
        var clone = Clone(false);
        clone.SetChildCapacity(ChildCount + 1, false);
        Children.CopyWithInsert(clone.Children, newChild, atIndex);
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
        splitChild.KeySegment = splitChild.KeySegment.Slice(newOffset, newCount);

        var splitParent = new RadixTreeNode<T>();
        splitParent.SetChildCapacity(1, false);
        splitParent.childrenBuffer[0] = splitChild;

        splitParent.KeySegment = new ArraySegment<byte>(child.FullKey, child.KeySegment.Offset, atKeyLength);
        child = splitParent;
    }


    public T? Get(ReadOnlySpan<byte> key)
    {
        var searchNode = this;
        var searchKeyByte = key[0];
        int matchingIndex = FindChildByFirstByte(searchNode, searchKeyByte);

        while (matchingIndex > -1)
        {
            searchNode = searchNode.childrenBuffer[matchingIndex];

            var keyLength = searchNode.KeySegment.Count;
            var bytesMatched = keyLength == 1 ? 1 : key.CommonPrefixLength(searchNode.KeySegment);

            if (bytesMatched == key.Length) {
                return bytesMatched == keyLength ? searchNode.Value : default;
            }
            key = key.Slice(bytesMatched);
            searchKeyByte = key[0];
            matchingIndex = FindChildByFirstByte(searchNode, searchKeyByte);
        }
        return default;
    }

    public void CollectKeyValues(ArrayPoolList<KeyValuePair<ReadOnlyMemory<byte>, T?>> collector)
    {
        if (Value is not null)
        {
            collector.Add(new KeyValuePair<ReadOnlyMemory<byte>, T?>(FullKey.AsMemory(0, KeySegment.Offset + KeySegment.Count), Value));
        }
        for(int i = 0; i < ChildCount; i++)
        {
            childrenBuffer[i].CollectKeyValues(collector);
        }
    }

    public void CollectKeyValueStrings(ArrayPoolList<KeyValuePair<string, T?>> collector)
    {
        if (Value is not null)
        {
            collector.Add(new KeyValuePair<string, T?>(System.Text.Encoding.UTF8.GetString(FullKey.AsSpan(0, KeySegment.Offset + KeySegment.Count)), Value));
        }
        for (int i = 0; i < ChildCount; i++)
        {
            childrenBuffer[i].CollectKeyValueStrings(collector);
        }
    }

    public void CollectValues(ArrayPoolList<T?> collector)
    {
        if (Value is not null)
        {
            collector.Add(Value);
        }
        for (int i = 0; i < ChildCount; i++)
        {
            childrenBuffer[i].CollectValues(collector);
        }
    }

    private RadixTreeNode<T>? FindPrefixMatch(ReadOnlySpan<byte> key)
    {
        var node = this;
        var childIndex = FindChildByFirstByte(node, key[0]);
        while (childIndex > -1)
        {
            node = node.childrenBuffer[childIndex];
            var nodeKeyLength = node.KeySegment.Count;
            var matchingBytes = nodeKeyLength == 1 ? 1 : key.CommonPrefixLength(node.KeySegment);

            if (matchingBytes == key.Length) { return node; }

            // We have to match the whole child key to be a match
            if (matchingBytes != nodeKeyLength) return null;

            key = key.Slice(matchingBytes);

            childIndex = FindChildByFirstByte(node, key[0]);
        }
        return null;
    }

    public void SearchPrefixValues(ReadOnlySpan<byte> key, ArrayPoolList<T?> collector)
    {
        var matchingNode = FindPrefixMatch(key);
        if (matchingNode is null) return;
        matchingNode.CollectValues(collector);
    }

    public void SearchPrefix(ReadOnlySpan<byte> key, ArrayPoolList<KeyValuePair<ReadOnlyMemory<byte>, T?>> collector)
    {
        var matchingNode = FindPrefixMatch(key);
        if (matchingNode is null) return;
        matchingNode.CollectKeyValues(collector);
    }

    public void SearchPrefixStrings(ReadOnlySpan<byte> key, ArrayPoolList<KeyValuePair<string, T?>> collector)
    {
        var matchingNode = FindPrefixMatch(key);
        if (matchingNode is null) return;
        matchingNode.CollectKeyValueStrings(collector);
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
        for (int i = 0; i < ChildCount; i++)
        {
            childrenBuffer[i].GetValuesCountInternal(ref runningCount);
        }

    }

    public override string ToString()
    {
        return System.Text.Encoding.UTF8.GetString(KeySegment);
    }

    internal void Reset()
    {
        this.Value = default;
        this.ChildCount = 0;
        this.childrenBuffer = EmptyNodes;
        Array.Clear(childrenBuffer);
        this.KeySegment = default;
    }


}