using System.Collections;
using System.Text.Unicode;
using System.Threading.Channels;
using TrieHard.Abstractions;
using TrieHard.PrefixLookup;

namespace TrieHard.Collections
{
    /// <summary>
    /// This is a data oriented inspired take on a Trie that is focused on throughput,
    /// even at the expense of (some) garbage generation. It stores the entire keys when
    /// they are set, but the key references are shared across all nodes created with that
    /// key to reduce the cost. This simplifies retrieval at the cost of additional memory.
    /// </summary>
    /// <remarks>
    /// Being a data oriented design, most of the data is stored in arrays (though a wrapper
    /// to allow array pooling).
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public class FlatTrie<T> : IPrefixLookup<string, T>
    {


        public static bool IsImmutable => false;

        public static Concurrency ThreadSafety => Concurrency.Read;

        public static bool IsSorted => true;

        private static readonly int[] EmptyChildIndexes = new int[0];
        private static readonly byte[] EmptyKeyBytes = new byte[0];

        private const int CanonicalRootIndex = 0;
        /// <summary>
        /// When the root has been pushed into swap space the root index might be
        /// different temporarily different than its than its canonical location 
        /// </summary>
        private int RootIndex = CanonicalRootIndex;
        private const int SwapNodeIndex = 1;

        /// <summary>
        /// When a node is created, we store the byte range of the original full key
        /// that corresponds to all the nodes created with that key
        /// </summary>
        private ArrayPoolList<byte[]> FullKeys = new(4);

        private ArrayPoolList<T> Values = new(4);

        private ArrayPoolList<int> ValueIndexes = new(4);

        /// <summary>
        /// For each 'node' represented as an index, contains the length into the 
        /// full key entry with the same index.
        /// </summary>
        private ArrayPoolList<short> KeyLengths = new(4);

        /// <summary>
        /// For each 'node' represented as an index, contains the indexes
        /// of children of that node
        /// </summary>
        private ArrayPoolList<int[]> ChildIndexes = new(4);
        private ArrayPoolList<byte[]> ChildKeys = new(4);

        public SpanSearchResult<KeyValuePair<ReadOnlyMemory<byte>, T?>> SearchSpans(ReadOnlySpan<byte> prefixKey)
        {
            var matchingIndex = FindMatchingIndex(prefixKey);
            if (matchingIndex == -1) return new SpanSearchResult<KeyValuePair<ReadOnlyMemory<byte>, T?>>(null);
            var searchResults = new ArrayPoolList<KeyValuePair<ReadOnlyMemory<byte>, T?>>();
            CollectResults(matchingIndex, searchResults);
            return new SpanSearchResult<KeyValuePair<ReadOnlyMemory<byte>, T?>>(searchResults);
        }

        public SearchResult<KeyValuePair<ReadOnlyMemory<byte>, T?>> SearchUtf8(ReadOnlySpan<byte> prefixKey)
        {
            var matchingIndex = FindMatchingIndex(prefixKey);
            if (matchingIndex == -1) return new SearchResult<KeyValuePair<ReadOnlyMemory<byte>, T?>>(null);
            var searchResults = new ArrayPoolList<KeyValuePair<ReadOnlyMemory<byte>, T?>>();
            CollectResults(matchingIndex, searchResults);
            return new SearchResult<KeyValuePair<ReadOnlyMemory<byte>, T?>>(searchResults);
        }

        public SearchResult<KeyValuePair<string, T?>> Search(ReadOnlySpan<byte> prefixKey)
        {
            var matchingIndex = FindMatchingIndex(prefixKey);
            if (matchingIndex == -1) return new SearchResult<KeyValuePair<string, T?>>(null);
            var searchResults = new ArrayPoolList<KeyValuePair<string, T?>>();
            CollectResultsStrings(matchingIndex, searchResults);
            return new SearchResult<KeyValuePair<string, T?>>(searchResults);
        }

        public SearchResult<T?> SearchValues(ReadOnlySpan<byte> prefixKey)
        {
            var matchingIndex = FindMatchingIndex(prefixKey);
            if (matchingIndex == -1) return new SearchResult<T?>(null);
            var searchResults = new ArrayPoolList<T?>();
            CollectValues(matchingIndex, searchResults);
            return new SearchResult<T?>(searchResults);
        }


        /// <summary>
        /// This method will only return a 'page' worth of search results at a time as a Span.
        /// </summary>
        /// <param name="prefixKey"></param>
        /// <param name="buffer"></param>
        /// <param name="skip"></param>
        /// <returns></returns>
        public ReadOnlySpan<ReadOnlyKeyValuePair<T>> SearchPage(ReadOnlySpan<byte> prefixKey, Span<ReadOnlyKeyValuePair<T>> buffer, int skip = 0)
        {
            var matchingIndex = FindMatchingIndex(prefixKey);
            int count = 0;
            if (matchingIndex > -1)
            {
                //int bufferLength = buffer.Length;
                ReadOnlySpan<int> valueIndexes = ValueIndexes.Span;
                ReadOnlySpan<byte[]> fullKeys = FullKeys.Span;
                ReadOnlySpan<T> values = Values.Span;
                ReadOnlySpan<short> keyLengths = KeyLengths.Span;
                //ReadOnlySpan<int[]> childIndexes = ChildIndexes.Span;
                count = count - skip; // Simple but janky way to skip the right number of elements
                CollectPageResults(valueIndexes, fullKeys, keyLengths, values, matchingIndex, ref count, buffer);
            };
            return buffer.Slice(0, count);
        }

        private void CollectPageResults(
            ReadOnlySpan<int> valueIndexes,
            ReadOnlySpan<byte[]> fullKeys,
            ReadOnlySpan<short> keyLengths,
            ReadOnlySpan<T> values,
            int nodeIndex,
            ref int count,
            Span<ReadOnlyKeyValuePair<T>> buffer)
        {
            var valueIndex = valueIndexes[nodeIndex];
            if ( valueIndex > -1)
            {
                if (count > -1)
                {
                    buffer[count] = new ReadOnlyKeyValuePair<T>(fullKeys[nodeIndex], keyLengths[nodeIndex], values[valueIndex]);
                }
                count++;
            }

            Span<int> childIndexes = ChildIndexes.Items[nodeIndex].AsSpan();
           
            foreach (var childIndex in childIndexes)
            {
                if (count == buffer.Length) return;
                CollectPageResults(
                    valueIndexes,
                    fullKeys,
                    keyLengths,
                    values,
                    childIndex,
                    ref count,
                    buffer
                );
            }
        }

        private void CollectResults(int nodeIndex, ArrayPoolList<KeyValuePair<ReadOnlyMemory<byte>, T?>> collector)
        {
            var valueIndex = ValueIndexes[nodeIndex];
            var keyLength = KeyLengths[nodeIndex];
            if (valueIndex > -1) collector.Add(
                new KeyValuePair<ReadOnlyMemory<byte>, T?>(FullKeys[nodeIndex].AsMemory(0, keyLength), Values[valueIndex])
            );
            Span<int> childIndexes = ChildIndexes[nodeIndex].AsSpan();
            foreach(var childIndex in childIndexes)
            {
                CollectResults(childIndex, collector);
            }
        }

        private void CollectResultsStrings(int nodeIndex, ArrayPoolList<KeyValuePair<string, T?>> collector)
        {
            var valueIndex = ValueIndexes[nodeIndex];
            var keyLength = KeyLengths[nodeIndex];
            var key = System.Text.Encoding.UTF8.GetString(FullKeys[nodeIndex].AsSpan(0, keyLength));
            if (valueIndex > -1) collector.Add(
                new KeyValuePair<string, T?>(key, Values[valueIndex])
            );
            Span<int> childIndexes = ChildIndexes[nodeIndex].AsSpan();
            foreach (var childIndex in childIndexes)
            {
                CollectResultsStrings(childIndex, collector);
            }
        }

        private void CollectValues(int nodeIndex, ArrayPoolList<T?> collector)
        {
            var valueIndex = ValueIndexes[nodeIndex];
            if (valueIndex > -1) collector.Add(Values[valueIndex]);
            Span<int> childIndexes = ChildIndexes[nodeIndex].AsSpan();
            foreach (var childIndex in childIndexes)
            {
                CollectValues(childIndex, collector);
            }
        }

        private static readonly KeyValuePair<byte[], T?>[] EmptySearchResult = [];

        public int Count => Values.Count;


        public T? this[string key] 
        { 
            get
            {
                Span<byte> keyBuffer = stackalloc byte[key.Length * 4];
                Span<byte> keySpan = GetKeyStringBytes(key, keyBuffer);
                return Get(keySpan);
            }
            set 
            {
                Span<byte> keyBuffer = stackalloc byte[key.Length * 4];
                Span<byte> keySpan = GetKeyStringBytes(key, keyBuffer);
                Set(keySpan, value);
            }
        }

        private int FindMatchingIndex(ReadOnlySpan<byte> prefixKey)
        {
            int keyIndex = 0;
            ref int searchIndex = ref RootIndex;
            ref byte[][] childKeys = ref ChildKeys.Items;
            ref int[][] childIndexes = ref ChildIndexes.Items;
            while (true)
            {
                byte byteToMatch = prefixKey[keyIndex];
                Span<byte> searchChildKeys = childKeys[searchIndex].AsSpan();
                var match = searchChildKeys.BinarySearch(byteToMatch);
                if (match < 0) return -1;
                searchIndex = ref childIndexes[searchIndex][match];
                keyIndex++;
                if (keyIndex == prefixKey.Length) return searchIndex;
            }
        }

        public void Set(ReadOnlySpan<byte> key, T? value)
        {
            // Its important that we only copy the key once into
            // a byte array. We will share that reference for every
            // child that gets created with that key at a time
            byte[]? keyBytes = null;

            ref readonly int[][] childIndexes = ref ChildIndexes.Items;
            ref readonly byte[][] childKeys = ref ChildKeys.Items;

            short keyIndex = 0;
            ref int searchNode = ref RootIndex;

            while (true)
            {
                Span<byte> searchKeys = childKeys[searchNode].AsSpan();
                byte keyByte = key[keyIndex];
                var matchIndex = searchKeys.BinarySearch(keyByte);

                if (matchIndex < 0)
                {
                    matchIndex = ~matchIndex;
                    if (keyBytes is null) keyBytes = key.ToArray();                                      
                    InsertChildAtomic(ref searchNode, atIndex: matchIndex, keyBytes, keyIndex);
                }

                //Span<int> searchChildren = childIndexes[searchNode].AsSpan();
                // Is array indexing better here since we only want one and no span apis?
                searchNode = ref childIndexes[searchNode][matchIndex];

                keyIndex++;

                if (keyIndex == key.Length)
                {
                    var valueIndex = ValueIndexes[searchNode];
                    if (valueIndex == -1)
                    {
                        ValueIndexes[searchNode] = Values.Add(value!);
                    }
                    else
                    {
                        Values[valueIndex] = value!;
                    }
                    return;
                }
            }
        }

        private int CreateEmptyNode(byte[] key, int keyIndex)
        {
            var nodeIndex = FullKeys.Add(key);
            ChildIndexes.Add(EmptyChildIndexes);
            ChildKeys.Add(EmptyKeyBytes);
            short keyLength = (short)(keyIndex);
            KeyLengths.Add(keyLength);
            ValueIndexes.Add(-1);
            return nodeIndex;
        }

        /// <summary>
        /// Atomically inserts a new child into a node. Creates new
        /// arrays to contain the modified child references, pushes the nodes
        /// values into a swap 'node' index, swaps the nodes index reference
        /// to point to the swap space, modifies the node at the original
        /// index location (temporarily not referenced by its parent), then
        /// swaps the nodes reference back to its original index location
        /// </summary>
        /// <param name="nodeIndex">A reference to the node index that we are adding a child to</param>
        /// <param name="atIndex">The position in the node's children that this node should be inserted at</param>
        /// <param name="childFullKey">
        /// The full key being inserted that lead to this specific node's creation
        /// (this node only represents a prefix portion of that key)
        /// </param>
        /// <param name="childKeyLength">The length of the full key that this new node will represent</param>
        private void InsertChildAtomic(ref int nodeIndex, int atIndex, byte[] childFullKey, int childKeyLength)
        {
            var newChildIndex = FullKeys.Add(childFullKey);
            ChildIndexes.Add(EmptyChildIndexes);
            ChildKeys.Add(EmptyKeyBytes);
            short keyLength = (short)(childKeyLength + 1);
            KeyLengths.Add(keyLength);
            ValueIndexes.Add(-1);

            // Prep our inserted sort into new key and index child arrays

            Span<byte> childKeys = ChildKeys[nodeIndex].AsSpan();
            byte[] newChildKeysBuffer = new byte[childKeys.Length + 1];
            Span<byte> newChildKeys = newChildKeysBuffer.AsSpan(); 

            childKeys.Slice(0, atIndex).CopyTo(newChildKeys);
            newChildKeys[atIndex] = childFullKey[childKeyLength];
            childKeys.Slice(atIndex).CopyTo(newChildKeys.Slice(atIndex + 1));

            Span<int> childIndexes = ChildIndexes[nodeIndex].AsSpan();
            int[] newChildIndexesBuffer = new int[childIndexes.Length + 1];
            Span<int> newChildIndexes = newChildIndexesBuffer.AsSpan();

            childIndexes.Slice(0, atIndex).CopyTo(newChildIndexes);
            newChildIndexes[atIndex] = newChildIndex;
            childIndexes.Slice(atIndex).CopyTo(newChildIndexes.Slice(atIndex + 1));

            // Push the node into the swap space
            FullKeys[SwapNodeIndex] = FullKeys[nodeIndex];
            ChildIndexes[SwapNodeIndex] = ChildIndexes[nodeIndex];
            ChildKeys[SwapNodeIndex] = ChildKeys[nodeIndex];
            KeyLengths[SwapNodeIndex] = KeyLengths[nodeIndex];
            ValueIndexes[SwapNodeIndex] = ValueIndexes[nodeIndex];

            int originalIndex = nodeIndex;

            Volatile.Write(ref nodeIndex, SwapNodeIndex);
            
            ChildIndexes[originalIndex] = newChildIndexesBuffer;
            ChildKeys[originalIndex] = newChildKeysBuffer;

            Volatile.Write(ref nodeIndex, originalIndex);
        }

        public T? Get(ReadOnlySpan<byte> key)
        {
            int matchingPrefix = FindMatchingIndex(key);
            if (matchingPrefix == -1) return default;
            var valueIndex = ValueIndexes.Items[matchingPrefix];
            return valueIndex > -1 ? Values.Items[valueIndex] : default;
        }

        public void Clear()
        {
           // Might not be safe to do with active readers
           Values.Clear();
        }

        private static Span<byte> GetKeyStringBytes(string key, Span<byte> buffer)
        {
            Utf8.FromUtf16(key, buffer, out var _, out var bytesWritten, false, true);
            return buffer.Slice(0, bytesWritten);
        }

        public SearchResult<KeyValuePair<string, T?>> Search(string keyPrefix)
        {
            Span<byte> keyBuffer = stackalloc byte[keyPrefix.Length * 4];
            keyBuffer = GetKeyStringBytes(keyPrefix, keyBuffer);
            return Search(keyBuffer);
        }

        IEnumerable<KeyValuePair<string, T?>> IPrefixLookup<string, T>.Search(string keyPrefix)
        {
            return Search(keyPrefix);
        }

        public SearchResult<T?> SearchValues(string prefix)
        {
            Span<byte> keyBuffer = stackalloc byte[prefix.Length * 4];
            keyBuffer = GetKeyStringBytes(prefix, keyBuffer);
            return SearchValues(keyBuffer);
        }

        IEnumerable<T?> IPrefixLookup<string, T>.SearchValues(string keyPrefix)
        {
            return SearchValues(keyPrefix);
        }

        public static IPrefixLookup<string, TValue?> Create<TValue>(IEnumerable<KeyValuePair<string, TValue?>> source)
        {
            var result = new FlatTrie<TValue?>();
            Span<byte> keyBuffer = stackalloc byte[4096];
            foreach(var kvp in source)
            {
                Span<byte> keySpan = GetKeyStringBytes(kvp.Key, keyBuffer);
                result.Set(keySpan, kvp.Value);
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        public static IPrefixLookup<string, TValue?> Create<TValue>()
        {
            return new FlatTrie<TValue?>();
        }

        public IEnumerator<KeyValuePair<string, T?>> GetEnumerator()
        {
            return this.Search(EmptyKeyBytes);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.Search(EmptyKeyBytes);
        }

        public FlatTrie()
        {
            CreateEmptyNode(EmptyKeyBytes, 0); // Creating the root 'node'
            CreateEmptyNode(EmptyKeyBytes, 0); // Create a dedicated 'swap' node
        }
    }



}
