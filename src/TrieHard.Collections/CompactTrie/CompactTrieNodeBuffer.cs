using System;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{

    /// <summary>
    /// A backing buffer for storing CompactTrieNodes instead of storing them in managed arrays.
    /// </summary>
    internal unsafe class CompactTrieNodeBuffer : IDisposable
    {
        private bool isDisposed = false;
        private void* pointer;
        private byte* currentAddress;

        private long size;
        private long consumed;

        public CompactTrieNodeBuffer(long size)
        {
            var sizeNint = (nuint)Convert.ToUInt64(size);
            pointer = NativeMemory.Alloc(sizeNint);
            currentAddress = (byte*)pointer;
            this.size = size;
        }
        public long Size => size;
        public byte* CurrentAddress => currentAddress;


        public bool IsAvailable(long sizeRequested)
        {
            return (consumed + sizeRequested) <= size;
        }

        public void Advance(int bytesToAdvance)
        {
            consumed += bytesToAdvance;
            currentAddress += bytesToAdvance;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            NativeMemory.Free(pointer);
            isDisposed = true;
            GC.SuppressFinalize(this);
        }

        ~CompactTrieNodeBuffer()
        {
            if (!isDisposed)
            {
                Dispose();
            }
        }
    }


}
