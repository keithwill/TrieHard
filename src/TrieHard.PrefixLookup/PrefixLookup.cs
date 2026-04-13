using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using TrieHard.Collections;

namespace TrieHard.PrefixLookup;

/// <summary>
/// A general-purpose sorted prefix lookup backed by a radix tree that stores values keyed by
/// string and supports efficient prefix-based retrieval.
/// <para>
/// Keys are strings stored as UTF-8 bytes internally. An optional case-insensitive mode
/// (see <see cref="PrefixLookup{T}(bool)"/>) folds all keys to upper-case (invariant culture)
/// before storage and lookup. Raw UTF-8 byte keys are also accepted via the
/// <see cref="this[ReadOnlySpan{byte}]"/> indexer and <see cref="Set"/>/<see cref="Get"/> overloads.
/// </para>
/// <para>
/// <b>Thread safety:</b> concurrent reads from multiple threads are safe. Writes must be
/// performed from a single thread.
/// </para>
/// <para>
/// All enumeration methods return results in lexicographic (sorted) key order.
/// </para>
/// </summary>
/// <typeparam name="T">The type of value stored with each key.</typeparam>
/// <seealso href="https://en.wikipedia.org/wiki/Radix_tree">Radix tree — Wikipedia</seealso>
public class PrefixLookup<T> : IPrefixLookup<T>
{
    /// <inheritdoc/>
    public static bool IsImmutable => false;
    /// <inheritdoc/>
    public static Concurrency ThreadSafety => Concurrency.Read;
    /// <inheritdoc/>
    public static bool IsSorted => true;

    private Node<T?> root;
    private bool isCaseSensitive = true;

    /// <summary>
    /// Initializes a new <see cref="PrefixLookup{T}"/>.
    /// </summary>
    /// <param name="caseSensitive">
    /// <c>true</c> (the default) to treat keys as case-sensitive.
    /// <c>false</c> to fold all keys to upper-case (invariant culture) before storage and lookup,
    /// making the collection case-insensitive.
    /// </param>
    public PrefixLookup(bool caseSensitive = true)
    {
        root = new Node<T?>();
        isCaseSensitive = caseSensitive;
    }

    /// <inheritdoc/>
    public int Count => root.GetValuesCount();

    /// <inheritdoc/>
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

    /// <summary>
    /// Gets or sets the value associated with the UTF-8 byte key <paramref name="key"/>.
    /// Returns <c>default</c> when the key is not present.
    /// </summary>
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
    private Span<byte> GetKeyStringBytes(string key, Span<byte> buffer)
    {
        if (!isCaseSensitive)
        {
            Span<char> upper = stackalloc char[key.Length];
            key.AsSpan().ToUpperInvariant(upper);
            Utf8.FromUtf16(upper, buffer, out var _, out var bytesWrittenUpper, false, true);
            return buffer.Slice(0, bytesWrittenUpper);
        }

        Utf8.FromUtf16(key, buffer, out var _, out var bytesWritten, false, true);
        return buffer.Slice(0, bytesWritten);
    }


    /// <summary>Sets the value for the UTF-8 byte key <paramref name="keyBytes"/>.</summary>
    public void Set(ReadOnlySpan<byte> keyBytes, T? value)
    {
        root.SetValue(ref root, keyBytes, value);
    }

    /// <summary>
    /// Returns the value stored for the UTF-8 byte key <paramref name="key"/>, or
    /// <c>default</c> if the key is not present.
    /// </summary>
    public T? Get(ReadOnlySpan<byte> key)
    {
        return root.Get(key);
    }

    /// <inheritdoc/>
    public T? LongestPrefix(string key)
    {
        Span<byte> keyBuffer = stackalloc byte[key.Length * 4];
        Span<byte> keySpan = GetKeyStringBytes(key, keyBuffer);
        return LongestPrefix(keySpan);
    }

    /// <summary>
    /// Returns the value associated with the longest UTF-8 key in the collection that is a
    /// prefix of <paramref name="key"/>, or <c>default</c> if no such prefix exists.
    /// </summary>
    public T? LongestPrefix(ReadOnlySpan<byte> key)
    {
        return root.LongestPrefix(key);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        root.Reset();
    }

    /// <summary>Returns an enumerator that yields all key-value pairs in lexicographic order.</summary>
    public IEnumerator<KeyValue<T?>> GetEnumerator()
    {
        return Search(ReadOnlySpan<byte>.Empty).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>Creates an empty <see cref="PrefixLookup{T}"/> for the given value type.</summary>
    public static IPrefixLookup<TValue?> Create<TValue>()
    {
        return new PrefixLookup<TValue?>();
    }

    /// <summary>
    /// Creates a <see cref="PrefixLookup{T}"/> populated from <paramref name="source"/>.
    /// </summary>
    public static IPrefixLookup<TValue?> Create<TValue>(IEnumerable<KeyValue<TValue?>> source)
    {
        var result = new PrefixLookup<TValue?>();
        foreach (var kvp in source)
        {
            result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    /// <summary>
    /// Returns a value-only enumerator for all entries whose keys begin with
    /// <paramref name="keyPrefix"/>, in lexicographic order.
    /// </summary>
    public ValueEnumerator SearchValues(string keyPrefix)
    {
        Span<byte> keyBuffer = stackalloc byte[keyPrefix.Length * 4];
        keyBuffer = GetKeyStringBytes(keyPrefix, keyBuffer);
        return SearchValues(keyBuffer);
    }

    /// <summary>
    /// Returns a value-only enumerator for all entries whose UTF-8 keys begin with
    /// <paramref name="keyPrefix"/>, in lexicographic order.
    /// </summary>
    public ValueEnumerator SearchValues(ReadOnlySpan<byte> keyPrefix)
    {
        Node<T?>? matchingNode = keyPrefix.Length == 0 ? root : root.FindPrefixMatch(keyPrefix);
        return new ValueEnumerator(matchingNode);
    }

    /// <summary>
    /// Returns an enumerator that yields all key-value pairs whose keys begin with
    /// <paramref name="keyPrefix"/>, in lexicographic order.
    /// </summary>
    public Enumerator Search(string keyPrefix)
    {
        Span<byte> keyBuffer = stackalloc byte[keyPrefix.Length * 4];
        keyBuffer = GetKeyStringBytes(keyPrefix, keyBuffer);
        return Search(keyBuffer);
    }

    /// <summary>
    /// Returns an enumerator that yields all key-value pairs whose UTF-8 keys begin with
    /// <paramref name="keyPrefix"/>, in lexicographic order.
    /// </summary>
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

    /// <summary>
    /// Key-value pair enumerator returned by <see cref="PrefixLookup{T}.Search(string)"/> and
    /// <see cref="PrefixLookup{T}.GetEnumerator()"/>. Yields results in lexicographic key order.
    /// <para>
    /// The traversal stack is rented from a pool on first <see cref="MoveNext"/> call and
    /// returned to the pool on <see cref="IDisposable.Dispose"/>. Always dispose via <c>using</c>
    /// or an explicit dispose to return the stack to the pool.
    /// </para>
    /// </summary>
    public struct Enumerator : IEnumerable<KeyValue<T?>>, IEnumerator<KeyValue<T?>>
    {

        private Node<T?>? searchNode;
        private Stack<(Node<T?>[] Siblings, int Index)>? stack;
        private KeyValue<T?> current;
        /// <summary>The current key-value pair.</summary>
        public KeyValue<T?> Current => current;

        object IEnumerator.Current => Current;

        /// <summary>Returns this enumerator, enabling use in <c>foreach</c> directly on the struct.</summary>
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

        /// <summary>Advances the enumerator to the next key-value pair. Returns <c>false</c> when exhausted.</summary>
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

        /// <summary>Not supported. Always throws <see cref="NotImplementedException"/>.</summary>
        public void Reset()
        {
            throw new NotImplementedException();
        }

        /// <summary>Returns the traversal stack to the pool.</summary>
        void IDisposable.Dispose()
        {
            if (stack is not null)
            {
                ReturnStack(stack);
                stack = null;
            }
        }
    }

    /// <summary>
    /// Value-only enumerator returned by <see cref="PrefixLookup{T}.SearchValues(string)"/>.
    /// Yields values in lexicographic key order without allocating key strings.
    /// <para>
    /// The traversal stack is rented from a pool on first <see cref="MoveNext"/> call and
    /// returned to the pool on <see cref="IDisposable.Dispose"/>. Always dispose via <c>using</c>
    /// or an explicit dispose to return the stack to the pool.
    /// </para>
    /// </summary>
    public struct ValueEnumerator : IEnumerable<T>, IEnumerator<T>
    {

        private Node<T?>? searchNode;
        private Stack<(Node<T?>[] Siblings, int Index)>? stack;
        private T current = default!;
        /// <summary>The current value.</summary>
        public T Current => current;

        object IEnumerator.Current => Current!;

        /// <summary>Returns this enumerator, enabling use in <c>foreach</c> directly on the struct.</summary>
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

        /// <summary>Advances the enumerator to the next value. Returns <c>false</c> when exhausted.</summary>
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

        /// <summary>Not supported. Always throws <see cref="NotImplementedException"/>.</summary>
        public void Reset()
        {
            throw new NotImplementedException();
        }

        /// <summary>Returns the traversal stack to the pool.</summary>
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
