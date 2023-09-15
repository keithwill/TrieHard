using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Running;
using System;
using System.Reflection;
using TrieHard.Alternatives.ExternalLibraries.rm.Trie;
using TrieHard.Alternatives.List;
using TrieHard.Alternatives.SQLite;
using TrieHard.Collections;

namespace TriHard.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var benchmarkConfig = new BenchmarkConfig();
            var summary = BenchmarkRunner.Run(new Type[]
            {
                typeof(CompactBench<CompactTrie<string>>),
                typeof(PrefixLookupBench<IndirectTrie<string>>),
                typeof(PrefixLookupBench<RadixTree<string>>),
                typeof(PrefixLookupBench<SimpleTrie<string>>),
                typeof(PrefixLookupBench<SQLiteLookup<string>>),
                typeof(PrefixLookupBench<ListPrefixLookup<string>>),
                typeof(PrefixLookupBench<rmTrie<string>>),
            }, args: args, config: benchmarkConfig);

        }
    }
}