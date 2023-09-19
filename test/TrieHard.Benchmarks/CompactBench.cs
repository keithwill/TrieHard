using BenchmarkDotNet.Attributes;
using System;
using System.Text;
using TrieHard.Collections;

namespace TrieHard.Benchmarks
{

    public class Compact : LookupBenchmark<CompactTrie<string>>
    {

        private ReadOnlyMemory<byte> testKeyUtf8 = Encoding.UTF8.GetBytes(testKey);
        //private ReadOnlyMemory<byte> testPrefixKeyUtf8 = Encoding.UTF8.GetBytes(testPrefixKey);

        private byte[] testPrefixKeyUtf8 = Encoding.UTF8.GetBytes(testPrefixKey);

        [Benchmark]
        public string Get_Utf8()
        {
            return lookup.Get(testKeyUtf8.Span);
        }

        [Benchmark]
        public string Search_Utf8()
        {
            string value = null;
            using var searchResult = lookup.SearchUtf8(testPrefixKeyUtf8);
            foreach (var kvp in searchResult)
            {
                value = kvp.Value;
            }
            searchResult.Dispose();
            return value;
        }

        [Benchmark]
        public string SearchValues_Utf8()
        {
            string result = null;
            foreach (var value in lookup.SearchValues(testPrefixKeyUtf8.AsSpan()))
            {
                result = value;
            }
            return result;
        }

        [Benchmark]
        public int SearchValues_Utf8Experiment()
        {
            int keyLength = 0;
            foreach(var kvp in lookup.SearchSpans(testPrefixKeyUtf8.AsSpan()))
            {
                keyLength = kvp.Key.Length;
            }

            return keyLength;
        }

    }
}
