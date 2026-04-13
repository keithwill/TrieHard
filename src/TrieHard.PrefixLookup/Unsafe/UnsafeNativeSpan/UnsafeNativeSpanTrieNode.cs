using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    /// <summary>
    /// A trie node for <see cref="UnsafeNativeSpanTrie"/> that stores a pointer into a
    /// value slab (managed by the owning trie) rather than an inline unmanaged value.
    /// <para>
    /// The child-navigation layout mirrors <see cref="UnsafeBlittableTrieNode{T}"/>:
    /// <see cref="ChildKeysAddress"/> points to an externally-allocated block whose layout is
    /// <c>ChildCount</c> key bytes followed by <c>ChildCount × 8</c> address bytes.
    /// </para>
    /// <para>
    /// When <see cref="HasValue"/> is <c>true</c> and <see cref="ValuePointer"/> is non-zero,
    /// the pointer addresses bytes within one of the trie's value slabs.
    /// A zero <see cref="ValuePointer"/> with <see cref="HasValue"/> <c>true</c> represents
    /// a stored <c>null</c> value. A non-zero <see cref="ValuePointer"/> with
    /// <see cref="ValueLength"/> zero represents a stored empty byte array.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [SkipLocalsInit]
    public unsafe struct UnsafeNativeSpanTrieNode
    {
        /// <summary>
        /// Pointer to the externally-allocated child keys/addresses block.
        /// Layout: <c>ChildCount</c> key bytes followed by <c>ChildCount * 8</c> address bytes.
        /// </summary>
        public long ChildKeysAddress;

        /// <summary>
        /// Pointer into a value slab managed by the owning trie.
        /// Zero when <see cref="HasValue"/> is <c>false</c>, or when the stored value is <c>null</c>.
        /// Non-zero with <see cref="ValueLength"/> zero represents an empty byte array.
        /// </summary>
        public long ValuePointer;

        /// <summary>Byte length of the stored value. Meaningful only when <see cref="ValuePointer"/> is non-zero.</summary>
        public int ValueLength;

        /// <summary>True when this node is a terminal for a stored key and a value (possibly null) has been set.</summary>
        public bool HasValue;

        /// <summary>Number of children currently stored in the child keys/addresses block.</summary>
        public byte ChildCount;

        /// <summary>Size of this node struct in bytes, including any sequential-layout padding.</summary>
        public static int Size => Unsafe.SizeOf<UnsafeNativeSpanTrieNode>();

        /// <summary>Pointer to the start of the child keys/addresses block as a native integer.</summary>
        public nint ChildKeys => (nint)ChildKeysAddress;

        /// <summary>
        /// Pointer to the start of the child addresses section within the child keys/addresses block
        /// (immediately after the <see cref="ChildCount"/> key bytes).
        /// </summary>
        public nint ChildLocations
        {
            get
            {
                byte* childKeys = (byte*)((nint)ChildKeysAddress).ToPointer();
                return new nint(childKeys + ChildCount);
            }
        }

        /// <summary>
        /// Searches the sorted child key bytes for <paramref name="keyByte"/>.
        /// Returns the index of the match, or a negative value (bitwise complement of the
        /// insertion point) when not found.
        /// </summary>
        public int BinarySearch(byte keyByte)
        {
            Span<byte> keys = new Span<byte>(ChildKeys.ToPointer(), ChildCount);
            return keys.BinarySearch(keyByte);
        }

        /// <summary>Returns the address of the child node at <paramref name="index"/>.</summary>
        public nint GetChild(int index)
        {
            var childrenLocations = (long*)ChildLocations.ToPointer();
            return (nint)childrenLocations[index];
        }

        /// <summary>Returns the key byte of the child at <paramref name="index"/>.</summary>
        public byte GetChildKey(int index)
        {
            byte* childKeys = (byte*)ChildKeys.ToPointer();
            return childKeys[index];
        }

        /// <summary>
        /// Adds a node as a child to this node by inserting the new entry at the sorted
        /// position <paramref name="atIndex"/> and swapping the children block pointer.
        /// </summary>
        public void AddChild(nint node, byte key, int atIndex)
        {
            int childCount = ChildCount;
            int newChildCount = childCount + 1;

            nint childKeys = ChildKeys;
            byte* childKeysPtr = (byte*)childKeys.ToPointer();
            long* childLocationsPtr = (long*)ChildLocations.ToPointer();

            nuint newBytesSize = (nuint)(newChildCount + (newChildCount * 8));
            byte* newKeys = (byte*)NativeMemory.Alloc(newBytesSize);
            long* newLocations = (long*)(newKeys + newChildCount);

            Span<byte> childKeysSpan = new Span<byte>(childKeysPtr, childCount);
            Span<long> childLocationsSpan = new Span<long>(childLocationsPtr, childCount);

            Span<byte> newKeysSpan = new Span<byte>(newKeys, newChildCount);
            Span<long> newLocationsSpan = new Span<long>(newLocations, newChildCount);

            childKeysSpan.Slice(0, atIndex).CopyTo(newKeysSpan);
            newKeysSpan[atIndex] = key;
            childKeysSpan.Slice(atIndex).CopyTo(newKeysSpan.Slice(atIndex + 1));

            childLocationsSpan.Slice(0, atIndex).CopyTo(newLocationsSpan);
            newLocationsSpan[atIndex] = node.ToInt64();
            childLocationsSpan.Slice(atIndex).CopyTo(newLocationsSpan.Slice(atIndex + 1));

            ChildKeysAddress = new nint(newKeys);
            ChildCount += 1;

            if (childCount > 0)
            {
                NativeMemory.Free(childKeys.ToPointer());
            }
        }
    }
}
