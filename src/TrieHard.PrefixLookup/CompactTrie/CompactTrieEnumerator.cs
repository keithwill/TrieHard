﻿using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace TrieHard.Collections
{
    [SkipLocalsInit]
    public unsafe struct CompactTrieEnumerator<T> : IEnumerable<KeyValuePair<string, T?>>, IEnumerator<KeyValuePair<string, T?>>
    {
        private static nuint StackEntrySize = (nuint)Convert.ToUInt64(sizeof(CompactTrieStackEntry));

        private readonly CompactTrie<T>? trie;
        private readonly ReadOnlyMemory<byte> rootPrefix;
        private nint collectNode;
        private readonly byte[]? keyBuffer;
        private nint currentNodeAddress;
        private int stackCount;
        private int stackSize;
        private void* stack;
        private bool isDisposed = false;
        private KeyValuePair<string, T?> currentValue;
        private bool finished = false;
        private const int initialStackSize = 32;

        public static readonly CompactTrieEnumerator<T> None = new CompactTrieEnumerator<T>(null!, ReadOnlyMemory<byte>.Empty, 0) { finished = true};

        internal CompactTrieEnumerator(CompactTrie<T> trie, ReadOnlyMemory<byte> rootPrefix, nint collectNode, byte[]? keyBuffer = null)
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

        public CompactTrieEnumerator()
        {
            finished = true;
        }

        private void Push(nint node, byte childIndex, byte key)
        {
            if (stackCount == 0)
            {
                stack = NativeMemory.Alloc(initialStackSize, StackEntrySize);
                stackSize = initialStackSize;
            }
            if (stackSize < stackCount + 1)
            {
                var newSizeInt = stackSize * 8;
                var newSize = (nuint)Convert.ToUInt64(newSizeInt);
                void* tmp = NativeMemory.Alloc(newSize, StackEntrySize);
                Span<CompactTrieStackEntry> newStack = new Span<CompactTrieStackEntry>(tmp, newSizeInt);
                Span<CompactTrieStackEntry> oldStack = new Span<CompactTrieStackEntry>(stack, stackSize);
                oldStack.CopyTo(newStack);
                NativeMemory.Free(stack);
                stack = tmp;
                stackSize = newSizeInt;
            }
            Span<CompactTrieStackEntry> stackEntries = new Span<CompactTrieStackEntry>(stack, stackSize);
            stackEntries[stackCount] = new CompactTrieStackEntry ( node, childIndex, key );
            stackCount++;
        }

        private CompactTrieStackEntry Pop()
        {
            Span<CompactTrieStackEntry> stackEntries = new Span<CompactTrieStackEntry>(stack, stackSize);
            CompactTrieStackEntry entry = stackEntries[stackCount - 1];
            stackCount--;
            return entry;
        }

        private CompactTrieStackEntry Peek()
        {
            Span<CompactTrieStackEntry> stackEntries = new Span<CompactTrieStackEntry>(stack, stackSize);
            CompactTrieStackEntry entry = stackEntries[stackCount - 1];
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
                    currentValue = new KeyValuePair<string, T?>(GetKeyFromStack(), value);
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
                    CompactTrieStackEntry parentEntry = Pop();
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

        private string GetKeyFromStack()
        {
            int prefixLength = rootPrefix.Length;
            Span<CompactTrieStackEntry> stackEntries = new Span<CompactTrieStackEntry>(this.stack, stackCount);
            Span<byte> keyBytes = stackalloc byte[prefixLength + stackCount];
            for(int i = 0; i < stackEntries.Length; i++)
            {
                var entry = stackEntries[i];
                keyBytes[i + prefixLength] = entry.Key;
            }
            var prefixTarget = keyBytes.Slice(0, rootPrefix.Length);
            rootPrefix.Span.CopyTo(prefixTarget);
            return Encoding.UTF8.GetString(keyBytes);
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                NativeMemory.Free(stack);
                if (keyBuffer is not null && keyBuffer.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(keyBuffer);
                }
                this.isDisposed = true;
            }
        }
        public void Reset() {
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
        public CompactTrieEnumerator<T> GetEnumerator() { return this; }
        IEnumerator<KeyValuePair<string, T?>> IEnumerable<KeyValuePair<string, T?>>.GetEnumerator() { return this; }
        IEnumerator IEnumerable.GetEnumerator() { return this; }
        public KeyValuePair<string, T?> Current => this.currentValue;
        object IEnumerator.Current => this.currentValue;
    }

}
