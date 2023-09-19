// This project is convenient for running VS Performance Profiler tests, but otherwise serves no important purpose.

using TrieHard.Alternatives.ExternalLibraries.rm.Trie;
using TrieHard.Alternatives.List;
using TrieHard.Alternatives.SQLite;
using TrieHard.Collections;

//var trie = new CompactTrie<string>();
var kvps = new List<KeyValuePair<string, string?>>();

for(int i = 0; i < 1_000; i++)
{
    var key = i.ToString();

    kvps.Add(new KeyValuePair<string, string?>(key, key));
}
GC.Collect(2, GCCollectionMode.Forced, true, true);
//Console.WriteLine("Keys Generated");
//Console.ReadKey();

var trie = (CompactTrie<string>)CompactTrie<string>.Create(kvps);
for (int i = 0; i < 10000; i++)
{
    //trie.SearchUtf8();
}
GC.Collect(2, GCCollectionMode.Forced, true, true);
//GC.Collect(2, GCCollectionMode.Forced, true, true);
//Console.WriteLine("Trie Generated");
//Console.ReadKey();
System.Threading.Thread.Sleep(12000);
Console.WriteLine(trie);
Console.WriteLine(kvps.Count.ToString());



//var searchKey = "5";
//ReadOnlySpan<byte> searchkeySpan = System.Text.Encoding.UTF8.GetBytes(searchKey);

//string value = "";
//for (int i = 0; i < 10_000_000; i++)
//{

//    foreach(var kvp in trie.SearchSpans(searchkeySpan))
//    {
//        value = kvp.Value;
//    }
//}

//if (value == "1234")
//{
//    Console.WriteLine("");
//}
