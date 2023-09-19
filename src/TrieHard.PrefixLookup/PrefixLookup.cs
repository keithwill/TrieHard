using System.Collections;
using TrieHard.Abstractions;
using TrieHard.Collections;

public class PrefixLookup<T> : IPrefixLookup<string, T>, IDisposable
{
    private CompactTrie<T> trie;
    public T this[string key]
    {
        get => trie[key];
        set => trie[key] = value;
    }

    public PrefixLookup()
    {
        trie = new CompactTrie<T>();
    }

    public static bool IsImmutable => CompactTrie<T>.IsImmutable;
    public static Concurrency ThreadSafety => CompactTrie<T>.ThreadSafety;

    public int Count => trie.Count;

    public static IPrefixLookup<string, TValue> Create<TValue>(IEnumerable<KeyValuePair<string, TValue>> source)
    {
        var result = new PrefixLookup<TValue>();
        result.trie = (CompactTrie<TValue>)CompactTrie<TValue>.Create(source);
        return result;
    }

    public static IPrefixLookup<string, TValue> Create<TValue>()
    {
        var result = new PrefixLookup<TValue>();
        result.trie = (CompactTrie<TValue>)CompactTrie<TValue>.Create<TValue>();
        return result;
    }

    public void Clear()
    {
        trie.Clear();
    }

    public CompactTrieEnumerator<T> GetEnumerator()
    {
        return trie.GetEnumerator();
    }

    IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    public CompactTrieEnumerator<T> Search(string keyPrefix)
    {
        return trie.Search(keyPrefix);
    }

    IEnumerable<KeyValuePair<string, T>> IPrefixLookup<string, T>.Search(string keyPrefix)
    {
        return Search(keyPrefix);
    }

    public CompactTrieValueEnumerator<T> SearchValues(string keyPrefix)
    {
        return trie.SearchValues(keyPrefix);
    }

    IEnumerable<T> IPrefixLookup<string, T>.SearchValues(string keyPrefix)
    {
        return trie.SearchValues(keyPrefix);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Dispose()
    {
        trie.Dispose();
    }
}