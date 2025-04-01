using NUnit.Framework.Internal;
using System.Text;
using TrieHard.Alternatives.List;
using TrieHard.Alternatives.SQLite;
using TrieHard.Collections;
using TrieHard.PrefixLookup;

namespace TrieHard.Tests;

public record class TestRecord(string Key);

public abstract class PrefixLookupTests<T> where T : IPrefixLookup<TestRecord?>
{
    protected TestRecord TestRecord { get; init; }
    private KeyValue<TestRecord> testKvp;
    private KeyValue<TestRecord>[] testKvpEnumerable;
    const string TestKey = "TestKey";
    const string TestKeyPrefix = "Test";
    private KeyValue<TestRecord>[] TestRecords;

    public PrefixLookupTests()
    {
        TestRecords = GetTestRecords(1000);
        TestRecord = TestRecords[0].Value!;
        testKvp = new KeyValue<TestRecord>(TestKey, TestRecord);
        testKvpEnumerable = [testKvp];
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

        var expectedResults = testKeyValues.Where(x => x.Key.StartsWith(prefix))
            .OrderBy(x => x.Key).ToArray();

        for(int i = 0; i < actualResults.Length; i++)
        {
            var actual = actualResults[i];
            var expected = expectedResults[i];
            Assert.That(actual.Key, Is.EqualTo(expected.Key));
            Assert.That(actual.Value, Is.SameAs(expected.Value));
        }
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
            Assert.That(expectedResult!.Key, Is.EqualTo(actualResult!.Key));
        }
    }


    protected KeyValue<TestRecord>[] GetTestRecords(int count)
    {
        List<KeyValue<TestRecord>> testKeyValues = new();
        for (int i = 0; i < count; i++)
        {
            var key = i.ToString();
            var record = new TestRecord(key);
            testKeyValues.Add(new KeyValue<TestRecord>(key, record));
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
public class SqliteLookupTests : PrefixLookupTests<SQLiteLookup<TestRecord?>> { }
public class ListPrefixLookupTests : PrefixLookupTests<ListPrefixLookup<TestRecord?>> { }
public class PrefixLookupTests : PrefixLookupTests<PrefixLookup<TestRecord?>> 
{

    [Test]
    public void CanRoundTripAlphaNumericKeys()
    {
        Assume.That(Add, Throws.Nothing);
        var lookup = PrefixLookup<TestRecord?>.Create<TestRecord>();
        foreach (var key in AlphaNumericKeys)
        {
            lookup[key] = TestRecord;
        }
        var expectedKeys = AlphaNumericKeys;
        var actualKeys = lookup.Search(string.Empty).Select(x => x.Key).ToArray();

        CollectionAssert.AreEqual(expectedKeys, actualKeys);
    }

    private static string[] AlphaNumericKeys = CreateAlphaNumericKeys();
    private static string[] CreateAlphaNumericKeys()
    {
        var keys = new List<string>();
        byte[] keyBytes = new byte[] { 0 };
        char[] keyChars = new char[] { ' ' };

        for (int i = byte.MinValue; i <= byte.MaxValue; i++)
        {
            keyBytes[0] = (byte)i;
            if (Encoding.ASCII.TryGetChars(keyBytes, keyChars, out var charCount))
            {
                if (charCount == 1)
                {
                    var keyCharacter = keyChars[0];
                    if (char.IsLetter(keyCharacter) || char.IsDigit(keyCharacter))
                    {
                        keys.Add(keyCharacter.ToString());
                    }
                }
            }
        }
        return keys.OrderBy(x => (int)x[0]).ToArray();
    }

}
public class UnsafeTrieTests : PrefixLookupTests<UnsafeTrie<TestRecord?>> { }