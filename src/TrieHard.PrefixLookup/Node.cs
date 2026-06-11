using System.Runtime.CompilerServices;
using System.Text;
using TrieHard.Collections;

namespace TrieHard.PrefixLookup;

/// <summary>
/// A typed Radix Tree Node containing a payload value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity stored within this node</typeparam>
internal class Node<T>
{
    public string? Key;
    private byte[] keyBytes = EmptyBytes;
    private int keyBytesLength;
    private int keySegmentStart;

    public Node<T>[] childrenBuffer = EmptyNodes;
    private byte[] childFirstBytes = EmptyBytes;
    private Span<Node<T>> Children => childrenBuffer.AsSpan();
    public T? Value;
    public byte FirstKeyByte;

    public static readonly Node<T>[] EmptyNodes = Array.Empty<Node<T>>();
    private static readonly byte[] EmptyBytes = [];

    private int KeySegmentLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => keyBytesLength - keySegmentStart;
    }

    private ReadOnlySpan<byte> KeySegmentSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => keyBytes.AsSpan(keySegmentStart, keyBytesLength - keySegmentStart);
    }

    /// <summary>
    /// Returns the index of the child whose key segment starts with
    /// <paramref name="searchKeyByte"/>, or -1 if there is no such child. One
    /// contiguous, vectorized scan of the packed first-byte array instead of
    /// dereferencing a child node per comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindChildByFirstByte(byte searchKeyByte)
    {
        return childFirstBytes.AsSpan().IndexOf(searchKeyByte);
    }

    /// <summary>
    /// Searches the packed first-byte array. Returns the index of the matching
    /// child, or the bitwise complement of the index where a child with
    /// <paramref name="searchKeyByte"/> would be inserted to keep children sorted.
    /// </summary>
    private int FindChildOrInsertionIndex(byte searchKeyByte)
    {
        // Match with the same vectorized search the read paths use, so updating an
        // existing key costs the same as a Get. Only a miss (a real insert, which
        // already allocates) pays the binary search for the insertion point.
        byte[] bytes = childFirstBytes;
        int index = bytes.AsSpan().IndexOf(searchKeyByte);
        if (index >= 0) return index;

        int lo = 0;
        int hi = bytes.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (bytes[mid] < searchKeyByte) lo = mid + 1;
            else hi = mid - 1;
        }
        return ~lo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KeyValue<T?> AsKeyValue()
    {
        // The key string is materialized lazily: nodes are created on the write
        // path without one, and only enumerators that actually yield a node need
        // its UTF-16 key. The race under concurrent readers is benign — competing
        // threads compute identical strings and the reference assignment is atomic.
        Key ??= Encoding.UTF8.GetString(keyBytes, 0, keyBytesLength);
        return new KeyValue<T?>(Key, keyBytes.AsMemory(0, keyBytesLength), Value);
    }

    /// <summary>
    /// Steps down the child nodes of this node, creating missing key segments as necessary before
    /// setting the value on the last matching child node.
    /// </summary>
    /// <param name="rootNode">A reference to the root node. Necessary for one fringe concurrency case</param>
    /// <param name="key">The full key to set the value on</param>
    /// <param name="value">The value to set on the matching node</param>
    public void SetValue(ref Node<T> rootNode, ReadOnlySpan<byte> key, T? value)
    {
        ref Node<T> searchNode = ref rootNode;
        var searchKey = key;
        int keyOffset = 0;
        byte[]? keyBytes = null;

        // Every branch of this while statement must either set a new descendant search node or return
        while (true)
        {
            searchKey = key.Slice(keyOffset);
            var searchKeyByte = searchKey[0];

            int matchingIndex = searchNode.FindChildOrInsertionIndex(searchKeyByte);

            if (matchingIndex > -1)
            {
                ref Node<T> matchingChild = ref searchNode.childrenBuffer[matchingIndex]!;

                int matchingLength = 1;
                int childKeySegmentLength = matchingChild.KeySegmentLength;

                if (childKeySegmentLength > 1)
                {
                    ReadOnlySpan<byte> matchingChildKey = matchingChild.KeySegmentSpan;
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
                var newChild = new Node<T>();

                newChild.keyBytes = keyBytes;
                newChild.keySegmentStart = keyOffset;
                newChild.keyBytesLength = keyOffset + searchKey.Length;
                newChild.FirstKeyByte = keyBytes[keyOffset];
                // Key is materialized lazily in AsKeyValue() — see that method.
                newChild.Value = value;
                searchNode = searchNode.CloneWithNewChild(newChild, insertChildAtIndex);

                return;
            }
        }
    }

    public Node<T> Clone(bool copyChildren)
    {
        var clone = new Node<T>();
        clone.Key = Key;
        clone.keyBytes = keyBytes;
        clone.keyBytesLength = keyBytesLength;
        clone.keySegmentStart = keySegmentStart;
        clone.FirstKeyByte = FirstKeyByte;
        clone.Value = Value;
        if (copyChildren)
        {
            clone.childrenBuffer = childrenBuffer;
            clone.childFirstBytes = childFirstBytes;
        }
        return clone;
    }

    public Node<T> CloneWithNewChild(Node<T> newChild, int atIndex)
    {
        var clone = Clone(false);
        clone.childrenBuffer = new Node<T>[childrenBuffer.Length + 1];
        Children.CopyWithInsert(clone.Children, newChild, atIndex);
        clone.childFirstBytes = new byte[childFirstBytes.Length + 1];
        childFirstBytes.AsSpan().CopyWithInsert(clone.childFirstBytes, newChild.FirstKeyByte, atIndex);
        return clone;
    }

    private static void SplitNode(ref Node<T> child, int atKeyLength)
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
        splitChild.keySegmentStart += atKeyLength;
        splitChild.FirstKeyByte = splitChild.keyBytes[splitChild.keySegmentStart];

        var splitParent = new Node<T>();
        splitParent.childrenBuffer = [splitChild];
        splitParent.childFirstBytes = [splitChild.FirstKeyByte];

        splitParent.keyBytes = child.keyBytes;
        splitParent.keySegmentStart = child.keySegmentStart;
        splitParent.keyBytesLength = child.keySegmentStart + atKeyLength;
        splitParent.FirstKeyByte = child.keyBytes[child.keySegmentStart];
        // Key is materialized lazily in AsKeyValue() — see that method.

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

            var keySegment = searchNode.KeySegmentSpan;
            var keyLength = keySegment.Length;
            var bytesMatched = keyLength == 1 ? 1 : key.CommonPrefixLength(keySegment);

            if (bytesMatched == key.Length)
            {
                return bytesMatched == keyLength ? searchNode.Value : default;
            }
            key = key.Slice(bytesMatched);
            searchKeyByte = key[0];
            matchingIndex = searchNode.FindChildByFirstByte(searchKeyByte);
        }
        return default;
    }

    public T? LongestPrefix(ReadOnlySpan<byte> key)
    {
        T? longestValue = default;
        bool hasLongestValue = Value is not null;

        if (hasLongestValue)
        {
            longestValue = Value;
        }

        if (key.Length == 0)
        {
            return hasLongestValue ? longestValue : default;
        }

        var searchNode = this;

        while (key.Length > 0)
        {
            int matchingIndex = searchNode.FindChildByFirstByte(key[0]);
            if (matchingIndex < 0)
            {
                break;
            }

            searchNode = searchNode.childrenBuffer[matchingIndex];

            var keySegment = searchNode.KeySegmentSpan;
            int keyLength = keySegment.Length;
            int matchingBytes = keyLength == 1 ? 1 : key.CommonPrefixLength(keySegment);

            if (matchingBytes != keyLength)
            {
                break;
            }

            key = key.Slice(matchingBytes);

            if (searchNode.Value is not null)
            {
                longestValue = searchNode.Value;
                hasLongestValue = true;
            }
        }

        return hasLongestValue ? longestValue : default;
    }

    internal Node<T>? FindPrefixMatch(ReadOnlySpan<byte> key)
    {
        var node = this;
        var childIndex = node.FindChildByFirstByte(key[0]);
        while (childIndex > -1)
        {
            node = node.childrenBuffer[childIndex];
            var keySegment = node.KeySegmentSpan;
            var keyLength = keySegment.Length;
            var matchingBytes = keyLength == 1 ? 1 : key.CommonPrefixLength(keySegment);

            if (matchingBytes == key.Length) { return node; }

            // We have to match the whole child key to be a match
            if (matchingBytes != keyLength) return null;

            key = key.Slice(matchingBytes);

            childIndex = node.FindChildByFirstByte(key[0]);
        }
        return null;
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
        foreach (var child in childrenBuffer)
        {
            child.GetValuesCountInternal(ref runningCount);
        }
    }

    public override string ToString()
    {
        return Key ?? Encoding.UTF8.GetString(keyBytes, 0, keyBytesLength);
    }

    internal void Reset()
    {
        keyBytes = EmptyBytes;
        keyBytesLength = 0;
        keySegmentStart = 0;
        Value = default;
        // Clear childFirstBytes first: a concurrent reader that searches an empty
        // byte array just misses, while a stale (longer) byte array paired with the
        // emptied childrenBuffer could index out of bounds.
        childFirstBytes = EmptyBytes;
        childrenBuffer = EmptyNodes;
    }


}
