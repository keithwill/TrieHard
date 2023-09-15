using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Order;

namespace TriHard.Benchmarks
{
    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            
            //.AddDiagnoser(EventPipeProfiler.Default)
            Add(
                DefaultConfig.Instance
                .AddDiagnoser(MemoryDiagnoser.Default)
                .WithOptions(ConfigOptions.JoinSummary)
                .WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest, MethodOrderPolicy.Alphabetical))
                .WithOptions(ConfigOptions.DisableLogFile)
                .AddJob(Job.Default
                    .WithMinWarmupCount(1)
                    .WithMinIterationCount(1)
                    .WithMaxRelativeError(0.08)
                ));

        }

    }
}
