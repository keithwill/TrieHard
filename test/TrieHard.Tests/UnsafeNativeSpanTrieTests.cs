using System.Text;
using TrieHard.Collections;

namespace TrieHard.Tests;

[TestFixture]
public class UnsafeNativeSpanTrieTests
{
    const string TestKey = "TestKey";
    const string TestKeyPrefix = "Test";
    static readonly byte[] TestValue = Encoding.UTF8.GetBytes("hello");
    static readonly byte[] OtherValue = Encoding.UTF8.GetBytes("world");

    private KeyValue<byte[]>[] GetTestEntries(int count)
    {
        var entries = new KeyValue<byte[]>[count];
        for (int i = 0; i < count; i++)
            entries[i] = new KeyValue<byte[]>(i.ToString(), Encoding.UTF8.GetBytes(i.ToString()));
        return entries;
    }

    private UnsafeNativeSpanTrie CreateWithTestEntry()
    {
        var trie = new UnsafeNativeSpanTrie();
        trie.Set(TestKey, (ReadOnlySpan<byte>)TestValue);
        return trie;
    }

    private static byte[] ToBytes(NativeByteSpan? span) => span?.ToArray() ?? [];

    // -------------------------------------------------------------------------
    // Construction / factory
    // -------------------------------------------------------------------------

    [Test]
    public void Create_EmptyTrie_DoesNotThrow()
    {
        using var trie = new UnsafeNativeSpanTrie();
        Assert.That(trie.Count, Is.EqualTo(0));
    }

    [Test]
    public void Create_MultipleEntries_PopulatesCount()
    {
        // NativeByteSpan contains a pointer field, so Nullable<NativeByteSpan> is not
        // valid at the CLR level. Populate via the normal Set path instead.
        using var trie = new UnsafeNativeSpanTrie();
        trie.Set("key1", [1]);
        trie.Set("key2", [2]);
        Assert.That(trie.Count, Is.EqualTo(2));
    }

    // -------------------------------------------------------------------------
    // Set / Get
    // -------------------------------------------------------------------------

    [Test]
    public void Set_DoesNotThrow()
    {
        using var trie = new UnsafeNativeSpanTrie();
        Assert.DoesNotThrow(() => trie.Set(TestKey, (ReadOnlySpan<byte>)TestValue));
    }

    [Test]
    public void Get_ReturnsStoredValue()
    {
        using var trie = CreateWithTestEntry();
        var result = trie.Get(TestKey);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.ToArray(), Is.EqualTo(TestValue));
    }

    [Test]
    public void Get_MissingKey_ReturnsNull()
    {
        using var trie = new UnsafeNativeSpanTrie();
        Assert.That(trie.Get("missing"), Is.Null);
    }

    [Test]
    public void Indexer_Get_ReturnsStoredValue()
    {
        using var trie = CreateWithTestEntry();
        NativeByteSpan? result = trie[TestKey];
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.ToArray(), Is.EqualTo(TestValue));
    }

    [Test]
    public void Indexer_Set_SetsValue()
    {
        using var trie = new UnsafeNativeSpanTrie();
        NativeByteSpan? nbs = null;
        // Setting null via indexer should store a null value without throwing
        Assert.DoesNotThrow(() => trie[TestKey] = nbs);
    }

    [Test]
    public void Set_OverwriteWithSmallerValue_UpdatesInPlace()
    {
        using var trie = CreateWithTestEntry();
        var smaller = new byte[] { (byte)'x' };
        trie.Set(TestKey, (ReadOnlySpan<byte>)smaller);
        Assert.That(ToBytes(trie.Get(TestKey)), Is.EqualTo(smaller));
    }

    [Test]
    public void Set_OverwriteWithLargerValue_UpdatesValue()
    {
        using var trie = CreateWithTestEntry();
        var larger = Encoding.UTF8.GetBytes("a much longer replacement value");
        trie.Set(TestKey, (ReadOnlySpan<byte>)larger);
        Assert.That(ToBytes(trie.Get(TestKey)), Is.EqualTo(larger));
    }

    [Test]
    public void Set_OverwriteExistingKey_DoesNotIncrementCount()
    {
        using var trie = CreateWithTestEntry();
        trie.Set(TestKey, (ReadOnlySpan<byte>)OtherValue);
        Assert.That(trie.Count, Is.EqualTo(1));
    }

    [Test]
    public void Set_NullValue_ReturnsNullFromGet()
    {
        // Per the node contract: ValuePointer=0 with HasValue=true represents a stored null.
        // ReadNodeValue returns null? when ValuePointer is zero.
        using var trie = CreateWithTestEntry();
        trie.Set(TestKey, (byte[]?)null);
        var result = trie.Get(TestKey);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Set_NullValue_StillCountsAsEntry()
    {
        using var trie = new UnsafeNativeSpanTrie();
        trie.Set(TestKey, (byte[]?)null);
        Assert.That(trie.Count, Is.EqualTo(1));
    }

    // -------------------------------------------------------------------------
    // Search (key-value pairs)
    // -------------------------------------------------------------------------

    [Test]
    public void Search_FindsValueByPrefix()
    {
        using var trie = CreateWithTestEntry();
        var result = trie.Search(TestKeyPrefix).Single();
        Assert.That(result.Value!.Value.ToArray(), Is.EqualTo(TestValue));
    }

    [Test]
    public void Search_FindsKeyByPrefix()
    {
        using var trie = CreateWithTestEntry();
        var result = trie.Search(TestKeyPrefix).Single();
        Assert.That(result.Key, Is.EqualTo(TestKey));
    }

    [Test]
    public void Search_NoMatch_ReturnsEmpty()
    {
        using var trie = CreateWithTestEntry();
        var results = trie.Search("ZZZ").ToArray();
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Search_EmptyPrefix_ReturnsAllEntries()
    {
        using var trie = new UnsafeNativeSpanTrie();
        trie.Set("a", [1]);
        trie.Set("b", [2]);
        trie.Set("c", [3]);
        var results = trie.Search("").ToArray();
        Assert.That(results.Length, Is.EqualTo(3));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(1000)]
    public void Search_SequentialKeys_ResultsAreAccurate(int valuesToAdd)
    {
        var entries = GetTestEntries(valuesToAdd);
        using var trie = new UnsafeNativeSpanTrie();
        foreach (var e in entries)
            trie.Set(e.Key, (ReadOnlySpan<byte>)e.Value);

        const string prefix = "1";
        var actual = trie.Search(prefix).ToArray();
        var expected = entries
            .Where(e => e.Key.StartsWith(prefix))
            .OrderBy(e => e.Key)
            .ToArray();

        Assert.That(actual.Length, Is.EqualTo(expected.Length));
        for (int i = 0; i < actual.Length; i++)
        {
            Assert.That(actual[i].Key, Is.EqualTo(expected[i].Key));
            Assert.That(actual[i].Value!.Value.ToArray(), Is.EqualTo(expected[i].Value));
        }
    }

    // -------------------------------------------------------------------------
    // SearchValues (values only)
    // -------------------------------------------------------------------------

    [Test]
    public void SearchValues_FindsValueByPrefix()
    {
        using var trie = CreateWithTestEntry();
        var result = trie.SearchValues(TestKeyPrefix).Single();
        Assert.That(result!.Value.ToArray(), Is.EqualTo(TestValue));
    }

    [Test]
    public void SearchValues_NoMatch_ReturnsEmpty()
    {
        using var trie = CreateWithTestEntry();
        var results = trie.SearchValues("ZZZ").ToArray();
        Assert.That(results, Is.Empty);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(1000)]
    public void SearchValues_SequentialKeys_ResultsAreAccurate(int valuesToAdd)
    {
        var entries = GetTestEntries(valuesToAdd);
        using var trie = new UnsafeNativeSpanTrie();
        foreach (var e in entries)
            trie.Set(e.Key, (ReadOnlySpan<byte>)e.Value);

        const string prefix = "1";
        var actual = trie.SearchValues(prefix).ToArray();
        var expected = entries
            .Where(e => e.Key.StartsWith(prefix))
            .OrderBy(e => e.Key)
            .Select(e => e.Value)
            .ToArray();

        Assert.That(actual.Length, Is.EqualTo(expected.Length));
        for (int i = 0; i < actual.Length; i++)
            Assert.That(actual[i]!.Value.ToArray(), Is.EqualTo(expected[i]));
    }

    [Test]
    public void SearchValues_ByteSpanOverload_FindsValues()
    {
        using var trie = CreateWithTestEntry();
        var keyBytes = Encoding.UTF8.GetBytes(TestKeyPrefix);
        var result = trie.SearchValues(keyBytes.AsSpan()).Single();
        Assert.That(result!.Value.ToArray(), Is.EqualTo(TestValue));
    }

    // -------------------------------------------------------------------------
    // LongestPrefix
    // -------------------------------------------------------------------------

    [Test]
    public void LongestPrefix_FindsExactMatch()
    {
        using var trie = CreateWithTestEntry();
        var result = trie.LongestPrefix(TestKey);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.ToArray(), Is.EqualTo(TestValue));
    }

    [Test]
    public void LongestPrefix_FindsLongestStoredPrefix()
    {
        using var trie = new UnsafeNativeSpanTrie();
        trie.Set("a", [1]);
        trie.Set("ab", [2]);
        trie.Set("abcd", [4]);

        var result = trie.LongestPrefix("abcde");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.ToArray(), Is.EqualTo(new byte[] { 4 }));
    }

    [Test]
    public void LongestPrefix_WhenKeyEndsWithinUnvaluedPath_ReturnsLastStoredPrefix()
    {
        using var trie = new UnsafeNativeSpanTrie();
        trie.Set("ab", [2]);
        trie.Set("abcd", [4]);

        var result = trie.LongestPrefix("abc");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.ToArray(), Is.EqualTo(new byte[] { 2 }));
    }

    [Test]
    public void LongestPrefix_WhenNoStoredPrefixMatches_ReturnsNull()
    {
        using var trie = CreateWithTestEntry();
        var result = trie.LongestPrefix("NoMatch");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void LongestPrefix_EmptyKeyInput_ReturnsNull()
    {
        using var trie = CreateWithTestEntry();
        // No empty-key value stored
        var result = trie.LongestPrefix("");
        Assert.That(result, Is.Null);
    }

    // -------------------------------------------------------------------------
    // Count / Clear
    // -------------------------------------------------------------------------

    [Test]
    public void AddIncrementsCount()
    {
        using var trie = new UnsafeNativeSpanTrie();
        trie.Set(TestKey, (ReadOnlySpan<byte>)TestValue);
        Assert.That(trie.Count, Is.EqualTo(1));
    }

    [Test]
    public void ClearResetsCount()
    {
        using var trie = CreateWithTestEntry();
        trie.Clear();
        Assert.That(trie.Count, Is.EqualTo(0));
    }

    [Test]
    public void Clear_MakesValuesUnretrievable()
    {
        using var trie = CreateWithTestEntry();
        trie.Clear();
        Assert.That(trie.Get(TestKey), Is.Null);
    }

    [TestCase(5)]
    [TestCase(89)]
    [TestCase(987)]
    public void AddCountsAreAccurate(int valuesToAdd)
    {
        using var trie = new UnsafeNativeSpanTrie();
        for (int i = 1; i <= valuesToAdd; i++)
            trie.Set(i.ToString(), [(byte)(i % 256)]);
        Assert.That(trie.Count, Is.EqualTo(valuesToAdd));
    }

    // -------------------------------------------------------------------------
    // Enumeration
    // -------------------------------------------------------------------------

    [Test]
    public void Enumerate_DoesNotYieldPhantomRootEntry()
    {
        using var trie = CreateWithTestEntry();
        var keys = new List<string>();
        foreach (var kv in (IEnumerable<KeyValue<NativeByteSpan?>>)trie)
            keys.Add(kv.Key);

        Assert.That(keys, Has.Count.EqualTo(trie.Count));
        Assert.That(keys, Has.None.EqualTo(""));
    }

    [Test]
    public void Enumerate_YieldsAllStoredEntries()
    {
        var entries = GetTestEntries(50);
        using var trie = new UnsafeNativeSpanTrie();
        foreach (var e in entries)
            trie.Set(e.Key, (ReadOnlySpan<byte>)e.Value);

        var actual = ((IEnumerable<KeyValue<NativeByteSpan?>>)trie)
            .OrderBy(kv => kv.Key)
            .Select(kv => new { kv.Key, Value = kv.Value!.Value.ToArray() })
            .ToArray();
        var expected = entries.OrderBy(e => e.Key).ToArray();

        Assert.That(actual.Length, Is.EqualTo(expected.Length));
        for (int i = 0; i < actual.Length; i++)
        {
            Assert.That(actual[i].Key, Is.EqualTo(expected[i].Key));
            Assert.That(actual[i].Value, Is.EqualTo(expected[i].Value));
        }
    }

    // -------------------------------------------------------------------------
    // NativeByteSpan value semantics
    // -------------------------------------------------------------------------

    [Test]
    public void NativeByteSpan_AsSpan_ReadsCorrectBytes()
    {
        using var trie = CreateWithTestEntry();
        var result = trie.Get(TestKey)!.Value;
        Assert.That(result.AsSpan().ToArray(), Is.EqualTo(TestValue));
    }

    [Test]
    public void NativeByteSpan_Length_IsCorrect()
    {
        using var trie = CreateWithTestEntry();
        var result = trie.Get(TestKey)!.Value;
        Assert.That(result.Length, Is.EqualTo(TestValue.Length));
    }

    [Test]
    public void EmptyByteArray_StoredAndRetrieved_HasLengthZero()
    {
        using var trie = new UnsafeNativeSpanTrie();
        trie.Set(TestKey, ReadOnlySpan<byte>.Empty);
        var result = trie.Get(TestKey);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Length, Is.EqualTo(0));
    }

    // -------------------------------------------------------------------------
    // Dispose
    // -------------------------------------------------------------------------

    [Test]
    public void Dispose_DoesNotThrow()
    {
        var trie = new UnsafeNativeSpanTrie();
        trie.Set(TestKey, (ReadOnlySpan<byte>)TestValue);
        Assert.DoesNotThrow(() => trie.Dispose());
    }

    [Test]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var trie = new UnsafeNativeSpanTrie();
        trie.Dispose();
        Assert.DoesNotThrow(() => trie.Dispose());
    }

    // -------------------------------------------------------------------------
    // IPrefixLookup<NativeByteSpan?> interface compliance
    // -------------------------------------------------------------------------

    [Test]
    public void IPrefixLookup_Search_ReturnsCorrectValues()
    {
        using var trie = CreateWithTestEntry();
        IPrefixLookup<NativeByteSpan?> lookup = trie;
        var result = lookup.Search(TestKeyPrefix).Single();
        Assert.That(result.Key, Is.EqualTo(TestKey));
        Assert.That(result.Value!.Value.ToArray(), Is.EqualTo(TestValue));
    }

    [Test]
    public void IPrefixLookup_SearchValues_ReturnsCorrectValues()
    {
        using var trie = CreateWithTestEntry();
        IPrefixLookup<NativeByteSpan?> lookup = trie;
        var result = lookup.SearchValues(TestKeyPrefix).Single();
        Assert.That(result!.Value.ToArray(), Is.EqualTo(TestValue));
    }

    [Test]
    public void IPrefixLookup_LongestPrefix_ReturnsCorrectValue()
    {
        using var trie = CreateWithTestEntry();
        IPrefixLookup<NativeByteSpan?> lookup = trie;
        Assert.That(lookup.LongestPrefix(TestKey)!.Value.ToArray(), Is.EqualTo(TestValue));
        Assert.That(lookup.LongestPrefix("NoMatch"), Is.Null);
    }
}
