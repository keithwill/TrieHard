using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrieHard.Collections;

namespace TrieHard.Benchmarks
{
    public class Radix : LookupBenchmark<RadixTree<string>>
    {

        private byte[] testPrefixKeyUtf8 = Encoding.UTF8.GetBytes(testPrefixKey);

        public override void Setup()
        {
            base.Setup();
        }

        [Benchmark]
        public string Search_Utf8()
        {
            string result = null;
            foreach (var kvp in lookup.SearchUtf8(testPrefixKeyUtf8.AsSpan()))
            {
                result = kvp.Value;
            }
            return result;
        }

        [Benchmark]
        public string Get_Utf8()
        {
            return lookup.Get(testPrefixKeyUtf8.AsSpan());
        }

        [Benchmark]
        public void Set_Utf8()
        {
            lookup.Set(testPrefixKeyUtf8.AsSpan(), testKey);
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

    }
}
