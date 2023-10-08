using System.Buffers;
using System.Runtime.CompilerServices;
using TrieHard.PrefixLookup;

namespace TrieHard.Collections;

/// <summary>
/// A typed Radix Tree Node containing a payload value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity stored within this node</typeparam>
public class RadixTreeNode<T>
{
    public RadixTreeNode<T>? Parent = null!;
    public byte[] FullKey => KeySegment.Array!;
    private ArraySegment<byte> KeySegment = ArraySegment<byte>.Empty;
    public RadixTreeNode<T>[] childrenBuffer = EmptyNodes;
    private Span<RadixTreeNode<T>> Children => childrenBuffer.AsSpan(0, ChildCount);
    internal byte ChildCount = 0;
    public T? Value;
    public byte FirstKeyByte;
    public byte IndexInParent;

    public static readonly RadixTreeNode<T>[] EmptyNodes = Array.Empty<RadixTreeNode<T>>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindChildByFirstByte(byte searchKeyByte)
    {
        // The 'default' search is a basic binary search (see the 'default' case
        // But if we specialize and unroll for other size counts, those sizes
        // can have improved performance. 
        var buffer = childrenBuffer;
        int childCount = ChildCount;

        switch (childCount)
        {
            case 0: return ~0;
            case 1:
                int cmp = buffer[0].FirstKeyByte - searchKeyByte;
                if (cmp == 0) return 0;
                return cmp > 0 ? ~0 : ~1;
                //if (buffer[0].FirstKeyByte == searchKeyByte) return 0;
                //if (buffer[0].FirstKeyByte > searchKeyByte) return ~0;
                //return ~1;
            case 2:
                if (buffer[1].FirstKeyByte == searchKeyByte) return 1;
                if (buffer[0].FirstKeyByte == searchKeyByte) return 0;

                if (buffer[0].FirstKeyByte > searchKeyByte) return ~0;
                if (buffer[1].FirstKeyByte > searchKeyByte) return ~1;
                return ~2;
            case 3:
                if (buffer[2].FirstKeyByte == searchKeyByte) return 2;
                if (buffer[1].FirstKeyByte == searchKeyByte) return 1;
                if (buffer[0].FirstKeyByte == searchKeyByte) return 0;

                if (buffer[0].FirstKeyByte > searchKeyByte) return ~0;
                if (buffer[1].FirstKeyByte > searchKeyByte) return ~1;
                if (buffer[2].FirstKeyByte > searchKeyByte) return ~2;
                return ~3;
            case 10:
                // Size 10 is very important. If this tree is used
                // to store codes and ids, then its likely many
                // nodes will only have children for decimals

                // Unroll loop and manually binary search
                int cmp5 = buffer[5].FirstKeyByte - searchKeyByte;
                if (cmp5 == 0) return 5;
                if (cmp5 < 0)
                {
                    int cmp8 = buffer[8].FirstKeyByte - searchKeyByte;
                    if (cmp8 == 0) return 8;
                    if (cmp8 < 0)
                    {
                        int cmp9 = buffer[9].FirstKeyByte - searchKeyByte;
                        if (cmp9 == 0) return 9;
                        if (cmp9 < 0) return ~9;
                        return ~8;
                    }

                    int cmp6 = buffer[6].FirstKeyByte - searchKeyByte;
                    if (cmp6 == 0) return 6;
                    if (cmp6 < 0)
                    {
                        int cmp7 = buffer[7].FirstKeyByte - searchKeyByte;
                        if (cmp7 == 0) return 7;
                        if (cmp7 < 0) return ~7;
                        return ~6;
                    }
                    return ~5;
                }

                int cmp2 = buffer[2].FirstKeyByte - searchKeyByte;
                if (cmp2 == 0) return 2;
                if (cmp2 < 0)
                {
                    int cmp3 = buffer[3].FirstKeyByte - searchKeyByte;
                    if (cmp3 == 0) return 3;
                    if (cmp3 < 0)
                    {
                        int cmp4 = buffer[4].FirstKeyByte - searchKeyByte;
                        if (cmp4 == 0) return 4;
                        if (cmp4 < 0) return ~4;
                        return ~3;
                    }
                    return ~2;
                }
                int cmp0 = buffer[0].FirstKeyByte - searchKeyByte;
                if (cmp0 == 0) return 0;
                if (cmp0 < 0)
                {
                    int cmp1 = buffer[1].FirstKeyByte - searchKeyByte;
                    if (cmp1 == 0) return 1;
                    if (cmp1 < 0) return ~1;
                    return ~0;
                };

                return -1;

            case 256:
                // If the node has every possible ordered value, no need to search
                return searchKeyByte;

            default:
                int lo = 0;
                int hi = childCount - 1;
                while (lo <= hi)
                {
                    int i = lo + ((hi - lo) >> 1);
                    int c = buffer[i].FirstKeyByte - searchKeyByte;
                    if (c == 0) return i;
                    if (c < 0)
                    {
                        lo = i + 1;
                    }
                    else
                    {
                        hi = i - 1;
                    }
                }
                return ~lo;
        }

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
                        
            int matchingIndex = searchNode.FindChildByFirstByte(searchKeyByte);

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
                        searchNode.SplitNode(ref matchingChild, matchingLength);
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
                        searchNode.SplitNode(ref matchingChild, matchingLength);
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
                newChild.FirstKeyByte = newChild.KeySegment[0];
                newChild.Value = value;

                // We can avoid cloning the searchNode when inserting a new child at the end of the 
                // children buffer. This happens often for sequential inserts, and is a common use
                // case for any trie being used to store generated keys.

                //sanity check insertChildAtIndex!
                // Some keys are being inserted twice, or as nonsense values 

                if (insertChildAtIndex == searchNode.ChildCount && searchNode.childrenBuffer.Length > insertChildAtIndex)
                {
                    newChild.Parent = searchNode;
                    newChild.IndexInParent = (byte)insertChildAtIndex;
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
        clone.FirstKeyByte = FirstKeyByte;
        clone.Value = Value;
        clone.ChildCount = ChildCount;
        clone.Parent = Parent;
        clone.IndexInParent = IndexInParent;
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
        var newCount = ChildCount + 1;
        clone.SetChildCapacity(newCount, false);
        Children.CopyWithInsert(clone.Children, newChild, atIndex);

        for (int i = 0; i < newCount; i++)
        {
            RadixTreeNode<T>? child = clone.childrenBuffer[i];
            child.IndexInParent = (byte)i;
            child.Parent = clone;
        }
        return clone;
    }

    private void SplitNode(ref RadixTreeNode<T> child, int atKeyLength)
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

        byte originalIndexInParent = child.IndexInParent;

        // We have to clone the child we split because we are changing its key size
        var splitChild = child.Clone(true);
        var newOffset = atKeyLength;
        var newCount = splitChild.KeySegment.Count - atKeyLength;
        splitChild.KeySegment = splitChild.KeySegment.Slice(newOffset, newCount);
        splitChild.FirstKeyByte = splitChild.KeySegment[0];
        

        var splitParent = new RadixTreeNode<T>();
        splitParent.SetChildCapacity(1, false);
        splitParent.childrenBuffer[0] = splitChild;
        splitParent.IndexInParent = originalIndexInParent;
        splitChild.IndexInParent = 0;

        splitParent.Parent = this;
        splitParent.KeySegment = new ArraySegment<byte>(child.FullKey, child.KeySegment.Offset, atKeyLength);
        splitParent.FirstKeyByte = splitParent.KeySegment[0];

        splitChild.Parent = splitParent;
        child = splitParent;
    }


    public T? Get(ReadOnlySpan<byte> key)
    {
        var searchNode = this;
        var searchKeyByte = key[0];
        int matchingIndex = searchNode.FindChildByFirstByte(searchKeyByte);

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
            matchingIndex = searchNode.FindChildByFirstByte(searchKeyByte);
        }
        return default;
    }

    public void CollectKeyValues(ArrayPoolList<KeyValuePair<ReadOnlyMemory<byte>, T?>> kvps)
    {
        if (Value is not null) kvps.Add(AsKeyValuePair());
        // Unrolling a bit still seems to be the best way to reduce the cost of recursion,
        // though the code is ugly and redundant
        var childCount = ChildCount;
        if (childCount == 0) return;
        var children = childrenBuffer;

        for (int i1 = 0; i1 < childCount; i1++)
        {
            var child1 = children[i1];
            if (child1.Value != null) kvps.Add(child1.AsKeyValuePair());
            if (child1.ChildCount == 0) continue;
            var children1 = child1.childrenBuffer;

            for (int i2 = 0; i2 < child1.ChildCount; i2++)
            {
                var child2 = children1[i2];
                if (child2.Value != null) kvps.Add(child2.AsKeyValuePair());
                if (child2.ChildCount == 0) continue;
                var children2 = child2.childrenBuffer;

                for(int i3 = 0; i3 < child2.ChildCount; i3++)
                {
                    var child3 = children2[i3];
                    if (child3.Value != null) kvps.Add(child3.AsKeyValuePair());
                    if (child3.ChildCount == 0) continue;
                    var children3 = child3.childrenBuffer;

                    for(int i4 = 0; i4 < child3.ChildCount; i4++)
                    {
                        children3[i4].CollectKeyValues(kvps);
                    }
                }
            }            
        }
    }

    public void CollectKeyValuesExperiment(ArrayPoolList<KeyValuePair<ReadOnlyMemory<byte>, T?>> kvps)
    {
        var childCount = ChildCount;
        if (childCount == 0)
        {
            if (Value is not null) kvps.Add(AsKeyValuePair());
            return;
        }

        var stack = new Stack<(RadixTreeNode<T> Parent, int ChildrenVisited)>();
        var searchNode = this;

        do
        {
            if (searchNode.Value is not null)
                kvps.Add(searchNode.AsKeyValuePair());

            if (searchNode.ChildCount > 0)
            {
                // Descend DFS search
                stack.Push((searchNode, 1));
                searchNode = searchNode.childrenBuffer[0];
                continue;
            }

            // We made it to a leaf. Go up a level and
            // go to the next sibling in the stack

            (RadixTreeNode<T> Parent, int ChildrenVisited) upLevel;

            while (true)
            {
                if (stack.Count == 0) { return; }
                upLevel = stack.Pop();
                if (upLevel.ChildrenVisited < upLevel.Parent.ChildCount)
                {
                    stack.Push((upLevel.Parent, upLevel.ChildrenVisited + 1));
                    searchNode = upLevel.Parent.childrenBuffer[upLevel.ChildrenVisited];
                    break;
                }
            }

        } while (stack.Count > 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KeyValuePair<ReadOnlyMemory<byte>, T?> AsKeyValuePair()
    {
        return new KeyValuePair<ReadOnlyMemory<byte>, T?>(FullKey.AsMemory(0, KeySegment.Offset + KeySegment.Count), Value);
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

    internal RadixTreeNode<T>? FindPrefixMatch(ReadOnlySpan<byte> key)
    {
        var node = this;
        var childIndex = node.FindChildByFirstByte(key[0]);
        while (childIndex > -1)
        {
            node = node.childrenBuffer[childIndex];
            var nodeKeyLength = node.KeySegment.Count;
            var matchingBytes = nodeKeyLength == 1 ? 1 : key.CommonPrefixLength(node.KeySegment);

            if (matchingBytes == key.Length) { return node; }

            // We have to match the whole child key to be a match
            if (matchingBytes != nodeKeyLength) return null;

            key = key.Slice(matchingBytes);

            childIndex = node.FindChildByFirstByte(key[0]);
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
        matchingNode.CollectKeyValuesExperiment(collector);
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