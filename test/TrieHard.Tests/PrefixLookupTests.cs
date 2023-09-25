using NUnit.Framework.Internal;
using TrieHard.Alternatives.ExternalLibraries.rm.Trie;
using TrieHard.Alternatives.List;
using TrieHard.Alternatives.SQLite;
using TrieHard.Collections;
using TrieHard.Abstractions;

namespace TrieHard.Tests;

public record class TestRecord(string Key);

public abstract class PrefixLookupTests<T> where T : IPrefixLookup<string, TestRecord?>
{
    protected TestRecord TestRecord { get; init; }
    private KeyValuePair<string, TestRecord> testKvp;
    private KeyValuePair<string, TestRecord>[] testKvpEnumerable;
    const string TestKey = "TestKey";
    const string TestKeyPrefix = "Test";
    private KeyValuePair<string, TestRecord>[] TestRecords;

    public PrefixLookupTests()
    {
        TestRecords = GetTestRecords(1000);
        TestRecord = TestRecords[0].Value;
        testKvp = new KeyValuePair<string, TestRecord>(TestKey, TestRecord);
        testKvpEnumerable = new KeyValuePair<string, TestRecord>[] { testKvp };
    }

    [Test]
    public void Create()
    {
        Assume.That(T.IsImmutable, Is.False);
        T lookup = (T)T.Create<TestRecord>();
    }

    [Test]
    public void CreateWithValues()
    {
        T lookup = (T)T.Create(testKvpEnumerable!);
    }

    [Test]
    public void Add()
    {
        Assume.That(T.IsImmutable, Is.False);
        Assume.That(Create, Throws.Nothing);
        var lookup = (T)T.Create<TestRecord>();
        lookup[TestKey] = TestRecord;
    }

    [Test]
    public void Get()
    {
        Assume.That(CreateWithValues, Throws.Nothing);
        var lookup = (T)T.Create(testKvpEnumerable!);
        var result = lookup[TestKey];
        Assert.That(result, Is.SameAs(TestRecord));
    }

    [Test]
    public void Search_FindsValueByPrefix()
    {
        Assume.That(Add, Throws.Nothing);
        T lookup = (T)T.Create(testKvpEnumerable!);
        lookup[TestKey] = TestRecord;
        var result = lookup.Search(TestKeyPrefix).Single();
        Assert.That(result.Value, Is.SameAs(TestRecord));
    }

    [Test]
    public void SearchValues_FindsValueByPrefix()
    {
        Assume.That(Add, Throws.Nothing);
        T lookup = (T)T.Create(testKvpEnumerable!);
        lookup[TestKey] = TestRecord;
        var result = lookup.SearchValues(TestKeyPrefix).Single();
        Assert.That(result, Is.SameAs(TestRecord));
    }

    [Test]
    public void Search_FindsKeyByPrefix()
    {
        Assume.That(CreateWithValues, Throws.Nothing);
        T lookup = (T)T.Create(testKvpEnumerable!);
        var result = lookup.Search(TestKeyPrefix).Single();
        Assert.That(result.Key, Is.EqualTo(TestKey));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(1000)]
    public void Search_SequentialKeys_ResultsAreAccurate(int valuesToAdd)
    {
        Assume.That(CreateWithValues, Throws.Nothing);
        var testKeyValues = GetTestRecords(valuesToAdd);
        var lookup = (T)T.Create(testKeyValues!);

        var prefix = "1";

        var actualResults = lookup.Search(prefix).ToArray();

        var expected = testKeyValues.Where(x => x.Key.StartsWith(prefix))
            .OrderBy(x => x.Key).ToArray();

        Assert.That(actualResults, Is.EquivalentTo(expected));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(1000)]
    public void SearchValues_SequentialKeys_ResultsAreAccurate(int valuesToAdd)
    {
        Assume.That(CreateWithValues, Throws.Nothing);
        var testKeyValues = GetTestRecords(valuesToAdd);
        var lookup = (T)T.Create(testKeyValues!);

        var prefix = "1";

        var actualResults = lookup.SearchValues(prefix).ToArray();

        var expected = testKeyValues
            .Where(x => x.Key.StartsWith(prefix))
            .OrderBy(x => x.Key)
            .Select(x => x.Value).ToArray();

        Assert.That(actualResults.Length, Is.EqualTo(expected.Length));

        for (int resultIndex = 0; resultIndex < expected.Length; resultIndex++)
        {
            TestRecord? expectedResult = expected[resultIndex];
            TestRecord? actualResult = actualResults[resultIndex];
            Assert.That(expectedResult.Key, Is.EqualTo(actualResult!.Key));
        }
    }


    protected KeyValuePair<string, TestRecord>[] GetTestRecords(int count)
    {
        List<KeyValuePair<string, TestRecord>> testKeyValues = new();
        for (int i = 0; i < count; i++)
        {
            var key = i.ToString();
            var record = new TestRecord(key);
            testKeyValues.Add(new KeyValuePair<string, TestRecord>(key, record));
        }

        return testKeyValues.ToArray();
    }



    [Test]
    public void AddIncrementsCount()
    {
        Assume.That(T.IsImmutable, Is.False);
        Assume.That(Add, Throws.Nothing);
        var lookup = (T)T.Create<TestRecord>();
        lookup[TestKey] = TestRecord;
        Assert.That(lookup.Count, Is.EqualTo(1));
    }

    [Test]
    public void ClearResetsCount()
    {
        Assume.That(T.IsImmutable, Is.False);
        Assume.That(AddIncrementsCount, Throws.Nothing);
        var lookup = (T)T.Create<TestRecord>();
        lookup[TestKey] = TestRecord;
        lookup.Clear();
        Assert.That(lookup.Count, Is.EqualTo(0));
    }

    [TestCase(5)]
    [TestCase(89)]
    [TestCase(987)]
    public void AddCountsAreAccurate(int valuesToAdd)
    {
        Assume.That(T.IsImmutable, Is.False);
        Assume.That(AddIncrementsCount, Throws.Nothing);
        var lookup = (T)T.Create<TestRecord>();
        for (int i = 1; i <= valuesToAdd; i++)
        {
            lookup[i.ToString()] = TestRecord;
        }
        Assert.That(lookup.Count, Is.EqualTo(valuesToAdd));
    }
}


public class SimpleTrieTests : PrefixLookupTests<SimpleTrie<TestRecord?>> { }
public class IndirectTrieTests : PrefixLookupTests<IndirectTrie<TestRecord?>> { }
public class SqliteLookupTests : PrefixLookupTests<SQLiteLookup<TestRecord?>> { }
public class ListPrefixLookupTests : PrefixLookupTests<ListPrefixLookup<TestRecord?>> { }
public class rmTrieTests : PrefixLookupTests<rmTrie<TestRecord?>> { }
public class FlatTrieTests : PrefixLookupTests<FlatTrie<TestRecord?>> { }