using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// This is a hybrid stack that uses an inline array for smaller quantities
/// and expands to native allocated memory when the inline array is full. Its designed specifically
/// for holding identifier sized values efficiently.
/// </summary>
/// <typeparam name="T">The unmanaged type to store</typeparam>
public unsafe struct HybridStack<T> where T : unmanaged
{
    public int Count;
    public int Capacity;

    internal HybridStackInlineArray<T> InlineBuffer = new HybridStackInlineArray<T>();
    internal T* NativeBuffer;

    public Span<T> Span => Capacity <= InlineSize ? 
        MemoryMarshal.CreateSpan(ref Unsafe.As<HybridStackInlineArray<T>, T>(ref InlineBuffer), InlineSize) :
        new Span<T>(NativeBuffer, Capacity);

    private const int InlineSize = HybridStackInlineArray<T>.Size;

    public HybridStack()
    {
        Count = 0;
        Capacity = InlineSize;
    }

    /// <summary>
    /// Makes a raw write to the start of the underlying buffer.
    /// Just useful for setting initial values without having to call Push repeatedly
    /// </summary>
    /// <param name="values"></param>
    public void Write(scoped ReadOnlySpan<T> values)
    {
        GrowIfNeeded(values.Length);
        values.CopyTo(Span.Slice(0, values.Length));       
        if (Count < values.Length)
        {
            Count = values.Length;
        }
    }

    private void GrowIfNeeded(int requestedSizeToAdd)
    {
        var originalCapacity = Capacity;
        var newSize = Count + requestedSizeToAdd;
        if (newSize > originalCapacity)
        {
            Span<T> buffer = Span;
            int newSizeBytes = sizeof(T) * originalCapacity * 2;
            var byteLength = (nuint)Convert.ToUInt64(newSizeBytes);
            T* tmp = (T*)NativeMemory.Alloc(byteLength);
            var tmpSpan = new Span<T>(tmp, Count);
            buffer.CopyTo(tmpSpan);
            if (originalCapacity > InlineSize)
            {
                NativeMemory.Free(NativeBuffer);
            }
            NativeBuffer = tmp;
            Capacity = newSize;
        }
    }

    public void Push(T value)
    {
        GrowIfNeeded(1);
        Span[Count] = value;
        Count++;
    }

    public ref readonly T Pop()
    {
        int lastIndex = Count - 1;
        Count--;
        return ref Span[lastIndex];
    }

    public void Dispose()
    {
        if (Capacity > InlineSize)
        {
            NativeMemory.Free(NativeBuffer);
        }
    }

}