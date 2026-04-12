using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

namespace TrieHard.Collections
{
    /// <summary>
    /// A trie that stores values of unmanaged type T directly within node structs in unmanaged memory,
    /// eliminating the managed <c>List&lt;T&gt;</c> used by <see cref="UnsafeTrie{T}"/>.
    /// <para>
    /// Each node contains the value inline. The managed heap is used only for bookkeeping structures
    /// (the list of node buffer objects and the trie class itself); all node data and value data lives
    /// in unmanaged slabs.
    /// </para>
    /// <para>
    /// The node struct switches from <c>LayoutKind.Explicit</c> to <c>LayoutKind.Sequential</c> because
    /// <c>[FieldOffset]</c> requires compile-time constant byte offsets, which cannot be specified for
    /// fields that follow the variable-size generic field <c>T Value</c>. Sequential layout lets the
    /// runtime derive correct offsets and trailing padding per closed generic instantiation.
    /// </para>
    /// </summary>
    [SkipLocalsInit]
    public unsafe class UnsafeBlittableTrie<T> : IPrefixLookup<T?>, IDisposable
        where T : unmanaged
    {
        public static bool IsImmutable => false;
        public static Concurrency ThreadSafety => Concurrency.None;
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

        public int Count => count;

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

        public UnsafeBlittableTrieEnumerator<T> Search(ReadOnlyMemory<byte> key, byte[]? keyBuffer = null)
        {
            nint matchingNode = FindNodeAddress(key.Span);
            if (matchingNode > 0)
            {
                return new UnsafeBlittableTrieEnumerator<T>(key, matchingNode, keyBuffer);
            }
            return new UnsafeBlittableTrieEnumerator<T>(key, 0, keyBuffer);
        }

        public UnsafeBlittableTrieValueEnumerator<T> SearchValues(ReadOnlySpan<byte> keyPrefix)
        {
            nint matchingNode = FindNodeAddress(keyPrefix);
            if (matchingNode > 0)
            {
                return new UnsafeBlittableTrieValueEnumerator<T>(matchingNode);
            }
            return UnsafeBlittableTrieValueEnumerator<T>.None;
        }

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

        public static IPrefixLookup<TValue?> Create<TValue>()
            where TValue : unmanaged
        {
            return new UnsafeBlittableTrie<TValue>();
        }
    }
}
