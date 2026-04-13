using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace TrieHard.Collections
{
    /// <summary>
    /// An enumerator that yields <see cref="KeyValue{T}"/> pairs from a <see cref="UnsafeBlittableTrie{T}"/>,
    /// starting from a given node and covering all of its descendants in lexicographic key order.
    /// <para>
    /// Obtain instances via <see cref="UnsafeBlittableTrie{T}.Search(string)"/> or
    /// <see cref="UnsafeBlittableTrie{T}.GetEnumerator"/>. Do not construct directly.
    /// </para>
    /// <para>
    /// This struct allocates a small unmanaged traversal stack. Always dispose it — either explicitly
    /// or by iterating with <see langword="foreach"/>, which calls <see cref="Dispose"/> automatically.
    /// </para>
    /// </summary>
    [SkipLocalsInit]
    public unsafe struct UnsafeBlittableTrieEnumerator<T> : IEnumerable<KeyValue<T?>>, IEnumerator<KeyValue<T?>>
        where T : unmanaged
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
        private KeyValue<T?> currentValue;
        private bool finished;
        private const int InitialStackSize = 32;

        /// <summary>A pre-finished, empty enumerator that yields no elements.</summary>
        public static readonly UnsafeBlittableTrieEnumerator<T> None =
            new UnsafeBlittableTrieEnumerator<T>(ReadOnlyMemory<byte>.Empty, 0) { finished = true };

        internal UnsafeBlittableTrieEnumerator(ReadOnlyMemory<byte> rootPrefix, nint collectNode, byte[]? keyBuffer = null)
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

        public UnsafeBlittableTrieEnumerator()
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

        /// <summary>Advances the enumerator to the next key-value pair.</summary>
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
                    currentValue = new KeyValue<T?>(GetKeyFromStack(), currentNode->Value);
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
                        Push(parentNodeAddress, childIndex, parentNode->GetChildKey(childIndex));
                        currentNodeAddress = nextSiblingAddress;

                        if (hasValue) return true;
                        break;
                    }
                }
            }
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
        /// Releases the unmanaged traversal stack and returns any pooled key buffer.
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
                if (keyBuffer is not null && keyBuffer.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(keyBuffer);
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
        public UnsafeBlittableTrieEnumerator<T> GetEnumerator() { return this; }
        IEnumerator<KeyValue<T?>> IEnumerable<KeyValue<T?>>.GetEnumerator() { return this; }
        IEnumerator IEnumerable.GetEnumerator() { return this; }

        /// <summary>Gets the current key-value pair. Valid only after a successful call to <see cref="MoveNext"/>.</summary>
        public KeyValue<T?> Current => currentValue;
        object IEnumerator.Current => currentValue;
    }
}
