using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    [StructLayout(LayoutKind.Explicit, Size = 10)]
    internal unsafe readonly struct StackEntry
    {
        public StackEntry(long node, byte childIndex, byte key)
        {
            this.Node = node;
            this.ChildIndex = childIndex;
            this.Key = key;
        }

        [FieldOffset(0)]
        public readonly long Node;

        [FieldOffset(8)]
        public readonly byte ChildIndex;

        [FieldOffset(9)]
        public readonly byte Key;
    }
}
