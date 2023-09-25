using TrieHard.Collections;

namespace TrieHard.PrefixLookup;

public ref struct SpanSearchResult<TElement>
{
    public readonly ReadOnlySpan<TElement> Span;
    internal readonly ArrayPoolList<TElement>? BackingList;
    private int index = -1;
    private int length;
    public TElement? Current => Span[index]!;

    public static readonly TElement[] Empty = new TElement[0];

    internal SpanSearchResult(ArrayPoolList<TElement>? arrayPoolList)
    {
        this.BackingList = arrayPoolList;
        if (arrayPoolList != null)
        {
            Span = arrayPoolList.Span;
            length = Span.Length;
        }
        else
        {
            Span = Empty.AsSpan();
        }
    }

    public SpanSearchResult<TElement> GetEnumerator()
    {
        return this;
    }

    public bool MoveNext()
    {
        index++;
        return index < length;
    }

    public void Dispose()
    {
        if (BackingList is not null)
        {
            ArrayPoolList<TElement>.Return(BackingList);

        }
    }
}
