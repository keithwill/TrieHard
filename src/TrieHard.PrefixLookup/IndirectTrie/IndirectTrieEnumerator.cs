using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrieHard.Collections
{
    public struct IndirectTrieEnumerator<T> : IEnumerator<KeyValuePair<string, T>>, IEnumerator, IEnumerable<KeyValuePair<string, T>>
    {
        private IndirectTrie<T>? trie;
        private int depthFromCollectRoot;
        private IndirectTrieLocation currentLocation;
        private readonly int initialDepth;
        private KeyValuePair<string, T> current;


        internal IndirectTrieEnumerator(IndirectTrie<T> trie, IndirectTrieLocation collectRoot, int initialDepth)
        {
            this.trie = trie;
            this.currentLocation = collectRoot;
            this.initialDepth = initialDepth;
            this.depthFromCollectRoot = -1;
        }

        public void Dispose()
        {
            trie = null;
            current = default;
        }

        internal string GetFullKey(in IndirectTrieNode<T> node, int depth)
        {
            IndirectTrieNode<T> searchNode = node;
            Span<char> result = stackalloc char[depth];
            for (int i = depth - 1; i >= 0; i--)
            {
                result[i] = searchNode.Key;
                searchNode = trie.Get(searchNode.Parent);
            }
            return result.ToString();
        }

        public bool MoveNext()
        {
            var currentLoc = currentLocation;
            if (!currentLoc.Exists)
            {
                return false;
            }

            ref readonly IndirectTrieNode<T> currentNode = ref trie.Get(currentLoc);

            if (depthFromCollectRoot == -1)
            {
                depthFromCollectRoot++;
                if (currentNode.Value is not null)
                {
                    current = new KeyValuePair<string, T>(GetFullKey(currentNode, initialDepth), currentNode.Value);
                    return true;
                }
            }

            var next = trie.NextDepthFirst(currentNode, ref depthFromCollectRoot);

            while (next.Exists)
            {
                ref readonly IndirectTrieNode<T> nextNode = ref trie.Get(next);
                if (nextNode.Value is not null)
                {
                    currentLocation = next;
                    var key = GetFullKey(nextNode, initialDepth + depthFromCollectRoot);
                    current = new KeyValuePair<string, T>(key, nextNode.Value);
                    return true;
                }
                next = trie.NextDepthFirst(nextNode, ref depthFromCollectRoot);
            }
            return false;
        }


        public KeyValuePair<string, T> Current
        {
            get
            {
                return current;
            }
        }

        object System.Collections.IEnumerator.Current
        {
            get
            {
                return current;
            }
        }

        void System.Collections.IEnumerator.Reset()
        {
            this.current = default;
            this.depthFromCollectRoot = -1;
        }

        public IndirectTrieEnumerator<T> GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator()
        {
            return this;
        }
    }

}
