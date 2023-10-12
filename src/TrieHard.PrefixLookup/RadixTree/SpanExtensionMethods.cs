using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrieHard.PrefixLookup.RadixTree
{
    internal static class SpanExtensionMethods
    {
        public static void CopyWithInsert<T>(this Span<T> source, Span<T> destination, T insertValue, int atIndex)
        {
            source.Slice(0, atIndex).CopyTo(destination.Slice(0, atIndex));
            destination[atIndex] = insertValue;
            source.Slice(atIndex).CopyTo(destination.Slice(atIndex + 1));
        }

    }
}
