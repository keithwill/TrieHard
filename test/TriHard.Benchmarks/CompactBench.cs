using BenchmarkDotNet.Attributes;
using System;
using System.Text;
using TrieHard.Collections;

namespace TriHard.Benchmarks
{

    public class Compact : LookupBenchmark<CompactTrie<string>>
    {

        private ReadOnlyMemory<byte> testKeyUtf8 = Encoding.UTF8.GetBytes(testKey);
        private ReadOnlyMemory<byte> testPrefixKeyUtf8 = Encoding.UTF8.GetBytes(testPrefixKey);

        [Benchmark]
        public string Get_Utf8()
        {
            return lookup.Get(testKeyUtf8.Span);
        }

        [Benchmark]
        public string Search_Utf8()
        {
            string value = null;
            foreach (var kvp in lookup.SearchUtf8(testPrefixKeyUtf8))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public string SearchValues_Utf8()
        {
            string result = null;
            foreach (var value in lookup.SearchValues(testPrefixKeyUtf8.Span))
            {
                result = value;
            }
            return result;
        }

    }
}
