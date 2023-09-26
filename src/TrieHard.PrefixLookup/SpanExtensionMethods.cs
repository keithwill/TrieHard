using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrieHard.PrefixLookup
{
    public static class SpanExtensionMethods
    {

        public static void Insert<T>(this Span<T> span, T insertValue, int atIndex)
        {
            var toShift = span.Slice(atIndex);
            for (int i = 0; i < toShift.Length; i++)
            {
                toShift[i + 1] = toShift[i];
            }
            toShift[atIndex] = insertValue;
        }

        public static void CopyWithInsert<T>(this Span<T> source, Span<T> destination, T insertValue, int atIndex)
        {
            source.Slice(0, atIndex).CopyTo(destination.Slice(0, atIndex));
            destination[atIndex] = insertValue;
            source.Slice(atIndex).CopyTo(destination.Slice(atIndex + 1));
        }

    }
}
