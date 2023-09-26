// This project is convenient for running VS Performance Profiler tests, but otherwise serves no important purpose.

using System.Buffers;
using TrieHard.Alternatives.ExternalLibraries.rm.Trie;
using TrieHard.Alternatives.List;
using TrieHard.Alternatives.SQLite;
using TrieHard.Collections;

var kvps = new List<KeyValuePair<string, string?>>();

for (int i = 0; i < 1_000_000; i++)
{
    //var key = i.ToString();
    var key = $"/customer/{i}/entity/{1_000_000 - i}/";
    kvps.Add(new KeyValuePair<string, string?>(key, key));
}

var trie = (FlatTrie<string>)FlatTrie<string>.Create(kvps);

//for (int i = 0; i < 10; i++)
//{
//    foreach (var kvp in trie.Search("12345"))
//    {
//        if (kvp.Value == null)
//        {
//            throw new ArgumentException();
//        }
//    }
//}

GC.Collect(2, GCCollectionMode.Forced, true, true);
System.Threading.Thread.Sleep(5000);
var workingMb = Environment.WorkingSet / 1_000_000.00;
Console.WriteLine("Working Set: " + workingMb.ToString());
Console.WriteLine(trie);

Console.ReadLine();

foreach(var item in kvps)
{
    if (item.Value is null)
    {
        throw new ArgumentException();
    }
}
foreach (var kvp in trie.Search("12345"))
{
    if (kvp.Value == null)
    {
        throw new ArgumentException();
    }
}

