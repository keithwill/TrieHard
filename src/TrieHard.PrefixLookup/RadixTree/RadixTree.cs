using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using TrieHard.Abstractions;
using TrieHard.PrefixLookup;

namespace TrieHard.Collections;

/// <summary>
/// This is an implementation of a RadixTree. It uses key segments of strings
/// instead of storing a character of the key per node. Segments are created
/// by merging runs of characters which have no values or splits in children.
/// Thread Safe: Multiple Reader, Single Writer
/// 
/// Advantages: Offers theoretical advantages when the key space is not saturated
/// or where the keys stored might have long repeated prefixes and special values (for example, route patterns).
/// Comparing operations are faster on arrays of characters than on individual characters
/// that must be retrieved by referencing children / siblings. This implementation does
/// not require any additional backing storage than object references from the root, and 
/// keys are stored as segments on each node, making this implementation easier to diagnose
/// than some of the other implementations.
/// 
/// Disadvantages: Inserting new values is very unintuitive with this implementation as it
/// requires a node to be able to replace itself in the graph and this type of cloning leads
/// to additional garbage generation.
/// </summary>
/// <seealso>https://en.wikipedia.org/wiki/Radix_tree</seealso>
/// 
/// <typeparam name="T"></typeparam>
public class RadixTree<T> : IPrefixLookup<string, T>
{

    public static bool IsImmutable => false;
    public static Concurrency ThreadSafety => Concurrency.Read;

    private RadixTreeNode<T> root;

    public RadixTree()
    {
        root = new RadixTreeNode<T>();
    }

    public int NodeCount => root.GetChildrenCount();

    public int Count => root.GetValuesCount();


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

    private static Span<byte> GetKeyStringBytes(string key, Span<byte> buffer)
    {
        Utf8.FromUtf16(key, buffer, out var _, out var bytesWritten, false, true);
        return buffer.Slice(0, bytesWritten);
    }

    public void Set(ReadOnlySpan<byte> keyBytes, T? value)
    {
        root.SetValue(ref root, keyBytes, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? Get(ReadOnlySpan<byte> key)
    {
        return root.GetValue(key);
    }

    public void Clear()
    {
        this.root.Reset();
    }

    public SearchResult<KeyValuePair<byte[], T?>> SearchUtf8(ReadOnlySpan<byte> keyPrefix)
    {
        var collector = ArrayPoolList<KeyValuePair<byte[], T?>>.Rent();
        if (keyPrefix.Length == 0)
        {
            root.CollectKeyValues(collector);
        }
        else
        {
            root.SearchPrefix(keyPrefix, collector);
        }
        return new SearchResult<KeyValuePair<byte[], T?>>(collector);
    }

    public SearchResult<KeyValuePair<string, T?>> Search(ReadOnlySpan<byte> keyPrefix)
    {
        var collector = ArrayPoolList<KeyValuePair<string, T?>>.Rent();
        if (keyPrefix.Length == 0)
        {
            root.CollectKeyValueStrings(collector);
        }
        else
        {
            root.SearchPrefixStrings(keyPrefix, collector);
        }
        return new SearchResult<KeyValuePair<string, T?>>(collector);
    }

    public IEnumerator<KeyValuePair<byte[], T?>> GetEnumerator()
    {
        return SearchUtf8(ReadOnlySpan<byte>.Empty).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static IPrefixLookup<string, TValue?> Create<TValue>(IEnumerable<KeyValuePair<string, TValue?>> source)
    {
        var result = new RadixTree<TValue?>();
        foreach (var kvp in source)
        {
            result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    public SearchResult<T?> SearchValues(ReadOnlySpan<byte> keyPrefix)
    {
        var collector = ArrayPoolList<T?>.Rent();
        if (keyPrefix.Length == 0)
        {
            root.CollectValues(collector);
        }
        else
        {
            root.SearchPrefixValues(keyPrefix, collector);
        }
        return new SearchResult<T?>(collector);
    }

    public static IPrefixLookup<string, TValue?> Create<TValue>()
    {
        return new RadixTree<TValue?>();
    }

    public IEnumerable<KeyValuePair<string, T?>> Search(string keyPrefix)
    {
        Span<byte> keyBuffer = stackalloc byte[keyPrefix.Length * 4];
        keyBuffer = GetKeyStringBytes(keyPrefix, keyBuffer);
        return Search(keyBuffer);
    }

    public IEnumerable<T?> SearchValues(string keyPrefix)
    {
        Span<byte> keyBuffer = stackalloc byte[keyPrefix.Length * 4];
        keyBuffer = GetKeyStringBytes(keyPrefix, keyBuffer);
        return SearchValues(keyBuffer);
    }

    IEnumerator<KeyValuePair<string, T?>> IEnumerable<KeyValuePair<string, T?>>.GetEnumerator()
    {
        return this.Search(string.Empty).GetEnumerator();
    }
}
