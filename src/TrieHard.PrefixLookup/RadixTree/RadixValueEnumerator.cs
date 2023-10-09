using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using TrieHard.Collections;

namespace TrieHard.PrefixLookup.RadixTree
{

    public struct RadixValueEnumerator<T> : IEnumerable<T?>, IEnumerator<T?>
    {

        private RadixTreeNode<T>? searchNode;
        private int depth = -1;
        private T? current;
        public T? Current => current;
        private const int finishedDepth = -2;

        object IEnumerator.Current => Current!;

        public RadixValueEnumerator<T> GetEnumerator() => this;

        internal RadixValueEnumerator(RadixTreeNode<T>? collectNode)
        {
            if (collectNode is null)
            {
                depth = finishedDepth;
            }
            this.searchNode = collectNode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            switch (depth)
            {
                case -1:
                    depth++;
                    if (searchNode!.Value is not null)
                    {
                        current = searchNode.Value;
                        return true;
                    }
                    break;
                case finishedDepth: return false;
            }

            // Go until we find enough values to fill the buffer or traverse the tree
            while (true)
            {

                // DFS: Go until we find a value or bottom out on a leaf node
                while (searchNode!.ChildCount > 0)
                {
                    depth++;
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
                    if (depth == 0)
                    {
                        depth = finishedDepth;
                        return false;
                    }

                    depth--;
                    
                    var siblingLastVisited = searchNode.IndexInParent + 1;
                    searchNode = searchNode.Parent;

                    if (siblingLastVisited < searchNode!.ChildCount)
                    {
                        depth++;
                        // This finds the next sibling of the search node to continue
                        // our depth first search.
                        searchNode = searchNode.childrenBuffer[siblingLastVisited];
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


        IEnumerator<T?> IEnumerable<T?>.GetEnumerator()
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
        }
    }
}
