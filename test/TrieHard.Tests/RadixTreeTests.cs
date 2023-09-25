using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrieHard.Collections;

namespace TrieHard.Tests
{
    public class RadixTreeTests : PrefixLookupTests<RadixTree<TestRecord?>>
    {
        [Test]
        public void SearchUtf8Works()
        {
            Assume.That(CreateWithValues, Throws.Nothing);
            var testKeyValues = GetTestRecords(1000);
            var lookup = (RadixTree<TestRecord?>)RadixTree<TestRecord?>.Create(testKeyValues!);

            var searchResults = lookup.SearchUtf8("1"u8).ToArray();
            ;

        }

    }
}
