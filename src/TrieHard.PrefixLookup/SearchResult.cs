using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrieHard.Collections;

namespace TrieHard.PrefixLookup
{
    public struct SearchResult<TElement> : IEnumerable<TElement?>, IEnumerator<TElement?>, IDisposable
    {
        private readonly ArrayPoolList<TElement>? BackingList;
        internal readonly TElement[] items;
        private int index = -1;
        private int length;

        public static readonly TElement[] Empty = new TElement[0];
        public TElement? Current => items[index];
        object IEnumerator.Current => items[index]!;
        public SearchResult<TElement> GetEnumerator() => this;
        internal SearchResult(ArrayPoolList<TElement>? arrayPoolList)
        {
            if (arrayPoolList is not null)
            {
                BackingList = arrayPoolList;
                length = BackingList.Count;
                items = arrayPoolList.Items;
            }
            else
            {
                length = 0;
                items = Empty;
            }
        }
        IEnumerator<TElement?> IEnumerable<TElement?>.GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        public bool MoveNext()
        {
            index++;
            return index < length;
        }
        public void Reset()
        {
            // Since we are the IEnumerable AND the Enumerator, we've likely already
            // returned out backing array to the pool
            throw new NotImplementedException("The search result may only be enumerated once");
        }
        public void Dispose()
        {
            if (BackingList != null)
            {
                BackingList.Dispose();
            }
        }
    }
}
