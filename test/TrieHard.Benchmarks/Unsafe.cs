using BenchmarkDotNet.Attributes;
using System;
using System.Text;
using TrieHard.Collections;

namespace TrieHard.Benchmarks
{

    public class Unsafe : LookupBenchmark<UnsafeTrie<string>>
    {

        private ReadOnlyMemory<byte> testKeyUtf8 = Encoding.UTF8.GetBytes(testKey);

        private byte[] testPrefixKeyUtf8 = Encoding.UTF8.GetBytes(testPrefixKey);

        public override void Setup()
        {
            base.Setup();
        }

        [Benchmark]
        public string Get_Utf8()
        {
            return lookup.Get(testKeyUtf8.Span);
        }

        [Benchmark]
        public void Set_Utf8()
        {
            lookup.Set(testPrefixKeyUtf8.AsSpan(), testKey);
        }

        [Benchmark]
        public string Search_Utf8()
        {
            string result = null;
            foreach (var kvp in lookup.Search(testPrefixKeyUtf8.AsMemory()))
            {
                result = kvp.Value;
            }
            return result;
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
        public override string SearchValues()
        {
            string result = null;
            foreach (var value in lookup.SearchValues(testPrefixKey))
            {
                result = value;
            }
            return result;
        }

        [Benchmark]
        public override string SearchKVP()
        {
            string value = null;
            foreach (var kvp in lookup.Search(testPrefixKey))
            {
                value = kvp.Value;
            }
            return value;
        }


    }
}
