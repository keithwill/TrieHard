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
            for(int i = 0; i < destination.Length; i++)
            {
                if (i == atIndex)
                {
                    destination[i] = insertValue;
                }
                else if (i < atIndex)
                {
                    destination[i] = source[i];
                }
                else
                {
                    destination[i] = source[i - 1];
                }
            }

            //if (atIndex > 0)
            //{
            //    var preSource = source.Slice(0, atIndex);
            //    var preDestination = destination.Slice(0, atIndex);
            //    preSource.CopyTo(preDestination);
            //    //source.Slice(0, atIndex).CopyTo(destination.Slice(0, atIndex));
            //}
            //destination[atIndex] = insertValue;
            //if (atIndex + 1 < destination.Length)
            //{
            //    var postSource = source.Slice(atIndex + 1);
            //    var postDestination = destination.Slice(atIndex + 1);
            //    postSource.CopyTo(postDestination);
            //}
            //if (atIndex > 1)
            //{
            //    ;
            //}
        }

    }
}
