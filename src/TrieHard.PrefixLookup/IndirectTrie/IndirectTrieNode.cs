using System.Collections.Generic;

namespace TrieHard.Collections
{
    public readonly record struct IndirectTrieNode<T>(IndirectTrieLocation Location, IndirectTrieLocation Parent, IndirectTrieLocation Sibbling, IndirectTrieLocation Child, char Key, T? Value)
    {
        public static readonly IndirectTrieNode<T> none = new(IndirectTrieLocation.None, IndirectTrieLocation.None, IndirectTrieLocation.None, IndirectTrieLocation.None, default, default!);
        public static ref readonly IndirectTrieNode<T> None => ref none;

        public bool IsNone => !Location.Exists;

        public bool HasChild => Child.Exists;

        public bool HasSibbling => Sibbling.Exists;

        public bool Exists => Location.Exists;

        public bool HasValue => !EqualityComparer<T>.Default.Equals(Value, default);

        public override string ToString()
        {
            return $"[{Location.Bucket}][{Location.Index}] {(Value is null ? string.Empty : Value)}";
        }
    }

}
