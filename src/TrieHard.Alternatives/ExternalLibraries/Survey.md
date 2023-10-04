This is a list of the libraries that were considered for inclusion in this project for comparison.


## Simple Dictionary Based Tries
- [kpol/trie](https://github.com/kpol/trie/)
- [rmandvikar/csharp-trie](https://github.com/rmandvikar/csharp-trie)
- [TomGullen/C-Sharp-Trie](https://github.com/TomGullen/C-Sharp-Trie)
- [IEnumerable-Trie](https://github.com/dagoltz/IEnumerable-Trie/)

I scanned the code of most of these projects and they all seemed very
similar to the 'SimpleTrie' implementation used in TrieHard with slight
variants on the API surface.

- [MoreComplexDataStructures](https://github.com/alastairwyse/MoreComplexDataStructures/)

Contains a generic Trie implementation that is based on dictionaries. Hasn't been updated in years and doesn't seem to have a nuget package link on the github.


## List Based
- [First Division Trie](https://github.com/firstdivision/trie/)  
This uses a List on each node instead of a dictionary. It does have a flag for collecting 'suggestions' as well.

## Other (some of these are not tries or have other issues)

- [DawgSharp](https://github.com/bzaar/DawgSharp)  
Definitely want to compare this library. It is one I have used previously when creating an auto complete for a commercial application and is what started my interests in prefix searching structures. Unfortunately it takes a while to build one of these, and they are immutable once built, so it can't be compared against the modifiable class of structures. (I need to review the license on this further. TrieHard is MIT, but DawgSharp is GPLv3 so I'll have to be careful to ensure the alternatives and benchmark projects are never distributed).

- [TernarySearchTrie](https://github.com/lewis267/TernarySearchTrie)  
I'll have to read this repo again. It claims to be a ternary trie, but says its implemented
using another structure and is for pattern matching. I'm not sure if its intended to be used
for prefix searching.


- [prefix-tree-trie](https://github.com/Dkendal/prefix-tree-trie/  )
A simple implementation of a Radix Tree. It doesn't seem to allow the storage of arbitrary values though

- [DBTrie](https://github.com/NicolasDorier/DBTrie/)  
This library has an interesting trie implementation, but unfortunately its tied to its
on-disk storage model and its not designed for reuse as a general collection.

- [AhoCorasickDoubleArrayTrie](https://github.com/nreco/AhoCorasickDoubleArrayTrie)  
An interesting implementation ported from Java for substring matches, but it does not seem 
to support prefix searches. I like that it comes with its own binary serialization format.

- [dotnet-trees](https://github.com/tunnelvisionlabs/dotnet-trees/)  
Its an interesting repository, but the tree implementations inside are only used for reimplementing
other .NET collections in the System namespace. I don't see how to use anything in this library for
efficient prefix lookups.

- [Stratis.Patricia](https://www.nuget.org/packages/Stratis.Patricia)  
It has dependencies on crypto libraries that seem unrelated...
Patricia tries are a particular trie variant, but this one does not store payloads (only checks for
the existence of keys), so it won't be useful for our comparisons.

- [ReTrie](https://www.nuget.org/packages/ReTrie)  
No readme and no link to a source repository.

- [Hash-Array-Mapped-Trie](https://github.com/phretaddin/Hash-Array-Mapped-Trie)  
HAMT tries hash the key before storing it. They are a fun concept, but not very useful for 
prefix searching. They are more like a dictionary replacement.

- [autocomplete](https://github.com/omerfarukz/autocomplete)  
Looked really promising, but its usage requires multiple files on disk and uses obsolete BinaryWriter
streams. I decided it wasn't worth trying to utilize.

- [AdaptiveRadixTree](https://github.com/manly/AdaptiveRadixTree)  
The ART (adaptive radix trie). I remember reading an article about ART tries. This looks like either a port
or a reimplementation by the dev in question based on the concept. Unfortunately its .NET Framework only
and I don't have the time to see how much effort it would take to migrate it.

- [PruningRadixTrie](https://github.com/wolfgarbe/PruningRadixTrie)  
Seems really promising. A radix tree that supports a type of early termination to improve performance.
I'm interested to see if it lives up to its claims of being up to 1000x faster than a normal radix tree.
(After considering, its interesting, but its specialized for word frequency searching, not for use as a 
general collection)

- [glenebob/Trie](https://github.com/glenebob/Trie)  
I'm not sure if this implementation works (the repo .md implies it is an attempt). It does seem to have
a few nice ideas inside of the code though.

- [nctrie](https://github.com/jsbattig/nctrie/)  
Claims to be a concurrent trie based on a ctrie implementation ported from Java (by Roman Levenstein)
...but the main class has a lot of not implemented exceptions and I don't see any tests from the root
of the repo. Might be worth another look at some point.

- [trienet](https://github.com/gmamaladze/trienet)  
This library has been around a while. The Trie implementation seems simple. It makes sense to include
both of the generic trie and concurrent trie implementations. It doesn't have a lot of recent activity.
(After reviewing it briefly, its only .NET framework 4.6)

- [ukitake/trie](https://github.com/ukitake/trie/)  
If I'm reading this correctly, it only supports ASCII case insensitive and uses an unsafe struct for
in-memory storage. It could have interesting performance. I should compare this library, though its constraints
probably make it unsuable as a general collection. (Upon review, it doesn't have any starts-with search capability,
it only provides checking for existence of words).