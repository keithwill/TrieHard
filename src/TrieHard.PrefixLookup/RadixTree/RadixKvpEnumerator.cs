using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using TrieHard.Collections;

namespace TrieHard.PrefixLookup.RadixTree
{

    public struct RadixKvpEnumerator<T> : IEnumerable<KeyValuePair<ReadOnlyMemory<byte>, T?>>, IEnumerator<KeyValuePair<ReadOnlyMemory<byte>, T?>>
    {

        private RadixTreeNode<T> searchNode;
        private int depth = -1;
        private const int finishedDepth = -2;
        private KeyValuePair<ReadOnlyMemory<byte>, T?> current;
        public KeyValuePair<ReadOnlyMemory<byte>, T?> Current => current;

        object IEnumerator.Current => Current;

        public RadixKvpEnumerator<T> GetEnumerator() => this;

        internal RadixKvpEnumerator(RadixTreeNode<T>? collectNode)
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
                    if (searchNode.Value is not null)
                    {
                        current = searchNode.AsKeyValuePair();
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
                        current = searchNode.AsKeyValuePair();
                        return true;
                    }
                }

                // We made it to a leaf. Backtrack and find next sibling
                // of our search node

                while (true)
                {
                    if (depth == 0)
                    {
                        //depth = FinishedDepth;
                        return false;
                    }

                    depth--;
                    var siblingLastVisited = searchNode.IndexInParent + 1;
                    searchNode = searchNode.Parent;

                    if (siblingLastVisited < searchNode.ChildCount)
                    {
                        depth++;
                        // This finds the next sibling of the search node to continue
                        // our depth first search.
                        searchNode = searchNode.childrenBuffer[siblingLastVisited];
                        if (searchNode.Value is not null)
                        {
                            current = searchNode.AsKeyValuePair();
                            return true;
                        }
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

        public void Reset()
        {
            throw new NotImplementedException();
        }

        void IDisposable.Dispose()
        {
        }
    }
}
