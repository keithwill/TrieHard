// This project is convenient for running arbitrary VS Performance Profiler tests and is currently setup to collect
// working set sizes. Its just a scratchpad, really.

using System.Buffers;
using TrieHard.Collections;
using TrieHard.Alternatives.List;
using TrieHard.Alternatives.SQLite;
using TrieHard.PrefixLookup;

Dictionary<string, Func<IEnumerable<KeyValue<string?>>, IPrefixLookup<string?>>?> implementations = new(StringComparer.OrdinalIgnoreCase)
{
    { "Baseline", (kvps) => null! },
    { "Radix", (kvps) => RadixTree<string>.Create(kvps) },
    { "Simple", (kvps) => SimpleTrie<string>.Create(kvps) },
    { "Unsafe", (kvps) => UnsafeTrie<string>.Create(kvps) },
    { "NaiveList", (kvps) => ListPrefixLookup<string>.Create(kvps) },
    { "Sqlite", (kvps) => SQLiteLookup<string>.Create(kvps) },
};

if (args.Length < 2)
{
    var lookupNames = implementations.Keys.OrderBy(x => x).ToArray();
    Console.WriteLine("Please specify which TrieHard implementation you would like to test.");
    Console.WriteLine("Valid values are: ");
    foreach(var name in lookupNames)
    {
        Console.WriteLine($"   {name}");
    }
    Console.WriteLine();
    Console.WriteLine("You must also specify the type of key pattern you would like to test against");
    Console.WriteLine("Valid value are: ");
    Console.WriteLine("   sequential");
    Console.WriteLine("   paths");
    return;
}

var keyType = args[1].ToLower();

var kvps = new List<KeyValue<string?>>();
string emptyPayload = string.Empty;

if (keyType == "sequential")
{
    for (int i = 0; i < 5_000_000; i++)
    {
        var key = i.ToString();
        //var key = $"/customer/{i}/entity/{1_000_000 - i}/";
        kvps.Add(new KeyValue<string?>(key, emptyPayload));
    }
}
else if (keyType == "paths")
{
    for (int i = 0; i < 5_000_000; i++)
    {
        var key = $"/customer/{i}/entity/{i}/";
        kvps.Add(new KeyValue<string?>(key, emptyPayload));
    }
}
else
{
    Console.WriteLine($"Key Type {keyType} is not an understood argument.");
    return;
}

var implementation = args[0];
var implementationName = string.Empty;

if (!implementations.TryGetValue(implementation, out var factory))
{
    return;
}

implementationName = implementations.Keys.First(x => string.Equals(x, implementation, StringComparison.OrdinalIgnoreCase));

if (factory == null) throw new NullReferenceException(nameof(factory));

var trie = factory(kvps);

if (implementationName != "Baseline")
{
    kvps.Clear();
    kvps = null;
}

var managedAlloc = GC.GetTotalMemory(true) / 1_000_000.00;
var gcPause = GC.GetTotalPauseDuration();
var processMemory = Environment.WorkingSet / 1_000_000.00;
var padRightLength = implementations.Keys.Max(k => k.Length);

var implementationText = implementationName.PadRight(padRightLength);
var keyTypeText = keyType.PadRight(10);
var managedAllocText = managedAlloc.ToString("#######0.00").PadLeft(11);
var gcPauseText = gcPause.TotalSeconds.ToString("0.0000").PadLeft(8);
var processMemoryText = processMemory.ToString("#######0.00").PadLeft(11);

Console.WriteLine($"| {implementationText} | {keyTypeText} | {managedAllocText} | {processMemoryText} | {gcPauseText} |");

// GC will collect the trie if it isn't used after the collection in this method
if (trie is not null)
{
     foreach(var value in trie.Search("asdf"))
    {
        if (value.Key is null)
        {
            throw new Exception();
        }
    }
}

if (implementationName == "Baseline")
{
    foreach(var value in kvps!)
    {
        if (value.Key is null)
        {
            throw new Exception();
        }
    }
}