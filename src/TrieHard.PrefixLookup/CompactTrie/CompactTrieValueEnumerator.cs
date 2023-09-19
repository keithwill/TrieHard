using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    [SkipLocalsInit]
    public unsafe struct CompactTrieValueEnumerator<T> : IEnumerable<T?>, IEnumerator<T?>
    {
        private static nuint StackEntrySize = (nuint)Convert.ToUInt64(sizeof(StackValueEntry));

        private readonly CompactTrie<T>? trie;
        private nint collectNode;
        private nint currentNodeAddress;
        private int stackCount;
        private int stackSize;
        private void* stack;
        private bool isDisposed = false;
        private T? currentValue;
        private bool finished = false;

        public readonly static CompactTrieValueEnumerator<T> None = new CompactTrieValueEnumerator<T>(null, 0) { finished = true};

        internal CompactTrieValueEnumerator(CompactTrie<T>? trie, nint collectNode)
        {
            this.trie = trie;
            this.collectNode = collectNode;
            this.currentNodeAddress = collectNode;
            if (trie == null)
            {
                finished = true;
            }
        }

        public CompactTrieValueEnumerator()
        {
            finished = true;
        }

        private void Push(nint node, byte childIndex, byte key)
        {
            if (stackCount == 0)
            {
                stack = NativeMemory.Alloc(4, StackEntrySize);
                stackSize = 4;
            }
            if (stackSize < stackCount + 1)
            {
                var newSizeInt = stackSize * 2;
                var newSize = (nuint)Convert.ToUInt64(newSizeInt);
                void* tmp = NativeMemory.Alloc(newSize, StackEntrySize);
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

        private StackValueEntry Peek()
        {
            Span<StackValueEntry> stackEntries = new Span<StackValueEntry>(stack, stackSize);
            StackValueEntry entry = stackEntries[stackCount - 1];
            return entry;
        }

        public bool MoveNext()
        {
            if (finished) return false;
            // Movement is descend to first child if one exists
            // If not, backtrack with stack and descend to next sibbling
            // Only return values when we descend.

            while (true)
            {
                CompactTrieNode* currentNode = (CompactTrieNode*)currentNodeAddress.ToPointer();
                bool hasValue = false;

                if (currentNode->ValueLocation > -1)
                {
                    var value = trie!.Values[currentNode->ValueLocation];
                    currentValue = value;
                    hasValue = true;
                }

                if (currentNode->ChildCount > 0)
                {
                    Push(currentNodeAddress, 0, currentNode->GetChildKey(0));
                    currentNodeAddress = currentNode->GetChild(0);
                    if (hasValue) return true;
                    continue;
                }

                //Backtrack until we find a node to descend on
                while (true)
                {
                    if (currentNodeAddress == collectNode)
                    {
                        // We made it back to the top
                        finished = true;
                        return hasValue;
                    }
                    StackValueEntry parentEntry = Pop();
                    nint parentNodeAddress = (nint)parentEntry.Node;
                    CompactTrieNode* parentNode = (CompactTrieNode*)parentNodeAddress.ToPointer();

                    if (parentEntry.ChildIndex >= parentNode->ChildCount - 1)
                    {
                        // Parent has no more children to transverse
                        currentNodeAddress = parentNodeAddress;
                        continue;
                    }
                    else
                    {
                        // From the current Node's parent, descend into the next sibbling of the current node
                        byte childIndex = parentEntry.ChildIndex;
                        childIndex++;
                        var nextSibblingAddress = parentNode->GetChild(childIndex);
                        Push(parentNodeAddress, childIndex, parentNode->GetChildKey(childIndex));
                        currentNodeAddress = nextSibblingAddress;

                        if (hasValue)
                        {
                            return true;
                        }
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
                this.isDisposed = true;
            }
        }
        public void Reset()
        {
            if (trie is not null)
            {
                if (stackSize > 0)
                {
                    NativeMemory.Free(stack);
                }
                stackSize = 0;
                stackCount = 0;
                this.currentNodeAddress = collectNode;
            }
        }
        public CompactTrieValueEnumerator<T> GetEnumerator() { return this; }
        IEnumerator<T?> IEnumerable<T?>.GetEnumerator() { return this; }
        IEnumerator IEnumerable.GetEnumerator() { return this; }
        public T? Current => this.currentValue;
        object? IEnumerator.Current => this.currentValue;
    }

    [StructLayout(LayoutKind.Explicit, Size = 9)]
    internal unsafe readonly struct StackValueEntry
    {
        public StackValueEntry(long node, byte childIndex)
        {
            this.Node = node;
            this.ChildIndex = childIndex;
        }

        [FieldOffset(0)]
        public readonly long Node;

        [FieldOffset(8)]
        public readonly byte ChildIndex;

    }

}
