using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    [SkipLocalsInit]
    public unsafe struct UnsafeTrieUtf8Enumerator<T> : IEnumerable<KeyValuePair<ReadOnlyMemory<byte>, T?>>, IEnumerator<KeyValuePair<ReadOnlyMemory<byte>, T?>>
    {
        private static nuint StackEntrySize = (nuint)Convert.ToUInt64(sizeof(UnsafeTrieStackEntry));

        private readonly UnsafeTrie<T>? trie;
        private readonly ReadOnlyMemory<byte> rootPrefix;
        private nint collectNode;
        private nint currentNodeAddress;

        private UnsafeTrieStackEntry[] nodeStack = Array.Empty<UnsafeTrieStackEntry>();
        private int nodeStackCount;
        private int nodeStackCapacity;
        
        private readonly byte[]? keyBuffer;
        private byte[] resultKeyBuffer = Empty;

        private bool isDisposed = false;
        private KeyValuePair<ReadOnlyMemory<byte>, T?> currentValue;
        private bool finished = false;
        

        private static readonly byte[] Empty = new byte[0];

    

        public readonly static UnsafeTrieUtf8Enumerator<T> None = new UnsafeTrieUtf8Enumerator<T>(null!, ReadOnlyMemory<byte>.Empty, 0);

        internal UnsafeTrieUtf8Enumerator(UnsafeTrie<T> trie, ReadOnlyMemory<byte> rootPrefix, nint collectNode, byte[] keyBuffer = null!)
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

        public UnsafeTrieUtf8Enumerator()
        {
            finished = true;
        }

        private void Push(nint node, byte childIndex, byte key)
        {

            if (nodeStackCount == 0)
            {
                nodeStack = ArrayPool<UnsafeTrieStackEntry>.Shared.Rent(64);
                nodeStackCapacity = 64;
            }
            if (nodeStackCapacity <= nodeStackCount)
            {
                var newStack = ArrayPool<UnsafeTrieStackEntry>.Shared.Rent(nodeStackCapacity * 2);
                var oldNodeStack = nodeStack;

                Array.Copy(oldNodeStack, newStack, nodeStackCount);
                ArrayPool<UnsafeTrieStackEntry>.Shared.Return(oldNodeStack);
                nodeStack = newStack;
            }
            nodeStack[nodeStackCount] = new UnsafeTrieStackEntry(node, childIndex, key);
            nodeStackCount++;
        }


        private UnsafeTrieStackEntry Pop()
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
                UnsafeTrieNode* currentNode = (UnsafeTrieNode*)currentNodeAddress.ToPointer();
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
                    UnsafeTrieStackEntry parentEntry = Pop();
                    nint parentNodeAddress = (nint)parentEntry.Node;
                    UnsafeTrieNode* parentNode = (UnsafeTrieNode*)parentNodeAddress.ToPointer();

                    if (parentEntry.ChildIndex >= parentNode->ChildCount - 1)
                    {
                        // Parent has no more children to transverse
                        currentNodeAddress = parentNodeAddress;
                        continue;
                    }
                    else
                    {
                        
                        // From the current Node's parent, descend into the next sibling of the current node
                        byte childIndex = parentEntry.ChildIndex;
                        childIndex++;
                        var nextSiblingAddress = parentNode->GetChild(childIndex);
                        //Node* nextSibling = (Node*)nextSiblingAddress.ToPointer();
                        Push(parentNodeAddress, childIndex, parentNode->GetChildKey(childIndex));
                        currentNodeAddress = nextSiblingAddress;

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
            Span<UnsafeTrieStackEntry> stackEntries = nodeStack.AsSpan(0, nodeStackCount);
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
                    ArrayPool<UnsafeTrieStackEntry>.Shared.Return(nodeStack);
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
        public UnsafeTrieUtf8Enumerator<T> GetEnumerator() { return this; }
        IEnumerator<KeyValuePair<ReadOnlyMemory<byte>, T?>> IEnumerable<KeyValuePair<ReadOnlyMemory<byte>, T?>>.GetEnumerator() { return this; }
        IEnumerator IEnumerable.GetEnumerator() { return this; }
        public KeyValuePair<ReadOnlyMemory<byte>, T?> Current => this.currentValue;
        object IEnumerator.Current => this.currentValue;
    }

}
