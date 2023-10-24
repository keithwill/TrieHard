using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using TrieHard.Collections;

namespace TrieHard.PrefixLookup;

/// <summary>
/// A collection for storing values keyed by string that offers efficient lookup by key prefixes.
/// </summary>
/// <remarks>
/// Implemented as a Radix Tree.
/// </remarks>
/// <seealso>https://en.wikipedia.org/wiki/Radix_tree</seealso>
/// <typeparam name="T"></typeparam>
public class PrefixLookup<T> : IPrefixLookup<T>
{
    public static bool IsImmutable => false;
    public static Concurrency ThreadSafety => Concurrency.Read;
    public static bool IsSorted => true;

    private Node<T?> root;

    public PrefixLookup()
    {
        root = new Node<T?>();
    }

    public int Count => root.GetValuesCount();

    public T? this[string key]
    {
        get
        {
            Span<byte> keyBuffer = stackalloc byte[key.Length * 4];
            Span<byte> keySpan = GetKeyStringBytes(key, keyBuffer);
            return Get(keySpan);
        }
        set
        {
            Span<byte> keyBuffer = stackalloc byte[key.Length * 4];
            Span<byte> keySpan = GetKeyStringBytes(key, keyBuffer);
            Set(keySpan, value);
        }
    }

    public T? this[ReadOnlySpan<byte> key]
    {
        get
        {
            return Get(key);
        }
        set
        {
            Set(key, value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<byte> GetKeyStringBytes(string key, Span<byte> buffer)
    {
        Utf8.FromUtf16(key, buffer, out var _, out var bytesWritten, false, true);
        return buffer.Slice(0, bytesWritten);
    }


    public void Set(ReadOnlySpan<byte> keyBytes, T? value)
    {
        root.SetValue(ref root, keyBytes, value);
    }

    public T? Get(ReadOnlySpan<byte> key)
    {
        return root.Get(key);
    }

    public void Clear()
    {
        root.Reset();
    }

    public IEnumerator<KeyValue<T?>> GetEnumerator()
    {
        return Search(ReadOnlySpan<byte>.Empty).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static IPrefixLookup<TValue?> Create<TValue>()
    {
        return new PrefixLookup<TValue?>();
    }

    public static IPrefixLookup<TValue?> Create<TValue>(IEnumerable<KeyValue<TValue?>> source)
    {
        var result = new PrefixLookup<TValue?>();
        foreach (var kvp in source)
        {
            result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    public ValueEnumerator SearchValues(string keyPrefix)
    {
        Span<byte> keyBuffer = stackalloc byte[keyPrefix.Length * 4];
        keyBuffer = GetKeyStringBytes(keyPrefix, keyBuffer);
        return SearchValues(keyBuffer);
    }

    public ValueEnumerator SearchValues(ReadOnlySpan<byte> keyPrefix)
    {
        Node<T?>? matchingNode = keyPrefix.Length == 0 ? root : root.FindPrefixMatch(keyPrefix);
        return new ValueEnumerator(matchingNode);
    }

    public Enumerator Search(string keyPrefix)
    {
        Span<byte> keyBuffer = stackalloc byte[keyPrefix.Length * 4];
        keyBuffer = GetKeyStringBytes(keyPrefix, keyBuffer);
        return Search(keyBuffer);
    }

    public Enumerator Search(ReadOnlySpan<byte> keyPrefix)
    {
        Node<T?>? matchingNode = keyPrefix.Length == 0 ? root : root.FindPrefixMatch(keyPrefix);
        return new Enumerator(matchingNode);
    }

    IEnumerator<KeyValue<T?>> IEnumerable<KeyValue<T?>>.GetEnumerator()
    {
        return Search(string.Empty).GetEnumerator();
    }

    IEnumerable<KeyValue<T?>> IPrefixLookup<T>.Search(string keyPrefix)
    {
        return Search(keyPrefix);
    }

    IEnumerable<T> IPrefixLookup<T>.SearchValues(string keyPrefix)
    {
        return SearchValues(keyPrefix);
    }


    #region Enumerators

    public struct Enumerator : IEnumerable<KeyValue<T?>>, IEnumerator<KeyValue<T?>>
    {

        private Node<T?>? searchNode;
        private Stack<(Node<T?>[] Siblings, int Index)>? stack;
        private KeyValue<T?> current;
        public KeyValue<T?> Current => current;

        object IEnumerator.Current => Current;

        public Enumerator GetEnumerator() => this;

        internal Enumerator(Node<T?>? collectNode)
        {
            searchNode = collectNode;
        }

        private static readonly ConcurrentQueue<Stack<(Node<T?>[] Siblings, int Index)>> stackPool = new();

        private Stack<(Node<T?>[] Siblings, int Index)> RentStack()
        {
            if (stackPool.TryDequeue(out var stack))
            {
                return stack;
            }
            return new Stack<(Node<T?>[] Siblings, int Index)>();
        }

        private void ReturnStack(Stack<(Node<T?>[] Siblings, int Index)> stack)
        {
            stack.Clear();
            stackPool.Enqueue(stack);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (searchNode is null) return false;
            if (stack == null)
            {
                stack = RentStack();
                if (searchNode!.Value is not null)
                {
                    current = searchNode.AsKeyValue();
                    return true;
                }
            }

            // Go until we find enough values to fill the buffer or traverse the tree
            while (true)
            {

                // DFS: Go until we find a value or bottom out on a leaf node
                while (searchNode!.childrenBuffer.Length > 0)
                {
                    stack.Push((searchNode.childrenBuffer, 0));
                    searchNode = searchNode.childrenBuffer[0];
                    if (searchNode.Value is not null)
                    {
                        current = searchNode.AsKeyValue();
                        return true;
                    }
                }

                // We made it to a leaf. Backtrack and find next sibling
                // of our search node

                while (true)
                {
                    if (stack.Count == 0)
                    {
                        searchNode = null;
                        var stackTmp = stack;
                        stack = null;
                        ReturnStack(stackTmp);
                        return false;
                    }

                    var parentStack = stack.Pop();
                    var siblings = parentStack.Siblings;
                    var nextSiblingIndex = parentStack.Index + 1;

                    if (nextSiblingIndex < siblings.Length)
                    {
                        stack.Push((parentStack.Siblings, nextSiblingIndex));
                        searchNode = siblings[nextSiblingIndex];

                        if (searchNode.Value is not null)
                        {
                            current = searchNode.AsKeyValue();
                            return true;
                        }
                        break;
                    }
                }
            }
        }


        IEnumerator<KeyValue<T?>> IEnumerable<KeyValue<T?>>.GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        void IDisposable.Dispose()
        {
            if (stack is not null)
            {
                ReturnStack(stack);
                stack = null;
            }
        }
    }

    public struct ValueEnumerator : IEnumerable<T>, IEnumerator<T>
    {

        private Node<T?>? searchNode;
        private Stack<(Node<T?>[] Siblings, int Index)>? stack;
        private T current = default!;
        public T Current => current;

        object IEnumerator.Current => Current!;

        public ValueEnumerator GetEnumerator() => this;

        internal ValueEnumerator(Node<T?>? collectNode)
        {
            searchNode = collectNode;
        }

        private static readonly ConcurrentQueue<Stack<(Node<T?>[] Siblings, int Index)>> stackPool = new();

        private Stack<(Node<T?>[] Siblings, int Index)> RentStack()
        {
            if (stackPool.TryDequeue(out var stack))
            {
                return stack;
            }
            return new Stack<(Node<T?>[] Siblings, int Index)>();
        }

        private void ReturnStack(Stack<(Node<T?>[] Siblings, int Index)> stack)
        {
            stack.Clear();
            stackPool.Enqueue(stack);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (searchNode is null) return false;
            if (stack == null)
            {
                stack = RentStack();
                if (searchNode!.Value is not null)
                {
                    current = searchNode.Value;
                    return true;
                }
            }

            // Go until we find enough values to fill the buffer or traverse the tree
            while (true)
            {

                // DFS: Go until we find a value or bottom out on a leaf node
                while (searchNode!.childrenBuffer.Length > 0)
                {
                    stack.Push((searchNode.childrenBuffer, 0));
                    searchNode = searchNode.childrenBuffer[0];
                    if (searchNode.Value is not null)
                    {
                        current = searchNode.Value;
                        return true;
                    }
                }

                // We made it to a leaf. Backtrack and find next sibling
                // of our search node

                while (true)
                {
                    if (stack.Count == 0)
                    {
                        searchNode = null;
                        var stackTmp = stack;
                        stack = null;
                        ReturnStack(stackTmp);
                        return false;
                    }

                    var parentStack = stack.Pop();
                    var siblings = parentStack.Siblings;
                    var nextSiblingIndex = parentStack.Index + 1;

                    if (nextSiblingIndex < siblings.Length)
                    {
                        stack.Push((parentStack.Siblings, nextSiblingIndex));
                        searchNode = siblings[nextSiblingIndex];

                        if (searchNode.Value is not null)
                        {
                            current = searchNode.Value;
                            return true;
                        }
                        break;
                    }
                }
            }
        }


        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        void IDisposable.Dispose()
        {
            if (stack is not null)
            {
                ReturnStack(stack);
                stack = null;
            }
        }
    }

    #endregion















}
