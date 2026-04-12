using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    /// <summary>
    /// A trie node that embeds its value T directly in the node struct stored in unmanaged memory.
    /// Unlike <see cref="UnsafeTrieNode"/>, there is no index into a managed values list — the value
    /// lives alongside the navigation metadata in the same allocation.
    /// <para>
    /// Uses <see cref="LayoutKind.Sequential"/> instead of <see cref="LayoutKind.Explicit"/> because
    /// <c>[FieldOffset]</c> attributes require compile-time constant offsets, which cannot be expressed
    /// for fields that follow a generic field <c>T Value</c> whose size varies by type argument.
    /// The sequential layout lets the runtime compute correct offsets and padding per closed generic type.
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

        public byte ChildCount;

        /// <summary>
        /// Size of this node struct in bytes, including any trailing padding inserted by the sequential layout
        /// to satisfy the alignment requirement of <see cref="ChildKeysAddress"/>.
        /// </summary>
        public static int Size => Unsafe.SizeOf<UnsafeBlittableTrieNode<T>>();

        public nint ChildKeys => (nint)ChildKeysAddress;

        public nint ChildLocations
        {
            get
            {
                byte* childKeys = (byte*)((nint)ChildKeysAddress).ToPointer();
                return new nint(childKeys + ChildCount);
            }
        }

        public int BinarySearch(byte keyByte)
        {
            Span<byte> keys = new Span<byte>(ChildKeys.ToPointer(), ChildCount);
            return keys.BinarySearch(keyByte);
        }

        public nint GetChild(int index)
        {
            var childrenLocations = (long*)ChildLocations.ToPointer();
            return (nint)childrenLocations[index];
        }

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
