using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using TrieHard.Abstractions;
using TrieHard.PrefixLookup;

namespace TrieHard.Collections
{
    [SkipLocalsInit]
    public unsafe class UnsafeTrie<T>  : IPrefixLookup<T?>, IDisposable
    {
        public static bool IsImmutable => false;
        /// <summary>
        /// This lookup passes the concurrency tests in the project, but I have
        /// reason to suspect that on 32bit architectures a few of its writes could
        /// be torn across structs.
        /// </summary>
        public static Concurrency ThreadSafety => Concurrency.None;
        public static bool IsSorted => true;

        private List<UnsafeTrieNodeBuffer> buffers = new List<UnsafeTrieNodeBuffer>();
        private UnsafeTrieNodeBuffer buffer;
        private nuint nodeCount = 0;

        private List<T?> values = new();
        private bool isDisposed = false;
        private UnsafeTrieNode* rootPointer;

        internal List<T?> Values => values;


        public int Count => values.Count;


        public T? this[string key]
        {
            get => Get(key);
            set => Set(key, value);
        }

        public UnsafeTrie()
        {
            this.buffer = new UnsafeTrieNodeBuffer(4096);
            this.buffers.Add(buffer);
            CreateRoot();
        }

        private void CreateRoot()
        {
            var nodeSize = UnsafeTrieNode.Size;
            EnsureNodeSpace();
            rootPointer = (UnsafeTrieNode*)buffer.CurrentAddress;
            *rootPointer = new UnsafeTrieNode();
            RecordNode();
        }

        private void EnsureNodeSpace()
        {
            if (!buffer.IsAvailable(UnsafeTrieNode.Size))
            {
                var newCapacity = buffer.Size * 2;
                var newBuffer = new UnsafeTrieNodeBuffer(newCapacity);
                buffers.Add(newBuffer);
                this.buffer = newBuffer;
            }
        }

        private void RecordNode()
        {
            this.buffer.Advance(UnsafeTrieNode.Size);
            nodeCount++;
        }

        public void Set(string key, T? value)
        {
            var maxByteSize = key.Length * 4;

            if (key.Length > 4096)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(maxByteSize);
                Span<byte> keySpan = buffer.AsSpan();
                Utf8.FromUtf16(key, keySpan, out var _, out var bytesWritten, false, true);
                keySpan = keySpan.Slice(0, bytesWritten);
                Set(keySpan, value);
                ArrayPool<byte>.Shared.Return(buffer);
            }
            else
            {
                Span<byte> stackKeySpan = stackalloc byte[maxByteSize];
                Utf8.FromUtf16(key, stackKeySpan, out var _, out var bytesWritten, false, true);
                stackKeySpan = stackKeySpan.Slice(0, bytesWritten);
                Set(stackKeySpan, value);
            }
        }

        public void Set(in ReadOnlySpan<byte> key, T? value)
        {
            int keyIndex = 0;

            UnsafeTrieNode* searchNode = rootPointer;
            while (true)
            {
                byte byteToMatch = key[keyIndex];

                var matchingIndex = searchNode->BinarySearch(byteToMatch);
                if (matchingIndex < 0)
                {
                    matchingIndex = ~matchingIndex;
                    EnsureNodeSpace();
                    UnsafeTrieNode* newNode = (UnsafeTrieNode*)buffer.CurrentAddress;
                    *newNode = new UnsafeTrieNode { ValueLocation = -1 };
                    RecordNode();
                    searchNode->AddChild(new nint(newNode), byteToMatch, matchingIndex);
                }

                searchNode = (UnsafeTrieNode*)searchNode->GetChild(matchingIndex).ToPointer();

                keyIndex++;
                if (key.Length == keyIndex)
                {
                    if (searchNode->ValueLocation > -1)
                    {
                        values[searchNode->ValueLocation] = value;
                    }
                    else
                    {
                        values.Add(value);
                        searchNode->ValueLocation = values.Count - 1;
                    }
                    return;
                }
            }
        }

        private static readonly byte[] EmptyKeyBytes = new byte[0];

        public UnsafeTrieEnumerator<T> Search(string key)
        {
            if (key.Length == 0)
            {
                return Search(EmptyKeyBytes);
            }

            var maxByteSize = key.Length * 4;
            var buffer = ArrayPool<byte>.Shared.Rent(maxByteSize);
            Span<byte> keySpan = buffer.AsSpan();
            Utf8.FromUtf16(key, keySpan, out var _, out var bytesWritten, false, true);
            return Search(buffer.AsMemory(0, bytesWritten), keyBuffer: buffer );
        }

        /// <summary>
        /// Performs a prefix search on the provided key, and returns an enumerator optimized
        /// for iteration which will not allocate unless boxed and which provides access to 
        /// the byte spans of UTF8 text of each key. Use the span only within the foreach loop
        /// body, as every time the returned enumerator iterates, the previously returned
        /// KeyValue will no longer contain valid data.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public CompactTrieNodeSpanEnumerable<T> SearchSpans(ReadOnlySpan<byte> key)
        {
            nint nodeAddress = FindNodeAddress(key);

            if (nodeAddress > 0)
            {
                ref readonly UnsafeTrieNode matchingNode = ref *(UnsafeTrieNode*)nodeAddress.ToPointer();
                return new CompactTrieNodeSpanEnumerable<T>(this, key, matchingNode);
            }
            return new CompactTrieNodeSpanEnumerable<T>();
        }

        public UnsafeTrieEnumerator<T> Search(ReadOnlyMemory<byte> key, byte[]? keyBuffer)
        {
            nint matchingNode = FindNodeAddress(key.Span);
            if (matchingNode > 0)
            {
                return new UnsafeTrieEnumerator<T>(this, key, matchingNode, keyBuffer);
            }
            return new UnsafeTrieEnumerator<T>(null!, key, 0, keyBuffer);
        }

        public UnsafeTrieEnumerator<T> Search(ReadOnlyMemory<byte> key)
        {
            nint matchingNode = FindNodeAddress(key.Span);
            if (matchingNode > 0)
            {
                return new UnsafeTrieEnumerator<T>(this, key, matchingNode);
            }
            return new UnsafeTrieEnumerator<T>(null!, key, 0);
        }

        public UnsafeTrieValueEnumerator<T?> SearchValues(ReadOnlySpan<byte> keyPrefix)
        {
            nint matchingNode = FindNodeAddress(keyPrefix);
            if (matchingNode > 0)
            {
                return new UnsafeTrieValueEnumerator<T?>(this, matchingNode);
            }
            return UnsafeTrieValueEnumerator<T?>.None;
        }

        public UnsafeTrieValueEnumerator<T?> SearchValues(string keyPrefix)
        {
            var maxByteSize = keyPrefix.Length * 3;
            if (maxByteSize > 4096)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(maxByteSize);
                Span<byte> keySpan = buffer.AsSpan();
                Utf8.FromUtf16(keyPrefix, keySpan, out var _, out var bytesWritten, false, true);
                keySpan = keySpan.Slice(0, bytesWritten);
                UnsafeTrieValueEnumerator<T?> result = SearchValues(keySpan);
                ArrayPool<byte>.Shared.Return(buffer);
                return result;
            }
            else
            {
                Span<byte> stackKeySpan = stackalloc byte[maxByteSize];
                Utf8.FromUtf16(keyPrefix, stackKeySpan, out var _, out var bytesWritten, false, true);
                stackKeySpan = stackKeySpan.Slice(0, bytesWritten);
                return SearchValues(stackKeySpan);
            }
        }

        private nint FindNodeAddress(ReadOnlySpan<byte> key)
        {
            int keyIndex = 0;
            UnsafeTrieNode* searchNode = rootPointer;
            while (true)
            {
                byte byteToMatch = key[keyIndex];

                var matchingIndex = searchNode->BinarySearch(byteToMatch);
                if (matchingIndex < 0)
                {
                    return 0;
                }

                var matchingChildAddress = searchNode->GetChild(matchingIndex);
                searchNode = (UnsafeTrieNode*)matchingChildAddress.ToPointer();

                keyIndex++;
                if (key.Length == keyIndex)
                {
                    return matchingChildAddress;
                }
            }
        }

        public T? Get(string key)
        {
            int keyByteMaxSize = key.Length * 4;

            if (key.Length > 4096)
            {
                Span<byte> keySpan;
                var buffer = ArrayPool<byte>.Shared.Rent(keyByteMaxSize);
                keySpan = buffer.AsSpan();
                Utf8.FromUtf16(key, keySpan, out var _, out var bytesWritten, false, true);
                keySpan = keySpan.Slice(0, bytesWritten);
                var result = Get(keySpan);
                ArrayPool<byte>.Shared.Return(buffer);
                return result;
            }
            else
            {
                Span<byte> stackKeySpan = stackalloc byte[keyByteMaxSize];
                Utf8.FromUtf16(key, stackKeySpan, out var _, out var bytesWritten, false, true);
                stackKeySpan = stackKeySpan.Slice(0, bytesWritten);
                return Get(stackKeySpan);
            }
        }

        public T? Get(ReadOnlySpan<byte> key)
        {
            var nodeAddress = FindNodeAddress(key);
            if (nodeAddress == 0)
            {
                return default!;
            }
            UnsafeTrieNode* node = (UnsafeTrieNode*)nodeAddress;
            if (node->ValueLocation == -1)
            {
                return default!;
            }
            return values[node->ValueLocation];
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            var freeList = new List<nint>();
            GetNodeFreeAddresses(new nint(rootPointer), freeList);
            foreach (var address in freeList)
            {
                NativeMemory.Free(address.ToPointer());
            }
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
            isDisposed = true;
            GC.SuppressFinalize(this);
        }

        private static readonly byte[] EmptyByteArray = Array.Empty<byte>();

        public UnsafeTrieEnumerator<T> GetEnumerator()
        {
            return new UnsafeTrieEnumerator<T>(this, EmptyByteArray, new nint(rootPointer));
        }

        private void GetNodeFreeAddresses(nint searchNode, List<nint> freeList)
        {
            UnsafeTrieNode* node = (UnsafeTrieNode*)searchNode.ToPointer();
            freeList.Add(new nint((void*)node->ChildKeysAddress));
            byte childCount = node->ChildCount;
            for (int i = 0; i < childCount; i++)
            {
                var childAddress = node->GetChild(i);
                GetNodeFreeAddresses(childAddress, freeList);
            }
        }

        public void Clear()
        {
            ClearNodeRecursive(new nint(rootPointer));
            Values.Clear();
        }

        public void ClearNodeRecursive(nint nodeAddress)
        {
            var node = (UnsafeTrieNode*)nodeAddress.ToPointer();
            node->ValueLocation = -1;
            for(int i = 0; i < node->ChildCount; i++)
            {
                var childAddress = node->GetChild(i);
                ClearNodeRecursive(childAddress);
            }
        }

        IEnumerable<KeyValue<T?>> IPrefixLookup<T?>.Search(string keyPrefix)
        {
            return this.Search(keyPrefix);
        }

        IEnumerator<KeyValue<T?>> IEnumerable<KeyValue<T?>>.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }


        public static IPrefixLookup<TValue?> Create<TValue>(IEnumerable<KeyValue<TValue?>> source)
        {
            var result = new UnsafeTrie<TValue>();
            foreach (var kvp in source)
            {
                result.Set(kvp.Key, kvp.Value);
            }
            return result;
        }

        IEnumerable<T> IPrefixLookup<T?>.SearchValues(string keyPrefix)
        {
            var keyBytes = Encoding.UTF8.GetBytes(keyPrefix);
            return SearchValues(keyBytes);
        }

        public static IPrefixLookup<TValue?> Create<TValue>()
        {
            return new UnsafeTrie<TValue>();
        }

    }



}