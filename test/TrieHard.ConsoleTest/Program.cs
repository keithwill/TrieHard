﻿// This project is convenient for running VS Performance Profiler tests, but otherwise serves no important purpose.

using TrieHard.Collections;

//var trie = new CompactTrie<string>();
var kvps = new List<KeyValuePair<string, string?>>();


for (int i = 0; i < 1_000; i++)
{
    var key = "/Customer/" + i.ToString() + "/Config/Key/" + i.ToString();

    kvps.Add(new KeyValuePair<string, string?>(key, key));
}

//GC.Collect(2, GCCollectionMode.Forced, true, true);
//Console.WriteLine("Keys Generated");
//System.Threading.Thread.Sleep(2000);

//for (int i = 0; i < 1000; i++)
//{
//    var trie = (RadixTree<string>)RadixTree<string>.Create(kvps);
//}

var trie = (RadixTree<string>)RadixTree<string>.Create(kvps);
var tmp = trie.SearchUtf8("/Customer/12"u8).ToArray();
;
//for (int i = 0; i < 100_000_000; i++)
//{
//    foreach(var kvp in trie.Search("/Customer/1234/"u8))
//    {
//        if (kvp.Value == null)
//        {
//            throw new ArgumentException();
//        }
//    }
//}
//GC.Collect(2, GCCollectionMode.Forced, true, true);
//GC.Collect(2, GCCollectionMode.Forced, true, true);
//Console.WriteLine("Trie Generated");
//Console.ReadKey();
//System.Threading.Thread.Sleep(2000);
//Console.WriteLine(trie);
//Console.WriteLine(kvps.Count.ToString());



