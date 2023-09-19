using TrieHard.Collections;

namespace TrieHard.Tests;

public class CompactTrieTests : PrefixLookupTests<CompactTrie<TestRecord?>>
{

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(1000)]
    public void SearchSpans_SequentialKeys_ResultsAreAccurate(int valuesToAdd)
    {
        Assume.That(CreateWithValues, Throws.Nothing);
        var testKeyValues = GetTestRecords(valuesToAdd);
        var lookup = (CompactTrie<TestRecord?>)CompactTrie<TestRecord?>.Create(testKeyValues!);

        var prefix = "1";
        Span<byte> utf8Prefix = System.Text.Encoding.UTF8.GetBytes(prefix).AsSpan();
        List<KeyValuePair<string, TestRecord?>> actualResultBuilder = new();

        foreach (var kvp in lookup.SearchSpans(utf8Prefix))
        {
            var key = System.Text.Encoding.UTF8.GetString(kvp.Key);
            actualResultBuilder.Add(new KeyValuePair<string, TestRecord?>(key, kvp.Value));
        }

        var actualResults = lookup.Search(prefix).ToArray();

        var expected = testKeyValues.Where(x => x.Key.StartsWith(prefix))
            .OrderBy(x => x.Key).ToArray();

        Assert.That(actualResults, Is.EquivalentTo(expected));
    }


}