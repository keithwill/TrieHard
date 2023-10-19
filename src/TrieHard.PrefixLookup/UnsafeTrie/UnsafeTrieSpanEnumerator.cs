using System;
using TrieHard.Collections;

public unsafe struct UnsafeTrieSpanEnumerator<T>
{

    private HybridStack<byte> keyStack;
    private HybridStack<NodeTransversal> nodeStack;

    private UnsafeTrie<T>? trie;
    private readonly UnsafeTrieNode collectNode;

    private UnsafeTrieNode currentNodeBacking;

    private Kvp<byte, T?> currentKeyValue;

    public Kvp<byte, T?> Current => currentKeyValue;

    private bool returnRootValue = false;

    /// <summary>
    /// This enumerator uses various optimizations to minimize heap allocations when it is used in a normal
    /// foreach loop. It does not materialize strings for returned keys, instead it returns ReadOnlySpans of bytes
    /// that live for the length of each loop body.
    /// </summary>
    /// <param name="trie">The trie to enumerate</param>
    /// <param name="prefix">The key prefix that matches the collect node</param>
    /// <param name="collectNodePtr">The node to start collecting values at</param>
    public UnsafeTrieSpanEnumerator(UnsafeTrie<T> trie, scoped ReadOnlySpan<byte> prefix, in UnsafeTrieNode collectNodePtr)
    {
        keyStack = new HybridStack<byte>();
        keyStack.Write(prefix);

        this.trie = trie;

        collectNode = collectNodePtr;
        currentNodeBacking = collectNode;

        nodeStack = new HybridStack<NodeTransversal>();
        returnRootValue = TrySetCurrentKeyValue(collectNode);
    }

    /// <summary>
    /// Checks the supplied node for a value, and if it has one
    /// sets the current key value to the Current property. If
    /// a current KeyValue is set, then the passed node will also
    /// be set as the current node to continue iteration from.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    private bool TrySetCurrentKeyValue(in UnsafeTrieNode node)
    {
        if (node.ValueLocation == -1) return false;

        T? value = trie!.Values[node.ValueLocation];
        currentKeyValue = new Kvp<byte, T?>(value, in keyStack);
        currentNodeBacking = node;
        return true;
    }

    public static UnsafeTrieSpanEnumerator<T> Empty()
    {
        return new UnsafeTrieSpanEnumerator<T>();
    }

    public UnsafeTrieSpanEnumerator()
    {
    }

    public bool MoveNext()
    {
        if (returnRootValue)
        {
            returnRootValue = false;
            return true;
        }

        if (trie == null) return false;

        ref readonly UnsafeTrieNode current = ref currentNodeBacking;

        while (true)
        {

            if (current.ChildCount > 0)
            {
                Push(current, 0);
                current = ref current.GetChildRef(0);
                if (TrySetCurrentKeyValue(current)) return true;
                continue;
            }

            //Backtrack until we find a node to descend on
            while (true)
            {
                if (current.Is(collectNode))
                {
                    // We made it back to the top
                    trie = null;
                    return false;
                }

                ref readonly NodeTransversal lastTransversal = ref Pop();
                UnsafeTrieNode parentNode = lastTransversal.FromParent;
                //fix // We are pointing to a ref of an element in the 'stack'
                // but that means our 'currentNode' points to something else as the stack is changed

                if (lastTransversal.ToChildIndex >= parentNode.ChildCount - 1)
                {
                    // Parent has no more children to transverse
                    currentNodeBacking = parentNode;
                    current = ref currentNodeBacking;
                    continue;
                }
                else
                {
                    // From the current Node's parent, descend into the next sibling of the current node
                    byte childIndex = lastTransversal.ToChildIndex;
                    childIndex++;
                    
                    Push(parentNode, childIndex);
                    current = ref parentNode.GetChildRef(childIndex);

                    if (TrySetCurrentKeyValue(current)) return true;
                    break;
                }
            }
        }
    }

    private void Push(in UnsafeTrieNode fromParent, byte toChildIndex)
    {
        byte childKey = fromParent.GetChildKey(toChildIndex);
        keyStack.Push(childKey);
        var transversal = new NodeTransversal(fromParent, toChildIndex, childKey);
        nodeStack.Push(transversal);
    }

    private ref readonly NodeTransversal Pop()
    {
        keyStack.Pop();
        return ref nodeStack.Pop();
    }

    private readonly struct NodeTransversal
    {

        public NodeTransversal(in UnsafeTrieNode parent, byte childIndex, byte childKey)
        {
            FromParent = parent;
            ToChildIndex = childIndex;
            this.ChildKey = childKey;
        }
        public readonly UnsafeTrieNode FromParent;
        public readonly byte ToChildIndex;
        public readonly byte ChildKey;

    }

    public void Dispose()
    {
        keyStack.Dispose();
        nodeStack.Dispose();
    }

}

public unsafe ref struct CompactTrieNodeSpanEnumerable<T>
{
    private readonly UnsafeTrie<T> trie;
    private readonly ReadOnlySpan<byte> keyPrefix;
    private readonly ref readonly UnsafeTrieNode node;

    public CompactTrieNodeSpanEnumerable(UnsafeTrie<T> trie, ReadOnlySpan<byte> keyPrefix, in UnsafeTrieNode node)
    {
        this.trie = trie;
        this.keyPrefix = keyPrefix;
        this.node = ref node;
    }

    public UnsafeTrieSpanEnumerator<T> GetEnumerator()
    {
        if (trie == null)
        {
            return UnsafeTrieSpanEnumerator<T>.Empty();
        }
        return new UnsafeTrieSpanEnumerator<T>(trie, keyPrefix, node);
    }

}