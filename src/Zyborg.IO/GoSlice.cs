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
        public static slice<char> AsCharSlice(this string s)
        {
            return new slice<char>(s.ToCharArray());
        }

        public static slice<byte> AsByteSlice(this string s)
        {
            return new slice<byte>(System.Text.Encoding.UTF8.GetBytes(s));
        }

        public static string AsString(this slice<char> s)
        {
            return new string(s.ToArray());
        }

        public static string AsString(this slice<byte> s)
        {
            return System.Text.Encoding.UTF8.GetString(s.ToArray());
        }

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

        /// EncodeRune writes into p (which must be large enough) the UTF-8 encoding of the rune.
        /// It returns the number of bytes written.
        public static int EncodeRune(this slice<byte> p, char r)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(new char[] { r });
            switch (bytes.Length)
            {
                case 1:
                    p[0] = bytes[0];
                    return 1;
                case 2:
                    p[0] = bytes[1];
                    p[1] = bytes[0];
                    return 2;
                case 3:
                    p[0] = bytes[2];
                    p[1] = bytes[1];
                    p[2] = bytes[0];
                    return 3;
                case 4:
                    p[0] = bytes[3];
                    p[1] = bytes[2];
                    p[2] = bytes[1];
                    p[3] = bytes[0];
                    return 4;
                default:
                    throw new Exception("failed to encode UTF8 character to slice"
                            + $" -- unexpected byte length [{bytes.Length}]");
            }
        }

        /// DecodeRune unpacks the first UTF-8 encoding in p and returns the rune and
        /// its width in bytes. If p is empty it returns (RuneError, 0). Otherwise, if
        /// the encoding is invalid, it returns (RuneError, 1). Both are impossible
        /// results for correct, non-empty UTF-8.
        ///
        /// An encoding is invalid if it is incorrect UTF-8, encodes a rune that is
        /// out of range, or is not the shortest possible UTF-8 encoding for the
        /// value. No other validation is performed.
        public static (char r, int size) DecodeRune(this slice<byte> p)
        {
            var (r, size) = NStack.Utf8.DecodeRune(p.ToArray());
            return ((char)r, size);

            // n := len(p)
            // if n < 1 {
            //     return RuneError, 0
            // }
            // p0 := p[0]
            // x := first[p0]
            // if x >= as {
            //     // The following code simulates an additional check for x == xx and
            //     // handling the ASCII and invalid cases accordingly. This mask-and-or
            //     // approach prevents an additional branch.
            //     mask := rune(x) << 31 >> 31 // Create 0x0000 or 0xFFFF.
            //     return rune(p[0])&^mask | RuneError&mask, 1
            // }
            // sz := x & 7
            // accept := acceptRanges[x>>4]
            // if n < int(sz) {
            //     return RuneError, 1
            // }
            // b1 := p[1]
            // if b1 < accept.lo || accept.hi < b1 {
            //     return RuneError, 1
            // }
            // if sz == 2 {
            //     return rune(p0&mask2)<<6 | rune(b1&maskx), 2
            // }
            // b2 := p[2]
            // if b2 < locb || hicb < b2 {
            //     return RuneError, 1
            // }
            // if sz == 3 {
            //     return rune(p0&mask3)<<12 | rune(b1&maskx)<<6 | rune(b2&maskx), 3
            // }
            // b3 := p[3]
            // if b3 < locb || hicb < b3 {
            //     return RuneError, 1
            // }
            // return rune(p0&mask4)<<18 | rune(b1&maskx)<<12 | rune(b2&maskx)<<6 | rune(b3&maskx), 4
        }        
    }
}