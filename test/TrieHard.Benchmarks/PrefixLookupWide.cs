using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using TrieHard.PrefixLookup;

namespace TrieHard.Benchmarks
{
    /// <summary>
    /// Exercises child search at wide fan-outs: every internal node has exactly 64
    /// children (3-byte keys over a 64-value alphabet). The standard suite's
    /// sequential numeric keys never produce more than 10 children per node, so it
    /// can't show how child search scales with node width. Lookups use a shuffled
    /// batch of keys so branch prediction can't memorize a single search path.
    /// </summary>
    public class PrefixLookupWide
    {
        private const int FanOut = 64;
        private const int LookupCount = 4096;
        private PrefixLookup<string> lookup;
        private byte[][] keys;

        [GlobalSetup]
        public void Setup()
        {
            lookup = new PrefixLookup<string>();
            var allKeys = new List<byte[]>(FanOut * FanOut * FanOut);
            for (int a = 0; a < FanOut; a++)
                for (int b = 0; b < FanOut; b++)
                    for (int c = 0; c < FanOut; c++)
                    {
                        var key = new byte[] { (byte)('0' + a), (byte)('0' + b), (byte)('0' + c) };
                        allKeys.Add(key);
                        lookup.Set(key, "value");
                    }

            var random = new Random(8675309);
            keys = new byte[LookupCount][];
            for (int i = 0; i < LookupCount; i++)
            {
                keys[i] = allKeys[random.Next(allKeys.Count)];
            }
        }

        [Benchmark(OperationsPerInvoke = LookupCount)]
        public string Get_Utf8_WideFanOut()
        {
            string result = null;
            var keysLocal = keys;
            var lookupLocal = lookup;
            for (int i = 0; i < keysLocal.Length; i++)
            {
                result = lookupLocal.Get(keysLocal[i]);
            }
            return result;
        }
    }
}
