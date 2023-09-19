using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrieHard.Abstractions
{
    public interface IPrefixLookup<TKey, TValue> : IPrefixLookup, IEnumerable<KeyValuePair<TKey, TValue>>
    {

        /// <summary>
        /// Gets the value of the supplied key. Unlike a Dictionary,
        /// this will return a default or null value if the value does not
        /// exist in the lookup.
        /// </summary>
        /// <param name="key">The key to match on</param>
        /// <returns>The value associated with the key or default(TElement)</returns>
        TValue this[TKey key] { get; set; }

        /// <summary>
        /// Removes all values from the lookup. Implementations are free to decide if this
        /// clears any other internal storage
        /// </summary>
        void Clear();

        /// <summary>
        /// The number of values stored in the lookup.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Returns the key value pairs the have a key that starts with the
        /// supplied keyPrefix value.
        /// </summary>
        /// <param name="keyPrefix">The value to use as a 'StartsWith' search of keys</param>
        /// <returns>An enumerable of the key value pairs matching the prefix</returns>
        IEnumerable<KeyValuePair<TKey, TValue>> Search(TKey keyPrefix);

        IEnumerable<TValue> SearchValues(TKey keyPrefix);

        /// <summary>
        /// If this type of lookup can be modified after creation or not.
        /// </summary>
        abstract static bool IsImmutable { get; }
        
        abstract static Concurrency ThreadSafety { get; }
    }

    public enum Concurrency
    {
        /// <summary>
        /// Instances of this class are not thread safe
        /// </summary>
        None,

        /// <summary>
        /// Instances of this class can be read from multiple threads, but are only thread safe to modify from a single thread.
        /// </summary>
        Read,

        /// <summary>
        /// Instances of this class can be written to from multiple threads, but are only thread safe to read from a single thread.
        /// </summary>
        Write,

        /// <summary>
        /// Instances of this class are fully thread safe
        /// </summary>
        Full
    }

    public interface IPrefixLookup
    {
        abstract static IPrefixLookup<string, TValue> Create<TValue>(IEnumerable<KeyValuePair<string, TValue>> source);
        abstract static IPrefixLookup<string, TValue> Create<TValue>();
    }

}
