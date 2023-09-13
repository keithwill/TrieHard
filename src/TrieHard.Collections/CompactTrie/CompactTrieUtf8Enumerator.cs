using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrieHard.Collections.Contributions.keithwill.CompactTrie
{
    using System;
    using System.Buffers;
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    namespace TrieHard.Collections
    {
        public unsafe struct CompactTrieUtf8Enumerator<T> : IEnumerable<KeyValuePair<ReadOnlyMemory<byte>, T>>, IEnumerator<KeyValuePair<ReadOnlyMemory<byte>, T>>
        {
            private static nuint StackEntrySize = (nuint)Convert.ToUInt64(sizeof(StackEntry));

            private readonly CompactTrie<T> trie;
            private readonly ReadOnlyMemory<byte> rootPrefix;
            private nint collectNode;
            private nint currentNodeAddress;
            private int stackCount;
            private int stackSize;
            private void* stack;
            private bool isDisposed = false;
            private KeyValuePair<ReadOnlyMemory<byte>, T> currentValue;
            private bool finished = false;
            private byte[] buffer = Empty;
            private static readonly byte[] Empty = new byte[0];

            internal CompactTrieUtf8Enumerator(CompactTrie<T> trie, ReadOnlyMemory<byte> rootPrefix, nint collectNode)
            {
                this.trie = trie;
                this.rootPrefix = rootPrefix;
                this.collectNode = collectNode;
                this.currentNodeAddress = collectNode;
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
                    Node* currentNode = (Node*)currentNodeAddress.ToPointer();
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
                        Node* parentNode = (Node*)parentNodeAddress.ToPointer();

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
                if (buffer.Length < keyByteLength)
                {
                    if (buffer.Length > 0)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                    buffer = ArrayPool<byte>.Shared.Rent(keyByteLength);
                }
                Span<StackEntry> stackEntries = new Span<StackEntry>(this.stack, stackCount);
                Span<byte> keyBytes = buffer.AsSpan(0, keyByteLength);

                for (int i = 0; i < stackEntries.Length; i++)
                {
                    var entry = stackEntries[i];
                    keyBytes[i + prefixLength] = entry.Key;
                }
                var prefixTarget = keyBytes.Slice(0, rootPrefix.Length);
                rootPrefix.Span.CopyTo(prefixTarget);
                return buffer.AsMemory(0, keyByteLength);
            }

            public void Dispose()
            {
                if (!isDisposed)
                {
                    NativeMemory.Free(stack);
                    ArrayPool<byte>.Shared.Return(buffer);
                    this.isDisposed = true;
                }
            }
            public void Reset()
            {
                if (trie is not null)
                {
                    NativeMemory.Free(stack);
                    stackSize = 0;
                    stackCount = 0;
                    this.currentNodeAddress = collectNode;
                }
            }
            public CompactTrieUtf8Enumerator<T> GetEnumerator() { return this; }
            IEnumerator<KeyValuePair<ReadOnlyMemory<byte>, T>> IEnumerable<KeyValuePair<ReadOnlyMemory<byte>, T>>.GetEnumerator() { return this; }
            IEnumerator IEnumerable.GetEnumerator() { return this; }
            public KeyValuePair<ReadOnlyMemory<byte>, T> Current => this.currentValue;
            object IEnumerator.Current => this.currentValue;
        }

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

}
