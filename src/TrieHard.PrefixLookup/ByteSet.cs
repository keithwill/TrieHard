using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TrieHard.PrefixLookup
{
    /// <summary>
    /// Eight 32 bit integers which contain flags for all possible byte values.
    /// This can be used instead of storing 256 bytes in cases where an existence
    /// check is the only thing needed.
    /// </summary>
    public struct ByteSet
    {
        public BitVector32 Exists1;
        public BitVector32 Exists2;
        public BitVector32 Exists3;
        public BitVector32 Exists4;
        public BitVector32 Exists5;
        public BitVector32 Exists6;
        public BitVector32 Exists7;
        public BitVector32 Exists8;

        public static readonly int[] BitMasks = new int[32];
        public static readonly int[] Sections = new int[256];
        public static readonly int[] Masks = new int[256];

        static ByteSet()
        {
            int mask = BitVector32.CreateMask();
            for (int i = 1; i < 32; i++)
            {
                BitMasks[i] = BitVector32.CreateMask(mask);
                mask = BitMasks[i];
            }
            for (int i = 0; i < 256; i++)
            {
                Sections[i] = (int)Math.Floor(i / 32M);
                Masks[i] = BitMasks[i - (Sections[i] * 32)];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(byte flag, bool value)
        {
            switch (Sections[flag])
            {
                case 0: Exists1[Masks[flag]] = value; break;
                case 1: Exists2[Masks[flag]] = value; break;
                case 2: Exists3[Masks[flag]] = value; break;
                case 3: Exists4[Masks[flag]] = value; break;
                case 4: Exists5[Masks[flag]] = value; break;
                case 5: Exists6[Masks[flag]] = value; break;
                case 6: Exists7[Masks[flag]] = value; break;
                case 7: Exists8[Masks[flag]] = value; break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(byte flag)
        {
            switch (Sections[flag])
            {
                case 0: return Exists1[Masks[flag]];
                case 1: return Exists2[Masks[flag]];
                case 2: return Exists3[Masks[flag]];
                case 3: return Exists4[Masks[flag]];
                case 4: return Exists5[Masks[flag]];
                case 5: return Exists6[Masks[flag]];
                case 6: return Exists7[Masks[flag]];
                case 7: return Exists8[Masks[flag]];
                default: return false;
            }
        }
    }
}
