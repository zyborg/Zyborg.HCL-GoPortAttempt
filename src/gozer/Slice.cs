using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NStack;

namespace gozer
{
    public static class slice
    {
        public static slice<T> Make<T>(int length, int capacity = int.MinValue)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (capacity == int.MinValue)
                capacity = length;
            if (capacity < length)
                throw new ArgumentException("length cannot exceed capacity", nameof(capacity));

            return new slice<T>(new T[capacity], 0, length);
        }

        public static slice<T> From<T>(params T[] items)
        {
            return slice<T>.From(items);
        }

        public static void WriteTo(this slice<byte> slice, Stream s, int lower = -1, int upper = -1)
        {
            if (lower == -1) lower = slice._lower;
            if (upper == -1) upper = slice._upper;
            var count = upper - lower;
            s.Write(slice._array, lower, count);
        }


        /// A format string is composed of zero or more runs of fixed text
        /// intermixed with one or more format items.  Format items take
        /// the following form:
        ///
        ///  <c>{</c><i>index</i>[<c>,</c><i>alignment</i>][<c>:</c><i>formatString</i>]]<c>}</c>
        ///
        /// This <c>Make</c> routine takes a formattable string and converts it to a
        /// slice of bytes.  It has special support for embedding byte values within
        /// the string that are normally not possible to encode in a normal CLR string.
        /// To embed such a byte value, for example 0xff, you specify a format item in
        /// the formattable string with a the special format suffix of <c>::</c>. 
        ///
        /// For example, if you specify the following:
        /// <code>
        ///    slice.Make($"Sample {0xff::} Embedded")
        /// </code>
        /// then it will return a byte slice that contains the following values:
        /// <code>
        ///    as byte[] :  83  97 109 112 108 101  32 255  32  69 109  98 101 100 100 101 100
        ///    as char[] : 'S' 'a' 'm' 'p' 'l' 'e' ' '  ff ' ' 'E' 'm' 'b' 'e' 'd' 'd' 'e' 'd'
        /// </code>
        ///
        /// In addition to the single-byte format variation, you can specify 2-, 3- and 4-byte
        /// variations using the format string suffix of <c>::2</c>, <c>::3</c> and <c>::4</c>
        /// respectively.  In these cases, the value associated with the format item will be
        /// interpretted as either an unsigned short (16 bits/2 bytes) or an unsigned int
        /// (24 bits/3 bytes or 32 bits/4 bytes).  The higher-order bytes will map to the lower
        /// index positions in the resulting byte slice.  For example:
        /// <code>
        ///    slice.Make($"Sample {0xa0fe::2} {0xfafbfcfd::3} Embedded")
        /// </code>
        /// would return a byte slice that contains the following values:
        /// <code>
        ///    as byte[] :  83  97 109 112 108 101  32 160 254  32 251 252 253  32  69 109  98 101 100 100 101 100
        ///    as char[] : 'S' 'a' 'm' 'p' 'l' 'e' ' '  a0  fe' '   fb  fc  fd ' ' 'E' 'm' 'b' 'e' 'd' 'd' 'e' 'd'
        /// </code>
        ///
        /// You'll note that for the 3-byte format item, we have to provide a 4-byte equivalent value,
        /// a 32-bit integer, but the high-order byte (0xfa in this case) is ignored.  If we used a
        /// 4-byte format item (::4) then all four bytes of the format item value would be embedded in
        /// the resultant value.
        ///
        /// Finally, if you the format item format string begins with <c>::</c> but contains any values
        /// afterwards that are not understood or supported, the default behavior is to treat the value
        /// as a single-byte to be embedded.
        public static slice<byte> Make(this FormattableString s)
        {
            var fmt = s.Format.Replace("\x00", "\x00\x00"); // Escape all embedded nil chars
            var prv = new ByteSliceFormatProvider(); // construct provider instance which will have state
            var str = string.Format(prv, fmt, s.GetArguments()); // First pass along and do normal substitution

            // Go through each of the provider's store of escaped objects
            // and add up how many bytes they will occupy in the final array
            var ext = 0;
            for (int i = 0; i < prv.ArgCount; i++)
                ext += prv[i].size;

            // Convert to bytes including the escaped place-holders
            var src = Encoding.UTF8.GetBytes(str);
            // Allocate enough space to hold the final byte array:
            //   the size of the src array, minus 2 bytes for
            //   each escaped placeholder, plus the computed
            //   size that the escaped values will occupy
            //   minus the number of escaped nil chars
            var dst = new byte[
                    src.Length
                    - (prv.ArgCount * 2)
                    + ext
                    - (fmt.Length - s.Format.Length)];

            for (int i = 0, j = 0; i < src.Length; i++, j++)
            {
                if (src[i] == 0) // We found the escape (null char)
                {
                    if (src[++i] == 0)
                    {
                        // Just an escaped null char
                        dst[j] = 0;
                    }
                    else
                    {
                        // After the escape is the index (+1) into the
                        // provider's storage of escaped object values
                        var arg = prv[src[i] - 1];
                        switch (arg.size)
                        {
                            case 4:
                                var v4 = Convert.ToUInt32(arg.arg);
                                dst[j++] = (byte)(0xff & (v4 >> 24));
                                dst[j++] = (byte)(0xff & (v4 >> 16));
                                dst[j++] = (byte)(0xff & (v4 >> 8));
                                dst[j] = (byte)(0xff & (v4));
                                break;
                            case 3:
                                var v3 = Convert.ToUInt32(arg.arg);
                                dst[j++] = (byte)(0xff & (v3 >> 16));
                                dst[j++] = (byte)(0xff & (v3 >> 8));
                                dst[j] = (byte)(0xff & (v3));
                                break;
                            case 2:
                                var v2 = Convert.ToUInt16(arg.arg);
                                dst[j++] = (byte)(0xff & (v2 >> 8));
                                dst[j] = (byte)(0xff & (v2));
                                break;
                            default:
                                dst[j] = Convert.ToByte(arg.arg);
                                break;
                        }
                    }
                }
                else
                {
                    dst[j] = src[i];
                }
            }

            return dst.Slice();

            // for ( var i = 0; i < prv.ArgCount; i++)
            // {
            //     str = str.Replace(new char)
            // }

            // //return s?.Format;
            // return s?.ToString();

        }

        class ByteSliceFormatProvider : IFormatProvider, ICustomFormatter
        {
            private slice<(object arg, int size)> _args;

            public int ArgCount => _args.Length;
            public (object arg, int size) this[int index] => _args[index];

            public object GetFormat(Type formatType)
            {
                if (formatType == typeof(ICustomFormatter))
                    return this;
                return null;
            }

            public string Format(string format, object arg, IFormatProvider formatProvider)
            {
                if (format != null && format.StartsWith(":"))
                {
                    if (format == ":4") _args = _args.Append((arg, 4));
                    else
                    if (format == ":3") _args = _args.Append((arg, 3));
                    else
                    if (format == ":2") _args = _args.Append((arg, 2));
                    else 
                        _args = _args.Append((arg, 1));

                    return new string(new char[] { (char)0, (char)(_args.Length) });
                }
                else
                {
                    return arg.ToString();
                }
            }
        }
    }

    public struct slice<T> : IEnumerable<T>, IEquatable<slice<T>>
    {
        public static readonly slice<T> Empty = default(slice<T>);

        internal T[] _array;
        internal int _lower;
        internal int _upper;

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
            s = s.Slice(0, n);
            items.CopyTo(s.Slice(len, n));
            return s;
        }

        public int IndexOf(T item)
        {
            var index = _array == null ? -1 : Array.IndexOf(_array, item, _lower, _upper - _lower);
            if (index >= 0)
                return index - _lower;
            return index;
        }

        public slice<T> Repeat(int count)
        {
            return _array == null ? Empty : _array.Repeat(count);
        }

        public T[] ToArray()
        {
            var span = _array == null ? default(Span<T>) : (Span<T>)_array;
            return span.Slice(_lower, _upper - _lower).ToArray();
        }

        public override string ToString()
        {
            return ToString(this);
        }

        // TODO:  Do we really want to implement Equals and GetHashCode?
        // This could be very expensive and maybe should be left up to
        // the client if they decide they need this

        public bool Equals(slice<T> obj)
        {
            return this.Equals(obj, null);
        }

        /// Returns true if the elements are equal.
        public bool Equals(slice<T> obj, IComparer<T> c)
        {
            // We short-circuit if the slices are exactly the same, for performance
            if (this._array == obj._array && this._lower == obj._lower && this._upper == obj._upper)
                return true;
        
            if (this.Length != obj.Length)
                return false;

            Func<T, T, bool> comparer;
            if (c != null)
                comparer = (t1, t2) => 0 == c.Compare(t1, t2);
            else
                comparer = (t1, t2) => object.Equals(t1, t2);

            for (int i = 0, len = Length; i < len; i++)
            {
                if (!comparer(this._array[this._lower + i], obj._array[obj._lower + i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj != null && (obj is slice<T>) && Equals((slice<T>)obj, null);
        }

        public override int GetHashCode()
        {
            if (_array == null || _array.Length == 0)
                return 0;
            var hash = 17;
            foreach (var item in _array)
            {
                hash = hash * 23 + (item == null ? 0 : item.GetHashCode());
            }
            return hash;
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
}