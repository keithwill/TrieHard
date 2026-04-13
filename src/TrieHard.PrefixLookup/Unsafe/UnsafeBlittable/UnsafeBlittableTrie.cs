using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

namespace TrieHard.Collections
{
    /// <summary>
    /// A prefix trie that maps UTF-8 string keys to values of an <see langword="unmanaged"/> type
    /// <typeparamref name="T"/>, with all node and value data stored in unmanaged memory.
    /// <para>
    /// <typeparamref name="T"/> must satisfy the <see langword="unmanaged"/> constraint, meaning it must
    /// be a value type whose fields (transitively) are all primitive or enum types — for example
    /// <see langword="int"/>, <see langword="long"/>, an enum, or a struct composed entirely of such types.
    /// The value is stored inline within each node struct, so no boxing or separate heap allocation occurs
    /// per entry. Nodes themselves are packed into large unmanaged slabs, keeping GC pressure near zero
    /// even for large tries.
    /// </para>
    /// <para>
    /// Results from <see cref="Search(string)"/> and <see cref="GetEnumerator"/> are yielded in
    /// lexicographic key order. The trie is not thread-safe; concurrent reads are safe only when no
    /// writes are occurring. This type implements <see cref="IDisposable"/>; call <see cref="Dispose"/>
    /// when finished to release all unmanaged memory.
    /// </para>
    /// </summary>
    [SkipLocalsInit]
    public unsafe class UnsafeBlittableTrie<T> : IPrefixLookup<T?>, IDisposable
        where T : unmanaged
    {
        /// <summary>Always <see langword="false"/>; entries can be added or updated after construction.</summary>
        public static bool IsImmutable => false;

        /// <summary>Always <see cref="Concurrency.None"/>; this type is not thread-safe.</summary>
        public static Concurrency ThreadSafety => Concurrency.None;

        /// <summary>Always <see langword="true"/>; enumeration yields key-value pairs in lexicographic key order.</summary>
        public static bool IsSorted => true;

        private const int InitialNodeBufferCount = 4096;
        private const int MaxStackAllocSize = 4096;

        private static readonly int NodeSize = UnsafeBlittableTrieNode<T>.Size;

        private static int KeyMaxByteSize(int utf16Length) => Encoding.UTF8.GetMaxByteCount(utf16Length);

        private readonly List<UnsafeTrieNodeBuffer> buffers = new();
        private UnsafeTrieNodeBuffer buffer;
        private int count;

        private bool isDisposed;
        private UnsafeBlittableTrieNode<T>* rootPointer;

        /// <summary>The number of keys with values stored in the trie.</summary>
        public int Count => count;

        /// <summary>
        /// Gets or sets the value associated with <paramref name="key"/>.
        /// Getting a key that does not exist returns <see langword="null"/>.
        /// Setting a key that already exists overwrites its value.
        /// </summary>
        /// <param name="key">The UTF-16 string key, encoded to UTF-8 internally.</param>
        public T? this[string key]
        {
            get => Get(key);
            set => Set(key, value);
        }

        public UnsafeBlittableTrie()
        {
            buffer = new UnsafeTrieNodeBuffer((long)InitialNodeBufferCount * NodeSize);
            buffers.Add(buffer);
            CreateRoot();
        }

        private void CreateRoot()
        {
            EnsureNodeSpace();
            rootPointer = (UnsafeBlittableTrieNode<T>*)buffer.CurrentAddress;
            *rootPointer = new UnsafeBlittableTrieNode<T> { HasValue = false };
            RecordNode();
        }

        private void EnsureNodeSpace()
        {
            if (!buffer.IsAvailable(NodeSize))
            {
                var newCapacity = buffer.Size * 2;
                var newBuffer = new UnsafeTrieNodeBuffer(newCapacity);
                buffers.Add(newBuffer);
                buffer = newBuffer;
            }
        }

        private void RecordNode()
        {
            buffer.Advance(NodeSize);
        }

        /// <summary>
        /// Adds or updates the entry for <paramref name="key"/> with <paramref name="value"/>.
        /// The string is encoded to UTF-8 before insertion.
        /// </summary>
        /// <param name="key">The string key.</param>
        /// <param name="value">The value to store. Pass <see langword="null"/> to store the default value of <typeparamref name="T"/>.</param>
        public void Set(string key, T? value)
        {
            var maxByteSize = KeyMaxByteSize(key.Length);
            if (maxByteSize > MaxStackAllocSize)
            {
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteSize);
                Span<byte> keySpan = rentedBuffer.AsSpan();
                Utf8.FromUtf16(key, keySpan, out _, out var bytesWritten, false, true);
                keySpan = keySpan.Slice(0, bytesWritten);
                Set(keySpan, value);
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
            else
            {
                Span<byte> stackKeySpan = stackalloc byte[maxByteSize];
                Utf8.FromUtf16(key, stackKeySpan, out _, out var bytesWritten, false, true);
                stackKeySpan = stackKeySpan.Slice(0, bytesWritten);
                Set(stackKeySpan, value);
            }
        }

        /// <summary>
        /// Adds or updates the entry for the UTF-8 byte sequence <paramref name="key"/> with <paramref name="value"/>.
        /// Use this overload to avoid a UTF-16-to-UTF-8 conversion when the key is already encoded.
        /// </summary>
        /// <param name="key">The UTF-8 encoded key bytes.</param>
        /// <param name="value">The value to store. Pass <see langword="null"/> to store the default value of <typeparamref name="T"/>.</param>
        public void Set(in ReadOnlySpan<byte> key, T? value)
        {
            int keyIndex = 0;
            UnsafeBlittableTrieNode<T>* searchNode = rootPointer;

            while (true)
            {
                byte byteToMatch = key[keyIndex];

                var matchingIndex = searchNode->BinarySearch(byteToMatch);
                if (matchingIndex < 0)
                {
                    matchingIndex = ~matchingIndex;
                    EnsureNodeSpace();
                    UnsafeBlittableTrieNode<T>* newNode = (UnsafeBlittableTrieNode<T>*)buffer.CurrentAddress;
                    *newNode = new UnsafeBlittableTrieNode<T> { HasValue = false };
                    RecordNode();
                    searchNode->AddChild(new nint(newNode), byteToMatch, matchingIndex);
                }

                searchNode = (UnsafeBlittableTrieNode<T>*)searchNode->GetChild(matchingIndex).ToPointer();

                keyIndex++;
                if (key.Length == keyIndex)
                {
                    if (!searchNode->HasValue)
                    {
                        count++;
                    }
                    searchNode->HasValue = true;
                    searchNode->Value = value.GetValueOrDefault();
                    return;
                }
            }
        }

        private static readonly byte[] EmptyKeyBytes = Array.Empty<byte>();

        /// <summary>
        /// Returns an enumerator over all key-value pairs whose keys begin with <paramref name="key"/>.
        /// An empty string returns all entries in the trie. Results are yielded in lexicographic key order.
        /// </summary>
        /// <remarks>
        /// The returned <see cref="UnsafeBlittableTrieEnumerator{T}"/> allocates unmanaged memory for its
        /// traversal stack. Dispose it (or use it in a <see langword="foreach"/> which disposes implicitly)
        /// to avoid a memory leak.
        /// </remarks>
        /// <param name="key">The prefix to match. An empty string matches all entries.</param>
        /// <returns>
        /// An enumerator over matching key-value pairs, or an empty enumerator if no keys match the prefix.
        /// </returns>
        public UnsafeBlittableTrieEnumerator<T> Search(string key)
        {
            if (key.Length == 0)
            {
                return Search(EmptyKeyBytes);
            }

            var maxByteSize = KeyMaxByteSize(key.Length);
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteSize);
            Span<byte> keySpan = rentedBuffer.AsSpan();
            Utf8.FromUtf16(key, keySpan, out _, out var bytesWritten, false, true);
            return Search(rentedBuffer.AsMemory(0, bytesWritten), keyBuffer: rentedBuffer);
        }

        /// <summary>
        /// Returns an enumerator over all key-value pairs whose UTF-8 keys begin with <paramref name="key"/>.
        /// Use this overload to avoid a UTF-16-to-UTF-8 conversion when the prefix is already encoded.
        /// </summary>
        /// <param name="key">The UTF-8 encoded prefix bytes.</param>
        /// <param name="keyBuffer">
        /// An optional pooled byte array that backs <paramref name="key"/>; if supplied it is returned to
        /// <see cref="System.Buffers.ArrayPool{T}.Shared"/> when the enumerator is disposed.
        /// </param>
        /// <returns>
        /// An enumerator over matching key-value pairs, or an empty enumerator if no keys match the prefix.
        /// </returns>
        public UnsafeBlittableTrieEnumerator<T> Search(ReadOnlyMemory<byte> key, byte[]? keyBuffer = null)
        {
            nint matchingNode = FindNodeAddress(key.Span);
            if (matchingNode > 0)
            {
                return new UnsafeBlittableTrieEnumerator<T>(key, matchingNode, keyBuffer);
            }
            return new UnsafeBlittableTrieEnumerator<T>(key, 0, keyBuffer);
        }

        /// <summary>
        /// Returns a value-only enumerator over all entries whose UTF-8 keys begin with <paramref name="keyPrefix"/>.
        /// Use this overload to avoid a UTF-16-to-UTF-8 conversion when the prefix is already encoded.
        /// Prefer this over <see cref="Search(ReadOnlyMemory{byte}, byte[])"/> when keys are not needed.
        /// </summary>
        /// <param name="keyPrefix">The UTF-8 encoded prefix bytes.</param>
        /// <returns>
        /// An enumerator over matching values, or an empty enumerator if no keys match the prefix.
        /// </returns>
        public UnsafeBlittableTrieValueEnumerator<T> SearchValues(ReadOnlySpan<byte> keyPrefix)
        {
            nint matchingNode = FindNodeAddress(keyPrefix);
            if (matchingNode > 0)
            {
                return new UnsafeBlittableTrieValueEnumerator<T>(matchingNode);
            }
            return UnsafeBlittableTrieValueEnumerator<T>.None;
        }

        /// <summary>
        /// Returns a value-only enumerator over all entries whose keys begin with <paramref name="keyPrefix"/>.
        /// Prefer this over <see cref="Search(string)"/> when keys are not needed, as it avoids allocating
        /// key strings during traversal.
        /// </summary>
        /// <remarks>
        /// The returned <see cref="UnsafeBlittableTrieValueEnumerator{T}"/> allocates unmanaged memory for its
        /// traversal stack. Dispose it (or use it in a <see langword="foreach"/> which disposes implicitly)
        /// to avoid a memory leak.
        /// </remarks>
        /// <param name="keyPrefix">The prefix to match. An empty string matches all entries.</param>
        /// <returns>
        /// An enumerator over matching values, or an empty enumerator if no keys match the prefix.
        /// </returns>
        public UnsafeBlittableTrieValueEnumerator<T> SearchValues(string keyPrefix)
        {
            var maxByteSize = KeyMaxByteSize(keyPrefix.Length);
            if (maxByteSize > MaxStackAllocSize)
            {
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteSize);
                Span<byte> keySpan = rentedBuffer.AsSpan();
                Utf8.FromUtf16(keyPrefix, keySpan, out _, out var bytesWritten, false, true);
                keySpan = keySpan.Slice(0, bytesWritten);
                UnsafeBlittableTrieValueEnumerator<T> result = SearchValues(keySpan);
                ArrayPool<byte>.Shared.Return(rentedBuffer);
                return result;
            }
            else
            {
                Span<byte> stackKeySpan = stackalloc byte[maxByteSize];
                Utf8.FromUtf16(keyPrefix, stackKeySpan, out _, out var bytesWritten, false, true);
                stackKeySpan = stackKeySpan.Slice(0, bytesWritten);
                return SearchValues(stackKeySpan);
            }
        }

        private nint FindNodeAddress(ReadOnlySpan<byte> key)
        {
            if (key.Length == 0)
            {
                return new nint(rootPointer);
            }
            int keyIndex = 0;
            UnsafeBlittableTrieNode<T>* searchNode = rootPointer;
            while (true)
            {
                byte byteToMatch = key[keyIndex];

                var matchingIndex = searchNode->BinarySearch(byteToMatch);
                if (matchingIndex < 0)
                {
                    return 0;
                }

                var matchingChildAddress = searchNode->GetChild(matchingIndex);
                searchNode = (UnsafeBlittableTrieNode<T>*)matchingChildAddress.ToPointer();

                keyIndex++;
                if (key.Length == keyIndex)
                {
                    return matchingChildAddress;
                }
            }
        }

        /// <summary>
        /// Returns the value stored for <paramref name="key"/>, or <see langword="null"/> if the key does
        /// not exist or has no value set. Prefer the indexer (<c>trie[key]</c>) for typical use.
        /// </summary>
        /// <param name="key">The string key to look up.</param>
        public T? Get(string key)
        {
            int keyByteMaxSize = KeyMaxByteSize(key.Length);
            if (keyByteMaxSize > MaxStackAllocSize)
            {
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(keyByteMaxSize);
                Span<byte> keySpan = rentedBuffer.AsSpan();
                Utf8.FromUtf16(key, keySpan, out _, out var bytesWritten, false, true);
                keySpan = keySpan.Slice(0, bytesWritten);
                var result = Get(keySpan);
                ArrayPool<byte>.Shared.Return(rentedBuffer);
                return result;
            }
            else
            {
                Span<byte> stackKeySpan = stackalloc byte[keyByteMaxSize];
                Utf8.FromUtf16(key, stackKeySpan, out _, out var bytesWritten, false, true);
                stackKeySpan = stackKeySpan.Slice(0, bytesWritten);
                return Get(stackKeySpan);
            }
        }

        /// <summary>
        /// Returns the value stored for the UTF-8 encoded <paramref name="key"/>, or <see langword="null"/>
        /// if the key does not exist or has no value set.
        /// Use this overload to avoid a UTF-16-to-UTF-8 conversion when the key is already encoded.
        /// </summary>
        /// <param name="key">The UTF-8 encoded key bytes.</param>
        public T? Get(ReadOnlySpan<byte> key)
        {
            var nodeAddress = FindNodeAddress(key);
            if (nodeAddress == 0)
            {
                return null;
            }
            UnsafeBlittableTrieNode<T>* node = (UnsafeBlittableTrieNode<T>*)nodeAddress;
            if (!node->HasValue)
            {
                return null;
            }
            return node->Value;
        }

        /// <summary>
        /// Returns the value associated with the stored key that is the longest prefix of <paramref name="key"/>.
        /// If no stored key is a prefix of <paramref name="key"/>, returns <see langword="null"/>.
        /// </summary>
        /// <remarks>
        /// This is useful for routing-style lookups where the most specific matching rule should win.
        /// For example, if the trie contains <c>"api"</c> and <c>"api/users"</c>, querying
        /// <c>"api/users/123"</c> returns the value for <c>"api/users"</c>.
        /// </remarks>
        /// <param name="key">The string key to match against stored prefixes.</param>
        public T? LongestPrefix(string key)
        {
            int keyByteMaxSize = KeyMaxByteSize(key.Length);
            if (keyByteMaxSize > MaxStackAllocSize)
            {
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(keyByteMaxSize);
                Span<byte> keySpan = rentedBuffer.AsSpan();
                Utf8.FromUtf16(key, keySpan, out _, out var bytesWritten, false, true);
                keySpan = keySpan.Slice(0, bytesWritten);
                var result = LongestPrefix(keySpan);
                ArrayPool<byte>.Shared.Return(rentedBuffer);
                return result;
            }
            else
            {
                Span<byte> stackKeySpan = stackalloc byte[keyByteMaxSize];
                Utf8.FromUtf16(key, stackKeySpan, out _, out var stackBytesWritten, false, true);
                stackKeySpan = stackKeySpan.Slice(0, stackBytesWritten);
                return LongestPrefix(stackKeySpan);
            }
        }

        /// <summary>
        /// Returns the value associated with the stored key that is the longest prefix of the UTF-8
        /// encoded <paramref name="key"/>. If no stored key is a prefix of <paramref name="key"/>,
        /// returns <see langword="null"/>.
        /// Use this overload to avoid a UTF-16-to-UTF-8 conversion when the key is already encoded.
        /// </summary>
        /// <param name="key">The UTF-8 encoded key bytes to match against stored prefixes.</param>
        public T? LongestPrefix(ReadOnlySpan<byte> key)
        {
            T? longestValue = rootPointer->HasValue ? rootPointer->Value : null;

            if (key.Length == 0)
            {
                return longestValue;
            }

            int keyIndex = 0;
            UnsafeBlittableTrieNode<T>* searchNode = rootPointer;
            while (keyIndex < key.Length)
            {
                byte byteToMatch = key[keyIndex];

                var matchingIndex = searchNode->BinarySearch(byteToMatch);
                if (matchingIndex < 0)
                {
                    break;
                }

                searchNode = (UnsafeBlittableTrieNode<T>*)searchNode->GetChild(matchingIndex).ToPointer();
                if (searchNode->HasValue)
                {
                    longestValue = searchNode->Value;
                }

                keyIndex++;
            }

            return longestValue;
        }

        /// <summary>
        /// Releases all unmanaged memory held by this trie, including node slabs and per-node child key blocks.
        /// After disposal the instance must not be used.
        /// </summary>
        public void Dispose()
        {
            if (isDisposed) return;

            var freeList = new List<nint>();
            GetNodeFreeAddresses(new nint(rootPointer), freeList);
            foreach (var address in freeList)
            {
                NativeMemory.Free(address.ToPointer());
            }
            foreach (var buf in buffers)
            {
                buf.Dispose();
            }
            isDisposed = true;
            GC.SuppressFinalize(this);
        }

        private static readonly byte[] EmptyByteArray = Array.Empty<byte>();

        /// <summary>
        /// Returns an enumerator over all key-value pairs in the trie in lexicographic key order.
        /// </summary>
        /// <remarks>
        /// The returned <see cref="UnsafeBlittableTrieEnumerator{T}"/> must be disposed to release its
        /// unmanaged traversal stack. A <see langword="foreach"/> loop disposes it automatically.
        /// </remarks>
        public UnsafeBlittableTrieEnumerator<T> GetEnumerator()
        {
            return new UnsafeBlittableTrieEnumerator<T>(EmptyByteArray, new nint(rootPointer));
        }

        private void GetNodeFreeAddresses(nint searchNode, List<nint> freeList)
        {
            UnsafeBlittableTrieNode<T>* node = (UnsafeBlittableTrieNode<T>*)searchNode.ToPointer();
            freeList.Add(new nint((void*)node->ChildKeysAddress));
            byte childCount = node->ChildCount;
            for (int i = 0; i < childCount; i++)
            {
                GetNodeFreeAddresses(node->GetChild(i), freeList);
            }
        }

        /// <summary>
        /// Marks all nodes as having no value, effectively removing all entries from the trie.
        /// The trie's node structure and allocated memory are retained so that subsequent insertions
        /// do not require immediate reallocation.
        /// </summary>
        public void Clear()
        {
            ClearNodeRecursive(new nint(rootPointer));
            count = 0;
        }

        private void ClearNodeRecursive(nint nodeAddress)
        {
            var node = (UnsafeBlittableTrieNode<T>*)nodeAddress.ToPointer();
            node->HasValue = false;
            for (int i = 0; i < node->ChildCount; i++)
            {
                ClearNodeRecursive(node->GetChild(i));
            }
        }

        IEnumerable<KeyValue<T?>> IPrefixLookup<T?>.Search(string keyPrefix)
        {
            return Search(keyPrefix);
        }

        IEnumerable<T?> IPrefixLookup<T?>.SearchValues(string keyPrefix)
        {
            return SearchValues(keyPrefix);
        }

        IEnumerator<KeyValue<T?>> IEnumerable<KeyValue<T?>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Creates a new <see cref="UnsafeBlittableTrie{T}"/> populated with all entries from <paramref name="source"/>.
        /// </summary>
        /// <typeparam name="TValue">The unmanaged value type to store.</typeparam>
        /// <param name="source">The key-value pairs to insert.</param>
        public static IPrefixLookup<TValue?> Create<TValue>(IEnumerable<KeyValue<TValue?>> source)
            where TValue : unmanaged
        {
            var result = new UnsafeBlittableTrie<TValue>();
            foreach (var kvp in source)
            {
                result.Set(kvp.Key, kvp.Value);
            }
            return result;
        }

        /// <summary>Creates an empty <see cref="UnsafeBlittableTrie{T}"/>.</summary>
        /// <typeparam name="TValue">The unmanaged value type to store.</typeparam>
        public static IPrefixLookup<TValue?> Create<TValue>()
            where TValue : unmanaged
        {
            return new UnsafeBlittableTrie<TValue>();
        }
    }
}
