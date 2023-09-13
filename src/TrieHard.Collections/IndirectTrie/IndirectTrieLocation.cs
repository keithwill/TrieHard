using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrieHard.Collections
{
    public readonly record struct IndirectTrieLocation(int Bucket, int Index)
    {
        public static ref readonly IndirectTrieLocation None => ref none;
        private static readonly IndirectTrieLocation none = new(NO_RESULT_BUCKET, -1);
        public const int NO_RESULT_BUCKET = -1;
        public bool Exists => Bucket != NO_RESULT_BUCKET;
        public static ref readonly IndirectTrieLocation Root => ref root;
        private static readonly IndirectTrieLocation root = new IndirectTrieLocation(0, 0);
    }

}
