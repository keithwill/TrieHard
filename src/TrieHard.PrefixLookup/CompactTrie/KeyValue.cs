using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public unsafe struct KeyValue<TKey, TValue> where TKey : unmanaged
{
    private const int InlineSize = HybridStackInlineArray<TKey>.Size;
    public ReadOnlySpan<TKey> Key
    {
        get
        {
            if (stack.Capacity > InlineSize)
            {
                return new Span<TKey>(stack.NativeBuffer, stack.Count);
            }
            // stack.Span should work, but there is some quirk with
            // escaping a Span of inline array data and it only fills the first
            // element of data.
            return MemoryMarshal.CreateSpan(ref Unsafe.As<HybridStackInlineArray<TKey>, TKey>(ref stack.InlineBuffer), stack.Count);
        }
    }

    public readonly TValue Value;
    private HybridStack<TKey> stack;

    public KeyValue(TValue value, in HybridStack<TKey> stack)
    {
        this.Value = value;
        this.stack = stack;
    }

}