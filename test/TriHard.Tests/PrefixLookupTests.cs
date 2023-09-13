using NUnit.Framework.Internal;
using TrieHard.Collections;
using TrieHard.Collections.Contributions;

namespace TriHard.Tests;

public record class TestRecord(string Key);

public abstract class PrefixLookupTests<T> where T : IPrefixLookup<string, TestRecord>
{
    public virtual T Lookup { get; init; }
    protected TestRecord TestRecord { get; init; }
    const string TestKey = "TestKey";
    const string TestKeyPrefix = "Test";

    public PrefixLookupTests(T lookup)
    {
        TestRecord = new TestRecord(TestKey);
        Lookup = lookup;
    }

    [Test]
    public void Add()
    {
        Assume.That(T.IsImmutable, Is.False);
        Lookup[TestKey] = TestRecord;
    }

    [Test]
    public void Get()
    {
        Assume.That(Add, Throws.Nothing);
        var result = Lookup[TestKey];
        Assert.That(result, Is.SameAs(TestRecord));
    }

    [Test]
    public void SearchFindsValueByPrefix()
    {
        Assume.That(Add, Throws.Nothing);
        var result = Lookup.Search(TestKeyPrefix).Single();
        Assert.That(result.Value, Is.SameAs(TestRecord));
    }

    [Test]
    public void SearchFindsKeyByPrefix()
    {
        Assume.That(Add, Throws.Nothing);
        var result = Lookup.Search(TestKeyPrefix).Single();
        Assert.That(result.Key, Is.EqualTo(TestKey));
    }

    [Test]
    public void SearchResultAreAccurate()
    {
        Randomizer random = new(3974503);
        Assume.That(T.IsImmutable, Is.False);
        Assume.That(Add, Throws.Nothing);
        Lookup.Clear();
        List<KeyValuePair<string, TestRecord>> testKeyValues = new();
        for(int i = 0; i < 1000; i++)
        {
            var key = i.ToString();
            var record = new TestRecord(key);
            Lookup[key] = record;
            testKeyValues.Add(new KeyValuePair<string, TestRecord>(key, record));
        }

        var prefix = "10";

        var actualResults = Lookup.Search(prefix).ToArray();

        var expected = testKeyValues.Where(x => x.Key.StartsWith(prefix))
            .OrderBy(x => x.Key).ToArray();

        Assert.That(actualResults, Is.EquivalentTo(expected));
    }

    [Test]
    public void AddIncrementsCount()
    {
        Assume.That(T.IsImmutable, Is.False);
        Assume.That(Add, Throws.Nothing);
        Assert.That(Lookup.Count, Is.EqualTo(1));
    }

    [Test]
    public void ClearResetsCount()
    {
        Assume.That(T.IsImmutable, Is.False);
        Assume.That(Add, Throws.Nothing);
        Lookup.Clear();
        Assert.That(Lookup.Count, Is.EqualTo(0));
    }

    [TestCase(5)]
    [TestCase(89)]
    [TestCase(987)]
    public void AddCountsAreAccurate(int valuesToAdd)
    {
        Assume.That(T.IsImmutable, Is.False);
        Assume.That(Add, Throws.Nothing);
        Assume.That(ClearResetsCount, Throws.Nothing);
        Lookup.Clear();
        for(int i = 1; i <= valuesToAdd; i++)
        {
            Lookup[i.ToString()] = TestRecord;
        }
        Assert.That(Lookup.Count, Is.EqualTo(valuesToAdd));
    }

}


public class SimpleTrieTests : PrefixLookupTests<SimpleTrie<TestRecord>>
{
    public SimpleTrieTests() : base(new SimpleTrie<TestRecord>()) { }
}

public class IndirectTrieTests : PrefixLookupTests<IndirectTrie<TestRecord>>
{
    public IndirectTrieTests() : base(new IndirectTrie<TestRecord>())
    {
    }
}

public class RadixTreeTests : PrefixLookupTests<RadixTree<TestRecord>>
{
    public RadixTreeTests() : base(new RadixTree<TestRecord>())
    {
    }
}

public class CompactTrieTests : PrefixLookupTests<CompactTrie<TestRecord>>
{
    public CompactTrieTests() : base(new CompactTrie<TestRecord>())
    {
    }
}