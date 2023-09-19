using System;
using System.Collections;
using TrieHard.Abstractions;
using rm.Trie;

namespace TrieHard.Alternatives.ExternalLibraries.rm.Trie
{
    /// <summary>
    /// This trie has more downloads than most of the other nuget or github repos I could find, and works
    /// on .NET standard / 5.0+
    /// </summary>
    public class rmTrie<T> : IPrefixLookup<string, T?>
    {
        TrieMap<T> trieMap;

        public rmTrie() 
        { 
            trieMap = new TrieMap<T>();
        }

        public T? this[string key] 
        {
            get => trieMap.ValueBy(key);
            set => trieMap.Add(key, value!); 
        }

        public static bool IsImmutable => false;

        public static Concurrency ThreadSafety => Concurrency.None;

        public int Count => trieMap.Values().Count();

        public static IPrefixLookup<string, TValue?> Create<TValue>(IEnumerable<KeyValuePair<string, TValue?>> source)
        {
            var trie = new rmTrie<TValue>();
            foreach(var kvp in source)
            {
                trie.trieMap.Add(kvp.Key, kvp.Value!);
            }
            return trie;
        }

        public static IPrefixLookup<string, TValue?> Create<TValue>()
        {
            return new rmTrie<TValue>();
        }

        public void Clear()
        {
            trieMap.Clear();
        }

        public IEnumerator<KeyValuePair<string, T?>> GetEnumerator()
        {
            return trieMap.KeyValuePairs().GetEnumerator()!;
        }

        public IEnumerable<KeyValuePair<string, T?>> Search(string keyPrefix)
        {
            return trieMap.KeyValuePairsBy(keyPrefix)!;
        }

        public IEnumerable<T> SearchValues(string keyPrefix)
        {
            return trieMap.ValuesBy(keyPrefix);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
