// This project is convenient for running VS Performance Profiler tests, but otherwise serves no important purpose.

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
System.Threading.Thread.Sleep(2000);

var trie = (FlatTrie<string>)FlatTrie<string>.Create(kvps);
for (int i = 0; i < 100_000; i++)
{
    foreach(var kvp in trie.Search("12"))
    {
        if (kvp.Value == null)
        {
            throw new ArgumentException();
        }
    }
}
GC.Collect(2, GCCollectionMode.Forced, true, true);
//GC.Collect(2, GCCollectionMode.Forced, true, true);
//Console.WriteLine("Trie Generated");
//Console.ReadKey();
System.Threading.Thread.Sleep(2000);
Console.WriteLine(trie);
Console.WriteLine(kvps.Count.ToString());



