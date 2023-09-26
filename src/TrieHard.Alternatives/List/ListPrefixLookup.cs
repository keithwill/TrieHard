using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrieHard.Abstractions;

namespace TrieHard.Alternatives.List
{
    /// <summary>
    /// This is what many developers end up doing for lookups when they expect the
    /// number of elements to be reasonable or small.
    /// </summary>
    public class ListPrefixLookup<T> : IPrefixLookup<string, T>
    {

        public static bool IsImmutable => false;

        public static Concurrency ThreadSafety => Concurrency.None;

        public static bool IsSorted => true;

        private List<KeyValuePair<string, T?>> values = new();
        public T? this[string key] {
            get => values.FirstOrDefault(x => x.Key == key).Value;
            set
            {
                var newKvp = new KeyValuePair<string, T?>(key, value); ;
                for (int i = 0; i < values.Count; i++)
                {
                    if (values[i].Key == key)
                    {
                        values[i] = newKvp;
                        return;
                    }
                }
                values.Add(newKvp);
            }
        }


        public int Count => values.Count;

        public static IPrefixLookup<string, TValue?> Create<TValue>(IEnumerable<KeyValuePair<string, TValue?>> source)
        {
            var lookup = new ListPrefixLookup<TValue?>();
            lookup.values = source.OrderBy(x => x.Key).ToList();
            return lookup;
        }

        public static IPrefixLookup<string, TValue?> Create<TValue>()
        {
            return new ListPrefixLookup<TValue?>();
        }

        public void Clear()
        {
            values.Clear();
        }

        public IEnumerator<KeyValuePair<string, T?>> GetEnumerator()
        {
            return this.values.GetEnumerator();
        }

        public IEnumerable<KeyValuePair<string, T?>> Search(string keyPrefix)
        {
            return values.Where(x => x.Key.StartsWith(keyPrefix));
        }

        public IEnumerable<T?> SearchValues(string keyPrefix)
        {
            return values.Where(x => x.Key.StartsWith(keyPrefix)).Select(x => x.Value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
