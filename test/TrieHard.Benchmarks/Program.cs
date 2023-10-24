using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Running;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
                typeof(Unsafe),
                typeof(Simple),
                typeof(PrefixLookup),
                typeof(NaiveList),
                typeof(SQLite),
            }, args: args, config: benchmarkConfig);

        }
    }
}