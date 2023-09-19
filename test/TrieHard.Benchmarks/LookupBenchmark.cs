using BenchmarkDotNet.Attributes;
using System;
using TrieHard.Alternatives.ExternalLibraries.rm.Trie;
using TrieHard.Alternatives.List;
using TrieHard.Alternatives.SQLite;
using TrieHard.Collections;
using TrieHard.Abstractions;

namespace TrieHard.Benchmarks
{

    public class Simple : LookupBenchmark<SimpleTrie<string>> { }
    public class Indirect : LookupBenchmark<IndirectTrie<string>> { }
    public class Radix : LookupBenchmark<RadixTree<string>> { }
    public class SQLite : LookupBenchmark<SQLiteLookup<string>> { }
    public class rmTrie : LookupBenchmark<rmTrie<string>> { }
    public class NaiveList : LookupBenchmark<ListPrefixLookup<string>> { }

    public abstract class LookupBenchmark<T> where T : IPrefixLookup<string, string>
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
