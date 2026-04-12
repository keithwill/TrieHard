using System.Text;
using TrieHard.Collections;

namespace TrieHard.Tests;

[TestFixture]
public class UnsafeBlittableTrieTests
{
    const string TestKey = "TestKey";
    const string TestKeyPrefix = "Test";
    const int TestValue = 42;

    private KeyValue<int>[] GetTestEntries(int count)
    {
        var entries = new KeyValue<int>[count];
        for (int i = 0; i < count; i++)
            entries[i] = new KeyValue<int>(i.ToString(), i);
        return entries;
    }

    private UnsafeBlittableTrie<int> CreateWithTestEntry()
    {
        var trie = new UnsafeBlittableTrie<int>();
        trie[TestKey] = TestValue;
        return trie;
    }

    // -------------------------------------------------------------------------
    // Construction / factory
    // -------------------------------------------------------------------------

    [Test]
    public void Create_EmptyTrie_DoesNotThrow()
    {
        using var trie = new UnsafeBlittableTrie<int>();
        Assert.That(trie.Count, Is.EqualTo(0));
    }

    [Test]
    public void Create_StaticFactory_PopulatesEntries()
    {
        var entries = GetTestEntries(10);
        using var trie = (UnsafeBlittableTrie<int>)UnsafeBlittableTrie<int>.Create<int>(
            entries.Select(e => new KeyValue<int?>(e.Key, e.Value)));
        Assert.That(trie.Count, Is.EqualTo(10));
    }

    // -------------------------------------------------------------------------
    // Set / Get
    // -------------------------------------------------------------------------

    [Test]
    public void Add_SetsValueViaIndexer()
    {
        using var trie = new UnsafeBlittableTrie<int>();
        Assert.DoesNotThrow(() => trie[TestKey] = TestValue);
    }

    [Test]
    public void Get_ReturnsStoredValue()
    {
        using var trie = CreateWithTestEntry();
        var result = trie[TestKey];
        Assert.That(result, Is.EqualTo((int?)TestValue));
    }

    [Test]
    public void Get_MissingKey_ReturnsNull()
    {
        using var trie = new UnsafeBlittableTrie<int>();
        var result = trie["missing"];
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Set_OverwriteExistingKey_UpdatesValue()
    {
        using var trie = CreateWithTestEntry();
        trie[TestKey] = 99;
        Assert.That(trie[TestKey], Is.EqualTo((int?)99));
    }

    [Test]
    public void Set_OverwriteExistingKey_DoesNotIncrementCount()
    {
        using var trie = CreateWithTestEntry();
        trie[TestKey] = 99;
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
        Assert.That(result.Value, Is.EqualTo((int?)TestValue));
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

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(1000)]
    public void Search_SequentialKeys_ResultsAreAccurate(int valuesToAdd)
    {
        var entries = GetTestEntries(valuesToAdd);
        using var trie = new UnsafeBlittableTrie<int>();
        foreach (var e in entries)
            trie[e.Key] = e.Value;

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
            Assert.That(actual[i].Value, Is.EqualTo((int?)expected[i].Value));
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
        Assert.That(result, Is.EqualTo((int?)TestValue));
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
        using var trie = new UnsafeBlittableTrie<int>();
        foreach (var e in entries)
            trie[e.Key] = e.Value;

        const string prefix = "1";
        var actual = trie.SearchValues(prefix).ToArray();
        var expected = entries
            .Where(e => e.Key.StartsWith(prefix))
            .OrderBy(e => e.Key)
            .Select(e => (int?)e.Value)
            .ToArray();

        Assert.That(actual.Length, Is.EqualTo(expected.Length));
        for (int i = 0; i < actual.Length; i++)
            Assert.That(actual[i], Is.EqualTo(expected[i]));
    }

    [Test]
    public void SearchValues_ByteSpanOverload_FindsValues()
    {
        using var trie = CreateWithTestEntry();
        var keyBytes = Encoding.UTF8.GetBytes(TestKeyPrefix);
        var result = trie.SearchValues(keyBytes.AsSpan()).Single();
        Assert.That(result, Is.EqualTo((int?)TestValue));
    }

    // -------------------------------------------------------------------------
    // LongestPrefix
    // -------------------------------------------------------------------------

    [Test]
    public void LongestPrefix_FindsExactMatch()
    {
        using var trie = CreateWithTestEntry();
        var result = trie.LongestPrefix(TestKey);
        Assert.That(result, Is.EqualTo((int?)TestValue));
    }

    [Test]
    public void LongestPrefix_FindsLongestStoredPrefix()
    {
        using var trie = new UnsafeBlittableTrie<int>();
        trie["a"] = 1;
        trie["ab"] = 2;
        trie["abcd"] = 4;

        var result = trie.LongestPrefix("abcde");
        Assert.That(result, Is.EqualTo((int?)4));
    }

    [Test]
    public void LongestPrefix_WhenKeyEndsWithinUnvaluedPath_ReturnsLastStoredPrefix()
    {
        using var trie = new UnsafeBlittableTrie<int>();
        trie["ab"] = 2;
        trie["abcd"] = 4;

        var result = trie.LongestPrefix("abc");
        Assert.That(result, Is.EqualTo((int?)2));
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
        using var trie = new UnsafeBlittableTrie<int>();
        trie[TestKey] = TestValue;
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
        Assert.That(trie[TestKey], Is.Null);
    }

    [TestCase(5)]
    [TestCase(89)]
    [TestCase(987)]
    public void AddCountsAreAccurate(int valuesToAdd)
    {
        using var trie = new UnsafeBlittableTrie<int>();
        for (int i = 1; i <= valuesToAdd; i++)
            trie[i.ToString()] = i;
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
        foreach (var kv in (IEnumerable<KeyValue<int?>>)trie)
            keys.Add(kv.Key);

        Assert.That(keys, Has.Count.EqualTo(trie.Count));
        Assert.That(keys, Has.None.EqualTo(""));
    }

    [Test]
    public void Enumerate_YieldsAllStoredEntries()
    {
        var entries = GetTestEntries(50);
        using var trie = new UnsafeBlittableTrie<int>();
        foreach (var e in entries)
            trie[e.Key] = e.Value;

        var actual = ((IEnumerable<KeyValue<int?>>)trie)
            .OrderBy(kv => kv.Key)
            .ToArray();
        var expected = entries.OrderBy(e => e.Key).ToArray();

        Assert.That(actual.Length, Is.EqualTo(expected.Length));
        for (int i = 0; i < actual.Length; i++)
        {
            Assert.That(actual[i].Key, Is.EqualTo(expected[i].Key));
            Assert.That(actual[i].Value, Is.EqualTo((int?)expected[i].Value));
        }
    }

    // -------------------------------------------------------------------------
    // Dispose
    // -------------------------------------------------------------------------

    [Test]
    public void Dispose_DoesNotThrow()
    {
        var trie = new UnsafeBlittableTrie<int>();
        trie[TestKey] = TestValue;
        Assert.DoesNotThrow(() => trie.Dispose());
    }

    [Test]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var trie = new UnsafeBlittableTrie<int>();
        trie.Dispose();
        Assert.DoesNotThrow(() => trie.Dispose());
    }

    // -------------------------------------------------------------------------
    // IPrefixLookup<int?> interface compliance
    // -------------------------------------------------------------------------

    [Test]
    public void IPrefixLookup_Indexer_RoundTrips()
    {
        using var trie = new UnsafeBlittableTrie<int>();
        IPrefixLookup<int?> lookup = trie;
        lookup[TestKey] = TestValue;
        Assert.That(lookup[TestKey], Is.EqualTo((int?)TestValue));
    }

    [Test]
    public void IPrefixLookup_Search_ReturnsCorrectValues()
    {
        using var trie = CreateWithTestEntry();
        IPrefixLookup<int?> lookup = trie;
        var result = lookup.Search(TestKeyPrefix).Single();
        Assert.That(result.Key, Is.EqualTo(TestKey));
        Assert.That(result.Value, Is.EqualTo((int?)TestValue));
    }

    [Test]
    public void IPrefixLookup_SearchValues_ReturnsCorrectValues()
    {
        using var trie = CreateWithTestEntry();
        IPrefixLookup<int?> lookup = trie;
        var result = lookup.SearchValues(TestKeyPrefix).Single();
        Assert.That(result, Is.EqualTo((int?)TestValue));
    }

    [Test]
    public void IPrefixLookup_LongestPrefix_ReturnsCorrectValue()
    {
        using var trie = CreateWithTestEntry();
        IPrefixLookup<int?> lookup = trie;
        Assert.That(lookup.LongestPrefix(TestKey), Is.EqualTo((int?)TestValue));
        Assert.That(lookup.LongestPrefix("NoMatch"), Is.Null);
    }
}
