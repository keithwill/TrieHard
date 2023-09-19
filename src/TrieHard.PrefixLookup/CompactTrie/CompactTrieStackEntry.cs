using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    /// <summary>
    /// A struct used to store entries in HybridStacks to allow navigation up and to siblings
    /// during CompactTrie node transversals.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 10)]
    internal unsafe readonly struct CompactTrieStackEntry
    {
        public CompactTrieStackEntry(long node, byte childIndex, byte key)
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
