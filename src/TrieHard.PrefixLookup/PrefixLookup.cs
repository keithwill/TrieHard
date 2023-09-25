using System.Collections;
using TrieHard.Abstractions;
using TrieHard.Collections;
using TrieHard.PrefixLookup;

public class PrefixLookup<T> : IPrefixLookup<string, T?>, IDisposable
{
    private FlatTrie<T> trie;
    public T? this[string key]
    {
        get => trie[key];
        set => trie[key] = value;
    }

    public PrefixLookup()
    {
        trie = new FlatTrie<T>();
    }

    public static bool IsImmutable => CompactTrie<T>.IsImmutable;
    public static Concurrency ThreadSafety => CompactTrie<T>.ThreadSafety;

    public int Count => trie.Count;

    public static IPrefixLookup<string, TValue?> Create<TValue>(IEnumerable<KeyValuePair<string, TValue?>> source)
    {
        var result = new PrefixLookup<TValue?>();
        result.trie = (FlatTrie<TValue?>)FlatTrie<TValue?>.Create(source);
        return result;
    }

    public static IPrefixLookup<string, TValue?> Create<TValue>()
    {
        var result = new PrefixLookup<TValue?>();
        result.trie = (FlatTrie<TValue?>)FlatTrie<TValue>.Create<TValue?>();
        return result;
    }

    public void Clear()
    {
        trie.Clear();
    }

    public IEnumerator<KeyValuePair<string, T?>> GetEnumerator()
    {
        return trie.GetEnumerator();
    }

    IEnumerator<KeyValuePair<string, T?>> IEnumerable<KeyValuePair<string, T?>>.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    public SearchResult<KeyValuePair<string, T?>> Search(string keyPrefix)
    {
        return trie.Search(keyPrefix);
    }

    IEnumerable<KeyValuePair<string, T?>> IPrefixLookup<string, T?>.Search(string keyPrefix)
    {
        return Search(keyPrefix);
    }

    public SearchResult<T?> SearchValues(string keyPrefix)
    {
        return trie.SearchValues(keyPrefix);
    }

    IEnumerable<T> IPrefixLookup<string, T?>.SearchValues(string keyPrefix)
    {
        return trie.SearchValues(keyPrefix);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Dispose()
    {
    }
}