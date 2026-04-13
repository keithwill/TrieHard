using BenchmarkDotNet.Running;

namespace TrieHard.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var benchmarkConfig = new BenchmarkConfig();
            var summary = BenchmarkRunner.Run(
            [
                typeof(Unsafe),
                typeof(UnsafeBlittable),
                typeof(UnsafeNativeSpan),
                typeof(Simple),
                typeof(PrefixLookup),
                typeof(NaiveList),
                typeof(SQLite),
            ], args: args, config: benchmarkConfig);

        }
    }
}