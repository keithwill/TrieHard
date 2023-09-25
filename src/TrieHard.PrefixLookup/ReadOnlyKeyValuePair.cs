using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrieHard.PrefixLookup;

public readonly struct ReadOnlyKeyValuePair<TElement>
{
    public ReadOnlyKeyValuePair(byte[] keyBuffer, short keyLength, TElement value)
    {
        Value = value;
        KeyBuffer = keyBuffer;
        KeyLength = keyLength;
    }
    public readonly TElement Value;
    public ReadOnlySpan<byte> Key => KeyBuffer.AsSpan(0, KeyLength);
    private readonly byte[] KeyBuffer;
    private readonly short KeyLength;
}
