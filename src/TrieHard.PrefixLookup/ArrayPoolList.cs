using System.Buffers;
using System.Collections.Concurrent;

namespace TrieHard.Collections
{
    /// <summary>
    /// This was an experiment taken from a dotnet GitHub issue where they were exploring
    /// the use of a pooled list class for the BCL. I pilfered it, but added some rent
    /// and return methods to also reused the list object itself and stripped down some of the
    /// code that implemented IList<typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ArrayPoolList<T> : IDisposable
    {
        private T[] _items;
        private int _size;

        public ArrayPoolList() => _items = Array.Empty<T>();
        public ArrayPoolList(int capacity) => _items = capacity == 0 ?
            Array.Empty<T>() : 
            ArrayPool<T>.Shared.Rent(capacity);

        public ref T[] Items => ref _items;
        public int Count => _size;
        public Span<T> Span => _items.AsSpan(0, _size);

        public int Add(T item)
        {
            var array = _items;
            int size = _size;
            if ((uint)size < (uint)array.Length)
            {
                _size = size + 1;
                array[size] = item;
                return size;
            }
            else
            {
                return AddWithResize(item);
            }
        }

        private int AddWithResize(T item)
        {
            int size = _size;
            EnsureCapacity(size + 1);
            _size = size + 1;
            _items[size] = item;
            return size;
        }

        private const int DefaultCapacity = 4, MaxArrayLength = 0x7FEFFFFF;

        private void EnsureCapacity(int min)
        {
            if (_items.Length < min)
            {
                int newCapacity = _items.Length == 0 ? DefaultCapacity : _items.Length * 2;
                if ((uint)newCapacity > MaxArrayLength) newCapacity = MaxArrayLength;
                if (newCapacity < min) newCapacity = min;
                Capacity = newCapacity;
            }
        }

        public int Capacity
        {
            get => _items.Length;
            set
            {
                if (value != _items.Length)
                {
                    if (value > 0)
                    {
                        T[] newItems = ArrayPool<T>.Shared.Rent(value);
                        var toReturn = _items;
                        if (_size > 0)
                        {
                            Array.Copy(_items, 0, newItems, 0, _size);
                        }
                        _items = newItems;
                        ArrayPool<T>.Shared.Return(toReturn);
                    }
                    else
                    {
                        ArrayPool<T>.Shared.Return(_items);
                        _items = Array.Empty<T>();
                    }
                }
            }
        }

        public T this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        public void Clear()
        {
            _size = 0;
        }

        internal static ArrayPoolList<T> Rent()
        {
            if (Pool.TryDequeue(out var arrayList))
            {
                return arrayList;
            }
            return new ArrayPoolList<T>();
        }

        internal static void Return(ArrayPoolList<T> arrayList)
        {
            arrayList.Clear();
            Array.Clear(arrayList.Items);
            Pool.Enqueue(arrayList);
        }
        
        internal static readonly ConcurrentQueue<ArrayPoolList<T>> Pool = new ConcurrentQueue<ArrayPoolList<T>>();

        public void Dispose()
        {
            var arr = _items;
            _items = null!;
            if (arr != null) ArrayPool<T>.Shared.Return(arr);
        }

    }

}


