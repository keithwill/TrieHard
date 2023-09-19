![Nuget](https://img.shields.io/nuget/v/PrefixLookup)

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
by a single thread while reads are going on concurrently. It is not an immutable
trie though, and if changes are performed while readers are enumerating, they will
see values that may have been modified after they started enumerating.

### [Indirect Trie](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.PrefixLookup/IndirectTrie)

This trie uses readonly structs stored in arrays to represent nodes.
The structs do not directly reference each other, instead referencing bucket and array
index locations where connected nodes are stored instead. Nodes only store links to their
parent, their first child and to their first in-order sibling. It has similar concurrency
characteristics to the RadixTree.

### [Compact Trie](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.PrefixLookup/CompactTrie)

This Trie uses unmanaged memory as storage for nodes and utilizes Spans and inline
arrays to reduce allocations during operations. It offers a few specialized APIs beyond
the IPrefixLookup methods, such as a non allocation search operation that gives access
to keys as UTF8 spans.

The string keys are converted to UTF8 bytes before they are stored and when retrieved.
Performance is better when using the UTF8 specific search methods (not shared by the other
implementations) or when retrieving values.

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
a wrapper around the CompactTrie implementation. This package should be considered
experimental at this time and plans are to target the most recent LTS of .NET

## Benchmarks

Benchmarks are contained in TrieHard.Benchmarks. Most of the tests are contained in
the [LookupBenchmark.cs](https://github.com/keithwill/TrieHard/blob/main/test/TrieHard.Benchmarks/LookupBenchmark.cs), but
there are a few tests in [CompatBench.cs](https://github.com/keithwill/TrieHard/blob/main/test/TrieHard.Benchmarks/CompactBench.cs) which
are specific to the CompactTrie.

### Creating a lookup with a million sequential entries (strings for keys and values)
`dotnet run -c Release --filter *Create*`

```console
| Type      | Method | Mean     | Error    | StdDev   | Gen0   | Gen1   | Gen2   | Allocated  |
|---------- |------- |---------:|---------:|---------:|-------:|-------:|-------:|-----------:|
| Simple    | Create | 153.4 us |  9.03 us |  0.49 us | 3.4180 | 1.2207 |      - |  569.18 KB |
| Radix     | Create | 193.0 us |  9.21 us |  1.43 us | 2.4414 | 0.4883 |      - |  417.63 KB |
| Compact   | Create | 238.4 us | 14.51 us |  5.17 us | 0.4883 |      - |      - |   80.87 KB |
| Indirect  | Create | 307.1 us | 22.58 us | 11.81 us | 1.4648 | 1.4648 | 1.4648 |  468.88 KB |
| NaiveList | Create | 370.0 us | 20.65 us |  1.13 us |      - |      - |      - |   43.35 KB |
| rmTrie    | Create | 446.1 us | 16.12 us |  2.49 us | 6.8359 | 2.4414 |      - | 1097.82 KB |
| SQLite    | Create | 935.2 us | 44.28 us |  2.43 us | 1.9531 |      - |      - |  381.76 KB |
```

### Getting a value by key
`dotnet run -c Release --filter *Get*`

```console
| Type      | Method   | Mean            | Error          | StdDev        | Gen0   | Allocated |
|---------- |--------- |----------------:|---------------:|--------------:|-------:|----------:|
| Compact   | Get      |        29.66 ns |       1.060 ns |      0.058 ns |      - |         - |
| Radix     | Get      |        30.13 ns |       1.712 ns |      0.265 ns |      - |         - |
| Simple    | Get      |        32.94 ns |       0.875 ns |      0.048 ns |      - |         - |
| rmTrie    | Get      |        35.13 ns |       0.490 ns |      0.027 ns |      - |         - |
| Indirect  | Get      |        84.71 ns |       0.812 ns |      0.044 ns |      - |         - |
| SQLite    | Get      |       832.92 ns |      32.031 ns |      4.957 ns | 0.0019 |     416 B |
| NaiveList | Get      | 8,030,960.16 ns | 610,192.498 ns | 33,446.710 ns |      - |     189 B |
```

A plain list struggles a bit at one million records.

### Setting a value by key
`dotnet run -c Release --filter *Set*`

```console
| Type      | Method | Mean            | Error         | StdDev       | Allocated |
|---------- |------- |----------------:|--------------:|-------------:|----------:|
| Compact   | Set    |        34.72 ns |      1.022 ns |     0.056 ns |         - |
| Radix     | Set    |        38.15 ns |      0.963 ns |     0.053 ns |         - |
| rmTrie    | Set    |        41.82 ns |      1.998 ns |     0.110 ns |         - |
| Simple    | Set    |        50.84 ns |      0.474 ns |     0.026 ns |         - |
| Indirect  | Set    |       189.52 ns |      1.363 ns |     0.075 ns |         - |
| NaiveList | Set    | 1,386,196.61 ns | 40,324.607 ns | 2,210.328 ns |      13 B |
```

### Searching Key Value Pairs by prefix (100 results enumerated)
`dotnet run -c Release --filter *SearchKVP*`

```console
| Type      | Method    | Mean            | Error           | StdDev        | Gen0   | Allocated |
|---------- |---------- |----------------:|----------------:|--------------:|-------:|----------:|
| Radix     | SearchKVP |        434.0 ns |        17.03 ns |       0.93 ns | 0.0029 |     496 B |
| Indirect  | SearchKVP |        478.8 ns |         9.61 ns |       0.53 ns | 0.0029 |     544 B |
| Simple    | SearchKVP |        837.8 ns |        36.14 ns |       5.59 ns | 0.0124 |    2120 B |
| rmTrie    | SearchKVP |      1,269.4 ns |        69.62 ns |       3.82 ns | 0.0153 |    2496 B |
| Compact   | SearchKVP |      1,620.1 ns |       109.85 ns |      65.37 ns | 0.0038 |     696 B |
| NaiveList | SearchKVP | 14,945,116.1 ns | 1,004,783.84 ns |  55,075.59 ns |      - |     378 B |
| SQLite    | SearchKVP | 33,577,520.0 ns | 1,675,232.52 ns | 259,244.09 ns |      - |    1368 B |
```

### Searching Values by prefix (100 results enumerated)
`dotnet run -c Release --filter *SearchValues*`

```console
| Type      | Method            | Mean            | Error           | StdDev       | Gen0   | Allocated |
|---------- |------------------ |----------------:|----------------:|-------------:|-------:|----------:|
| Compact   | SearchValues      |        540.4 ns |        21.30 ns |      3.30 ns | 0.0010 |     176 B |
| Radix     | SearchValues      |        559.3 ns |        41.35 ns |      2.27 ns | 0.0029 |     560 B |
| Indirect  | SearchValues      |        568.2 ns |        34.20 ns |      1.87 ns | 0.0029 |     528 B |
| Simple    | SearchValues      |        919.1 ns |        25.41 ns |      3.93 ns | 0.0134 |    2184 B |
| rmTrie    | SearchValues      |      1,039.2 ns |        33.74 ns |      1.85 ns | 0.0114 |    1968 B |
| NaiveList | SearchValues      | 15,526,997.4 ns |   729,173.19 ns | 39,968.44 ns |      - |     466 B |
| SQLite    | SearchValues      | 33,621,077.8 ns | 1,386,184.98 ns | 75,981.48 ns |      - |     928 B |
```

### Compact additional APIs
`dotnet run -c Release --filter *Compact*`

```console
| Method            | Mean            | Error          | StdDev      | Gen0   | Allocated |
|------------------ |----------------:|---------------:|------------:|-------:|----------:|
| Get_Utf8          |      23.5229 ns |      0.4000 ns |   0.0219 ns |      - |         - |
| SearchSpans       |     327.7641 ns |      7.8114 ns |   0.4282 ns |      - |         - |
| SearchValues_Utf8 |     439.9298 ns |     19.6796 ns |   1.0787 ns |      - |         - |
```

These methods require searching with UTF8 byte data. The SearchSpans method returns an enumerator
which only has access to the key for the duration of the loop body and will cause boxing if used
with any LINQ operations (or anything casting the result to IEnumerable). It avoids the cost
and garbage collection of converting the keys back to strings, but has much more limited utility
since a consumer has to use the search results in a narrow scope.

### Working set to create one million sequential entries
```console
| Method            | Working Set MiB |
|------------------ |----------------:|
| Baseline          |       93.15 MiB |
| Compact           |      132.00 MiB |
| Naive List        |      135.87 MiB |
| Indirect          |      139.03 MiB |
| SQLite            |      141.62 MiB |
| Radix             |      214.34 MiB |
| Simple            |      289.73 MiB |
| rmTrie            |      290.02 MiB |
```
Baseline in this case means creating one million key value pairs without putting them into
one of the lookups. This last 'benchmark' is meant to be illustrative, it is not a formal
benchmark. It was done using a console application and the Visual Studio performance profiler.

To properly compare the memory characteristics of the various implementations would require
testing more key and payload types (particularly value type payloads), as well as utilizing
keys that would exhibit various levels of branching (e.g. sequential vs random words vs
highly random like GUIDs).