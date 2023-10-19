using System.Collections;
using TrieHard.Abstractions;
using TrieHard.Collections;
using TrieHard.PrefixLookup;
using TrieHard.PrefixLookup.RadixTree;

public class PrefixLookup<T> : IPrefixLookup<T?>, IDisposable
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

    public static IPrefixLookup<TValue?> Create<TValue>(IEnumerable<KeyValue<TValue?>> source)
    {
        var result = new PrefixLookup<TValue?>();
        result.trie = (RadixTree<TValue?>)RadixTree<TValue?>.Create(source);
        return result;
    }

    public static IPrefixLookup<TValue?> Create<TValue>()
    {
        var result = new PrefixLookup<TValue?>();
        result.trie = (RadixTree<TValue?>)RadixTree<TValue>.Create<TValue?>();
        return result;
    }

    public void Clear()
    {
        trie.Clear();
    }

    public IEnumerator<KeyValue<T?>> GetEnumerator()
    {
        return trie.Search(string.Empty);
    }

    IEnumerator<KeyValue<T?>> IEnumerable<KeyValue<T?>>.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    public IEnumerable<KeyValue<T?>> Search(string keyPrefix)
    {

        return trie.Search(keyPrefix);
    }

    IEnumerable<KeyValue<T?>> IPrefixLookup<T?>.Search(string keyPrefix)
    {
        return Search(keyPrefix);
    }

    IEnumerable<T> IPrefixLookup<T?>.SearchValues(string keyPrefix)
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