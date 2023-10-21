using System.Collections;
using System.Text.Unicode;

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
public class RadixTree<T> : IPrefixLookup<T>
{

    public static bool IsImmutable => false;

    /// <summary>
    /// This lookup is thought to have the best support for single writer,
    /// multiple reader. All updates are done on cloned records and the
    /// update to the graph relies on a single object reference assignment
    /// (which is atomic).
    /// </summary>
    public static Concurrency ThreadSafety => Concurrency.Read;
    public static bool IsSorted => true;

    private RadixTreeNode<T?> root;

    public RadixTree()
    {
        root = new RadixTreeNode<T?>();
    }

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


    public T? Get(ReadOnlySpan<byte> key)
    {
        return root.Get(key);
    }

    public void Clear()
    {
        this.root.Reset();
    }

    public IEnumerator<KeyValue<T?>> GetEnumerator()
    {
        return Search(ReadOnlySpan<byte>.Empty).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static IPrefixLookup<TValue?> Create<TValue>()
    {
        return new RadixTree<TValue?>();
    }

    public static IPrefixLookup<TValue?> Create<TValue>(IEnumerable<KeyValue<TValue?>> source)
    {
        var result = new RadixTree<TValue?>();
        foreach (var kvp in source)
        {
            result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    public RadixValueEnumerator<T?> SearchValues(string keyPrefix)
    {
        Span<byte> keyBuffer = stackalloc byte[keyPrefix.Length * 4];
        keyBuffer = GetKeyStringBytes(keyPrefix, keyBuffer);
        return SearchValues(keyBuffer);
    }

    public RadixValueEnumerator<T?> SearchValues(ReadOnlySpan<byte> keyPrefix)
    {
        RadixTreeNode<T?>? matchingNode = keyPrefix.Length == 0 ? root : root.FindPrefixMatch(keyPrefix);
        return new RadixValueEnumerator<T?>(matchingNode);
    }

    public RadixKvpEnumerator<T?> Search(string keyPrefix)
    {
        Span<byte> keyBuffer = stackalloc byte[keyPrefix.Length * 4];
        keyBuffer = GetKeyStringBytes(keyPrefix, keyBuffer);
        return Search(keyBuffer);
    }

    public RadixKvpEnumerator<T?> Search(ReadOnlySpan<byte> keyPrefix)
    {
        RadixTreeNode<T?>? matchingNode = keyPrefix.Length == 0 ? root : root.FindPrefixMatch(keyPrefix);
        return new RadixKvpEnumerator<T?>(matchingNode);
    }

    IEnumerator<KeyValue<T?>> IEnumerable<KeyValue<T?>>.GetEnumerator()
    {
        return this.Search(string.Empty).GetEnumerator();
    }

    IEnumerable<KeyValue<T?>> IPrefixLookup<T>.Search(string keyPrefix)
    {
        return this.Search(keyPrefix);
    }

    IEnumerable<T?> IPrefixLookup<T>.SearchValues(string keyPrefix)
    {
        return this.SearchValues(keyPrefix);
    }
}
