using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    /// <summary>
    /// Value-only enumerator for <see cref="UnsafeBlittableTrie{T}"/>.
    /// Unlike <see cref="UnsafeTrieValueEnumerator{T}"/>, this enumerator does not hold a reference
    /// to the trie; values are read directly from each node's inline <c>Value</c> field.
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

        public UnsafeBlittableTrieValueEnumerator<T> GetEnumerator() { return this; }
        IEnumerator<T?> IEnumerable<T?>.GetEnumerator() { return this; }
        IEnumerator IEnumerable.GetEnumerator() { return this; }
        public T? Current => currentValue;
        object? IEnumerator.Current => currentValue;
    }
}
