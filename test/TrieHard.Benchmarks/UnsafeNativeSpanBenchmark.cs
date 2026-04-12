using BenchmarkDotNet.Attributes;
using System;
using System.Linq;
using System.Text;
using TrieHard.Collections;

namespace TrieHard.Benchmarks
{
    public class UnsafeNativeSpan
    {
        private UnsafeNativeSpanTrie _lookup;

        // Sequential keys with their UTF-8 byte representation as the stored value.
        private static readonly (string Key, byte[] Value)[] SequentialEntries;
        private static readonly (string Key, byte[] Value)[] EnglishWordEntries;

        static UnsafeNativeSpan()
        {
            SequentialEntries = new (string, byte[])[1_000_000];
            for (int i = 0; i < 1_000_000; i++)
            {
                var key = i.ToString();
                SequentialEntries[i] = (key, Encoding.UTF8.GetBytes(key));
            }

            EnglishWordEntries = CommonWords.English
                .Select(word => (word, Encoding.UTF8.GetBytes(word)))
                .ToArray();
        }

        [GlobalSetup]
        public void Setup()
        {
            _lookup = new UnsafeNativeSpanTrie();
            foreach (var (key, value) in SequentialEntries)
                _lookup.Set(key, (ReadOnlySpan<byte>)value);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _lookup?.Dispose();
        }

        [Benchmark]
        public void Set()
        {
            _lookup.Set(TestData.Key, (ReadOnlySpan<byte>)SequentialEntries[345678].Value);
        }

        [Benchmark]
        public void Set_Utf8()
        {
            _lookup.Set(TestData.KeyBytes.Span, (ReadOnlySpan<byte>)SequentialEntries[345678].Value);
        }

        [Benchmark]
        public NativeByteSpan? Get()
        {
            return _lookup.Get(TestData.Key);
        }

        [Benchmark]
        public NativeByteSpan? Get_Utf8()
        {
            return _lookup.Get(TestData.KeyBytes.Span);
        }


        [Benchmark]
        public NativeByteSpan? SearchKVP()
        {
            NativeByteSpan? value = null;
            foreach (var kvp in _lookup.Search(TestData.Prefix))
                value = kvp.Value;
            return value;
        }

        [Benchmark]
        public NativeByteSpan? Search_Utf8()
        {
            NativeByteSpan? value = null;
            foreach (var kvp in _lookup.Search(TestData.PrefixBytes))
                value = kvp.Value;
            return value;
        }

        [Benchmark]
        public NativeByteSpan? SearchValues()
        {
            NativeByteSpan? result = null;
            foreach (var value in _lookup.SearchValues(TestData.Prefix))
                result = value;
            return result;
        }

        [Benchmark]
        public NativeByteSpan? SearchValues_Utf8()
        {
            NativeByteSpan? result = null;
            foreach (var value in _lookup.SearchValues(TestData.PrefixBytes.Span))
                result = value;
            return result;
        }

        [Benchmark]
        public NativeByteSpan? LongestPrefix()
        {
            return _lookup.LongestPrefix("345678x");
        }

        [Benchmark]
        public NativeByteSpan? LongestPrefix_Utf8()
        {
            return _lookup.LongestPrefix(TestData.LongestPrefixKeyBytes.Span);
        }

        [Benchmark]
        public int Count()
        {
            return _lookup.Count;
        }

        [Benchmark]
        public void Create()
        {
            var trie = new UnsafeNativeSpanTrie();
            foreach (var (key, value) in EnglishWordEntries)
                trie.Set(key, (ReadOnlySpan<byte>)value);
            trie.Dispose();
        }
    }
}
