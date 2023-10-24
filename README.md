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

### [PrefixLookup](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.PrefixLookup)

This is a lookup implemented as a Radix Tree. When keys
are longer and highly unique, then this approach can perform well compared to normal tries.
Keys are stored in the tree internally as UTF8 to simplify searching operations.

### [Unsafe Trie](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.PrefixLookup/Unsafe)

This trie uses unmanaged memory as storage for node structs and utilizes spans and
other techniques to reduce allocations and GC pressure during operation. It offers a
few specialized APIs beyond the IPrefixLookup methods, such as a non allocating search
operation that gives access to keys as UTF8 spans.

### [SimpleTrie](https://github.com/keithwill/TrieHard/tree/main/src/TrieHard.Alternatives/SimpleTrie)

This was implemented as a reference C# trie based on various articles that suggest
using Dictionaries at each node to store keys and children. A number of NuGet packages
can be found that implement a similar approach.

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

The PrefixLookup.csproj includes output for a NugetPackage that includes the PrefixLookup
class and the UnsafeTrie.

## Benchmarks

Benchmarks are contained in TrieHard.Benchmarks. Most of the tests are contained in
the [LookupBenchmark.cs](https://github.com/keithwill/TrieHard/blob/main/test/TrieHard.Benchmarks/LookupBenchmark.cs), but
there are a few other files specific to tries that have additional functionality.

### Creating a lookup with a million sequential entries (strings for keys and values)
`dotnet run -c Release --filter *Create*`

```console
| Type         | Method | Mean     | Error   | StdDev  | Gen0   | Gen1   | Allocated |
|------------- |------- |---------:|--------:|--------:|-------:|-------:|----------:|
| Simple       | Create | 157.1 us | 2.23 us | 2.08 us | 3.4180 | 1.4648 | 569.18 KB |
| PrefixLookup | Create | 196.4 us | 0.79 us | 0.74 us | 1.9531 | 0.2441 | 345.82 KB |
| Unsafe       | Create | 239.3 us | 1.69 us | 1.50 us | 0.4883 |      - |  80.87 KB |
| NaiveList    | Create | 376.1 us | 2.17 us | 1.92 us |      - |      - |   74.6 KB |
| SQLite       | Create | 925.8 us | 2.63 us | 2.33 us | 1.9531 |      - | 397.38 KB |
```

### Getting a value by key
`dotnet run -c Release --filter *Get*`

```console
| Type         | Method   | Mean            | Error         | StdDev        | Gen0   | Allocated |
|------------- |--------- |----------------:|--------------:|--------------:|-------:|----------:|
| PrefixLookup | Get      |        27.53 ns |      0.048 ns |      0.040 ns |      - |         - |
| Unsafe       | Get      |        29.20 ns |      0.231 ns |      0.205 ns |      - |         - |
| Simple       | Get      |        33.57 ns |      0.419 ns |      0.392 ns |      - |         - |
| SQLite       | Get      |       834.28 ns |      6.481 ns |      6.062 ns | 0.0019 |     416 B |
| NaiveList    | Get      | 8,848,481.38 ns | 15,358.406 ns | 11,990.834 ns |      - |     164 B |
```

A plain list struggles a bit at one million records.

### Setting a value by key
`dotnet run -c Release --filter *Set*`

```console
| Type         | Method   | Mean            | Error        | StdDev       | Allocated |
|------------- |--------- |----------------:|-------------:|-------------:|----------:|
| PrefixLookup | Set      |        33.24 ns |     0.113 ns |     0.100 ns |         - |
| Unsafe       | Set      |        33.73 ns |     0.085 ns |     0.071 ns |         - |
| Simple       | Set      |        39.57 ns |     0.076 ns |     0.071 ns |         - |
| NaiveList    | Set      | 1,815,906.09 ns | 4,153.712 ns | 3,885.384 ns |      33 B |
```

### Searching Key Value Pairs by prefix (100 results enumerated)
`dotnet run -c Release --filter *SearchKVP*`

```console
| Type         | Method         | Mean            | Error         | StdDev        | Gen0   | Allocated |
|------------- |--------------- |----------------:|--------------:|--------------:|-------:|----------:|
| PrefixLookup | SearchKVP      |        112.5 ns |       0.13 ns |       0.11 ns |      - |         - |
| Unsafe       | SearchKVP      |        645.5 ns |       1.85 ns |       1.73 ns | 0.0048 |     784 B |
| Simple       | SearchKVP      |      1,058.3 ns |       7.18 ns |       6.72 ns | 0.0153 |    2648 B |
| NaiveList    | SearchKVP      | 19,382,526.2 ns | 208,371.33 ns | 194,910.67 ns |      - |     231 B |
| SQLite       | SearchKVP      | 33,652,122.3 ns | 401,508.08 ns | 355,926.46 ns |      - |    1350 B |
```

### Searching Values by prefix (100 results enumerated)
`dotnet run -c Release --filter *SearchValues*`

```console
| Type         | Method            | Mean             | Error          | StdDev         | Gen0   | Allocated |
|------------- |------------------ |-----------------:|---------------:|---------------:|-------:|----------:|
| Unsafe       | SearchValues**    |         43.77 ns |       0.059 ns |       0.052 ns |      - |         - |
| PrefixLookup | SearchValues      |        102.13 ns |       0.104 ns |       0.092 ns |      - |         - |
| Simple       | SearchValues      |      1,169.26 ns |       8.131 ns |       7.208 ns | 0.0153 |    2712 B |
| NaiveList    | SearchValues      | 19,610,249.38 ns | 239,773.289 ns | 224,284.083 ns |      - |     335 B |
| SQLite       | SearchValues      | 33,229,859.05 ns | 187,359.459 ns | 166,089.280 ns |      - |     545 B |
```

The Unsafe SearchValues result doesn't seem accurate.

### UTF8 Methods
`dotnet run -c Release --filter *Utf8*`

```console
| Type         | Method            | Mean      | Error    | StdDev   | Gen0   | Allocated |
|------------- |------------------ |----------:|---------:|---------:|-------:|----------:|
| PrefixLookup | Get_Utf8          |  20.72 ns | 0.045 ns | 0.042 ns |      - |         - |
| Unsafe       | Get_Utf8          |  22.82 ns | 0.127 ns | 0.113 ns |      - |         - |
| PrefixLookup | Set_Utf8          |  24.58 ns | 0.035 ns | 0.032 ns |      - |         - |
| Unsafe       | Set_Utf8          |  28.49 ns | 0.033 ns | 0.028 ns |      - |         - |
| PrefixLookup | SearchValues_Utf8 | 100.67 ns | 0.133 ns | 0.125 ns |      - |         - |
| PrefixLookup | SearchKVP_Utf8    | 134.14 ns | 0.425 ns | 0.398 ns |      - |         - |
| Unsafe       | SearchValues_Utf8 | 447.69 ns | 7.195 ns | 6.730 ns |      - |         - |
| Unsafe       | Search_Utf8       | 572.67 ns | 3.516 ns | 3.289 ns | 0.0048 |     784 B |
```

Several of these implementations store UTF8 data in the graph instead of strings or
character arrays. These implementations have additional methods that are not part of
the IPrefixLookup interface that should perform better when using UTF8 byte data instead
of C# strings when getting or setting values.


### Memory Usage of 5 Million Keys
```console
| Method       | Key Type   |  Managed MB |  Process MB | GC Pause |
|--------------|------------|-------------|-------------|---------:|
| Baseline     | sequential |      627.70 |      889.11 |   0.5402 |
| NaiveList    | sequential |      519.26 |     1217.32 |   0.4861 |
| Sqlite       | sequential |       40.11 |     1063.33 |   0.4054 |
| PrefixLookup | sequential |      811.27 |     1766.81 |   2.4133 |
| Unsafe       | sequential |       67.18 |      759.01 |   0.3741 |
| Simple       | sequential |      864.07 |     1756.80 |   1.8392 |
|--------------|------------|-------------|-------------|---------:|
| Baseline     | paths      |     1019.69 |     1287.34 |   0.6472 |
| NaiveList    | paths      |      911.25 |     1617.11 |   0.5960 |
| Sqlite       | paths      |       40.11 |     1712.70 |   0.4700 |
| PrefixLookup | paths      |     1275.25 |     2623.66 |   3.0919 |
| Unsafe       | paths      |       67.17 |     3947.65 |   0.4849 |
| Simple       | paths      |    20991.61 |    22617.46 |  21.9587 |
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