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
| Simple    | Create | 153.5 us | 11.07 us |  2.88 us | 3.4180 | 1.2207 |      - |  569.18 KB |
| Radix     | Create | 170.8 us | 13.47 us |  0.74 us | 1.4648 |      - |      - |   245.2 KB |
| Unsafe    | Create | 234.5 us |  5.97 us |  0.33 us | 0.4883 |      - |      - |   80.87 KB |
| Flat      | Create | 281.3 us | 17.16 us |  0.94 us | 1.9531 |      - |      - |  329.67 KB |
| Indirect  | Create | 308.7 us | 23.35 us | 12.21 us | 1.4648 | 1.4648 | 1.4648 |  468.88 KB |
| NaiveList | Create | 373.0 us | 27.61 us |  1.51 us |      - |      - |      - |   43.35 KB |
| rmTrie    | Create | 436.4 us | 16.81 us |  2.60 us | 6.8359 | 2.9297 |      - | 1097.82 KB |
| SQLite    | Create | 936.7 us |  2.83 us |  0.15 us | 1.9531 |      - |      - |  381.76 KB |
```

### Getting a value by key
`dotnet run -c Release --filter *Get*`

```console
| Type      | Method   | Mean            | Error          | StdDev        | Gen0   | Allocated |
|---------- |--------- |----------------:|---------------:|--------------:|-------:|----------:|
| Radix     | Get      |        22.35 ns |       0.758 ns |      0.042 ns |      - |         - |
| Unsafe    | Get      |        28.80 ns |       0.524 ns |      0.029 ns |      - |         - |
| Simple    | Get      |        32.45 ns |       1.340 ns |      0.073 ns |      - |         - |
| rmTrie    | Get      |        38.95 ns |       1.476 ns |      0.081 ns |      - |         - |
| Flat      | Get      |        40.90 ns |       2.421 ns |      0.375 ns |      - |         - |
| Indirect  | Get      |        82.79 ns |       0.368 ns |      0.020 ns |      - |         - |
| SQLite    | Get      |       855.16 ns |      57.964 ns |      3.177 ns | 0.0019 |     416 B |
| NaiveList | Get      | 7,597,596.88 ns | 296,698.123 ns | 16,263.025 ns |      - |     142 B |
```

A plain list struggles a bit at one million records.

### Setting a value by key
`dotnet run -c Release --filter *Set*`

```console
| Type      | Method   | Mean            | Error         | StdDev       | Allocated |
|---------- |--------- |----------------:|--------------:|-------------:|----------:|
| Radix     | Set      |        31.27 ns |      0.853 ns |     0.047 ns |         - |
| Unsafe    | Set      |        34.66 ns |      2.558 ns |     0.140 ns |         - |
| Simple    | Set      |        39.50 ns |      0.355 ns |     0.019 ns |         - |
| Flat      | Set      |        41.68 ns |      1.436 ns |     0.079 ns |         - |
| rmTrie    | Set      |        42.23 ns |      1.588 ns |     0.087 ns |         - |
| Indirect  | Set      |       193.18 ns |      7.130 ns |     0.391 ns |         - |
| NaiveList | Set      | 1,386,842.74 ns | 58,342.463 ns | 3,197.947 ns |       2 B |
```

### Searching Key Value Pairs by prefix (100 results enumerated)
`dotnet run -c Release --filter *SearchKVP*`

```console
| Type      | Method    | Mean            | Error         | StdDev        | Gen0   | Allocated |
|---------- |---------- |----------------:|--------------:|--------------:|-------:|----------:|
| Radix     | SearchKVP |        321.1 ns |      10.56 ns |       1.63 ns | 0.0029 |     528 B |
| Flat      | SearchKVP |        346.5 ns |      10.05 ns |       0.55 ns | 0.0029 |     528 B |
| Indirect  | SearchKVP |        482.2 ns |      25.76 ns |       1.41 ns | 0.0029 |     544 B |
| Simple    | SearchKVP |        863.6 ns |      38.22 ns |       2.10 ns | 0.0124 |    2120 B |
| rmTrie    | SearchKVP |      1,266.0 ns |      38.43 ns |       2.11 ns | 0.0153 |    2496 B |
| Unsafe    | SearchKVP |      1,596.8 ns |     116.35 ns |      51.66 ns | 0.0038 |     696 B |
| NaiveList | SearchKVP | 15,132,173.4 ns | 220,473.56 ns |  12,084.90 ns |      - |     190 B |
| SQLite    | SearchKVP | 34,370,661.7 ns | 950,924.59 ns | 147,156.63 ns |      - |     985 B |
```

### Searching Values by prefix (100 results enumerated)
`dotnet run -c Release --filter *SearchValues*`

```console
| Type      | Method            | Mean             | Error            | StdDev         | Gen0   | Allocated |
|---------- |------------------ |-----------------:|-----------------:|---------------:|-------:|----------:|
| Radix     | SearchValues      |        127.52 ns |         3.147 ns |       0.487 ns | 0.0005 |      96 B |
| Flat      | SearchValues      |        145.17 ns |         2.003 ns |       0.110 ns | 0.0005 |      96 B |
| Unsafe    | SearchValues      |        545.62 ns |        26.553 ns |       1.455 ns | 0.0010 |     176 B |
| Indirect  | SearchValues      |        564.49 ns |        33.624 ns |       1.843 ns | 0.0029 |     528 B |
| Simple    | SearchValues      |        917.13 ns |        27.469 ns |       1.506 ns | 0.0134 |    2184 B |
| rmTrie    | SearchValues      |      1,116.69 ns |        50.252 ns |       2.754 ns | 0.0114 |    1968 B |
| NaiveList | SearchValues      | 15,355,907.19 ns |   798,722.680 ns | 207,425.744 ns |      - |     276 B |
| SQLite    | SearchValues      | 33,475,025.00 ns | 1,666,252.438 ns |  91,332.919 ns |      - |     542 B |
```

### UTF8 Methods
`dotnet run -c Release --filter *Utf8*`

```console
| Type   | Method            | Mean      | Error     | StdDev   | Allocated |
|------- |------------------ |----------:|----------:|---------:|----------:|
| Radix  | Get_Utf8          | *11.81 ns |  0.289 ns | 0.016 ns |         - |
| Radix  | Set_Utf8          | *18.89 ns |  0.975 ns | 0.053 ns |         - |
| Unsafe | Get_Utf8          |  25.23 ns |  0.587 ns | 0.032 ns |         - |
| Flat   | Get_Utf8          |  30.46 ns |  0.898 ns | 0.139 ns |         - |
| Radix  | SearchValues_Utf8 |  70.27 ns |  2.093 ns | 0.115 ns |         - |
| Flat   | SearchValues_Utf8 |  90.79 ns |  4.205 ns | 0.230 ns |         - |
| Radix  | Search_Utf8       |  99.74 ns |  5.893 ns | 0.323 ns |         - |
| Unsafe | SearchValues_Utf8 | 449.89 ns | 26.927 ns | 1.476 ns |         - |
```

Several of these implementations store UTF8 data in the graph instead of strings or
character arrays. These implementations have additional methods that are not part of
the IPrefixLookup interface that should perform better when using UTF8 byte data instead
of C# strings when searching or setting values.

Note on the Radix Tree results. It has search logic targeting nodes with 10 children,
which is a legitimate improvement, but makes it perform unfairly on these benchmarks
over sequential keys. On other child node counts its results are very similar to the
Unsafe Trie.

### Memory Usage of 5 Million Keys
```console
| Method    | Key Type   |  Managed MB |  Process MB | GC Pause |
|-----------|------------|-------------|-------------|---------:|
| Baseline  | sequential |      333.48 |      458.52 |   0.2558 |
| NaiveList | sequential |      279.26 |      626.28 |   0.2436 |
| Sqlite    | sequential |       40.11 |      662.18 |   0.2189 |
| Radix     | sequential |      492.07 |      987.33 |   1.3271 |
| Indirect  | sequential |      240.57 |      441.25 |   0.2138 |
| Unsafe    | sequential |       67.18 |      380.85 |   0.2124 |
| Flat      | sequential |      849.72 |     1133.93 |   1.3176 |
| Simple    | sequential |      864.07 |     1388.93 |   1.4563 |
| rmTrie    | sequential |      864.07 |     1389.44 |   1.4915 |
|-----------|------------|-------------|-------------|---------:|
| Baseline  | paths      |      573.47 |      703.05 |   0.3638 |
| NaiveList | paths      |      519.26 |      871.51 |   0.3219 |
| Sqlite    | paths      |       40.11 |     1048.08 |   0.2663 |
| Radix     | paths      |      693.88 |     1440.84 |   1.6489 |
| Indirect  | paths      |     4027.05 |     4622.63 |   0.8288 |
| Unsafe    | paths      |       67.17 |     3879.76 |   0.2947 |
| Flat      | paths      |    13604.31 |    14153.24 |  12.1623 |
| Simple    | paths      |    20991.62 |    22058.59 |  22.3227 |
| rmTrie    | paths      |    20991.62 |    22063.23 |  22.8053 |
```

The 'paths' keys look like URL subpaths and follow a format of
/customer/{id}/entity/{id}/ with the ID being a sequential number. This
is generated with the project TrieHard.ConsoleTest. The results for
memory usage do not follow benchmarking best practices, and do not
account for differences in steady state, the ordering of keys inserted,
or other factors, so consider these as casual results.

It is enough to see that trie implementations become somewhat
bloated with even moderate length keys (and that SQLite has a very
compact in-memory format). For sequential integer keys, the unsafe
trie takes less memory than putting the keys into a List (a side
effect of storing the keys as UTF8).

Only the Radix Tree maintains decent size characteristics with
longer keys and repeated patterns.
