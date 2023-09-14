using System;
using System.Collections;
using System.Collections.Generic;
using TrieHard.Collections.Contributions;

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
/// that must be retreived by referencing children / sibblings. This implementation does
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


    public T this[string key]
    {
        get => Get(key);
        set => Set(key, value);
    }

    public void Set(in ReadOnlySpan<char> keyBytes, T value)
    {
        root.SetValue(ref root, keyBytes, value);
    }

    public T Get(in ReadOnlySpan<char> key)
    {
        return root.GetValue(key).Value;
    }

    public void Clear()
    {
        this.root.Value = default;
        this.root.Children = Array.Empty<RadixTreeNode<T>>();
    }

    public IEnumerable<KeyValuePair<string, T>> Search(string keyPrefix)
    {
        if (keyPrefix == string.Empty) return root.Collect();
        return root.EnumeratePrefix(keyPrefix);
    }

    public IEnumerator<KeyValuePair<string, T>> GetEnumerator()
    {
        return Search(string.Empty).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static IPrefixLookup<string, TValue> Create<TValue>(IEnumerable<KeyValuePair<string, TValue>> source)
    {
        var result = new RadixTree<TValue>();
        foreach (var kvp in source)
        {
            result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    public IEnumerable<T> SearchValues(string keyPrefix)
    {
        foreach (var kvp in Search(keyPrefix))
        {
            yield return kvp.Value;
        }
    }
}
