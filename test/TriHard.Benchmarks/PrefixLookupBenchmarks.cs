using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using TrieHard.Collections;
using TrieHard.Collections.Contributions;

namespace TriHard.Benchmarks
{

    [MemoryDiagnoser]
    public class PrefixLookupBenchmarks
    {

        private CompactTrie<string> compactTrie;
        private IndirectTrie<string> indirectTrie;
        private RadixTree<string> radixTree;
        private SimpleTrie<string> simpleTrie;
        private IPrefixLookup<string, string>[] tries;

        private const string testKey = "23456";
        private const string testPrefixKey = "2345";
        private ReadOnlyMemory<byte> testKeyUtf8 = System.Text.Encoding.UTF8.GetBytes("23456");
        private ReadOnlyMemory<byte> testPrefixKeyUtf8 = System.Text.Encoding.UTF8.GetBytes("23456");

        [GlobalSetup]
        public void Setup()
        {
            compactTrie = (CompactTrie<string>)CompactTrie<string>.Create(PrefixLookupTestValues.SequentialStrings);
            indirectTrie = (IndirectTrie<string>)IndirectTrie<string>.Create(PrefixLookupTestValues.SequentialStrings);
            radixTree = (RadixTree<string>)RadixTree<string>.Create(PrefixLookupTestValues.SequentialStrings);
            simpleTrie = (SimpleTrie<string>)SimpleTrie<string>.Create(PrefixLookupTestValues.SequentialStrings);
        }

        [Benchmark]
        public void Set_Compact()
        {
            compactTrie[testKey] = testKey;
        }

        [Benchmark]
        public void Set_Compact_Utf8()
        {
            compactTrie.Set(testKeyUtf8.Span, testKey);
        }

        [Benchmark]
        public void Set_Indirect()
        {
            indirectTrie[testKey] = testKey;
        }

        [Benchmark]
        public void Set_Radix()
        {
            radixTree[testKey] = testKey;
        }

        [Benchmark]
        public void Set_SimpleTrie()
        {
            simpleTrie[testKey] = testKey;
        }

        [Benchmark]
        public string Get_Compact()
        {
            return compactTrie[testKey];
        }

        [Benchmark]
        public string Get_Compact_Utf8()
        {
            return compactTrie.Get(testKeyUtf8.Span);
        }

        [Benchmark]
        public string Get_Indirect()
        {
            return indirectTrie[testKey];
        }

        [Benchmark]
        public string Get_Radix()
        {
            return radixTree[testKey];
        }

        [Benchmark]
        public string Get_Simple()
        {
            return simpleTrie[testKey];
        }

        [Benchmark]
        public string Search_Compact()
        {
            string value = null;
            foreach(var kvp in compactTrie.Search(testPrefixKey))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public string Search_Compact_Utf8()
        {
            string value = null;
            foreach (var kvp in compactTrie.SearchUtf8(testPrefixKeyUtf8))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public string Search_Indirect()
        {
            string value = null;
            foreach (var kvp in indirectTrie.Search(testPrefixKey))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public string Search_Radix()
        {
            string value = null;
            foreach (var kvp in radixTree.Search(testPrefixKey))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public string Search_SimpleTrie()
        {
            string value = null;
            foreach (var kvp in simpleTrie.Search(testPrefixKey))
            {
                value = kvp.Value;
            }
            return value;
        }

    }
}
