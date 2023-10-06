using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Diagnostics.Windows;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace TrieHard.Benchmarks
{
    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            Add(
                DefaultConfig.Instance
                .AddDiagnoser(MemoryDiagnoser.Default)
                //.AddDiagnoser(EventPipeProfiler.Default)
                .WithOptions(ConfigOptions.JoinSummary)
                .WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest, MethodOrderPolicy.Alphabetical))
                .WithOptions(ConfigOptions.DisableLogFile)
                .AddJob(Job.Default));

        }

    }
}
