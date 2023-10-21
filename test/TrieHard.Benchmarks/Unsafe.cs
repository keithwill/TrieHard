using BenchmarkDotNet.Attributes;
using System;
using System.Text;
using TrieHard.Collections;

namespace TrieHard.Benchmarks
{

    public class Unsafe : LookupBenchmark<UnsafeTrie<string>>
    {

        public override void Setup()
        {
            lookup = (UnsafeTrie<string>)UnsafeTrie<string>.Create(TestData.Sequential);
        }

        [Benchmark]
        public string Get_Utf8()
        {
            return lookup.Get(TestData.KeyBytes.Span);
        }

        [Benchmark]
        public void Set_Utf8()
        {
            lookup.Set(TestData.KeyBytes.Span, TestData.Key);
        }

        [Benchmark]
        public string Search_Utf8()
        {
            string result = null;
            foreach (var kvp in lookup.Search(TestData.PrefixBytes))
            {
                result = kvp.Value;
            }
            return result;
        }

        [Benchmark]
        public override string SearchKVP()
        {
            string value = null;
            foreach (var kvp in lookup.Search(TestData.Prefix))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public string SearchValues_Utf8()
        {
            string result = null;
            foreach (var value in lookup.SearchValues(TestData.PrefixBytes.Span))
            {
                result = value;
            }
            return result;
        }

        [Benchmark]
        public override string SearchValues()
        {
            string result = null;
            foreach (var value in lookup.SearchValues(TestData.Key))
            {
                result = value;
            }
            return result;
        }




    }
}
