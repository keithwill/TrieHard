using System.Collections;
using TrieHard.Abstractions;
using TrieHard.Collections;
using TrieHard.PrefixLookup;
using TrieHard.PrefixLookup.RadixTree;

public class PrefixLookup<T> : IPrefixLookup<string, T?>, IDisposable
{
    private RadixTree<T> trie;
    public T? this[string key]
    {
        get => trie[key];
        set => trie[key] = value;
    }

    public PrefixLookup()
    {
        trie = new RadixTree<T>();
    }

    public static bool IsImmutable => UnsafeTrie<T>.IsImmutable;
    public static Concurrency ThreadSafety => UnsafeTrie<T>.ThreadSafety;
    public static bool IsSorted => RadixTree<T>.IsSorted;

    public int Count => trie.Count;

    public static IPrefixLookup<string, TValue?> Create<TValue>(IEnumerable<KeyValuePair<string, TValue?>> source)
    {
        var result = new PrefixLookup<TValue?>();
        result.trie = (RadixTree<TValue?>)RadixTree<TValue?>.Create(source);
        return result;
    }

    public static IPrefixLookup<string, TValue?> Create<TValue>()
    {
        var result = new PrefixLookup<TValue?>();
        result.trie = (RadixTree<TValue?>)RadixTree<TValue>.Create<TValue?>();
        return result;
    }

    public void Clear()
    {
        trie.Clear();
    }

    public IEnumerator<KeyValuePair<string, T?>> GetEnumerator()
    {
        return trie.Search(string.Empty);
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

    public RadixValueEnumerator<T?> SearchValues(string keyPrefix)
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