using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    [StructLayout(LayoutKind.Explicit, Size = 13)]
    [SkipLocalsInit]
    internal unsafe struct Node
    {

        public static readonly int Size = sizeof(Node);

        [FieldOffset(0)]
        public long ChildKeysAddress;

        [FieldOffset(8)]
        public int ValueLocation;

        [FieldOffset(12)]
        public byte ChildCount;

        public nint ChildKeys => (nint)ChildKeysAddress;

        public nint ChildLocations
        {
            get
            {
                byte* childKeys = (byte*)((nint)ChildKeysAddress).ToPointer();
                return new nint((childKeys + ChildCount));
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
        /// Adds a node as a child to this node by copying the child
        /// data (including the new child in the appropriate place) and then
        /// swapping the pointer reference to the children data
        /// </summary>
        public void AddChild(nint node, byte key, int atIndex)
        {

            int childCount = ChildCount;
            int newChildCount = childCount + 1;

            nint childKeys = ChildKeys;

            byte* childKeysPtr = (byte*)childKeys.ToPointer();
            long* childLocationsPtr = (long*)ChildLocations.ToPointer();

            nuint newBytesSize = (nuint)Convert.ToUInt64(newChildCount + (newChildCount * 8));
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
