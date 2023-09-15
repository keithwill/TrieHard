using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using Perfolizer.Horology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Diagnosers;

namespace TriHard.Benchmarks
{
    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            
            AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(EventPipeProfiler.Default)
            .Add(
                DefaultConfig.Instance
                .WithOptions(ConfigOptions.JoinSummary)
                .WithOptions(ConfigOptions.DisableLogFile)
                .AddJob(
                        Job.Default
                    .WithMinWarmupCount(1)
                    .WithMinIterationCount(1)
                    .WithMaxRelativeError(0.08)
                )
            );


        }
    }
}
