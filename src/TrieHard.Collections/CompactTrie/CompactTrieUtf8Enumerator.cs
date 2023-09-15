using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    [SkipLocalsInit]
    public unsafe struct CompactTrieUtf8Enumerator<T> : IEnumerable<KeyValuePair<ReadOnlyMemory<byte>, T>>, IEnumerator<KeyValuePair<ReadOnlyMemory<byte>, T>>
    {
        private static nuint StackEntrySize = (nuint)Convert.ToUInt64(sizeof(StackEntry));

        private readonly CompactTrie<T> trie;
        private readonly ReadOnlyMemory<byte> rootPrefix;
        private nint collectNode;
        private readonly byte[] keyBuffer;
        private nint currentNodeAddress;
        private int stackCount;
        private int stackSize;
        private void* stack;
        private bool isDisposed = false;
        private KeyValuePair<ReadOnlyMemory<byte>, T> currentValue;
        private bool finished = false;
        private byte[] resultKeyBuffer = Empty;
        private static readonly byte[] Empty = new byte[0];

        public readonly static CompactTrieUtf8Enumerator<T> None = new CompactTrieUtf8Enumerator<T>(null, ReadOnlyMemory<byte>.Empty, 0);

        internal CompactTrieUtf8Enumerator(CompactTrie<T> trie, ReadOnlyMemory<byte> rootPrefix, nint collectNode, byte[] keyBuffer = null)
        {
            this.trie = trie;
            this.rootPrefix = rootPrefix;
            this.collectNode = collectNode;
            this.keyBuffer = keyBuffer;
            this.currentNodeAddress = collectNode;
            if (trie == null)
            {
                finished = true;
            }
        }

        public CompactTrieUtf8Enumerator()
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
                Span<StackEntry> newStack = new Span<StackEntry>(tmp, newSizeInt);
                Span<StackEntry> oldStack = new Span<StackEntry>(stack, stackSize);
                oldStack.CopyTo(newStack);
                NativeMemory.Free(stack);
                stack = tmp;
                stackSize = newSizeInt;
            }
            Span<StackEntry> stackEntries = new Span<StackEntry>(stack, stackSize);
            stackEntries[stackCount] = new StackEntry(node, childIndex, key);
            stackCount++;
        }

        private StackEntry Pop()
        {
            Span<StackEntry> stackEntries = new Span<StackEntry>(stack, stackSize);
            StackEntry entry = stackEntries[stackCount - 1];
            stackCount--;
            return entry;
        }

        private StackEntry Peek()
        {
            Span<StackEntry> stackEntries = new Span<StackEntry>(stack, stackSize);
            StackEntry entry = stackEntries[stackCount - 1];
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
                CompactNodeTrie* currentNode = (CompactNodeTrie*)currentNodeAddress.ToPointer();
                bool hasValue = false;

                if (currentNode->ValueLocation > -1)
                {
                    var value = trie.Values[currentNode->ValueLocation];
                    currentValue = new KeyValuePair<ReadOnlyMemory<byte>, T>(GetKeyFromStack(), value);
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
                    StackEntry parentEntry = Pop();
                    nint parentNodeAddress = (nint)parentEntry.Node;
                    CompactNodeTrie* parentNode = (CompactNodeTrie*)parentNodeAddress.ToPointer();

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
                        //Node* nextSibbling = (Node*)nextSibblingAddress.ToPointer();
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

        private ReadOnlyMemory<byte> GetKeyFromStack()
        {
            int prefixLength = rootPrefix.Length;
            int keyByteLength = prefixLength + stackCount;
            if (resultKeyBuffer.Length < keyByteLength)
            {
                if (resultKeyBuffer.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(resultKeyBuffer);
                }
                resultKeyBuffer = ArrayPool<byte>.Shared.Rent(keyByteLength);
            }
            Span<StackEntry> stackEntries = new Span<StackEntry>(this.stack, stackCount);
            Span<byte> keyBytes = resultKeyBuffer.AsSpan(0, keyByteLength);

            for (int i = 0; i < stackEntries.Length; i++)
            {
                var entry = stackEntries[i];
                keyBytes[i + prefixLength] = entry.Key;
            }
            var prefixTarget = keyBytes.Slice(0, rootPrefix.Length);
            rootPrefix.Span.CopyTo(prefixTarget);
            return resultKeyBuffer.AsMemory(0, keyByteLength);
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                if (stackSize > 0)
                {
                    NativeMemory.Free(stack);
                }
                if (resultKeyBuffer.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(resultKeyBuffer);
                }
                if (keyBuffer is not null && keyBuffer.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(keyBuffer);
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
                if (resultKeyBuffer.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(resultKeyBuffer);
                }
                stackSize = 0;
                stackCount = 0;
                this.resultKeyBuffer = Empty;
                this.currentNodeAddress = collectNode;
            }
        }
        public CompactTrieUtf8Enumerator<T> GetEnumerator() { return this; }
        IEnumerator<KeyValuePair<ReadOnlyMemory<byte>, T>> IEnumerable<KeyValuePair<ReadOnlyMemory<byte>, T>>.GetEnumerator() { return this; }
        IEnumerator IEnumerable.GetEnumerator() { return this; }
        public KeyValuePair<ReadOnlyMemory<byte>, T> Current => this.currentValue;
        object IEnumerator.Current => this.currentValue;
    }

}
