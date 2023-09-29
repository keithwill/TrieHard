using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrieHard.Abstractions
{

    /// <summary>
    /// The interface that all of the TrieHard lookup implementations adhere to. Most lookups have methods
    /// that will perform better if this interface is not used directly; this interface exists to document
    /// the expected interface of each implementation and to simplify testing of lookups. A developer consuming
    /// the lookups from this project would not be expected to use this interface directly in a consuming project.
    /// </summary>
    /// <typeparam name="TKey">The type of the key. Most lookups only support string, or bytes</typeparam>
    /// <typeparam name="TValue">
    /// The type of the value to store in the lookup.
    /// </typeparam>
    public interface IPrefixLookup<TKey, TValue> : IPrefixLookup, IEnumerable<KeyValuePair<TKey, TValue?>>
    {

        /// <summary>
        /// Gets the value of the supplied key. Unlike a Dictionary,
        /// this will return a default or null value if the value does not
        /// exist in the lookup.
        /// </summary>
        /// <param name="key">The key to match on</param>
        /// <returns>The value associated with the key or default(TElement)</returns>
        TValue? this[TKey key] { get; set; }

        /// <summary>
        /// Removes all values from the lookup. Implementations are free to decide if this
        /// clears any other internal storage or node connections.
        /// </summary>
        void Clear();

        /// <summary>
        /// The number of values stored in the lookup.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Returns the key value pairs that have a key that starts with the
        /// supplied key prefix value.
        /// </summary>
        /// <param name="keyPrefix">The value to use as a 'StartsWith' search of keys</param>
        /// <returns>An enumerable of the key value pairs matching the prefix</returns>
        IEnumerable<KeyValuePair<TKey, TValue?>> Search(TKey keyPrefix);

        /// <summary>
        /// Returns the values that have are associated with keys that start with the
        /// supplied key prefix.
        /// </summary>
        /// <param name="keyPrefix">The value to use as a 'StartsWith' search of keys</param>
        /// <returns>The value stored with each matching key</returns>
        IEnumerable<TValue?> SearchValues(TKey keyPrefix);

        /// <summary>
        /// If this type of lookup can be modified after creation or not.
        /// </summary>
        virtual static bool IsImmutable => false;

        /// <summary>
        /// The concurrency level that this lookup is expected to exhibit.
        /// </summary>
        virtual static Concurrency ThreadSafety => Concurrency.None;

        /// <summary>
        /// If the lookup provides results in a sorted order. Some implementations
        /// may use strategies for storage or retrieval that do not guarantee an implicit
        /// sort order of keys returned.
        /// </summary>
        virtual static bool IsSorted => false;
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
        /// <summary>
        /// Creates an instance of this type of prefix and populates it with the supplied key and value pairs.
        /// This method exists for testing the lookups abstractly.
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="source"></param>
        virtual static IPrefixLookup<string, TValue?> Create<TValue>(IEnumerable<KeyValuePair<string, TValue?>> source) => throw new NotImplementedException();

        /// <summary>
        /// Creates an instance of this type of prefix. Used for testing purposes.
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="source"></param>
        virtual static IPrefixLookup<string, TValue?> Create<TValue>() => throw new NotImplementedException();
    }

}
