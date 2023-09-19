// This project is convenient for running VS Performance Profiler tests, but otherwise serves no important purpose.

using TrieHard.Collections;

var trie = new CompactTrie<string>();

for(int i = 0; i < 100; i++)
{
    var key = i.ToString();
    ReadOnlySpan<byte> keySpan = System.Text.Encoding.UTF8.GetBytes(key);
    trie.Set(keySpan, key);
}

var searchKey = "5";
ReadOnlySpan<byte> searchkeySpan = System.Text.Encoding.UTF8.GetBytes(searchKey);

string value = "";
for (int i = 0; i < 10_000_000; i++)
{

    foreach(var kvp in trie.SearchSpans(searchkeySpan))
    {
        value = kvp.Value;
    }
}

if (value == "1234")
{
    Console.WriteLine("");
}
