using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Running;
using System;
using System.Reflection;
using TrieHard.Alternatives.ExternalLibraries.rm.Trie;
using TrieHard.Alternatives.List;
using TrieHard.Alternatives.SQLite;
using TrieHard.Collections;

namespace TrieHard.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var benchmarkConfig = new BenchmarkConfig();
            var summary = BenchmarkRunner.Run(new Type[]
            {
                typeof(Compact),
                typeof(Simple),
                typeof(Radix),
                typeof(NaiveList),
                typeof(Indirect),
                typeof(SQLite),
                typeof(rmTrie),
                typeof(Flat)
            }, args: args, config: benchmarkConfig);

        }
    }
}