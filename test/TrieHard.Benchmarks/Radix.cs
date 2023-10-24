using BenchmarkDotNet.Attributes;
using System;
using TrieHard.PrefixLookup;

namespace TrieHard.Benchmarks
{
    public class Radix : LookupBenchmark<RadixTree<string>>
    {

        public override void Setup()
        {
            lookup = (RadixTree<string>)RadixTree<string>.Create(TestData.Sequential);
        }

        [Benchmark]
        public void SearchKVP_Utf8()
        {
            var searchResult = lookup.Search(TestData.PrefixBytes.Span);
            foreach (var kvp in searchResult)
            {
                if (kvp.Value is null) throw new Exception();
            }
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
        public override string Get()
        {
            return lookup[TestData.Key];
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
        public override void Set()
        {
            lookup[TestData.Key] = TestData.Key;
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
            foreach (var value in lookup.SearchValues(TestData.Prefix))
            {
                result = value;
            }
            return result;
        }



    }
}
