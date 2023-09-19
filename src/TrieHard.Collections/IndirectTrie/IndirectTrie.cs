using System;
using System.Collections;
using System.Collections.Generic;
using TrieHard.Abstractions;

namespace TrieHard.Collections
{
    /// <summary>
    /// This trie uses immutable structs stored in arrays to represent nodes.
    /// The structs do not directly reference each other, instead referencing bucket and array
    /// index locations where connected nodes are stored instead. Nodes only store links to their
    /// parent, their first child and to their first in-order sibbling.
    /// 
    /// Thread Safe: Multiple Readers, Single Writer
    /// 
    /// Advantages: The immutable struct nodes make it easier to reason about concurrency 
    /// and theoretically reduces the amount of memory consumed by each node. Garbage creation
    /// is greatly reduced. Because the node for a key is always stored in the same array location
    /// after being created, it allows exposing to consumers a cached jump to that location which
    /// makes polling a specific key's value very fast.
    /// 
    /// Disadvantages: This approach uses more complicated logic for iterating, updating values,
    /// inserting nodes, and allocating storage for backing arrays. It can be difficult to debug
    /// and understand. While structs offer theoretical improvements in memory usage per node, 
    /// most of that advantage is lost in this implementation on storing additional bucket indexes
    /// for each node connection.
    /// </summary>
    public class IndirectTrie<T> : IPrefixLookup<string, T>
    {
        public static bool IsImmutable => false;
        public static Concurrency ThreadSafety => Concurrency.Read;

        private const int BUCKET_SIZE = 10000;
        private IndirectTrieNode<T>[][] Nodes = new IndirectTrieNode<T>[1][];
        private IndirectTrieLocation NextNodeLocation = new IndirectTrieLocation(0, 1);
        private int nodeCount = 0;
        private int valueCount = 0;

        public int Count => valueCount;


        public T this[string key]
        {
            get => Get(key);
            set => Add(key, value);
        }

        internal ref readonly IndirectTrieNode<T> Get(in IndirectTrieLocation location) => ref Nodes[location.Bucket][location.Index];

        private ref readonly IndirectTrieNode<T> GetByIndex(int bucket, int index) => ref Nodes[bucket][index];

        private void SetNode(in IndirectTrieNode<T> node) => Nodes[node.Location.Bucket][node.Location.Index] = node;
        
        public IndirectTrie()
        {
            var root = new IndirectTrieNode<T>(IndirectTrieLocation.Root, IndirectTrieLocation.None, IndirectTrieLocation.None, IndirectTrieLocation.None, default, default);
            Nodes[0] = new IndirectTrieNode<T>[BUCKET_SIZE];
            SetNode(in root);
        }

        public readonly record struct CachedKey(IndirectTrie<T> Trie, IndirectTrieLocation NodeLocation, int Depth);

        public CachedKey Cache(string key)
        {
            var existing = FindNode(key);
            if (!existing.Exists)
            {
                Add(key, default);
                existing = FindNode(key);
            }
            return new CachedKey(this, existing, key.Length);
        }

        public T Get(in ReadOnlySpan<char> key)
        {
            var result = FindNode(key);
            return result.Exists ? Get(result).Value : default;
        }

        public T Get(CachedKey cachedKey)
        {
            ref readonly IndirectTrieNode<T> result = ref Get(cachedKey.NodeLocation);
            return result.Value;
        }

        internal IndirectTrieLocation NextDepthFirst(in IndirectTrieNode<T> node, ref int depth)
        {
            if (node.HasChild)
            {
                depth += 1;
                return node.Child;
            }

            // We are at the collect root and have no children
            if (depth == 0) { return IndirectTrieLocation.None; }

            if (node.HasSibbling) { return node.Sibbling; }

            while (true)
            {
                depth -= 1;
                if (depth == 0)
                {
                    return IndirectTrieLocation.None;
                }
                ref readonly IndirectTrieNode<T> parent = ref Get(node.Parent);

                if (parent.HasSibbling) {
                    return parent.Sibbling;
                }
            }
        }

        public void Add(string key, T value)
        {
            ReadOnlySpan<char> keySlice = key;
            var remainingKey = FindPrefixMatch(keySlice, out var setSearch);
            IndirectTrieLocation setLocation = setSearch.Location;
            for (int i = 0; i < remainingKey.Length; i++)
            {
                setLocation = InsertNode(setLocation, remainingKey[i]);
            }
            ref readonly IndirectTrieNode<T> node = ref Get(setLocation);
            var nodeWithNewValue = node with { Value = value };
            var countChange = CountChange(node, nodeWithNewValue);
            SetNode(nodeWithNewValue);
            valueCount += countChange;
        }

        private int CountChange(IndirectTrieNode<T> oldNode, IndirectTrieNode<T> newNode)
        {
            var hadValue = oldNode.HasValue;
            var hasValue = newNode.HasValue;
            if (hadValue && !hasValue)
            {
                return -1;
            }
            if (!hadValue && hasValue)
            {
                return 1;
            }
            return 0;
        }

        private IndirectTrieLocation AllocateNode()
        {
            // improve allocation approach?
            // first dimension gets allocated/resized often

            // does the bucket array need to double each time.
            var nextLocation = NextNodeLocation;
            if (nextLocation.Index == BUCKET_SIZE - 1)
            {
                var newBucketLength = nextLocation.Bucket + 2;
                Array.Resize(ref Nodes, newBucketLength);
                Nodes[newBucketLength - 1] = new IndirectTrieNode<T>[BUCKET_SIZE];
                NextNodeLocation = new IndirectTrieLocation(newBucketLength - 1, 0);
            }
            else
            {
                NextNodeLocation = nextLocation with { Index = nextLocation.Index + 1 };
            }
            return nextLocation;
        }



        private IndirectTrieLocation InsertNode(IndirectTrieLocation parent, char key)
        {
            
            // Get sibbling 
            var newLocation = AllocateNode();

            ref readonly IndirectTrieNode<T> parentNode = ref Get(parent);

            // We have to find the first child of our parent that
            // has a key greater than our current one, or the last key
            // then stitch up the references between sibblings
            // Example: Adding 'B' to a parent.
            // The parent's 'Child' location points to 'A'
            // 'A' points to 'C'
            // In that case, we need to find 'C' then change
            // 'A' sibbling to point to our new 'B' and have
            // 'B' point to 'C'

            IndirectTrieLocation sibbling = IndirectTrieLocation.None;
            
            if (parentNode.HasChild)
            {
                //Node sibblingSearchNode = Get(parentNode.Child);
                var sibblingLocation = parentNode.Child;
                IndirectTrieLocation previous = IndirectTrieLocation.None;

                while (sibblingLocation != IndirectTrieLocation.None)
                {
                    ref readonly IndirectTrieNode<T> sibblingSearchNode = ref Get(sibblingLocation);

                    if (sibblingSearchNode.Key > key)
                    {
                        if (previous == IndirectTrieLocation.None)
                        {
                            // We are inserting as the new first child of the parent
                            var newParent = parentNode with { Child = newLocation };
                            sibbling = sibblingSearchNode.Location;
                            var newFirstChildNode = new IndirectTrieNode<T>(newLocation, newParent.Location, sibbling, IndirectTrieLocation.None, key, default);
                            SetNode(newFirstChildNode);
                            SetNode(newParent);
                            return newLocation;
                        }
                        else
                        {
                            // We are inserting after the first element
                            // so we need to patch the previous sibbling's Node

                            ref readonly IndirectTrieNode<T> previousNodeToPatch = ref Get(previous);
                            var newPreviousNodeToPatch = previousNodeToPatch with { Sibbling = newLocation };
                            var newInsertNode = new IndirectTrieNode<T>(newLocation, parent, previousNodeToPatch.Sibbling, IndirectTrieLocation.None, key, default);
                            SetNode(newInsertNode);
                            SetNode(newPreviousNodeToPatch);
                            return newLocation;
                        }
                    }

                    previous = sibblingSearchNode.Location;
                    sibblingLocation = sibblingSearchNode.Sibbling;
                }

                // Parent had a child, but we didn't find one greater than the current key
                ref readonly IndirectTrieNode<T> previousNode = ref Get(previous);
                var newPreviousNode = previousNode with { Sibbling = newLocation };
                var newNode = new IndirectTrieNode<T>(newLocation, parent, IndirectTrieLocation.None, IndirectTrieLocation.None, key, default);
                SetNode(newNode);
                SetNode(newPreviousNode);
                return newLocation;
            }
            else
            {
                var newParent = parentNode with { Child = newLocation }; ;
                var newSingleChildNode = new IndirectTrieNode<T>(newLocation, parent, sibbling, IndirectTrieLocation.None, key, default);
                Nodes[newLocation.Bucket][newLocation.Index] = newSingleChildNode; //Set(newSingleChildNode);
                SetNode(newParent);
                return newLocation;
            }


        }

        public IndirectTrieEnumerator<T> Search(CachedKey cacheKey)
        {
            return new IndirectTrieEnumerator<T>(this, cacheKey.NodeLocation, cacheKey.Depth);
        }

        IEnumerable<KeyValuePair<string, T>> IPrefixLookup<string, T>.Search(string prefix)
        {
            return Search(prefix);
        }

        public IndirectTrieEnumerator<T> Search(string prefix)
        {
            var remainingKey = FindPrefixMatch(prefix, out var match);
            if (remainingKey.Length == 0)
            {
                return new IndirectTrieEnumerator<T>(this, match.Location, prefix.Length);
            }
            else
            {
                ref readonly IndirectTrieNode<T> none =  ref IndirectTrieNode<T>.None;
                return new IndirectTrieEnumerator<T>(this, IndirectTrieLocation.None, 0);
            }
        }

        private IndirectTrieLocation FindNode(in ReadOnlySpan<char> key)
        {
            ref readonly IndirectTrieNode<T> root = ref Get(IndirectTrieLocation.Root);

            var searchLocation = root.Child;
            char matchChar = key[0];
            int offset = 0;
            var length = key.Length;

            while (searchLocation.Exists)
            {
                ref readonly IndirectTrieNode<T> node = ref Get(searchLocation);
                
                if (!node.Key.Equals(matchChar))
                {
                    searchLocation = node.Sibbling;
                }
                else
                {
                    // Found at least partial match
                    offset++;

                    if (offset == length)
                    {
                        // Exact match
                        return searchLocation;
                    }
                    matchChar = key[offset];

                    searchLocation = node.Child;
                }
            }
            return IndirectTrieLocation.None;
        }

        private ReadOnlySpan<char> FindPrefixMatch(ReadOnlySpan<char> key, out IndirectTrieNode<T> bestMatch)
        {

            //ref readonly Location rootLocation = ref Location.Root;
            //ref readonly Node root = ref GetByIndex(0, 0);
            IndirectTrieNode<T> root = GetByIndex(0, 0);

            bestMatch = root;
            if (!root.Child.Exists)
            {
                return key;
            }
            IndirectTrieNode<T> node = Get(root.Child);
            while(true)
            {
                if (node.Key != key[0])
                {
                    if (!node.HasSibbling) break;
                    node = Get(node.Sibbling);
                }
                else
                {
                    // Found at least partial match
                    bestMatch = node;
                    key = key.Slice(1);

                    if (key.Length == 0)
                    {
                        // Exact match
                        return key;
                    }
                    if (!node.HasChild) break;
                    node = Get(node.Child);
                }
            }
            return key;

        }

        public void Clear()
        {
            for (int bucketIndex = 0; bucketIndex < Nodes.Length; bucketIndex++)
            {
                IndirectTrieNode<T>[] buffer = Nodes[bucketIndex];
                for (int nodeIndex = 0; nodeIndex < buffer.Length; nodeIndex++)
                {
                    IndirectTrieNode<T> existingNode = buffer[nodeIndex];
                    if (existingNode.HasValue)
                    {
                        SetNode(existingNode with { Value = default });
                    }
                }
            }
            valueCount = 0;
        }

        public IndirectTrieEnumerator<T> GetEnumerator()
        {
            return new IndirectTrieEnumerator<T>(this, IndirectTrieLocation.Root, 0);
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
            var result = new IndirectTrie<TValue>();
            foreach (var kvp in source)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        /// <remarks>
        /// This could be replaced with a struct enumerator that only gets values
        /// </remarks>
        public IEnumerable<T> SearchValues(string keyPrefix)
        {
            foreach(var kvp in Search(keyPrefix))
            {
                yield return kvp.Value;
            }
        }

        public static IPrefixLookup<string, TValue> Create<TValue>()
        {
            return new IndirectTrie<TValue>();
        }
    }

}
