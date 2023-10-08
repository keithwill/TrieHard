using System;
using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using TrieHard.Collections;

namespace TrieHard.PrefixLookup.RadixTree
{

    /// <summary>
    /// Kept for reference. This is what it would look like to 
    /// enumerate the search results node by node DFS instead of buffering them
    /// eagerly to a pooled array backed collection.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct RadixEnumerator<T> : IEnumerable<KeyValuePair<ReadOnlyMemory<byte>, T?>>, IEnumerator<KeyValuePair<ReadOnlyMemory<byte>, T?>>
    {

        private readonly RadixTreeNode<T> collectNode;
        private RadixTreeNode<T>? searchNode;
        private Stack<(RadixTreeNode<T>, int)> stack;

        internal RadixEnumerator(RadixTreeNode<T> collectNode)
        {
            stack = new();
            this.collectNode = collectNode;
        }

        public RadixEnumerator<T> GetEnumerator() => this;

        public void Reset()
        {
            stack.Clear();
            searchNode = null;
        }

        public KeyValuePair<ReadOnlyMemory<byte>, T?> Current => searchNode!.AsKeyValuePair();

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (searchNode == null)
            {
                searchNode = collectNode;
                if (searchNode.Value is not null)
                {
                    return true;
                }
            }

            // Go until we find a value or transverse the rest of the tree
            while (true) 
            {
                // DFS: Go until we find a value or bottom out on a leaf node
                while (true) 
                {                    
                    if (searchNode!.ChildCount > 0)
                    {
                        //depth++;
                        stack.Push((searchNode, 1));

                        //Push((searchNode, 1));
                        searchNode = searchNode.childrenBuffer[0];
                        if (searchNode.Value is not null) return true;
                        continue;
                    }
                    break;
                }

                // We made it to a leaf. Backtrack and find next sibling
                // of our search node (in it parent, by getting it from the stack)
                (RadixTreeNode<T> Parent, int ChildrenVisited) upLevel;

                while (true)
                {
                    if (stack.Count == 0) {
                        return false; 
                    }
                    upLevel = stack.Pop();
                    if (upLevel.ChildrenVisited < upLevel.Parent.ChildCount)
                    {
                        //depth++;
                        stack.Push((upLevel.Parent, upLevel.ChildrenVisited + 1));
                        searchNode = upLevel.Parent.childrenBuffer[upLevel.ChildrenVisited];
                        if (searchNode.Value is not null) return true;
                        break;
                    }
                }
            }
        }

        IEnumerator<KeyValuePair<ReadOnlyMemory<byte>, T?>> IEnumerable<KeyValuePair<ReadOnlyMemory<byte>, T?>>.GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        public void Dispose()
        {

        }
    }
}
