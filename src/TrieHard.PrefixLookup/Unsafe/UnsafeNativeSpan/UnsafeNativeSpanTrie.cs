using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

namespace TrieHard.Collections
{
    /// <summary>
    /// A sorted prefix trie that stores variable-length byte sequences as values in unmanaged
    /// memory, eliminating managed-heap pressure on both the write and read paths.
    /// <para>
    /// Keys are UTF-8 encoded strings. Values are raw byte arrays appended into pooled unmanaged
    /// slabs rather than being individually heap-allocated.
    /// <see cref="Get(ReadOnlySpan{byte})"/> and all enumerators return <see cref="NativeByteSpan?"/>,
    /// a zero-allocation struct that points directly into the owning slab. No <c>byte[]</c> is
    /// allocated on the read path. Callers that need an owned copy can call
    /// <see cref="NativeByteSpan.ToArray"/>.
    /// </para>
    /// <para>
    /// <b>Thread safety:</b> instances are not thread-safe. Concurrent reads without writes are
    /// safe, but any concurrent write requires external synchronisation.
    /// </para>
    /// <para>
    /// <b>Value lifetime:</b> a <see cref="NativeByteSpan"/> remains valid until the key is
    /// overwritten with a larger value, or until the trie is <see cref="Clear"/>ed or
    /// <see cref="Dispose"/>d. Do not hold a <see cref="NativeByteSpan"/> beyond those events.
    /// </para>
    /// <para>
    /// <b>Overwrite behaviour:</b> if a key is set to a new value whose byte length fits within
    /// the previously stored allocation, the bytes are updated in place with no slab waste.
    /// If the new value is larger, the old bytes are abandoned and the new bytes are appended to
    /// the current value slab; wasted space is reclaimed only on <see cref="Clear"/> or
    /// <see cref="Dispose"/>.
    /// </para>
    /// <para>
    /// <b>Disposal:</b> the trie must be disposed when no longer needed. On <see cref="Dispose"/>,
    /// all node buffers, child-key blocks, and value slabs are freed without requiring a per-node
    /// traversal for value memory.
    /// </para>
    /// </summary>
    [SkipLocalsInit]
    public unsafe class UnsafeNativeSpanTrie : IPrefixLookup<NativeByteSpan?>, IDisposable
    {
        /// <inheritdoc/>
        public static bool IsImmutable => false;
        /// <inheritdoc/>
        public static Concurrency ThreadSafety => Concurrency.None;
        /// <inheritdoc/>
        public static bool IsSorted => true;

        private const int InitialNodeBufferCount = 4096;
        private const int InitialValueBufferSize = 65536;
        private const int MaxStackAllocSize = 4096;

        private static readonly int NodeSize = UnsafeNativeSpanTrieNode.Size;

        private static int KeyMaxByteSize(int utf16Length) => Encoding.UTF8.GetMaxByteCount(utf16Length);

        // Node storage — same slab approach as UnsafeBlittableTrie<T>.
        private readonly List<UnsafeTrieNodeBuffer> buffers = new();
        private UnsafeTrieNodeBuffer buffer;

        // Value storage — byte slabs; values are appended sequentially.
        private readonly List<UnsafeTrieNodeBuffer> valueBuffers = new();
        private UnsafeTrieNodeBuffer valueBuffer;

        private int count;
        private bool isDisposed;
        private UnsafeNativeSpanTrieNode* rootPointer;

        /// <summary>The number of keys with stored values in the trie.</summary>
        public int Count => count;

        /// <summary>
        /// Gets or sets the value associated with <paramref name="key"/>.
        /// Returns <c>null</c> when the key is absent or was explicitly stored as <c>null</c>.
        /// Setting to <c>null</c> stores a null value; the key is still counted as present.
        /// </summary>
        public NativeByteSpan? this[string key]
        {
            get => Get(key);
            set
            {
                if (value.HasValue)
                    Set(key, value.Value.AsSpan());
                else
                    SetNullValue(key);
            }
        }

        public UnsafeNativeSpanTrie()
        {
            buffer = new UnsafeTrieNodeBuffer((long)InitialNodeBufferCount * NodeSize);
            buffers.Add(buffer);
            valueBuffer = new UnsafeTrieNodeBuffer(InitialValueBufferSize);
            valueBuffers.Add(valueBuffer);
            CreateRoot();
        }

        private void CreateRoot()
        {
            EnsureNodeSpace();
            rootPointer = (UnsafeNativeSpanTrieNode*)buffer.CurrentAddress;
            *rootPointer = new UnsafeNativeSpanTrieNode { HasValue = false };
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
        /// Ensures the value slab has at least <paramref name="required"/> bytes available.
        /// When needed, allocates a new slab at least twice the current size (or <paramref name="required"/>
        /// bytes, whichever is larger).
        /// </summary>
        private void EnsureValueSpace(int required)
        {
            if (!valueBuffer.IsAvailable(required))
            {
                long newCapacity = Math.Max(valueBuffer.Size * 2, required);
                var newBuffer = new UnsafeTrieNodeBuffer(newCapacity);
                valueBuffers.Add(newBuffer);
                valueBuffer = newBuffer;
            }
        }

        /// <summary>
        /// Sets the value for <paramref name="key"/>. Pass <c>null</c> to store a null value for the key.
        /// </summary>
        public void Set(string key, byte[]? value)
        {
            var maxByteSize = KeyMaxByteSize(key.Length);
            if (maxByteSize > MaxStackAllocSize)
            {
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteSize);
                Span<byte> keySpan = rentedBuffer.AsSpan();
                Utf8.FromUtf16(key, keySpan, out _, out var bytesWritten, false, true);
                keySpan = keySpan.Slice(0, bytesWritten);
                if (value != null) Set(keySpan, (ReadOnlySpan<byte>)value); else SetNullValue(keySpan);
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
            else
            {
                Span<byte> stackKeySpan = stackalloc byte[maxByteSize];
                Utf8.FromUtf16(key, stackKeySpan, out _, out var bytesWritten, false, true);
                stackKeySpan = stackKeySpan.Slice(0, bytesWritten);
                if (value != null) Set(stackKeySpan, (ReadOnlySpan<byte>)value); else SetNullValue(stackKeySpan);
            }
        }

        /// <summary>Sets the value for <paramref name="key"/> from a <see cref="ReadOnlySpan{T}"/>.</summary>
        public void Set(string key, ReadOnlySpan<byte> value)
        {
            var maxByteSize = KeyMaxByteSize(key.Length);
            if (maxByteSize > MaxStackAllocSize)
            {
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteSize);
                Span<byte> keySpan = rentedBuffer.AsSpan();
                Utf8.FromUtf16(key, keySpan, out _, out var bytesWritten, false, true);
                Set(keySpan.Slice(0, bytesWritten), value);
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
            else
            {
                Span<byte> stackKeySpan = stackalloc byte[maxByteSize];
                Utf8.FromUtf16(key, stackKeySpan, out _, out var bytesWritten, false, true);
                Set(stackKeySpan.Slice(0, bytesWritten), value);
            }
        }

        /// <summary>Sets the value for the given UTF-8 key from a <see cref="ReadOnlySpan{T}"/>.</summary>
        public void Set(in ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            SetCore(key, valueBytes: value, storeNull: false);
        }

        private void SetNullValue(string key)
        {
            var maxByteSize = KeyMaxByteSize(key.Length);
            if (maxByteSize > MaxStackAllocSize)
            {
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteSize);
                Span<byte> keySpan = rentedBuffer.AsSpan();
                Utf8.FromUtf16(key, keySpan, out _, out var bytesWritten, false, true);
                SetNullValue(keySpan.Slice(0, bytesWritten));
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
            else
            {
                Span<byte> stackKeySpan = stackalloc byte[maxByteSize];
                Utf8.FromUtf16(key, stackKeySpan, out _, out var bytesWritten, false, true);
                SetNullValue(stackKeySpan.Slice(0, bytesWritten));
            }
        }

        private void SetNullValue(in ReadOnlySpan<byte> key)
        {
            SetCore(key, valueBytes: ReadOnlySpan<byte>.Empty, storeNull: true);
        }

        private void SetCore(in ReadOnlySpan<byte> key, ReadOnlySpan<byte> valueBytes, bool storeNull)
        {
            int keyIndex = 0;
            UnsafeNativeSpanTrieNode* searchNode = rootPointer;

            while (true)
            {
                byte byteToMatch = key[keyIndex];

                var matchingIndex = searchNode->BinarySearch(byteToMatch);
                if (matchingIndex < 0)
                {
                    matchingIndex = ~matchingIndex;
                    EnsureNodeSpace();
                    UnsafeNativeSpanTrieNode* newNode = (UnsafeNativeSpanTrieNode*)buffer.CurrentAddress;
                    *newNode = new UnsafeNativeSpanTrieNode { HasValue = false };
                    RecordNode();
                    searchNode->AddChild(new nint(newNode), byteToMatch, matchingIndex);
                }

                searchNode = (UnsafeNativeSpanTrieNode*)searchNode->GetChild(matchingIndex).ToPointer();

                keyIndex++;
                if (key.Length == keyIndex)
                {
                    if (!searchNode->HasValue)
                    {
                        count++;
                        searchNode->HasValue = true;
                    }

                    if (storeNull)
                    {
                        searchNode->ValuePointer = 0;
                        searchNode->ValueLength = 0;
                    }
                    else
                    {
                        WriteValue(searchNode, valueBytes);
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Writes <paramref name="value"/> bytes into the node's value storage.
        /// If the new value fits within the existing slab allocation for this node, it is
        /// written in-place (no slab waste). Otherwise the bytes are appended to the current
        /// value slab and the old slab bytes are abandoned until <see cref="Clear"/> or
        /// <see cref="Dispose"/>.
        /// </summary>
        private void WriteValue(UnsafeNativeSpanTrieNode* node, ReadOnlySpan<byte> value)
        {
            // In-place overwrite when the new value fits in the existing allocation.
            if (node->ValuePointer != 0 && value.Length <= node->ValueLength)
            {
                value.CopyTo(new Span<byte>((void*)node->ValuePointer, value.Length));
                node->ValueLength = value.Length;
                return;
            }

            // Append to the current value slab.
            // Allocate at least 1 byte so a non-null ValuePointer can distinguish a stored
            // empty byte array (non-null pointer, Length=0) from a stored null (pointer=0).
            int slabAdvance = value.Length > 0 ? value.Length : 1;
            EnsureValueSpace(slabAdvance);

            byte* dest = valueBuffer.CurrentAddress;
            if (value.Length > 0)
            {
                value.CopyTo(new Span<byte>(dest, value.Length));
            }
            node->ValuePointer = (long)new nint(dest);
            node->ValueLength = value.Length;
            valueBuffer.Advance(slabAdvance);
        }

        private static readonly byte[] EmptyKeyBytes = Array.Empty<byte>();

        /// <summary>
        /// Returns an enumerator that yields all key-value pairs whose keys begin with
        /// <paramref name="key"/>. Dispose the enumerator when done to release pooled resources.
        /// </summary>
        public UnsafeNativeSpanTrieEnumerator Search(string key)
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
        /// Returns an enumerator that yields all key-value pairs whose UTF-8 keys begin with
        /// <paramref name="key"/>. When a pooled <paramref name="keyBuffer"/> is provided it is
        /// returned to <see cref="ArrayPool{T}.Shared"/> when the enumerator is disposed.
        /// </summary>
        public UnsafeNativeSpanTrieEnumerator Search(ReadOnlyMemory<byte> key, byte[]? keyBuffer = null)
        {
            nint matchingNode = FindNodeAddress(key.Span);
            if (matchingNode > 0)
            {
                return new UnsafeNativeSpanTrieEnumerator(key, matchingNode, keyBuffer);
            }
            return new UnsafeNativeSpanTrieEnumerator(key, 0, keyBuffer);
        }

        /// <summary>
        /// Returns a value-only enumerator for all entries whose UTF-8 keys begin with
        /// <paramref name="keyPrefix"/>. Dispose the enumerator when done to release unmanaged resources.
        /// </summary>
        public UnsafeNativeSpanTrieValueEnumerator SearchValues(ReadOnlySpan<byte> keyPrefix)
        {
            nint matchingNode = FindNodeAddress(keyPrefix);
            if (matchingNode > 0)
            {
                return new UnsafeNativeSpanTrieValueEnumerator(matchingNode);
            }
            return UnsafeNativeSpanTrieValueEnumerator.None;
        }

        /// <summary>
        /// Returns a value-only enumerator for all entries whose keys begin with
        /// <paramref name="keyPrefix"/>. Dispose the enumerator when done to release unmanaged resources.
        /// </summary>
        public UnsafeNativeSpanTrieValueEnumerator SearchValues(string keyPrefix)
        {
            var maxByteSize = KeyMaxByteSize(keyPrefix.Length);
            if (maxByteSize > MaxStackAllocSize)
            {
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteSize);
                Span<byte> keySpan = rentedBuffer.AsSpan();
                Utf8.FromUtf16(keyPrefix, keySpan, out _, out var bytesWritten, false, true);
                keySpan = keySpan.Slice(0, bytesWritten);
                UnsafeNativeSpanTrieValueEnumerator result = SearchValues(keySpan);
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
            UnsafeNativeSpanTrieNode* searchNode = rootPointer;
            while (true)
            {
                byte byteToMatch = key[keyIndex];

                var matchingIndex = searchNode->BinarySearch(byteToMatch);
                if (matchingIndex < 0)
                {
                    return 0;
                }

                var matchingChildAddress = searchNode->GetChild(matchingIndex);
                searchNode = (UnsafeNativeSpanTrieNode*)matchingChildAddress.ToPointer();

                keyIndex++;
                if (key.Length == keyIndex)
                {
                    return matchingChildAddress;
                }
            }
        }

        /// <summary>
        /// Returns the value stored for <paramref name="key"/>, or <c>null</c> if the key is not
        /// present or was stored as <c>null</c>.
        /// </summary>
        public NativeByteSpan? Get(string key)
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
        /// Returns the value stored for the UTF-8 <paramref name="key"/>, or <c>null</c> if
        /// the key is not present or was stored as <c>null</c>.
        /// </summary>
        public NativeByteSpan? Get(ReadOnlySpan<byte> key)
        {
            var nodeAddress = FindNodeAddress(key);
            if (nodeAddress == 0) return null;
            UnsafeNativeSpanTrieNode* node = (UnsafeNativeSpanTrieNode*)nodeAddress;
            if (!node->HasValue) return null;
            return UnsafeNativeSpanTrieEnumerator.ReadNodeValue(node);
        }

        /// <summary>
        /// Returns the value associated with the longest key in the trie that is a prefix of
        /// <paramref name="key"/>, or <c>null</c> if no such prefix exists.
        /// </summary>
        public NativeByteSpan? LongestPrefix(string key)
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
        /// Returns the value associated with the longest UTF-8 key in the trie that is a prefix
        /// of <paramref name="key"/>, or <c>null</c> if no such prefix exists.
        /// </summary>
        public NativeByteSpan? LongestPrefix(ReadOnlySpan<byte> key)
        {
            NativeByteSpan? longestValue = rootPointer->HasValue
                ? UnsafeNativeSpanTrieEnumerator.ReadNodeValue(rootPointer)
                : null;

            if (key.Length == 0)
            {
                return longestValue;
            }

            int keyIndex = 0;
            UnsafeNativeSpanTrieNode* searchNode = rootPointer;
            while (keyIndex < key.Length)
            {
                byte byteToMatch = key[keyIndex];

                var matchingIndex = searchNode->BinarySearch(byteToMatch);
                if (matchingIndex < 0)
                {
                    break;
                }

                searchNode = (UnsafeNativeSpanTrieNode*)searchNode->GetChild(matchingIndex).ToPointer();
                if (searchNode->HasValue)
                {
                    longestValue = UnsafeNativeSpanTrieEnumerator.ReadNodeValue(searchNode);
                }

                keyIndex++;
            }

            return longestValue;
        }

        /// <summary>
        /// Frees all unmanaged memory owned by the trie: node buffers, child-key blocks, and
        /// value slabs. Any <see cref="NativeByteSpan"/> instances obtained from this trie become
        /// invalid after this call.
        /// </summary>
        public void Dispose()
        {
            if (isDisposed) return;

            // Free only child-key/address blocks (one per node). Value memory is owned
            // by the value slabs freed below — no per-node traversal needed for values.
            FreeChildKeyBlocks(new nint(rootPointer));

            foreach (var buf in buffers)
                buf.Dispose();

            foreach (var vbuf in valueBuffers)
                vbuf.Dispose();

            isDisposed = true;
            GC.SuppressFinalize(this);
        }

        private void FreeChildKeyBlocks(nint searchNode)
        {
            UnsafeNativeSpanTrieNode* node = (UnsafeNativeSpanTrieNode*)searchNode.ToPointer();
            byte childCount = node->ChildCount;
            // Collect child addresses before freeing the child-key block; GetChild reads
            // from that same allocation, so reading after Free is use-after-free.
            nint* children = stackalloc nint[childCount];
            for (int i = 0; i < childCount; i++)
                children[i] = node->GetChild(i);
            // NativeMemory.Free accepts null (no-op), so nodes with no children are safe.
            NativeMemory.Free((void*)node->ChildKeysAddress);
            for (int i = 0; i < childCount; i++)
                FreeChildKeyBlocks(children[i]);
        }

        private static readonly byte[] EmptyByteArray = Array.Empty<byte>();

        /// <summary>
        /// Returns an enumerator that yields all key-value pairs in the trie in sorted
        /// (lexicographic) order. Dispose the enumerator when done to release unmanaged resources.
        /// </summary>
        public UnsafeNativeSpanTrieEnumerator GetEnumerator()
        {
            return new UnsafeNativeSpanTrieEnumerator(EmptyByteArray, new nint(rootPointer));
        }

        /// <summary>
        /// Removes all stored values and reclaims value slab memory, while preserving the
        /// allocated node structure for reuse. Any <see cref="NativeByteSpan"/> instances
        /// obtained before this call become invalid.
        /// </summary>
        public void Clear()
        {
            ClearNodeRecursive(new nint(rootPointer));

            // Reclaim all value slab space: discard extra slabs and reset the first.
            for (int i = 1; i < valueBuffers.Count; i++)
                valueBuffers[i].Dispose();
            valueBuffers.RemoveRange(1, valueBuffers.Count - 1);
            valueBuffers[0].Reset();
            valueBuffer = valueBuffers[0];

            count = 0;
        }

        private void ClearNodeRecursive(nint nodeAddress)
        {
            var node = (UnsafeNativeSpanTrieNode*)nodeAddress.ToPointer();
            // Mark as empty; ValuePointer/ValueLength become stale but won't be read
            // because HasValue is false. The slab will be reset after this traversal.
            node->HasValue = false;
            for (int i = 0; i < node->ChildCount; i++)
                ClearNodeRecursive(node->GetChild(i));
        }

        IEnumerable<KeyValue<NativeByteSpan?>> IPrefixLookup<NativeByteSpan?>.Search(string keyPrefix)
        {
            return Search(keyPrefix);
        }

        IEnumerable<NativeByteSpan?> IPrefixLookup<NativeByteSpan?>.SearchValues(string keyPrefix)
        {
            return SearchValues(keyPrefix);
        }

        IEnumerator<KeyValue<NativeByteSpan?>> IEnumerable<KeyValue<NativeByteSpan?>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Creates and populates an <see cref="UnsafeNativeSpanTrie"/> from <paramref name="source"/>.
        /// <typeparamref name="TValue"/> must be <see cref="NativeByteSpan"/>; throws
        /// <see cref="NotSupportedException"/> otherwise.
        /// </summary>
        public static IPrefixLookup<TValue?> Create<TValue>(IEnumerable<KeyValue<TValue?>> source)
        {
            if (source is not IEnumerable<KeyValue<NativeByteSpan?>> byteSource)
                throw new NotSupportedException($"{nameof(UnsafeNativeSpanTrie)} only supports {nameof(NativeByteSpan)} values.");
            var trie = new UnsafeNativeSpanTrie();
            foreach (var kvp in byteSource)
            {
                if (kvp.Value.HasValue)
                    trie.Set(kvp.Key, kvp.Value.Value.AsSpan());
                else
                    trie.SetNullValue(kvp.Key);
            }
            return (IPrefixLookup<TValue?>)(object)trie;
        }

        /// <summary>
        /// Creates an empty <see cref="UnsafeNativeSpanTrie"/>.
        /// <typeparamref name="TValue"/> must be <see cref="NativeByteSpan"/>; throws
        /// <see cref="NotSupportedException"/> otherwise.
        /// </summary>
        public static IPrefixLookup<TValue?> Create<TValue>()
        {
            if (typeof(TValue) != typeof(NativeByteSpan))
                throw new NotSupportedException($"{nameof(UnsafeNativeSpanTrie)} only supports {nameof(NativeByteSpan)} values.");
            return (IPrefixLookup<TValue?>)(object)new UnsafeNativeSpanTrie();
        }
    }
}
