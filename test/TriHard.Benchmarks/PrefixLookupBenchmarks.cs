using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.Diagnostics.Tracing.Parsers.IIS_Trace;
using TrieHard.Alternatives.List;
using TrieHard.Alternatives.SQLite;
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
        private SQLiteLookup<string> sqliteLookup;
        private ListPrefixLookup<string> listLookup;

        private const string testKey = "555555";
        private const string testPrefixKey = "55555";
        private ReadOnlyMemory<byte> testKeyUtf8 = System.Text.Encoding.UTF8.GetBytes("555555");
        private ReadOnlyMemory<byte> testPrefixKeyUtf8 = System.Text.Encoding.UTF8.GetBytes("55555");

        [GlobalSetup]
        public void Setup()
        {
            compactTrie = (CompactTrie<string>)CompactTrie<string>.Create(PrefixLookupTestValues.SequentialStrings);
            indirectTrie = (IndirectTrie<string>)IndirectTrie<string>.Create(PrefixLookupTestValues.SequentialStrings);
            radixTree = (RadixTree<string>)RadixTree<string>.Create(PrefixLookupTestValues.SequentialStrings);
            simpleTrie = (SimpleTrie<string>)SimpleTrie<string>.Create(PrefixLookupTestValues.SequentialStrings);
            sqliteLookup = (SQLiteLookup<string>)SQLiteLookup<string>.Create(PrefixLookupTestValues.SequentialStrings);
            listLookup = (ListPrefixLookup<string>)ListPrefixLookup<string>.Create(PrefixLookupTestValues.SequentialStrings);
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
        public void Set_List()
        {
            listLookup[testKey] = testKey;
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
        public string Get_Sqlite()
        {
            return sqliteLookup[testKey];
        }

        [Benchmark]
        public string Get_List()
        {
            return listLookup[testKey];
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

        [Benchmark]
        public string Search_Sqlite()
        {
            string value = null;
            foreach (var kvp in sqliteLookup.Search(testPrefixKey))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public string Search_List()
        {
            string value = null;
            foreach (var kvp in listLookup.Search(testPrefixKey))
            {
                value = kvp.Value;
            }
            return value;
        }

        [Benchmark]
        public string SearchValues_Compact()
        {
            string resultValue = null;
            foreach (var value in compactTrie.SearchValues(testPrefixKey))
            {
                resultValue = value;
            }
            return resultValue;
        }


        [Benchmark]
        public string SearchValues_Indirect()
        {
            string resultValue = null;
            foreach (var value in indirectTrie.SearchValues(testPrefixKey))
            {
                resultValue = value;
            }
            return resultValue;
        }

        [Benchmark]
        public string SearchValues_Radix()
        {
            string resultValue = null;
            foreach (var value in radixTree.SearchValues(testPrefixKey))
            {
                resultValue = value;
            }
            return resultValue;
        }

        [Benchmark]
        public string SearchValues_Simple()
        {
            string resultValue = null;
            foreach (var value in simpleTrie.SearchValues(testPrefixKey))
            {
                resultValue = value;
            }
            return resultValue;
        }

        [Benchmark]
        public string SearchValues_Sqlite()
        {
            string resultValue = null;
            foreach (var value in sqliteLookup.SearchValues(testPrefixKey))
            {
                resultValue = value;
            }
            return resultValue;
        }

        [Benchmark]
        public string SearchValues_List()
        {
            string resultValue = null;
            foreach (var value in listLookup.SearchValues(testPrefixKey))
            {
                resultValue = value;
            }
            return resultValue;
        }

        [Benchmark]
        public int Create_Compact()
        {
            using var trie = (CompactTrie<string>)CompactTrie<string>.Create(PrefixLookupTestValues.EnglishWords);
            return trie.Count;
        }

        [Benchmark]
        public int Create_Indirect()
        {
            var indirectTrie = (IndirectTrie<string>)IndirectTrie<string>.Create(PrefixLookupTestValues.EnglishWords);
            return indirectTrie.Count;
        }

        [Benchmark]
        public int Create_Radix()
        {
            var radixTree = (RadixTree<string>)RadixTree<string>.Create(PrefixLookupTestValues.EnglishWords);
            return radixTree.Count;
        }

        [Benchmark]
        public int Create_Simple()
        {
            var simpleTrie = (SimpleTrie<string>)SimpleTrie<string>.Create(PrefixLookupTestValues.EnglishWords);
            return simpleTrie.Count;
        }

        [Benchmark]
        public int Create_Sqlite()
        {
            var lookup = (SQLiteLookup<string>)SQLiteLookup<string>.Create(PrefixLookupTestValues.EnglishWords);
            return lookup.Count;
        }

        [Benchmark]
        public int Create_List()
        {
            var lookup = (ListPrefixLookup<string>)ListPrefixLookup<string>.Create(PrefixLookupTestValues.EnglishWords);
            return lookup.Count;
        }

    }
}
