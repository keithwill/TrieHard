using System;
using System.Runtime.InteropServices;

namespace TrieHard.Collections
{
    /// <summary>
    /// A zero-allocation view of a byte sequence stored in unmanaged memory by
    /// <see cref="UnsafeNativeSpanTrie"/>.
    /// <para>
    /// The lifetime of the underlying bytes is tied to the owning trie: the span remains
    /// valid until the key is overwritten with a larger value, or until the trie is disposed
    /// or cleared. Callers should not hold a <see cref="NativeByteSpan"/> beyond those events.
    /// </para>
    /// <para>
    /// Use <see cref="AsSpan"/> to obtain a <see cref="ReadOnlySpan{T}"/> of the bytes, or
    /// call <see cref="ToArray"/> to copy them into a new managed array.
    /// </para>
    /// </summary>
    public readonly unsafe struct NativeByteSpan
    {
        internal readonly void* Pointer;

        /// <summary>The number of bytes in this span.</summary>
        public readonly int Length;

        internal NativeByteSpan(void* pointer, int length)
        {
            Pointer = pointer;
            Length = length;
        }

        /// <summary>
        /// Returns a <see cref="ReadOnlySpan{T}"/> over the stored bytes.
        /// The span is only valid for the lifetime described on <see cref="NativeByteSpan"/>.
        /// </summary>
        public ReadOnlySpan<byte> AsSpan() => new ReadOnlySpan<byte>(Pointer, Length);

        /// <summary>Copies the stored bytes into a new managed <c>byte[]</c>.</summary>
        public byte[] ToArray() => AsSpan().ToArray();
    }
}
