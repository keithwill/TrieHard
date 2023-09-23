using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Xml.Linq;

namespace TrieHard.Collections
{
    internal class ArrayPoolList<T> : IList<T>, IDisposable
    {
        
        internal static ArrayPoolList<T> Rent()
        {
            if (Pool.Reader.TryRead(out var arrayList))
            {
                return arrayList;
            }
            return new ArrayPoolList<T>();
        }

        internal static void Return(ArrayPoolList<T> arrayList)
        {
            arrayList.Clear();
            Array.Clear(arrayList.Items);
            Pool.Writer.TryWrite(arrayList);
        }

        internal static Channel<ArrayPoolList<T>> Pool =
            Channel.CreateBounded<ArrayPoolList<T>>(
            new BoundedChannelOptions(10000)
            {
                FullMode = BoundedChannelFullMode.DropNewest
            }, x => { x.Dispose(); }
        );

        private T[] _items;
        private int _size, _version;
        public ArrayPoolList()
            => _items = Array.Empty<T>();

        public ref T[] Items => ref _items;

        public ArrayPoolList(int capacity)
            // note: can probably just use  => _items = ArrayPool<T>.Shared.Rent(capacity);
            => _items = capacity == 0 ? Array.Empty<T>() : ArrayPool<T>.Shared.Rent(capacity);

        public int Count => _size;

        public Span<T> Span => _items.AsSpan(0, _size);

        public bool IsReadOnly => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(T item)
        {
            _version++;
            var array = DisposeCheck(_items);
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

        public void Dispose()
        {
            var arr = _items;
            _items = null!;
            if (arr != null) ArrayPool<T>.Shared.Return(arr);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        static T[] ThrowObjectDisposedException()
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(ArrayPoolList<T>));
            return null; // just to make compiler happy
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static T[] DisposeCheck(T[] array)
            => array ?? ThrowObjectDisposedException();

        public int Capacity
        {
            get => DisposeCheck(_items).Length;
            set
            {
                if (value < _size) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(Capacity));

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
            get
            {
                if ((uint)index >= (uint)_size) ThrowHelper.ThrowIndexOutOfRangeException();
                return DisposeCheck(_items)[index];
            }

            set
            {
                if ((uint)index >= (uint)_size) ThrowHelper.ThrowIndexOutOfRangeException();
                _version++;
                DisposeCheck(_items)[index] = value;
            }
        }

        public void Clear()
        {
            _version++;
            _size = 0;
        }

        public bool Contains(T item)
            => _size != 0 && IndexOf(item) != -1;

        public int IndexOf(T item)
            => Array.IndexOf(DisposeCheck(_items), item, 0, _size);

        public void CopyTo(T[] array, int arrayIndex)
            => Array.Copy(DisposeCheck(_items), 0, array, arrayIndex, _size);

        public IEnumerator<T> GetEnumerator()
            => throw new NotImplementedException();

        public bool Remove(T item)
            => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator()
            => throw new NotImplementedException();

        public void Insert(int index, T item)
            => throw new NotImplementedException();

        public void RemoveAt(int index)
            => throw new NotImplementedException();

        void ICollection<T>.Add(T item)
        {
            this.Add(item);
        }
    }

    static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRangeException(string paramName)
            => throw new ArgumentOutOfRangeException(paramName);
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowIndexOutOfRangeException()
            => throw new IndexOutOfRangeException();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowObjectDisposedException(string objectName)
            => throw new ObjectDisposedException(objectName);
    }
}


