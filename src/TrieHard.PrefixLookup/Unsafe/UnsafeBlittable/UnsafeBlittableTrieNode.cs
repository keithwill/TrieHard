using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    /// <summary>
    /// A node in a <see cref="UnsafeBlittableTrie{T}"/>, stored in unmanaged memory.
    /// <para>
    /// Each node holds a value of type <typeparamref name="T"/> inline alongside its navigation
    /// metadata. Children are maintained as a sorted array of key bytes with a parallel array of
    /// node pointers, both stored in a single externally-allocated unmanaged block pointed to by
    /// <see cref="ChildKeysAddress"/>. Child lookup uses binary search, so insertion and lookup
    /// cost O(log k) where k is the number of children at a node.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [SkipLocalsInit]
    public unsafe struct UnsafeBlittableTrieNode<T> where T : unmanaged
    {
        /// <summary>
        /// Pointer to the externally-allocated child keys/addresses block.
        /// Layout of that block: <c>ChildCount</c> key bytes followed by <c>ChildCount * 8</c> address bytes.
        /// </summary>
        public long ChildKeysAddress;

        /// <summary>The value stored at this node, valid only when <see cref="HasValue"/> is true.</summary>
        public T Value;

        /// <summary>True when this node is a terminal for a stored key and <see cref="Value"/> is meaningful.</summary>
        public bool HasValue;

        /// <summary>The number of immediate children of this node.</summary>
        public byte ChildCount;

        /// <summary>
        /// Size of this node struct in bytes, including any trailing padding inserted by the sequential layout
        /// to satisfy the alignment requirement of <see cref="ChildKeysAddress"/>.
        /// </summary>
        public static int Size => Unsafe.SizeOf<UnsafeBlittableTrieNode<T>>();

        /// <summary>Pointer to the start of the child-keys block (the key bytes portion).</summary>
        public nint ChildKeys => (nint)ChildKeysAddress;

        /// <summary>
        /// Pointer to the child-addresses portion of the child block, which begins immediately after the
        /// <see cref="ChildCount"/> key bytes.
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
        /// Searches the sorted child-key array for <paramref name="keyByte"/> using binary search.
        /// </summary>
        /// <returns>
        /// The non-negative index of the child if found; otherwise the bitwise complement (~) of the
        /// index at which <paramref name="keyByte"/> would be inserted to preserve sort order.
        /// </returns>
        public int BinarySearch(byte keyByte)
        {
            Span<byte> keys = new Span<byte>(ChildKeys.ToPointer(), ChildCount);
            return keys.BinarySearch(keyByte);
        }

        /// <summary>Returns the node pointer for the child at the given sorted <paramref name="index"/>.</summary>
        public nint GetChild(int index)
        {
            var childrenLocations = (long*)ChildLocations.ToPointer();
            return (nint)childrenLocations[index];
        }

        /// <summary>Returns the key byte for the child at the given sorted <paramref name="index"/>.</summary>
        public byte GetChildKey(int index)
        {
            byte* childKeys = (byte*)ChildKeys.ToPointer();
            return childKeys[index];
        }

        /// <summary>
        /// Adds a node as a child to this node by copying the child data (including the new child
        /// in the appropriate sorted position) and then swapping the pointer to the children block.
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
