using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    /// <summary>
    /// An enumerator that yields values from a <see cref="UnsafeBlittableTrie{T}"/>, starting from a
    /// given node and covering all of its descendants in lexicographic key order, without reconstructing
    /// the key strings.
    /// <para>
    /// Obtain instances via <see cref="UnsafeBlittableTrie{T}.SearchValues(string)"/>. Do not construct directly.
    /// </para>
    /// <para>
    /// This struct allocates a small unmanaged traversal stack. Always dispose it — either explicitly
    /// or by iterating with <see langword="foreach"/>, which calls <see cref="Dispose"/> automatically.
    /// </para>
    /// </summary>
    [SkipLocalsInit]
    public unsafe struct UnsafeBlittableTrieValueEnumerator<T> : IEnumerable<T?>, IEnumerator<T?>
        where T : unmanaged
    {
        private static readonly nuint StackEntrySize = (nuint)Convert.ToUInt64(sizeof(StackValueEntry));

        private readonly nint collectNode;
        private nint currentNodeAddress;
        private int stackCount;
        private int stackSize;
        private void* stack;
        private bool isDisposed;
        private T? currentValue;
        private bool finished;

        /// <summary>A pre-finished, empty enumerator that yields no elements.</summary>
        public readonly static UnsafeBlittableTrieValueEnumerator<T> None =
            new UnsafeBlittableTrieValueEnumerator<T>(0) { finished = true };

        internal UnsafeBlittableTrieValueEnumerator(nint collectNode)
        {
            this.collectNode = collectNode;
            this.currentNodeAddress = collectNode;
            if (collectNode == 0)
            {
                finished = true;
            }
        }

        public UnsafeBlittableTrieValueEnumerator()
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

        /// <summary>Advances the enumerator to the next value.</summary>
        /// <returns><see langword="true"/> if a value is available in <see cref="Current"/>; <see langword="false"/> when all entries have been yielded.</returns>
        public bool MoveNext()
        {
            if (finished) return false;

            while (true)
            {
                UnsafeBlittableTrieNode<T>* currentNode = (UnsafeBlittableTrieNode<T>*)currentNodeAddress.ToPointer();
                bool hasValue = false;

                if (currentNode->HasValue)
                {
                    currentValue = currentNode->Value;
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
                    UnsafeBlittableTrieNode<T>* parentNode = (UnsafeBlittableTrieNode<T>*)parentNodeAddress.ToPointer();

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

        /// <summary>
        /// Releases the unmanaged traversal stack.
        /// Must be called when enumeration is complete unless a <see langword="foreach"/> loop is used.
        /// </summary>
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

        /// <summary>Resets the enumerator to the beginning of the subtree so it can be iterated again.</summary>
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

        /// <summary>Returns this enumerator, enabling use in <see langword="foreach"/> loops directly on the enumerator struct.</summary>
        public UnsafeBlittableTrieValueEnumerator<T> GetEnumerator() { return this; }
        IEnumerator<T?> IEnumerable<T?>.GetEnumerator() { return this; }
        IEnumerator IEnumerable.GetEnumerator() { return this; }

        /// <summary>Gets the current value. Valid only after a successful call to <see cref="MoveNext"/>.</summary>
        public T? Current => currentValue;
        object? IEnumerator.Current => currentValue;
    }
}
