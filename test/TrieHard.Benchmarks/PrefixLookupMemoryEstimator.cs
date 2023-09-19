using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrieHard.Abstractions;

namespace TrieHard.Benchmarks
{
    /// <summary>
    /// TODO: Implement a clunky way to estimate the amount of managed garbage that is released
    /// when a Trie is created and then garbage collected as a way to reason about the size of
    /// a Tries total (managed) object graph size at runtime.
    /// </summary>
    public class PrefixLookupMemoryEstimator<T> where T : IPrefixLookup<string, string>
    {

        public PrefixLookupMemoryEstimator()
        {

        }

    }
}
