using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using TrieHard.Collections.Contributions;

namespace TrieHard.Collections
{
    [SkipLocalsInit]
    public unsafe class CompactTrie<T>  : IPrefixLookup<string, T>, IDisposable
    {
        public static bool IsImmutable => false;
        public static Concurrency ThreadSafety => Concurrency.Read;

        private List<CompactTrieNodeBuffer> buffers = new List<CompactTrieNodeBuffer>();
        private CompactTrieNodeBuffer buffer;
        private nuint nodeCount = 0;

        private List<T> values = new();
        private bool isDisposed = false;
        private Node* rootPointer;

        internal List<T> Values => values;

        public int Count => values.Count;


        public T this[string key]
        {
            get => Get(key);
            set => Set(key, value);
        }

        public CompactTrie()
        {
            this.buffer = new CompactTrieNodeBuffer(4096);
            this.buffers.Add(buffer);
            CreateRoot();
        }

        private void CreateRoot()
        {
            var nodeSize = Node.Size;
            EnsureNodeSpace();
            rootPointer = (Node*)buffer.CurrentAddress;
            *rootPointer = new Node();
            RecordNode();
        }

        private void EnsureNodeSpace()
        {
            if (!buffer.IsAvailable(Node.Size))
            {
                var newCapacity = buffer.Size * 2;
                var newBuffer = new CompactTrieNodeBuffer(newCapacity);
                buffers.Add(newBuffer);
                this.buffer = newBuffer;
            }
        }

        private void RecordNode()
        {
            this.buffer.Advance(Node.Size);
            nodeCount++;
        }

        public void Set(string key, T value)
        {
            var keyByteSize = Encoding.UTF8.GetByteCount(key);
            Span<byte> keySpan;

            if (key.Length > 4096)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(keyByteSize);
                keySpan = buffer.AsSpan();
                Set(keySpan, value);
                ArrayPool<byte>.Shared.Return(buffer);
            }
            else
            {
                Span<byte> stackKeySpan = stackalloc byte[keyByteSize];
                Encoding.UTF8.GetBytes(key, stackKeySpan);
                Set(stackKeySpan, value);
            }
        }

        public void Set(in ReadOnlySpan<byte> key, T value)
        {
            int keyIndex = 0;

            Node* searchNode = rootPointer;
            while (true)
            {
                byte byteToMatch = key[keyIndex];

                var matchingIndex = searchNode->BinarySearch(byteToMatch);
                if (matchingIndex < 0)
                {
                    matchingIndex = ~matchingIndex;
                    EnsureNodeSpace();
                    Node* newNode = (Node*)buffer.CurrentAddress;
                    *newNode = new Node { ValueLocation = -1 };
                    RecordNode();
                    searchNode->AddChild(new nint(newNode), byteToMatch, matchingIndex);
                }

                searchNode = (Node*)searchNode->GetChild(matchingIndex).ToPointer();

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

        public CompactTrieEnumerator<T> Search(string key)
        {
            // TODO: We can't use stackalloc because the backing for the string
            // needs to live as long as the returned enumerator (its used to generate key
            // values while the consumer is iterating).
            // Maybe make this use a pooled array that gets passed to the enumerator to return?

            ReadOnlyMemory<byte> keyMemory = System.Text.Encoding.UTF8.GetBytes(key);
            return Search(keyMemory);
        }

        public CompactTrieUtf8Enumerator<T> SearchUtf8(ReadOnlyMemory<byte> key)
        {
            var keySpan = key.Span;
            int keyIndex = 0;
            Node* searchNode = rootPointer;
            while (true)
            {
                byte byteToMatch = keySpan[keyIndex];

                var matchingIndex = searchNode->BinarySearch(byteToMatch);
                if (matchingIndex < 0)
                {
                    return new CompactTrieUtf8Enumerator<T>(null, key, 0);
                }

                var matchingChildAddress = searchNode->GetChild(matchingIndex);
                searchNode = (Node*)matchingChildAddress.ToPointer();

                keyIndex++;
                if (key.Length == keyIndex)
                {
                    return new CompactTrieUtf8Enumerator<T>(this, key, matchingChildAddress);
                }
            }
        }

        public CompactTrieEnumerator<T> Search(ReadOnlyMemory<byte> key)
        {
            var keySpan = key.Span;
            int keyIndex = 0;
            Node* searchNode = rootPointer;
            while (true)
            {
                byte byteToMatch = keySpan[keyIndex];

                var matchingIndex = searchNode->BinarySearch(byteToMatch);
                if (matchingIndex < 0)
                {
                    return new CompactTrieEnumerator<T>(null, key, 0);
                }

                var matchingChildAddress = searchNode->GetChild(matchingIndex);
                searchNode = (Node*)matchingChildAddress.ToPointer();

                keyIndex++;
                if (key.Length == keyIndex)
                {
                    return new CompactTrieEnumerator<T>(this, key, matchingChildAddress);
                }
            }
        }

        public T Get(string key)
        {
            var keyByteSize = Encoding.UTF8.GetByteCount(key);
            Span<byte> keySpan;

            if (key.Length > 4096)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(keyByteSize);
                keySpan = buffer.AsSpan();
                var result = Get(keySpan);
                ArrayPool<byte>.Shared.Return(buffer);
                return result;
            }
            else
            {
                Span<byte> stackKeySpan = stackalloc byte[keyByteSize];
                Encoding.UTF8.GetBytes(key, stackKeySpan);
                return Get(stackKeySpan);
            }
        }

        public T Get(ReadOnlySpan<byte> key)
        {
            int keyIndex = 0;
            Node* searchNode = rootPointer;
            while (true)
            {
                byte byteToMatch = key[keyIndex];

                var matchingIndex = searchNode->BinarySearch(byteToMatch);
                if (matchingIndex < 0)
                {
                    return default;
                }

                searchNode = (Node*)searchNode->GetChild(matchingIndex).ToPointer();

                keyIndex++;
                if (key.Length == keyIndex)
                {
                    if (searchNode->ValueLocation == -1)
                    {
                        return default;
                    }
                    return values[searchNode->ValueLocation];
                }
            }
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

        public CompactTrieEnumerator<T> GetEnumerator()
        {
            return new CompactTrieEnumerator<T>(this, EmptyByteArray, new nint(rootPointer));
        }

        private void GetNodeFreeAddresses(nint searchNode, List<nint> freeList)
        {
            Node* node = (Node*)searchNode.ToPointer();
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
            var node = (Node*)nodeAddress.ToPointer();
            node->ValueLocation = -1;
            for(int i = 0; i < node->ChildCount; i++)
            {
                var childAddress = node->GetChild(i);
                ClearNodeRecursive(childAddress);
            }
        }

        IEnumerable<KeyValuePair<string, T>> IPrefixLookup<string, T>.Search(string keyPrefix)
        {
            return this.Search(keyPrefix);
        }

        IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }


        public static IPrefixLookup<string, TValue> Create<TValue>(IEnumerable<KeyValuePair<string, TValue>> source)
        {
            var result = new CompactTrie<TValue>();
            foreach (var kvp in source)
            {
                result.Set(kvp.Key, kvp.Value);
            }
            return result;
        }

        ~CompactTrie()
        {
            if (!isDisposed)
            {
                Dispose();
            }
        }

    }



}