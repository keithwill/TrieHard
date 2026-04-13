using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    /// <summary>
    /// Value-only enumerator returned by <see cref="UnsafeNativeSpanTrie.SearchValues(string)"/>
    /// that yields values in lexicographic key order without allocating key strings.
    /// <para>
    /// Values are returned as <see cref="NativeByteSpan?"/> — a zero-allocation struct that
    /// points directly into the trie's unmanaged value slabs. The spans are valid only for the
    /// lifetime of the owning trie (see <see cref="NativeByteSpan"/> for details).
    /// </para>
    /// <para>
    /// The enumerator allocates a small unmanaged traversal stack. Always dispose it (via
    /// <c>using</c> or an explicit <see cref="Dispose"/> call) to avoid unmanaged memory leaks.
    /// </para>
    /// </summary>
    [SkipLocalsInit]
    public unsafe struct UnsafeNativeSpanTrieValueEnumerator : IEnumerable<NativeByteSpan?>, IEnumerator<NativeByteSpan?>
    {
        private static readonly nuint StackEntrySize = (nuint)Convert.ToUInt64(sizeof(StackValueEntry));

        private readonly nint collectNode;
        private nint currentNodeAddress;
        private int stackCount;
        private int stackSize;
        private void* stack;
        private bool isDisposed;
        private NativeByteSpan? currentValue;
        private bool finished;

        /// <summary>A pre-finished, empty enumerator that yields no results.</summary>
        public readonly static UnsafeNativeSpanTrieValueEnumerator None =
            new UnsafeNativeSpanTrieValueEnumerator(0) { finished = true };

        internal UnsafeNativeSpanTrieValueEnumerator(nint collectNode)
        {
            this.collectNode = collectNode;
            this.currentNodeAddress = collectNode;
            if (collectNode == 0)
            {
                finished = true;
            }
        }

        public UnsafeNativeSpanTrieValueEnumerator()
        {
            finished = true;
        }

        private void Push(nint node, byte childIndex)
        {
            if (stackSize == 0)
            {
                stack = NativeMemory.Alloc(4, StackEntrySize);
                stackSize = 4;
            }
            if (stackSize < stackCount + 1)
            {
                var newSizeInt = stackSize * 2;
                void* tmp = NativeMemory.Alloc((nuint)Convert.ToUInt64(newSizeInt), StackEntrySize);
                Span<StackValueEntry> newStack = new Span<StackValueEntry>(tmp, newSizeInt);
                Span<StackValueEntry> oldStack = new Span<StackValueEntry>(stack, stackSize);
                oldStack.CopyTo(newStack);
                NativeMemory.Free(stack);
                stack = tmp;
                stackSize = newSizeInt;
            }
            Span<StackValueEntry> stackEntries = new Span<StackValueEntry>(stack, stackSize);
            stackEntries[stackCount] = new StackValueEntry(node, childIndex);
            stackCount++;
        }

        private StackValueEntry Pop()
        {
            Span<StackValueEntry> stackEntries = new Span<StackValueEntry>(stack, stackSize);
            StackValueEntry entry = stackEntries[stackCount - 1];
            stackCount--;
            return entry;
        }

        /// <summary>Advances the enumerator to the next value. Returns <c>false</c> when exhausted.</summary>
        public bool MoveNext()
        {
            if (finished) return false;

            while (true)
            {
                UnsafeNativeSpanTrieNode* currentNode = (UnsafeNativeSpanTrieNode*)currentNodeAddress.ToPointer();
                bool hasValue = false;

                if (currentNode->HasValue)
                {
                    currentValue = UnsafeNativeSpanTrieEnumerator.ReadNodeValue(currentNode);
                    hasValue = true;
                }

                if (currentNode->ChildCount > 0)
                {
                    Push(currentNodeAddress, 0);
                    currentNodeAddress = currentNode->GetChild(0);
                    if (hasValue) return true;
                    continue;
                }

                // Backtrack until we find a node to descend on
                while (true)
                {
                    if (currentNodeAddress == collectNode)
                    {
                        finished = true;
                        return hasValue;
                    }
                    StackValueEntry parentEntry = Pop();
                    nint parentNodeAddress = (nint)parentEntry.Node;
                    UnsafeNativeSpanTrieNode* parentNode = (UnsafeNativeSpanTrieNode*)parentNodeAddress.ToPointer();

                    if (parentEntry.ChildIndex >= parentNode->ChildCount - 1)
                    {
                        // Parent has no more children to traverse
                        currentNodeAddress = parentNodeAddress;
                        continue;
                    }
                    else
                    {
                        // Descend into the next sibling of the current node
                        byte childIndex = (byte)(parentEntry.ChildIndex + 1);
                        var nextSiblingAddress = parentNode->GetChild(childIndex);
                        Push(parentNodeAddress, childIndex);
                        currentNodeAddress = nextSiblingAddress;

                        if (hasValue) return true;
                        break;
                    }
                }
            }
        }

        /// <summary>Frees the unmanaged traversal stack.</summary>
        public void Dispose()
        {
            if (!isDisposed)
            {
                if (stackSize > 0)
                {
                    NativeMemory.Free(stack);
                }
                isDisposed = true;
            }
        }

        /// <summary>Resets the enumerator to the beginning of the result set.</summary>
        public void Reset()
        {
            if (collectNode != 0)
            {
                if (stackSize > 0)
                {
                    NativeMemory.Free(stack);
                }
                stackSize = 0;
                stackCount = 0;
                currentNodeAddress = collectNode;
            }
        }

        /// <summary>Returns this enumerator, enabling use in <c>foreach</c> directly on the struct.</summary>
        public UnsafeNativeSpanTrieValueEnumerator GetEnumerator() { return this; }
        IEnumerator<NativeByteSpan?> IEnumerable<NativeByteSpan?>.GetEnumerator() { return this; }
        IEnumerator IEnumerable.GetEnumerator() { return this; }
        /// <summary>The current value.</summary>
        public NativeByteSpan? Current => currentValue;
        object? IEnumerator.Current => currentValue;
    }
}
