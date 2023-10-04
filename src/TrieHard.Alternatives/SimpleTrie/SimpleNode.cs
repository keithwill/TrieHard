using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrieHard.Collections
{


    public class SimpleNode<T>
    {
        public T? Value;
        public bool HasValue = false;
        
        public Dictionary<char, SimpleNode<T>> Children { get; set; } = new Dictionary<char, SimpleNode<T>>();

        /// <summary>
        /// Descends through child nodes until it finds the matching key and then sets the value on that node.
        /// </summary>
        /// <param name="keySegment">The portion of the key to match on</param>
        /// <param name="value">The value to set on the node or its matching children (recursive)</param>
        /// <returns>True if a new value was added to a node, or false if an existing value was overwritten</returns>
        public int Set(ReadOnlySpan<char> keySegment, T? value)
        {
            var matchingNode = GetNode(keySegment, createIfMissing: true);
            matchingNode!.Value = value;
            int valueCountChange = 0;
            if (matchingNode.HasValue)
            {
                if (value is null)
                {
                    valueCountChange = -1;
                    matchingNode.HasValue = false;
                }
            }
            else
            {
                if (value is not null)
                {
                    valueCountChange = 1;
                    matchingNode.HasValue = true;
                }
            }
            return valueCountChange;
        }

        public T? Get(ReadOnlySpan<char> keySegment)
        {
            return GetNode(keySegment)!.Value;
        }

        public IEnumerable<KeyValuePair<string, T?>> Search(string keySegment)
        {
            var matchingRoot = GetNode(keySegment);
            if (matchingRoot is null) return Enumerable.Empty<KeyValuePair<string, T?>>();
            StringBuilder keyBuilder = new();
            keyBuilder.Append(keySegment);
            return matchingRoot.CollectValues(keySegment.AsMemory(), keyBuilder);
        }

        public IEnumerable<KeyValuePair<string, T?>> CollectValues(ReadOnlyMemory<char> keySegment, StringBuilder keyBuilder)
        {
            if (HasValue)
            {
                yield return new KeyValuePair<string, T?>(keyBuilder.ToString(), Value!);
            }
            foreach((var key, var child) in Children)
            {
                keyBuilder.Append(key);
                foreach(var childValue in child.CollectValues(keySegment, keyBuilder))
                {
                    yield return childValue;
                }
                keyBuilder.Length -= 1;
            }
        }

        public SimpleNode<T>? GetNode(ReadOnlySpan<char> keySegment, bool createIfMissing = false)
        {
            var searchNode = this;
            while (true)
            {
                if (searchNode.Children.TryGetValue(keySegment[0], out var child))
                {
                    searchNode = child;
                }
                else
                {
                    if (createIfMissing)
                    {
                        child = new SimpleNode<T>();
                        searchNode.Children.Add(keySegment[0], child);
                        searchNode = child;
                    }
                    else
                    {
                        return null;
                    }
                }
                if (keySegment.Length == 1)
                {
                    return searchNode;
                }
                keySegment = keySegment.Slice(1);

            }
        }


        
    }
}
