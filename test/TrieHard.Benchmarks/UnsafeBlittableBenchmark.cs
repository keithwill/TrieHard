using BenchmarkDotNet.Attributes;
using System;
using System.Linq;
using TrieHard.Collections;

namespace TrieHard.Benchmarks
{
    public class UnsafeBlittable
    {
        private UnsafeBlittableTrie<int> _intLookup;
        private UnsafeBlittableTrie<Guid> _guidLookup;

        private static readonly KeyValue<int?>[] SequentialInt;
        private static readonly KeyValue<Guid?>[] SequentialGuid;
        private static readonly KeyValue<int?>[] EnglishWordsInt;
        private static readonly KeyValue<Guid?>[] EnglishWordsGuid;

        static UnsafeBlittable()
        {
            var rng = new Random(42);
            Span<byte> guidBytes = stackalloc byte[16];

            SequentialInt = new KeyValue<int?>[1_000_000];
            SequentialGuid = new KeyValue<Guid?>[1_000_000];
            for (int i = 0; i < 1_000_000; i++)
            {
                var key = i.ToString();
                SequentialInt[i] = new KeyValue<int?>(key, i);
                rng.NextBytes(guidBytes);
                SequentialGuid[i] = new KeyValue<Guid?>(key, new Guid(guidBytes));
            }

            EnglishWordsInt = CommonWords.English
                .Select((word, idx) => new KeyValue<int?>(word, idx))
                .ToArray();
            var guidBytesArray = new byte[16];
            EnglishWordsGuid = new KeyValue<Guid?>[CommonWords.English.Length];
            for (int i = 0; i < CommonWords.English.Length; i++)
            {
                rng.NextBytes(guidBytesArray);
                EnglishWordsGuid[i] = new KeyValue<Guid?>(CommonWords.English[i], new Guid(guidBytesArray));
            }
        }

        [GlobalSetup]
        public void Setup()
        {
            _intLookup = (UnsafeBlittableTrie<int>)UnsafeBlittableTrie<int>.Create<int>(SequentialInt);
            _guidLookup = (UnsafeBlittableTrie<Guid>)UnsafeBlittableTrie<Guid>.Create<Guid>(SequentialGuid);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _intLookup?.Dispose();
            _guidLookup?.Dispose();
        }

        // -------------------------------------------------------------------------
        // Int benchmarks
        // -------------------------------------------------------------------------

        [Benchmark]
        public void Set_Int()
        {
            _intLookup[TestData.Key] = 345678;
        }

        [Benchmark]
        public int? Get_Int()
        {
            return _intLookup[TestData.Key];
        }

        [Benchmark]
        public void Set_Utf8_Int()
        {
            _intLookup.Set(TestData.KeyBytes.Span, 345678);
        }

        [Benchmark]
        public int? Get_Utf8_Int()
        {
            return _intLookup.Get(TestData.KeyBytes.Span);
        }

        [Benchmark]
        public int? SearchKVP_Int()
        {
            int? value = null;
            foreach (var kvp in _intLookup.Search(TestData.Prefix))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public int? Search_Utf8_Int()
        {
            int? value = null;
            foreach (var kvp in _intLookup.Search(TestData.PrefixBytes))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public int? SearchValues_Int()
        {
            int? result = null;
            foreach (var value in _intLookup.SearchValues(TestData.Prefix))
            {
                result = value;
            }
            return result;
        }

        [Benchmark]
        public int? SearchValues_Utf8_Int()
        {
            int? result = null;
            foreach (var value in _intLookup.SearchValues(TestData.PrefixBytes.Span))
            {
                result = value;
            }
            return result;
        }

        [Benchmark]
        public int? LongestPrefix_Int()
        {
            return _intLookup.LongestPrefix("345678x");
        }

        [Benchmark]
        public int? LongestPrefix_Utf8_Int()
        {
            return _intLookup.LongestPrefix(TestData.LongestPrefixKeyBytes.Span);
        }

        [Benchmark]
        public int Count_Int()
        {
            return _intLookup.Count;
        }

        [Benchmark]
        public void Create_Int()
        {
            var trie = (UnsafeBlittableTrie<int>)UnsafeBlittableTrie<int>.Create<int>(EnglishWordsInt);
            trie.Dispose();
        }

        // -------------------------------------------------------------------------
        // Guid benchmarks
        // -------------------------------------------------------------------------

        [Benchmark]
        public void Set_Guid()
        {
            _guidLookup[TestData.Key] = new Guid(345678, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        [Benchmark]
        public Guid? Get_Guid()
        {
            return _guidLookup[TestData.Key];
        }

        [Benchmark]
        public void Set_Utf8_Guid()
        {
            _guidLookup.Set(TestData.KeyBytes.Span, new Guid(345678, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        [Benchmark]
        public Guid? Get_Utf8_Guid()
        {
            return _guidLookup.Get(TestData.KeyBytes.Span);
        }

        [Benchmark]
        public Guid? SearchKVP_Guid()
        {
            Guid? value = null;
            foreach (var kvp in _guidLookup.Search(TestData.Prefix))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public Guid? Search_Utf8_Guid()
        {
            Guid? value = null;
            foreach (var kvp in _guidLookup.Search(TestData.PrefixBytes))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public Guid? SearchValues_Guid()
        {
            Guid? result = null;
            foreach (var value in _guidLookup.SearchValues(TestData.Prefix))
            {
                result = value;
            }
            return result;
        }

        [Benchmark]
        public Guid? SearchValues_Utf8_Guid()
        {
            Guid? result = null;
            foreach (var value in _guidLookup.SearchValues(TestData.PrefixBytes.Span))
            {
                result = value;
            }
            return result;
        }

        [Benchmark]
        public Guid? LongestPrefix_Guid()
        {
            return _guidLookup.LongestPrefix("345678x");
        }

        [Benchmark]
        public Guid? LongestPrefix_Utf8_Guid()
        {
            return _guidLookup.LongestPrefix(TestData.LongestPrefixKeyBytes.Span);
        }

        [Benchmark]
        public int Count_Guid()
        {
            return _guidLookup.Count;
        }

        [Benchmark]
        public void Create_Guid()
        {
            var trie = (UnsafeBlittableTrie<Guid>)UnsafeBlittableTrie<Guid>.Create<Guid>(EnglishWordsGuid);
            trie.Dispose();
        }
    }
}
