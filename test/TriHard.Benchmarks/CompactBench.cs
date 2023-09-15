using BenchmarkDotNet.Attributes;
using System;
using System.Text;
using TrieHard.Collections;
using TrieHard.Collections.Contributions;

namespace TriHard.Benchmarks
{

    public class CompactBench<T> : PrefixLookupBench<T> where T : IPrefixLookup<string, string>
    {

        private ReadOnlyMemory<byte> testKeyUtf8 = Encoding.UTF8.GetBytes(testKey);
        private ReadOnlyMemory<byte> testPrefixKeyUtf8 = Encoding.UTF8.GetBytes(testPrefixKey);
        private CompactTrie<string> compactLookup;

        public override void Setup()
        {
            base.Setup();
            compactLookup = lookup as CompactTrie<string>;
        }

        [Benchmark]
        public string Get_Utf8()
        {
            return compactLookup.Get(testKeyUtf8.Span);
        }

        [Benchmark]
        public string Search_Utf8()
        {
            string value = null;
            foreach (var kvp in compactLookup.SearchUtf8(testPrefixKeyUtf8))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public string SearchValues_Utf8()
        {
            string result = null;
            foreach (var value in compactLookup.SearchValues(testPrefixKeyUtf8.Span))
            {
                result = value;
            }
            return result;
        }

    }
}
