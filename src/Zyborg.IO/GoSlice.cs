using System;
using System.Collections;
using System.Collections.Generic;

namespace Zyborg.IO
{
    public struct slice<T> : IEnumerable<T>
    {
        public static readonly slice<T> Empty = default(slice<T>);

        private T[] _array;
        private int _lower;
        private int _upper;

        public slice(T[] array, int lower = 0, int upper = int.MinValue)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (lower < 0 || lower > array.Length)
                throw new ArgumentOutOfRangeException(nameof(lower));
            if (upper != int.MinValue && (upper< lower || upper > array.Length))
                throw new ArgumentOutOfRangeException(nameof(upper));

            _array = array;
            _lower = lower;
            _upper = upper == int.MinValue ? array.Length : upper;
        }

        public slice(slice<T> s, int lower = 0, int upper = int.MinValue)
        {
            if (lower < 0 || lower > s._array?.Length - s._lower)
                throw new ArgumentOutOfRangeException(nameof(lower));
            if (upper != int.MinValue && (upper < lower || upper > s._array?.Length - s._lower))
                throw new ArgumentOutOfRangeException(nameof(upper));

            _array = s._array;
            _lower = s._lower + lower;
            _upper = upper == int.MinValue ? s._upper : s._lower + upper;
            
        }

        public bool IsEmpty => _array == null;

        public int Lower => _lower;
        public int Upper => _upper;
        public int Length => _upper - _lower;
        public int Capacity => _array == null ? 0 : _array.Length - _lower;

        public T this[int index]
        {
            get
            {
                if (index < 0)
                    throw new IndexOutOfRangeException($"slice index [{index}] must be non-negative");
                if (index >= _upper)
                    throw new IndexOutOfRangeException($"slice index [{index}] exceeds upper bound");

                return _array[_lower + index];
            }

            set
            {
                if (index < 0)
                    throw new IndexOutOfRangeException($"slice index [{index}] must be non-negative");
                if (index >= _upper)
                    throw new IndexOutOfRangeException($"slice index [{index}] exceeds upper bound");

               _array[_lower + index] = value;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int index = _lower; index < _upper; index++)
                yield return _array[index];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            for (int index = _lower; index < _upper; index++)
                yield return _array[index];
        }
 
        public int CopyTo(slice<T> dst)
        {
            int max = Math.Min(this.Length, dst.Length);

            // Properly handle copying overlapping regions by
            // either copying right to left or left to right

            if (this._array == dst._array && dst._lower > this._lower)
            {
                for (int i = max - 1; i >= 0; --i)
                    dst[i] = this[i];
            }
            else
            {
                for (int i = 0; i < max; ++i)
                    dst[i] = this[i];
            }

            return max;
        }

        public int CopyFrom(slice<T> src)
        {
            return src.CopyTo(this);
        }

        public slice<T> Append(params T[] items)
        {
            // TODO: naive implementation, optimize
            return AppendAll(new slice<T>(items));
        }

        public slice<T> AppendAll(slice<T> items)
        {
            // TODO: naive implementation (based on AppendByte sample in
            // https://blog.golang.org/go-slices-usage-and-internals), optimize
            var len = this.Length;
            var n = Length + items.Length;
            var s = this;

            if (n > this.Capacity) // if necessary, reallocate
            {
                // allocate double what's needed, for future growth (+1 in case n == 0)
                s = slice<T>.Make((n + 1) * 2);
                this.CopyTo(s);
            }
            s = s.slice(0, n);
            items.CopyTo(s.slice(len, n));
            return s;
        }

        public int IndexOf(T item) =>
                _array == null ? -1 : Array.IndexOf(_array, item, _lower, _upper - _lower);

        public T[] ToArray()
        {
            var span = _array == null ? default(Span<T>) : (Span<T>)_array;
            return span.Slice(_lower, _upper - _lower).ToArray();
        }

        public override string ToString()
        {
            return ToString(this);
        }

        /// Returns true if the elements are equal.
        public bool Equals(slice<T> obj, IComparer<T> c = null)
        {
            // We short-circuit if the slices are exactly the same, for performance
            if (this._array == obj._array && this._lower == obj._lower && this._upper == obj._upper)
                return true;
        
            if (this.Length != obj.Length)
                return false;

            if (c == null)
                c = Comparer<T>.Default;

            for (var i = 0; i < _upper; i++)
            {
                if (0 != c.Compare(this._array[this._lower + i], obj._array[obj._lower + i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj != null && (obj is slice<T>) && Equals((slice<T>)obj, null);
        }
        
        public static slice<T> Make(int length, int capacity = int.MinValue)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (capacity == int.MinValue)
                capacity = length;
            if (capacity < length)
                throw new ArgumentException("length cannot exceed capacity", nameof(capacity));

            return new slice<T>(new T[capacity], 0, length);
        }
        
        public static slice<T> From(params T[] items)
        {
            return new slice<T>(items);
        }

        public static string ToString(IEnumerable<T> e)
        {
            return "[" + string.Join(" ", e) + "]";
        }
   }

    public static class SliceExtensions
    {
        public static slice<T> slice<T>(this T[] array, int lower = 0, int upper = int.MinValue)
        {
            return new slice<T>(array, lower, upper);
        }

        public static slice<T> slice<T>(this slice<T> slice, int lower = 0, int upper = int.MinValue)
        {
            return new slice<T>(slice, lower, upper);
        }

        public static IEnumerable<(int index, T value)> Range<T>(this IEnumerable<T> e,
                int lower = 0, int upper = int.MaxValue)
        {
            int index = 0;
            foreach (var value in e)
            {
                if (index >= upper)
                    break;

                if (index >= lower)
                    yield return (index, value);

                index++;
            }
        }
    }
}