[![Version](https://img.shields.io/nuget/vpre/PrefixLookup.svg)](https://www.nuget.org/packages/PrefixLookup)

# Prefix Lookup and the TrieHard Project

Hi! I'm keithwill. I'm a C# dev that has been in the industry for a while,
and while working on an open source project I realized I needed an industrial
grade prefix search collection. There doesn't appear to be one in the .NET class
library, and
[after surveying the libraries available on Nuget and GitHub](https://github.com/keithwill/TrieHard/blob/6429b09d1985ea5f73bb5b9cece84d5645a57d7f/src/TrieHard.Alternatives/ExternalLibraries/Survey.md),
I realized that there are very few libraries available for this purpose that have been kept
updated, that are complete and functional, and that perform well.

This repository is a playground for testing various implementations
of structures and approaches for performing prefix (starts with)
searches, like the kind used for autocomplete or typeahead functionality.

I will gladly take contributions to this project and I plan to expose some
of its results as Nuget packages for general use.

## What is with the name? What is a Trie?

A [Trie](https://en.wikipedia.org/wiki/Trie) is a data structure that was developed for
performing prefix searches on text and has been around for a while. It is a commonly
recommended data structure for this requirement, and it has many variations.

A basic trie isn't too hard to understand. In C# terms, it can be thought of as a hierarchical
collection. Each object in the trie is a node and the trie starts with a single 'root' node. Each
node has a key character and references to its children and optionally a payload value. The root
node does not typically have a key character. There is a small amount of space saving since the
entire keys are not stored in each node, only a single character.

To do a prefix search using a trie, the children of the root node are iterated to see if one has a matching key
character to the first character in the prefix search string. If so, repeat the search with THAT child node and try
to match it on the second character of the search string. If you don't find matches for all of the characters
in the search string then there were no matches in the trie for that prefix. If you found matches for all of
the characters in the search string, then that node and all of its children (recursively) are the search results
that match the search.

Searching for a key is done the same way. The only difference is that the entire search string must be
matched and the children of the matches are not considered. Accumulating results in an efficient way can be
tricky. The most common approaches of recursion or using collections to build the results can perform poorly
in C# (due to our lack of tail call optimization) or generate excessive garbage.

## Radix Tree

A radix tree is a variant of a trie where some nodes are merged together. If a node has a child and that child
doesn't have any children of its own, then it can be merged with its parent. Instead of storing a character,
each node in the tree can contain a string. For example, if a radix tree contained only the keys 'alternate' and 'alter',
it would have three nodes: the root node, a child with a key of 'alter' and it would have a single child of 'nate'.
Later, if 'alt' was stored, then there would be nodes of 'alt', 'ter' and 'nate'. When insertions and removals are
done from the graph, nodes are merged and split as needed.

This allows fewer nodes to be created and maintained, but requires more complicated logic for inserts
and removals from the tree and requires string processing at each node which sometimes requires special consideration
in C#.

## Other Trie Variants

Many of the other trie variants utilize approaches to further compact the keys. This makes them perform well and
require less storage when checking if a key exists, but makes them ill suited for searching by prefix. Some variants are
the HAMT, the hash tree, and the patricia trie. The first two act more like dictionaries than a trie (as the keys are
hashed). I have found conflicting information on the patricia trie, but as described in its original paper it does
not store whole keys and can only be used to check for the existence of complete matches.

# Typical Alternatives to Tries

The most common alternative for implementing a prefix lookup is a naive enumeration over a list.
Typically this could be done with a LINQ Where query passing in a lambda to check if each key element StartsWith
a given search text. This can perform well for smaller collections, but quickly becomes a hindrance with
millions of items.

Database queries using a 'LIKE' clause are also quite common. While the latency can be quite bad compared to
an in-memory collection and depends on networking stability, it usually has an advantage of simplifying concurrency
concerns and ensuring the client always has access to the most recent data. For smaller projects its not uncommon
for instances to contain a local database file to cache lookups (such as SQLite).

Another option is to use a dedicated text searching tool or system, such as ElasticSearch. These can perform well and can
provide advanced searching capabilities (such as transforming the search text and analyzing it before searching,
as well as fuzzy matches), but they can be quite complicated to properly setup and maintain, and still induce
additional latency and networking concerns.

# What's Currently Included In This Project

In TrieHard.Abstractions is an interface IPrefixLookup. All implementations
in this project implement that interface. An IPrefixLookup has an indexer and
can be enumerated for key value pairs like a Dictionary, but also exposes
Search and SearchValues methods which take a key prefix and return enumerables
of KeyValuePairs or the generic value results respectively.

### [SimpleTrie](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.PrefixLookup/SimpleTrie)

This was implemented as a reference C# trie based on various articles that suggest
using Dictionaries at each node to store keys and children. A number of nuget packages
can be found that implement a similar approach.

### [RadixTree](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.PrefixLookup/RadixTree)

This is similar to a trie, but key values that don't branch can be combined. When keys
are longer and highly unique, then this approach can perform well. This particular
implementation was tuned to reduce recursion and can can be modified
by a single thread while reads are going on concurrently.

### [Indirect Trie](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.PrefixLookup/IndirectTrie)

This trie uses readonly structs stored in arrays to represent nodes.
The structs do not directly reference each other, instead referencing bucket and array
index locations where connected nodes are stored instead. Nodes only store links to their
parent, their first child and to their first in-order sibling. It has similar concurrency
characteristics to the RadixTree.

### [Unsafe Trie](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.PrefixLookup/UnsafeTrie)

This Trie uses unmanaged memory as storage for nodes and utilizes Spans and inline
arrays to reduce allocations during operations. It offers a few specialized APIs beyond
the IPrefixLookup methods, such as a non allocation search operation that gives access
to keys as UTF8 spans.

The string keys are converted to UTF8 bytes before they are stored and when retrieved.
Performance is better when using the UTF8 specific search methods or when retrieving values.

It lives up to its name, as 0.1.3 this trie has a memory leak issue (related to memory management
of native memory during search operations) and its use outside of experimentation
is not recommended at this time.

### [Flat Trie](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.PrefixLookup/FlatTrie)
This Trie backs all of the node data in arrays and takes advantage of lower allocation
patterns similar to the Compact Trie, such as the usage of structs and array pooling.
Additional Key data is stored with each node to optimize for read heavy workloads. Several
APIs unique to this trie also exist (such as paginating through page data).

### rm.Trie

I found this library on nuget/github. It was one of the few trie libraries that had
a reasonable number of downloads, was up to date, and contained functional code. An
IPrefixLookup wrapper was implemented to include it as a comparison.

### ListPrefixLookup (NaiveList)

Many devs will use LINQ on a List/Array to perform a StartsWith search to implement an
autocomplete instead of using a specialized collection. This performs well for small
lists, but can become a hindrance as the size increases. Lists are also not thread safe,
which makes modifying them at runtime awkward.

### SQLiteLookup

SQLite is a very common single file database and is often used within applications to
store read-heavy data. Using a database is a common way to query data such as for
an autocomplete or typeahead. Included is an IPrefixLookup wrapper that creates an
in-memory SQLite connection and creates a table to contain keys and value indexes.

Searches are done using SQL queries. A real database would likely perform worse
(involving networking and data retrieval). An advantage of this approach is that
the queries involved are simple, and SQLite will handle concurrency issues for
the developer (though it does not perform well when undergoing sustained
concurrent write pressure). I don't recommend reusing this SQLiteLookup, as
its not intended for use outside of these contrived benchmarks and it wasn't
made to support updates.


### Nuget Package [PrefixLookup](https://camo.githubusercontent.com/887adb22225c0f98b23fecc0aca4b12ae232332941c37fc0615ab57d4dc03ade/68747470733a2f2f696d672e736869656c64732e696f2f6e756765742f762f5072656669784c6f6f6b7570)

Also included is a project for building a Nuget package. Currently this utilizes
a wrapper around the RadixTree implementation. This package should be considered
experimental at this time and plans are to target the most recent LTS of .NET

## Benchmarks

Benchmarks are contained in TrieHard.Benchmarks. Most of the tests are contained in
the [LookupBenchmark.cs](https://github.com/keithwill/TrieHard/blob/main/test/TrieHard.Benchmarks/LookupBenchmark.cs), but
there are a few tests in [CompatBench.cs](https://github.com/keithwill/TrieHard/blob/main/test/TrieHard.Benchmarks/UnsafeBench.cs) which
are specific to the UnsafeTrie.

### Creating a lookup with a million sequential entries (strings for keys and values)
`dotnet run -c Release --filter *Create*`

```console
| Type      | Method | Mean     | Error    | StdDev   | Gen0   | Gen1   | Gen2   | Allocated  |
|---------- |------- |---------:|---------:|---------:|-------:|-------:|-------:|-----------:|
| Simple    | Create | 155.3 us |  6.93 us |  1.07 us | 3.4180 | 1.2207 |      - |  569.18 KB |
| Unsafe    | Create | 240.3 us |  9.23 us |  0.51 us | 0.4883 |      - |      - |   80.87 KB |
| Flat      | Create | 295.7 us | 23.23 us |  1.27 us | 1.9531 | 0.4883 |      - |  329.67 KB |
| Indirect  | Create | 302.1 us | 23.19 us | 15.34 us | 1.4648 | 1.4648 | 1.4648 |  468.88 KB |
| NaiveList | Create | 375.4 us | 22.66 us |  3.51 us |      - |      - |      - |   43.35 KB |
| rmTrie    | Create | 442.5 us | 31.50 us |  1.73 us | 6.8359 | 2.4414 |      - | 1097.82 KB |
| Radix     | Create | 721.0 us | 39.26 us |  6.08 us | 1.9531 | 0.9766 |      - |  311.33 KB |
| SQLite    | Create | 968.9 us | 60.55 us |  3.32 us | 1.9531 |      - |      - |  381.76 KB |
```

### Getting a value by key
`dotnet run -c Release --filter *Get*`

```console
| Type      | Method   | Mean            | Error          | StdDev        | Gen0   | Allocated |
|---------- |--------- |----------------:|---------------:|--------------:|-------:|----------:|
| Unsafe    | Get      |        28.95 ns |       0.908 ns |      0.050 ns |      - |         - |
| Simple    | Get      |        32.99 ns |       1.458 ns |      0.080 ns |      - |         - |
| Flat      | Get      |        39.63 ns |       0.897 ns |      0.049 ns |      - |         - |
| rmTrie    | Get      |        40.81 ns |       0.590 ns |      0.032 ns |      - |         - |
| Radix     | Get      |        51.08 ns |       1.258 ns |      0.069 ns |      - |         - |
| Indirect  | Get      |        82.87 ns |       0.489 ns |      0.027 ns |      - |         - |
| SQLite    | Get      |       871.57 ns |      20.913 ns |      3.236 ns | 0.0019 |     416 B |
| NaiveList | Get      | 7,967,827.47 ns | 293,004.700 ns | 16,060.576 ns |      - |     142 B |
```

A plain list struggles a bit at one million records.

### Setting a value by key
`dotnet run -c Release --filter *Set*`

```console
| Type      | Method   | Mean            | Error         | StdDev     | Allocated |
|---------- |--------- |----------------:|--------------:|-----------:|----------:|
| Unsafe    | Set      |        34.81 ns |      0.253 ns |   0.014 ns |         - |
| Simple    | Set      |        39.85 ns |      0.839 ns |   0.046 ns |         - |
| rmTrie    | Set      |        43.46 ns |      0.175 ns |   0.010 ns |         - |
| Flat      | Set      |        44.59 ns |      1.228 ns |   0.190 ns |         - |
| Radix     | Set      |        89.03 ns |      3.358 ns |   0.184 ns |         - |
| Indirect  | Set      |       198.78 ns |     11.151 ns |   0.611 ns |         - |
| NaiveList | Set      | 1,504,546.19 ns | 14,195.198 ns | 778.087 ns |       1 B |
```

### Searching Key Value Pairs by prefix (100 results enumerated)
`dotnet run -c Release --filter *SearchKVP*`

```console
| Type      | Method    | Mean            | Error         | StdDev       | Gen0   | Allocated |
|---------- |---------- |----------------:|--------------:|-------------:|-------:|----------:|
| Flat      | SearchKVP |        421.6 ns |      11.97 ns |      0.66 ns | 0.0029 |     528 B |
| Radix     | SearchKVP |        451.1 ns |      13.68 ns |      2.12 ns | 0.0029 |     528 B |
| Indirect  | SearchKVP |        477.3 ns |      15.03 ns |      0.82 ns | 0.0033 |     544 B |
| Simple    | SearchKVP |        828.7 ns |      37.04 ns |      2.03 ns | 0.0124 |    2120 B |
| rmTrie    | SearchKVP |      1,269.4 ns |      16.72 ns |      0.92 ns | 0.0153 |    2496 B |
| Unsafe    | SearchKVP |      1,672.3 ns |      90.25 ns |      4.95 ns | 0.0038 |     696 B |
| NaiveList | SearchKVP | 15,277,728.6 ns | 610,732.64 ns | 33,476.32 ns |      - |     208 B |
| SQLite    | SearchKVP | 34,304,568.9 ns | 874,812.75 ns | 47,951.44 ns |      - |     966 B |
```

### Searching Values by prefix (100 results enumerated)
`dotnet run -c Release --filter *SearchValues*`

```console
| Type      | Method            | Mean            | Error           | StdDev        | Gen0   | Allocated |
|---------- |------------------ |----------------:|----------------:|--------------:|-------:|----------:|
| Flat      | SearchValues      |        195.9 ns |        10.59 ns |       0.58 ns | 0.0005 |      96 B |
| Radix     | SearchValues      |        252.2 ns |         3.43 ns |       0.19 ns | 0.0005 |      96 B |
| Unsafe    | SearchValues      |        541.4 ns |         3.53 ns |       0.19 ns | 0.0010 |     176 B |
| Indirect  | SearchValues      |        567.2 ns |        14.24 ns |       0.78 ns | 0.0029 |     528 B |
| Simple    | SearchValues      |        918.0 ns |        37.53 ns |       2.06 ns | 0.0134 |    2184 B |
| rmTrie    | SearchValues      |      1,045.0 ns |        49.76 ns |       2.73 ns | 0.0114 |    1968 B |
| NaiveList | SearchValues      | 15,389,530.2 ns |   451,202.11 ns |  24,731.91 ns |      - |     278 B |
| SQLite    | SearchValues      | 33,978,553.3 ns | 1,645,314.16 ns | 427,282.86 ns |      - |     564 B |
```

### UTF8 Methods
`dotnet run -c Release --filter *Utf8*`

```console
| Type    | Method            | Mean      | Error     | StdDev   | Allocated |
|-------- |------------------ |----------:|----------:|---------:|----------:|
| Unsafe  | Get_Utf8          |  23.65 ns |  1.614 ns | 0.088 ns |         - |
| Flat    | Get_Utf8          |  30.74 ns |  1.701 ns | 0.263 ns |         - |
| Radix   | Get_Utf8          |  35.17 ns |  0.795 ns | 0.044 ns |         - |
| Radix   | Set_Utf8          |  61.55 ns |  0.654 ns | 0.036 ns |         - |
| Flat    | SearchValues_Utf8 | 133.28 ns |  4.329 ns | 0.237 ns |         - |
| Radix   | SearchValues_Utf8 | 163.66 ns |  3.148 ns | 0.173 ns |         - |
| Radix   | Search_Utf8       | 173.24 ns |  0.468 ns | 0.026 ns |         - |
| Unsafe  | SearchValues_Utf8 | 449.75 ns | 25.694 ns | 3.976 ns |         - |
```

Several of these implementations store UTF8 data in the graph instead of strings or
character arrays. These implementations have additional methods that are not part of
the IPrefixLookup interface that should perform better when using UTF8 byte data instead
of C# strings when searching or setting values.

### Working set to create one million sequential entries
```console
| Method            | Working Set MiB |
|------------------ |----------------:|
| Baseline          |        53.20 MB |
| Naive List        |        85.62 MB |
| Unsafe            |       110.30 MB |
| Indirect          |       116.33 MB |
| SQLite            |       110.98 MB |
| Flat              |       116.85 MB |
| Simple            |       249.98 MB |
| rmTrie            |       242.04 MB |
| Radix             |       299.40 MB |
```
Baseline in this case means creating one million key value pairs without putting them into
one of the lookups. This last is meant to be illustrative, it is not a formal
benchmark. It was done using a console application and the reported Environment.WorkingSet size.

The size of the Radix is misleading. Nearly 80mb of the working set is pooled arrays that are
waiting to be reused.

### Working set to create one million sequential entries with a longer key pattern of \/customer/{i}/entity/{1_000_000 - i}/\
```console
| Method            | Working Set MiB |
|------------------ |----------------:|
| Baseline          |       150.26 MB |
| Naive List        |       169.05 MB |
| SQLite            |       222.04 MB |
| Radix             |       422.15 MB |
| Indirect          |       795.04 MB |
| Unsafe            |       846.19 MB |
| Flat              |      2171.89 MB |
| Simple            |      4149.96 MB |
| rmTrie            |      4139.12 MB |
```

Tries can struggle with longer keys. The strength of the RadixTree is more evident with longer
keys that have repeated patterns embedded.