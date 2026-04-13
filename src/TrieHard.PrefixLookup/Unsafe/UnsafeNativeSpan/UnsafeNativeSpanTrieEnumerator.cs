using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace TrieHard.Collections
{
    /// <summary>
    /// Enumerator returned by <see cref="UnsafeNativeSpanTrie.Search(string)"/> and
    /// <see cref="UnsafeNativeSpanTrie.GetEnumerator()"/> that yields key-value pairs in
    /// lexicographic order.
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
    public unsafe struct UnsafeNativeSpanTrieEnumerator : IEnumerable<KeyValue<NativeByteSpan?>>, IEnumerator<KeyValue<NativeByteSpan?>>
    {
        private static readonly nuint StackEntrySize = (nuint)Convert.ToUInt64(sizeof(UnsafeTrieStackEntry));

        private readonly ReadOnlyMemory<byte> rootPrefix;
        private readonly nint collectNode;
        private byte[]? keyBuffer;
        private nint currentNodeAddress;
        private int stackCount;
        private int stackSize;
        private void* stack;
        private bool isDisposed;
        private KeyValue<NativeByteSpan?> currentValue;
        private bool finished;
        private const int InitialStackSize = 32;

        /// <summary>A pre-finished, empty enumerator that yields no results.</summary>
        public static readonly UnsafeNativeSpanTrieEnumerator None =
            new UnsafeNativeSpanTrieEnumerator(ReadOnlyMemory<byte>.Empty, 0) { finished = true };

        internal UnsafeNativeSpanTrieEnumerator(ReadOnlyMemory<byte> rootPrefix, nint collectNode, byte[]? keyBuffer = null)
        {
            this.rootPrefix = rootPrefix;
            this.collectNode = collectNode;
            this.keyBuffer = keyBuffer;
            this.currentNodeAddress = collectNode;
            if (collectNode == 0)
            {
                finished = true;
            }
        }

        public UnsafeNativeSpanTrieEnumerator()
        {
            finished = true;
        }

        private void Push(nint node, byte childIndex, byte key)
        {
            if (stackSize == 0)
            {
                stack = NativeMemory.Alloc(InitialStackSize, StackEntrySize);
                stackSize = InitialStackSize;
            }
            if (stackSize < stackCount + 1)
            {
                var newSizeInt = stackSize * 8;
                void* tmp = NativeMemory.Alloc((nuint)Convert.ToUInt64(newSizeInt), StackEntrySize);
                Span<UnsafeTrieStackEntry> newStack = new Span<UnsafeTrieStackEntry>(tmp, newSizeInt);
                Span<UnsafeTrieStackEntry> oldStack = new Span<UnsafeTrieStackEntry>(stack, stackSize);
                oldStack.CopyTo(newStack);
                NativeMemory.Free(stack);
                stack = tmp;
                stackSize = newSizeInt;
            }
            Span<UnsafeTrieStackEntry> stackEntries = new Span<UnsafeTrieStackEntry>(stack, stackSize);
            stackEntries[stackCount] = new UnsafeTrieStackEntry(node, childIndex, key);
            stackCount++;
        }

        private UnsafeTrieStackEntry Pop()
        {
            Span<UnsafeTrieStackEntry> stackEntries = new Span<UnsafeTrieStackEntry>(stack, stackSize);
            UnsafeTrieStackEntry entry = stackEntries[stackCount - 1];
            stackCount--;
            return entry;
        }

        /// <summary>Advances the enumerator to the next key-value pair. Returns <c>false</c> when exhausted.</summary>
        public bool MoveNext()
        {
            if (finished) return false;

            while (true)
            {
                UnsafeNativeSpanTrieNode* currentNode = (UnsafeNativeSpanTrieNode*)currentNodeAddress.ToPointer();
                bool hasValue = false;

                if (currentNode->HasValue)
                {
                    NativeByteSpan? value = ReadNodeValue(currentNode);
                    currentValue = new KeyValue<NativeByteSpan?>(GetKeyFromStack(), value);
                    hasValue = true;
                }

                if (currentNode->ChildCount > 0)
                {
                    Push(currentNodeAddress, 0, currentNode->GetChildKey(0));
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
                    UnsafeTrieStackEntry parentEntry = Pop();
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
                        Push(parentNodeAddress, childIndex, parentNode->GetChildKey(childIndex));
                        currentNodeAddress = nextSiblingAddress;

                        if (hasValue) return true;
                        break;
                    }
                }
            }
        }

        internal static NativeByteSpan? ReadNodeValue(UnsafeNativeSpanTrieNode* node)
        {
            if (node->ValuePointer == 0) return null;
            return new NativeByteSpan((void*)node->ValuePointer, node->ValueLength);
        }

        private string GetKeyFromStack()
        {
            int prefixLength = rootPrefix.Length;
            Span<UnsafeTrieStackEntry> stackEntries = new Span<UnsafeTrieStackEntry>(stack, stackCount);
            Span<byte> keyBytes = stackalloc byte[prefixLength + stackCount];
            rootPrefix.Span.CopyTo(keyBytes.Slice(0, prefixLength));
            for (int i = 0; i < stackEntries.Length; i++)
            {
                keyBytes[i + prefixLength] = stackEntries[i].Key;
            }
            return Encoding.UTF8.GetString(keyBytes);
        }

        /// <summary>
        /// Frees the unmanaged traversal stack and returns any pooled key buffer to
        /// <see cref="ArrayPool{T}.Shared"/>.
        /// </summary>
        public void Dispose()
        {
            if (!isDisposed)
            {
                if (stackSize > 0)
                {
                    NativeMemory.Free(stack);
                }
                if (keyBuffer is not null && keyBuffer.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(keyBuffer);
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
        public UnsafeNativeSpanTrieEnumerator GetEnumerator() { return this; }
        IEnumerator<KeyValue<NativeByteSpan?>> IEnumerable<KeyValue<NativeByteSpan?>>.GetEnumerator() { return this; }
        IEnumerator IEnumerable.GetEnumerator() { return this; }
        /// <summary>The current key-value pair.</summary>
        public KeyValue<NativeByteSpan?> Current => currentValue;
        object IEnumerator.Current => currentValue;
    }
}
