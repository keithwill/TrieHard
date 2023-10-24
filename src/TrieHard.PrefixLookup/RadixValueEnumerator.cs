﻿using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace TrieHard.PrefixLookup
{

    public struct RadixValueEnumerator<T> : IEnumerable<T>, IEnumerator<T>
    {

        private RadixTreeNode<T>? searchNode;
        private Stack<(RadixTreeNode<T>[] Siblings, int Index)>? stack;
        private T current = default!;
        public T Current => current;

        object IEnumerator.Current => Current!;

        public RadixValueEnumerator<T> GetEnumerator() => this;

        internal RadixValueEnumerator(RadixTreeNode<T>? collectNode)
        {
            searchNode = collectNode;
        }

        private static readonly ConcurrentQueue<Stack<(RadixTreeNode<T>[] Siblings, int Index)>> stackPool = new();

        private Stack<(RadixTreeNode<T>[] Siblings, int Index)> RentStack()
        {
            if (stackPool.TryDequeue(out var stack))
            {
                return stack;
            }
            return new Stack<(RadixTreeNode<T>[] Siblings, int Index)>();
        }

        private void ReturnStack(Stack<(RadixTreeNode<T>[] Siblings, int Index)> stack)
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
            }
        }
    }
}