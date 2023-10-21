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
of its results as NuGet packages for general use.

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

The most common alternative for implementing a prefix lookup is a naïve enumeration over a list.
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

### [SimpleTrie](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.Alternatives/SimpleTrie)

This was implemented as a reference C# trie based on various articles that suggest
using Dictionaries at each node to store keys and children. A number of NuGet packages
can be found that implement a similar approach.

### [RadixTree](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.PrefixLookup/RadixTree)

This is similar to a trie, but key values that don't branch can be combined. When keys
are longer and highly unique, then this approach can perform well. This particular
implementation was tuned to store UTF8 key data and has some additional optimizations meant
to address sequential keys.


### [Unsafe Trie](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.PrefixLookup/UnsafeTrie)

This trie uses unmanaged memory as storage for node structs and utilizes spans and
other techniques to reduce allocations and GC pressure during operation. It offers a
few specialized APIs beyond the IPrefixLookup methods, such as a non allocating search
operation that gives access to keys as UTF8 spans.

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

Also included is a project for building a NuGet package. Currently the PrefixLookup
class utilizes a wrapper around the RadixTree implementation. The library also contains
the other usable collections being developed as part of this project, you don't have to use
the wrapper class. This package should be considered experimental at this time and plans
are to target the most recent LTS of .NET

## Benchmarks

Benchmarks are contained in TrieHard.Benchmarks. Most of the tests are contained in
the [LookupBenchmark.cs](https://github.com/keithwill/TrieHard/blob/main/test/TrieHard.Benchmarks/LookupBenchmark.cs), but
there are a few other files specific to tries that have additional functionality.

### Creating a lookup with a million sequential entries (strings for keys and values)
`dotnet run -c Release --filter *Create*`

```console
| Type      | Method | Mean     | Error   | StdDev  | Gen0   | Gen1   | Allocated |
|---------- |------- |---------:|--------:|--------:|-------:|-------:|----------:|
| Simple    | Create | 154.7 us | 1.64 us | 1.53 us | 3.4180 | 1.2207 | 569.18 KB |
| Radix     | Create | 201.9 us | 0.65 us | 0.61 us | 2.1973 | 0.4883 | 348.01 KB |
| Unsafe    | Create | 224.9 us | 0.63 us | 0.56 us | 0.4883 |      - |  80.87 KB |
| NaiveList | Create | 378.5 us | 1.86 us | 1.65 us |      - |      - |   74.6 KB |
| SQLite    | Create | 928.5 us | 3.91 us | 3.47 us | 1.9531 |      - | 397.38 KB |
```

### Getting a value by key
`dotnet run -c Release --filter *Get*`

```console
| Type      | Method   | Mean            | Error         | StdDev        | Gen0   | Allocated |
|---------- |--------- |----------------:|--------------:|--------------:|-------:|----------:|
| Radix     | Get      |        23.86 ns |      0.037 ns |      0.033 ns |      - |         - |
| Unsafe    | Get      |        28.94 ns |      0.084 ns |      0.065 ns |      - |         - |
| Simple    | Get      |        32.64 ns |      0.050 ns |      0.047 ns |      - |         - |
| SQLite    | Get      |       847.87 ns |      5.698 ns |      5.330 ns | 0.0019 |     416 B |
| NaiveList | Get      | 8,570,001.56 ns | 40,448.613 ns | 35,856.642 ns |      - |     164 B |
```

A plain list struggles a bit at one million records.

### Setting a value by key
`dotnet run -c Release --filter *Set*`

```console
| Type      | Method   | Mean            | Error        | StdDev       | Allocated |
|---------- |--------- |----------------:|-------------:|-------------:|----------:|
| Unsafe    | Set      |        33.62 ns |     0.070 ns |     0.055 ns |         - |
| Radix     | Set      |        39.27 ns |     0.073 ns |     0.068 ns |         - |
| Simple    | Set      |        42.62 ns |     0.153 ns |     0.136 ns |         - |
| NaiveList | Set      | 1,828,051.09 ns | 5,013.714 ns | 4,689.831 ns |      33 B |
```

### Searching Key Value Pairs by prefix (100 results enumerated)
`dotnet run -c Release --filter *SearchKVP*`

```console
| Type      | Method         | Mean            | Error         | StdDev        | Gen0   | Allocated |
|---------- |--------------- |----------------:|--------------:|--------------:|-------:|----------:|
| Radix     | SearchKVP      |        113.0 ns |       0.64 ns |       0.60 ns |      - |         - |
| Unsafe    | SearchKVP      |        631.4 ns |       1.88 ns |       1.76 ns | 0.0048 |     784 B |
| Simple    | SearchKVP      |      1,043.6 ns |       8.09 ns |       7.56 ns | 0.0153 |    2648 B |
| NaiveList | SearchKVP      | 19,457,791.0 ns | 270,125.84 ns | 252,675.88 ns |      - |     231 B |
| SQLite    | SearchKVP      | 33,484,737.3 ns | 289,299.95 ns | 270,611.36 ns |      - |    1353 B |
```

### Searching Values by prefix (100 results enumerated)
`dotnet run -c Release --filter *SearchValues*`

```console
| Type      | Method            | Mean             | Error          | StdDev         | Gen0   | Allocated |
|---------- |------------------ |-----------------:|---------------:|---------------:|-------:|----------:|
| Unsafe    | SearchValues      |         42.27 ns |       0.062 ns |       0.058 ns |      - |         - |
| Radix     | SearchValues      |        102.02 ns |       0.298 ns |       0.264 ns |      - |         - |
| Simple    | SearchValues      |      1,130.11 ns |       5.555 ns |       5.196 ns | 0.0153 |    2712 B |
| NaiveList | SearchValues      | 19,755,966.49 ns | 379,241.845 ns | 405,784.525 ns |      - |     335 B |
| SQLite    | SearchValues      | 33,289,459.58 ns | 381,098.453 ns | 356,479.729 ns |      - |     542 B |
```

### UTF8 Methods
`dotnet run -c Release --filter *Utf8*`

```console
| Type   | Method            | Mean      | Error    | StdDev   | Median    | Gen0   | Allocated |
|------- |------------------ |----------:|---------:|---------:|----------:|-------:|----------:|
| Radix  | Get_Utf8          |  16.21 ns | 0.042 ns | 0.039 ns |  16.22 ns |      - |         - |
| Unsafe | Get_Utf8          |  22.97 ns | 0.504 ns | 0.655 ns |  22.55 ns |      - |         - |
| Radix  | Set_Utf8          |  29.74 ns | 0.319 ns | 0.299 ns |  29.60 ns |      - |         - |
| Unsafe | Set_Utf8          |  30.38 ns | 0.072 ns | 0.056 ns |  30.38 ns |      - |         - |
| Radix  | SearchValues_Utf8 | 184.43 ns | 2.862 ns | 2.677 ns | 185.16 ns |      - |         - |
| Radix  | SearchKVP_Utf8    | 199.63 ns | 0.335 ns | 0.313 ns | 199.55 ns |      - |         - |
| Unsafe | SearchValues_Utf8 | 455.36 ns | 3.880 ns | 3.629 ns | 457.00 ns |      - |         - |
| Unsafe | Search_Utf8       | 599.63 ns | 4.196 ns | 3.925 ns | 598.75 ns | 0.0048 |     784 B |
```

Several of these implementations store UTF8 data in the graph instead of strings or
character arrays. These implementations have additional methods that are not part of
the IPrefixLookup interface that should perform better when using UTF8 byte data instead
of C# strings when getting or setting values.


### Memory Usage of 5 Million Keys
```console
| Method    | Key Type   |  Managed MB |  Process MB | GC Pause |
|-----------|------------|-------------|-------------|---------:|
| Baseline  | sequential |      627.70 |      889.23 |   0.5330 |
| NaiveList | sequential |      519.26 |     1217.40 |   0.4853 |
| Sqlite    | sequential |       40.11 |     1063.65 |   0.4082 |
| Radix     | sequential |      811.27 |     1751.98 |   2.1489 |
| Unsafe    | sequential |       67.18 |      759.13 |   0.3753 |
| Simple    | sequential |      864.07 |     1756.98 |   1.8613 |
|-----------|------------|-------------|-------------|---------:|
| Baseline  | paths      |     1019.69 |     1287.43 |   0.6472 |
| NaiveList | paths      |      911.25 |     1617.13 |   0.5957 |
| Sqlite    | paths      |       40.11 |     1712.70 |   0.4732 |
| Radix     | paths      |     1293.07 |     2639.56 |   2.7658 |
| Unsafe    | paths      |       67.17 |     3947.64 |   0.4828 |
| Simple    | paths      |    20991.61 |    22611.11 |  22.5606 |
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
