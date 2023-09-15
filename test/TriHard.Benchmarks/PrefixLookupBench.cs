using BenchmarkDotNet.Attributes;
using System;
using TrieHard.Collections.Contributions;

namespace TriHard.Benchmarks
{

    public class PrefixLookupBench<T> where T : IPrefixLookup<string, string>
    {
        protected T lookup;
        protected const string testKey = "555555";
        protected const string testPrefixKey = "55555";

        [GlobalSetup]
        public virtual void Setup()
        {
            lookup = (T)T.Create(PrefixLookupTestValues.SequentialStrings);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (lookup is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        [Benchmark]
        public void Set()
        {
            lookup[testKey] = testKey;
        }

        [Benchmark]
        public string Get()
        {
            return lookup[testKey];
        }

        [Benchmark]
        public string Search()
        {
            string value = null;
            foreach (var kvp in lookup.Search(testPrefixKey))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public virtual string SearchValues()
        {
            string result = null;
            foreach (var value in lookup.SearchValues(testPrefixKey))
            {
                result = value;
            }
            return result;
        }

        [Benchmark]
        public int Count()
        {
            return lookup.Count;
        }

        [Benchmark]
        public int Create()
        {
            var lookup = (T)T.Create(PrefixLookupTestValues.EnglishWords);
            int count = lookup.Count;
            if (lookup is IDisposable disposable)
            {
                disposable.Dispose();
            }
            return count;
        }


    }
}
