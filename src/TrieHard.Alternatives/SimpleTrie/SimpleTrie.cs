using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TrieHard.Abstractions;
using TrieHard.PrefixLookup;

namespace TrieHard.Collections
{

    /// <summary>
    /// This is a simple trie implementation. It is provided as an example of how 
    /// many C# articles suggest creating a trie in C#.
    /// Thread Safe: No
    /// 
    /// Advantages: Easy to understand and debug. Backed by Dictionaries which have
    /// great retreival performance, even when there are many branches at each Node.
    /// 
    /// Disadvantages: No concurrency. Class nodes and dictionary backing can consume excessive memory
    /// compared to other approaches. The simplistic yield iterator approach to collecting values creates
    /// a lot of garbage.
    /// 
    /// </summary>
    public class SimpleTrie<T> : IPrefixLookup<T>
    {
        public static bool IsImmutable => false;
        public static Concurrency ThreadSafety => Concurrency.Read;
        public static bool IsSorted => false;

        private SimpleNode<T> rootNode = new();
        private int count;        

        public T? this[string key] { 
            get => rootNode.Get(key);
            set
            {
                count += rootNode.Set(key, value);
            }
        }

        public int Count => count;

        public void Clear()
        {
            rootNode = new();
            count = 0;
        }

        public IEnumerable<KeyValue<T?>> GetValues()
        {
            return rootNode.CollectValues(string.Empty.AsMemory(), new StringBuilder());
        }

        public IEnumerator<KeyValue<T?>> GetEnumerator()
        {
            return GetValues().GetEnumerator();
        }

        public IEnumerable<KeyValue<T?>> Search(string keyPrefix)
        {
            return rootNode.Search(keyPrefix);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public static IPrefixLookup<TValue?> Create<TValue>(IEnumerable<KeyValue<TValue?>> source)
        {
            var result = new SimpleTrie<TValue?>();
            foreach (var kvp in source)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        public IEnumerable<T?> SearchValues(string keyPrefix)
        {
            foreach (var kvp in Search(keyPrefix))
            {
                yield return kvp.Value;
            }
        }

        public static IPrefixLookup<TValue?> Create<TValue>()
        {
            return new SimpleTrie<TValue?>();
        }
    }
}
