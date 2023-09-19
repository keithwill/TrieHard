using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    [SkipLocalsInit]
    public unsafe struct CompactTrieUtf8Enumerator<T> : IEnumerable<KeyValuePair<ReadOnlyMemory<byte>, T?>>, IEnumerator<KeyValuePair<ReadOnlyMemory<byte>, T?>>
    {
        private static nuint StackEntrySize = (nuint)Convert.ToUInt64(sizeof(CompactTrieStackEntry));

        private readonly CompactTrie<T>? trie;
        private readonly ReadOnlyMemory<byte> rootPrefix;
        private nint collectNode;
        private nint currentNodeAddress;

        private CompactTrieStackEntry[] nodeStack = Array.Empty<CompactTrieStackEntry>();
        private int nodeStackCount;
        private int nodeStackCapacity;
        
        private readonly byte[]? keyBuffer;
        private byte[] resultKeyBuffer = Empty;

        private bool isDisposed = false;
        private KeyValuePair<ReadOnlyMemory<byte>, T?> currentValue;
        private bool finished = false;
        

        private static readonly byte[] Empty = new byte[0];

    

        public readonly static CompactTrieUtf8Enumerator<T> None = new CompactTrieUtf8Enumerator<T>(null!, ReadOnlyMemory<byte>.Empty, 0);

        internal CompactTrieUtf8Enumerator(CompactTrie<T> trie, ReadOnlyMemory<byte> rootPrefix, nint collectNode, byte[] keyBuffer = null!)
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

            if (nodeStackCount == 0)
            {
                nodeStack = ArrayPool<CompactTrieStackEntry>.Shared.Rent(64);
                nodeStackCapacity = 64;
            }
            if (nodeStackCapacity <= nodeStackCount)
            {
                var newStack = ArrayPool<CompactTrieStackEntry>.Shared.Rent(nodeStackCapacity * 2);
                var oldNodeStack = nodeStack;

                Array.Copy(oldNodeStack, newStack, nodeStackCount);
                ArrayPool<CompactTrieStackEntry>.Shared.Return(oldNodeStack);
                nodeStack = newStack;
            }
            nodeStack[nodeStackCount] = new CompactTrieStackEntry(node, childIndex, key);
            nodeStackCount++;
        }


        private CompactTrieStackEntry Pop()
        {
            var entry = nodeStack[nodeStackCount - 1];
            nodeStackCount--;
            return entry;
        }


        public bool MoveNext()
        {
            if (finished) return false;
            // Movement is descend to first child if one exists
            // If not, backtrack with stack and descend to next sibling
            // Only return values when we descend.

            while (true)
            {
                CompactTrieNode* currentNode = (CompactTrieNode*)currentNodeAddress.ToPointer();
                bool hasValue = false;

                if (currentNode->ValueLocation > -1)
                {
                    var value = trie!.Values[currentNode->ValueLocation];
                    currentValue = new KeyValuePair<ReadOnlyMemory<byte>, T?>(GetKeyFromStack(), value);
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
                        //Node* nextSibling = (Node*)nextSiblingAddress.ToPointer();
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
            int keyByteLength = prefixLength + nodeStackCount;

            if (resultKeyBuffer.Length < keyByteLength)
            {
                if (resultKeyBuffer.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(resultKeyBuffer);
                }
                resultKeyBuffer = ArrayPool<byte>.Shared.Rent(keyByteLength);
            }
            Span<CompactTrieStackEntry> stackEntries = nodeStack.AsSpan(0, nodeStackCount);
            Span<byte> keyBytes = resultKeyBuffer.AsSpan(0, keyByteLength);

            var prefixTarget = keyBytes.Slice(0, rootPrefix.Length);
            rootPrefix.Span.CopyTo(prefixTarget);
            for (int i = 0; i < nodeStackCount - 1; i++)
            {
                var entry = stackEntries[i];
                keyBytes[i + prefixLength] = entry.Key;
            }

            return resultKeyBuffer.AsMemory(0, keyByteLength);
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                if (nodeStackCapacity > 0)
                {
                    ArrayPool<CompactTrieStackEntry>.Shared.Return(nodeStack);
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
                // if (stackSize > 0)
                // {
                //     NativeMemory.Free(stack);
                // }
                if (resultKeyBuffer.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(resultKeyBuffer);
                }
                // stackSize = 0;
                // stackCount = 0;
                this.resultKeyBuffer = Empty;
                this.currentNodeAddress = collectNode;
            }
        }
        public CompactTrieUtf8Enumerator<T> GetEnumerator() { return this; }
        IEnumerator<KeyValuePair<ReadOnlyMemory<byte>, T?>> IEnumerable<KeyValuePair<ReadOnlyMemory<byte>, T?>>.GetEnumerator() { return this; }
        IEnumerator IEnumerable.GetEnumerator() { return this; }
        public KeyValuePair<ReadOnlyMemory<byte>, T?> Current => this.currentValue;
        object IEnumerator.Current => this.currentValue;
    }

}
