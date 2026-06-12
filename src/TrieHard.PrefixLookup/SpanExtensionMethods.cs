using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TrieHard.PrefixLookup
{
    internal static class SpanExtensionMethods
    {
        /// <summary>
        /// Below this source width the scalar <see cref="CopyWithInsertScalar"/> beats the
        /// block copy for small (byte-sized) elements: a <c>memmove</c> carries a fixed
        /// setup cost (~6 ns measured) that only a handful of element copies can't amortize.
        /// The measured crossover for <c>byte[]</c> is ~width 8; narrower spans use the loop.
        /// Reference-element arrays cross over at ~width 3, so they always block-copy and
        /// don't consult this threshold.
        /// </summary>
        public const int ByteBlockCopyThreshold = 8;

        /// <summary>
        /// Copies <paramref name="source"/> into <paramref name="destination"/> while inserting
        /// <paramref name="insertValue"/> at <paramref name="atIndex"/>, using two block copies
        /// (a vectorized <c>memmove</c>) around the insertion point. Assumes <paramref name="source"/>
        /// and <paramref name="destination"/> are distinct buffers (the trie always allocates a
        /// fresh destination), so the post-insert tail copy can safely read the original source.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyWithInsert<T>(this Span<T> source, Span<T> destination, T insertValue, int atIndex)
        {
            source[..atIndex].CopyTo(destination);
            destination[atIndex] = insertValue;
            source[atIndex..].CopyTo(destination[(atIndex + 1)..]);
        }

        /// <summary>
        /// Like <see cref="CopyWithInsert"/> but picks the scalar loop for spans narrower than
        /// <see cref="ByteBlockCopyThreshold"/>. Used for small-element arrays (e.g. the
        /// <c>childFirstBytes</c> array) where the block copy's fixed cost loses at small widths.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyWithInsertThresholded<T>(this Span<T> source, Span<T> destination, T insertValue, int atIndex)
        {
            if (source.Length < ByteBlockCopyThreshold)
            {
                CopyWithInsertScalar(source, destination, insertValue, atIndex);
            }
            else
            {
                source.CopyWithInsert(destination, insertValue, atIndex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyWithInsertScalar<T>(Span<T> source, Span<T> destination, T insertValue, int atIndex)
        {
            for (int i = 0; i < destination.Length; i++)
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
        }

    }
}
