using System;
using System.IO;
using System.Text;
using gozer.io;

namespace gozer.bytes
{
    /// Simple byte buffer for marshaling data.
    /// A Buffer is a variable-sized buffer of bytes with Read and Write methods.
    /// The zero value for Buffer is an empty buffer ready to use.
    public class Buffer : Stream, IWriter
    {
        private slice<byte> _buf; // contents are the bytes buf[off : len(buf)]
        private int _off;    // read at &buf[off], write at &buf[len(buf)]
        private ReadOp _lastRead; // last read operation, so that Unread* can work correctly.
        // FIXME: lastRead can fit in a single byte

        // memory to hold first slice; helps small buffers avoid allocation.
        // FIXME: it would be advisable to align Buffer to cachelines to avoid false
        // sharing.
        private byte[] _bootstrap = new byte[64];


        /// ErrTooLarge is passed to panic if memory cannot be allocated to store data in a buffer.
        private Exception ErrTooLarge() => new IOException("bytes.Buffer: too large");

        /// Necessary to inherit Stream's protected constructor
        public Buffer()
        { }

        /// Bytes returns a slice of length b.Len() holding the unread portion of the buffer.
        /// The slice is valid for use only until the next buffer modification (that is,
        /// only until the next call to a method like Read, Write, Reset, or Truncate).
        /// The slice aliases the buffer content at least until the next buffer modification,
        /// so immediate changes to the slice will affect the result of future reads.
        public slice<byte> Bytes() => _buf.Slice(_off);

        /// String returns the contents of the unread portion of the buffer
        /// as a string. If the Buffer is a nil pointer, it returns "&lt;nil&gt;".
        public override string ToString() => Encoding.UTF8.GetString(Bytes().ToArray());

        /// Len returns the number of bytes of the unread portion of the buffer;
        /// b.Len() == len(b.Bytes()).
        public int Len() => _buf.Length - _off;

        /// Cap returns the capacity of the buffer's underlying byte slice, that is, the
        /// total space allocated for the buffer's data.
        public int Cap() => _buf.Capacity;

        /// Truncate discards all but the first n unread bytes from the buffer
        /// but continues to use the same allocated storage.
        /// It panics if n is negative or greater than the length of the buffer.
        public void Truncate(int n)
        {
            if (n == 0) {
                Reset();
                return;
            }
            _lastRead = ReadOp.opInvalid;
            if (n < 0 || n > Len())
                throw new Exception("bytes.Buffer: truncation out of range");

            _buf = _buf.Slice(0, _off + n);
        }

        /// Reset resets the buffer to be empty,
        /// but it retains the underlying storage for use by future writes.
        /// Reset is the same as Truncate(0).
        public void Reset()
        {
            _buf = _buf.Slice(upper: 0);
            _off = 0;
            _lastRead = ReadOp.opInvalid;
        }

        /// tryGrowByReslice is a inlineable version of grow for the fast-case where the
        /// internal buffer only needs to be resliced.
        /// It returns the index where bytes should be written and whether it succeeded.
        private (int index, bool success) TryGrowByReslice(int n)
        {
            var l = _buf.Length;
            if (l + n <= _buf.Capacity)
            {
                _buf = _buf.Slice(upper: l + n);
                return (l, true);
            }
            return (0, false);
        }

        /// grow grows the buffer to guarantee space for n more bytes.
        /// It returns the index where bytes should be written.
        /// If the buffer can't grow it will panic with ErrTooLarge.
        private int GrowInternal(int n)
        {
            var m = Len();
            // If buffer is empty, reset to recover space.
            if (m == 0 && _off != 0)
            {
                Reset();
            }
            // Try to grow by means of a reslice.
            var (i, ok) = TryGrowByReslice(n);
            if (ok)
                return i;
            // Check if we can make use of bootstrap array.
            if (_buf.IsEmpty && n <= _bootstrap.Length)
            {
                _buf = _bootstrap.Slice(upper: n);
                return 0;
            }
            if (m + n <= _buf.Capacity / 2)
            {
                // We can slide things down instead of allocating a new
                // slice. We only need m+n <= cap(b.buf) to slide, but
                // we instead let capacity get twice as large so we
                // don't spend all our time copying.
                //copy(b.buf[:], b.buf[b.off:])
                _buf.Slice().CopyFrom(_buf.Slice(_off));
            }
            else
            {
                // Not enough space anywhere, we need to allocate.
                var buf = MakeSlice(2 * _buf.Capacity + n);
                buf.CopyFrom(_buf.Slice(_off));
                _buf = buf;
            }
            // Restore b.off and len(b.buf).
            _off = 0;
            _buf = _buf.Slice(upper: m + n);
            return m;
        }

        /// Grow grows the buffer's capacity, if necessary, to guarantee space for
        /// another n bytes. After Grow(n), at least n bytes can be written to the
        /// buffer without another allocation.
        /// If n is negative, Grow will panic.
        /// If the buffer can't grow it will panic with ErrTooLarge.
        public void Grow(int n)
        {
            if (n < 0)
                throw new Exception("bytes.Buffer.Grow: negative count");

            var m = GrowInternal(n);
            _buf = _buf.Slice(0, m);
        }

        /// Write appends the contents of p to the buffer, growing the buffer as
        /// needed. The return value n is the length of p; err is always nil. If the
        /// buffer becomes too large, Write will panic with ErrTooLarge.
        public int Write(slice<byte> p)
        {
            _lastRead = ReadOp.opInvalid;
            var (m, ok) = TryGrowByReslice(p.Length);
            if (!ok)
                m = GrowInternal(p.Length);

            return _buf.Slice(m).CopyFrom(p);
        }

        /// WriteString appends the contents of s to the buffer, growing the buffer as
        /// needed. The return value n is the length of s; err is always nil. If the
        /// buffer becomes too large, WriteString will panic with ErrTooLarge.
        public int WriteString(string s)
        {
            _lastRead = ReadOp.opInvalid;
            var (m, ok) = TryGrowByReslice(s.Length);
            if (!ok)
                m = GrowInternal(s.Length);
            return _buf.Slice(m).CopyFrom(Encoding.UTF8.GetBytes(s).Slice());
        }

        /// MinRead is the minimum slice size passed to a Read call by
        /// Buffer.ReadFrom. As long as the Buffer has at least MinRead bytes beyond
        /// what is required to hold the contents of r, ReadFrom will not grow the
        /// underlying buffer.
        public const int MinRead = 512;

        /// ReadFrom reads data from r until EOF and appends it to the buffer, growing
        /// the buffer as needed. The return value n is the number of bytes read. Any
        /// error except io.EOF encountered during the read is also returned. If the
        /// buffer becomes too large, ReadFrom will panic with ErrTooLarge.
        public long ReadFrom(Stream r)
        //func (b *Buffer) ReadFrom(r io.Reader) (n int64, err error) {
        {
            var n = 0L;

            _lastRead = ReadOp.opInvalid;
            // If buffer is empty, reset to recover space.
            if (_off >= _buf.Length)
            {
                Reset();
            }
            for (;;)
            {
                var free = _buf.Capacity - _buf.Length;
                if (free < MinRead)
                {
                    // not enough space at end
                    var newBuf = _buf;
                    if (_off + free < MinRead)
                    {
                        // not enough space using beginning of buffer;
                        // double buffer capacity
                        newBuf = MakeSlice(2 * _buf.Capacity + MinRead);
                    }
                    newBuf.CopyFrom(_buf.Slice(_off));
                    _buf = newBuf.Slice(upper: _buf.Length - _off);
                    _off = 0;
                }
                
                var bytes = new byte[_buf.Capacity - _buf.Length];
                var m = r.Read(bytes, 0, bytes.Length);
                if (m > 0)
                {
                    var slice = bytes.Slice(upper: m);
                    _buf = _buf.AppendAll(slice);

                    n += (long)m;
                }
                else
                {
                    break;
                }

                // m, e := r.Read(b.buf[len(b.buf):cap(b.buf)])
                // b.buf = b.buf[0 : len(b.buf)+m]
                // n += int64(m)
                // if e == io.EOF {
                //     break
                // }
                // if e != nil {
                //     return n, e
                // }
            }

            return n;
            //return n, nil // err is EOF, so return nil explicitly
        }

        /// makeSlice allocates a slice of size n. If the allocation fails, it panics
        /// with ErrTooLarge.
        private slice<byte> MakeSlice(int n)
        {
            // If the make fails, give a known error.
            try
            {
                return slice<byte>.Make(n);
            }
            catch (Exception)
            {
                throw ErrTooLarge();
            }

            // defer func() {
            //     if recover() != nil {
            //         panic(ErrTooLarge)
            //     }
            // }()
            // return make([]byte, n)
        }

        /// WriteTo writes data to w until the buffer is drained or an error occurs.
        /// The return value n is the number of bytes written; it always fits into an
        /// int, but it is int64 to match the io.WriterTo interface. Any error
        /// encountered during the write is also returned.
        public long WriteTo(Stream w)
        {
            var n = 0L;

            _lastRead = ReadOp.opInvalid;
            if (_off < _buf.Length)
            {
                var nBytes = this.Len();
                
                var arr = _buf.Slice(_off).ToArray();
                w.Write(arr, 0, arr.Length);
                var m = arr.Length;
                if (m > nBytes)
                    throw new Exception("bytes.Buffer.WriteTo: invalid Write count");

                _off += m;
                n = (long)m;
                // all bytes should have been written, by definition of
                // Write method in io.Writer
                if (m != nBytes)
                {
                    return n;
                }
            }
            // Buffer is now empty; reset.
            Reset();
            return n;
        }

        /// WriteByte appends the byte c to the buffer, growing the buffer as needed.
        /// The returned error is always nil, but is included to match bufio.Writer's
        /// WriteByte. If the buffer becomes too large, WriteByte will panic with
        /// ErrTooLarge.
        public override void WriteByte(byte c)
        {
            _lastRead = ReadOp.opInvalid;
            var (m, ok) = TryGrowByReslice(1);
            if (!ok)
                m = GrowInternal(1);
            _buf[m] = c;
        }


        /// the "error" Rune or "Unicode replacement character"
        public const char RuneError = '\uFFFD';
        /// characters below Runeself are represented as themselves in a single byte.
        public const char RuneSelf = (char)0x80;
        /// Maximum valid Unicode code point.
        // TODO: this doesn't seem to work
        //public const char MaxRune = '\U0010ffff';
        /// maximum number of bytes of a UTF-8 encoded Unicode character.
        public const char UTFMax = (char)4;

        /// WriteRune appends the UTF-8 encoding of Unicode code point r to the
        // buffer, returning its length and an error, which is always nil but is
        /// included to match bufio.Writer's WriteRune. The buffer is grown as needed;
        /// if it becomes too large, WriteRune will panic with ErrTooLarge.
        public int WriteRune(char r)
        {
            var n = 0;
            if (r < RuneSelf)
            {
                WriteByte((byte)r);
                return 1;
            }

            _lastRead = ReadOp.opInvalid;
            var (m, ok) = TryGrowByReslice(UTFMax);
            if (!ok)
            {
                m = GrowInternal(UTFMax);
            }

            n = _buf.Slice(m, m + UTFMax).EncodeRune(r);
            _buf = _buf.Slice(upper: m + n);
            return n;
        }

        /// Read reads the next len(p) bytes from the buffer or until the buffer
        /// is drained. The return value n is the number of bytes read. If the
        /// buffer has no data to return, err is io.EOF (unless len(p) is zero);
        /// otherwise it is nil.
        public (int val, bool eof) Read(slice<byte> p)
        {
            var n = 0;

            _lastRead = ReadOp.opInvalid;
            if (_off >= _buf.Length)
            {
                // Buffer is empty, reset to recover space.
                Reset();
                if (p.Length == 0)
                    return (n, false);
                return (0, eof: true);
            }
            n = p.CopyFrom(_buf.Slice(_off));
            _off += n;
            if (n > 0)
                _lastRead = ReadOp.opRead;
            return (n, false);
        }

        /// Next returns a slice containing the next n bytes from the buffer,
        /// advancing the buffer as if the bytes had been returned by Read.
        /// If there are fewer than n bytes in the buffer, Next returns the entire buffer.
        /// The slice is only valid until the next call to a read or write method.
        public slice<byte> Next(int n)
        {
            _lastRead = ReadOp.opInvalid;
            var m = Len();
            if (n > m) {
                n = m;
            }
            var data = _buf.Slice(_off, _off + n);
            _off += n;
            if (n > 0)
                _lastRead = ReadOp.opRead;
            return data;
        }

        /// ReadByte reads and returns the next byte from the buffer.
        /// If no byte is available, it returns error io.EOF.
        ///
        /// NOTE: we added some additional logic here to support returning
        /// 0 when the buffer is currently empty but not signaling an EOF.
        public (byte val, bool eof) ReadByteOrEof(bool eofIfEmpty = true)
        {
            _lastRead = ReadOp.opInvalid;
            if (_off >= _buf.Length)
            {
                if (eofIfEmpty)
                {
                    // Buffer is empty, reset to recover space.
                    Reset();
                    return (0, eof: true);
                }
                else
                {
                    return (0, eof: false);
                }
            }
            var c = _buf[_off];
            _off++;
            _lastRead = ReadOp.opRead;
            return (c, false);
        }

        public override int ReadByte()
        {
            return ReadByteOrEof().val;
        }

        // ReadRune reads and returns the next UTF-8-encoded
        // Unicode code point from the buffer.
        // If no bytes are available, the error returned is io.EOF.
        // If the bytes are an erroneous UTF-8 encoding, it
        // consumes one byte and returns U+FFFD, 1.
        public (char r, int size, bool eof) ReadRune()
        {
            _lastRead = ReadOp.opInvalid;
            if (_off >= _buf.Length)
            {
                // Buffer is empty, reset to recover space.
                Reset();
                return (char.MinValue, 0, eof: true);
            }
            var c = _buf[_off];
            if (c < RuneSelf)
            {
                _off++;
                _lastRead = ReadOp.opReadRune1;
                return ((char)c, 1, false);
            }
            var (r, n) = _buf.Slice(_off).DecodeRune();
            _off += n;
            _lastRead = (ReadOp)n;
            return (r, n, false);
        }

        /// UnreadRune unreads the last rune returned by ReadRune.
        /// If the most recent read or write operation on the buffer was
        /// not a successful ReadRune, UnreadRune returns an error.  (In this regard
        /// it is stricter than UnreadByte, which will unread the last byte
        /// from any read operation.)
        public void UnreadRune()
        {
            if (_lastRead <= ReadOp.opInvalid)
            {
                throw new Exception("bytes.Buffer: UnreadRune: previous operation was not a successful ReadRune");
            }
            if (_off >= (int)_lastRead)
                _off -= (int)_lastRead;
            _lastRead = ReadOp.opInvalid;
        }

        /// UnreadByte unreads the last byte returned by the most recent successful
        /// read operation that read at least one byte. If a write has happened since
        /// the last read, if the last read returned an error, or if the read read zero
        /// bytes, UnreadByte returns an error.
        public void UnreadByte()
        {
            if (_lastRead == ReadOp.opInvalid)
            {
                throw new Exception("bytes.Buffer: UnreadByte: previous operation was not a successful read");
            }
            _lastRead = ReadOp.opInvalid;
            if (_off > 0)
                _off--;
        }

        /// ReadBytes reads until the first occurrence of delim in the input,
        /// returning a slice containing the data up to and including the delimiter.
        /// If ReadBytes encounters an error before finding a delimiter,
        /// it returns the data read before the error and the error itself (often io.EOF).
        /// ReadBytes returns err != nil if and only if the returned data does not end in
        /// delim.
        public (slice<byte> line, bool eof) ReadBytes(byte delim)
        {
            var line = new slice<byte>();
            var (slice, eof) = ReadSlice(delim);
            // return a copy of slice. The buffer's backing array may
            // be overwritten by later calls.
            line = line.AppendAll(slice);
            return (line, eof);
        }

        /// readSlice is like ReadBytes but returns a reference to internal buffer data.
        private (slice<byte> line, bool eof) ReadSlice(byte delim)
        {
            var line = slice<byte>.Empty;
            var eof = false;

            var i = _buf.Slice(_off).IndexOf(delim);
            var end = _off + i + 1;
            if (i < 0)
            {
                end = _buf.Length;
                eof = true;
            }
            line = _buf.Slice(_off, end);
            _off = end;
            _lastRead = ReadOp.opRead;
            return (line, eof);
        }


        /// ReadString reads until the first occurrence of delim in the input,
        /// returning a string containing the data up to and including the delimiter.
        /// If ReadString encounters an error before finding a delimiter,
        /// it returns the data read before the error and the error itself (often io.EOF).
        /// ReadString returns err != nil if and only if the returned data does not end
        /// in delim.
        public (string line, bool eof) ReadString(byte delim)
        {
            var (slice, eof) = ReadSlice(delim);
            return (Encoding.UTF8.GetString(slice.ToArray()), eof);
        }

        /// NewBuffer creates and initializes a new Buffer using buf as its
        /// initial contents. The new Buffer takes ownership of buf, and the
        /// caller should not use buf after this call. NewBuffer is intended to
        /// prepare a Buffer to read existing data. It can also be used to size
        /// the internal buffer for writing. To do that, buf should have the
        /// desired capacity but a length of zero.
        ///
        /// In most cases, new(Buffer) (or just declaring a Buffer variable) is
        /// sufficient to initialize a Buffer.
        //func NewBuffer(buf []byte) *Buffer { return &Buffer{buf: buf} }
        public static Buffer NewBuffer(slice<byte> buf)
        {
            return new Buffer { _buf = buf };
        }

        /// NewBufferString creates and initializes a new Buffer using string s as its
        /// initial contents. It is intended to prepare a buffer to read an existing
        /// string.
        ///
        /// In most cases, new(Buffer) (or just declaring a Buffer variable) is
        /// sufficient to initialize a Buffer.
        // func NewBufferString(s string) *Buffer {
        //     return &Buffer{buf: []byte(s)}
        // }        
        public static Buffer NewBufferString(string s)
        {
            return new Buffer { _buf = Encoding.UTF8.GetBytes(s).Slice() };
        }

        // The following implement the Stream base class semantics
 
        public override long Length => Len();

        public override bool CanRead => true;

        public override bool CanWrite => true;

        /// Seeking is not supported, and therefore a number of other properties
        /// and methods throw <c>NotImplementedException</c> (Position, Seek);
        public override bool CanSeek => false;

        public override long Position
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var slice = buffer.Slice(offset, offset + count);
            var (n, eof) = this.Read(slice);

            return n;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var slice = buffer.Slice(offset, offset + count);
            this.Write(slice);
        }

        // public override int ReadByte()
        // {
        //     return this.ReadByte();
        // }

        // public override void WriteByte(byte value)
        // {

        // }

       public override void Flush()
        {
            // Noop
        }

        /// Throws <c>NotImplementedException</c>.
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// Throws <c>NotImplementedException</c>.
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
    }

    /// The readOp constants describe the last action performed on
    /// the buffer, so that UnreadRune and UnreadByte can check for
    /// invalid usage. opReadRuneX constants are chosen such that
    /// converted to int they correspond to the rune size that was read.
    internal enum ReadOp
    {
        opRead             = -1, // Any other read operation.
        opInvalid          = 0,  // Non-read operation.
        opReadRune1        = 1,  // Read rune of size 1.
        opReadRune2        = 2,  // Read rune of size 2.
        opReadRune3        = 3,  // Read rune of size 3.
        opReadRune4        = 4,  // Read rune of size 4.
    }

    public static class BufferExtensions
    {
        /// Mimics the same-named Go method, including the
        /// special case for a null buffer, useful for debugging.
        public static string String(this Buffer b)
        {
            if (b == null)
                // Special case, useful in debugging.
                return "<nil>";
            return b.ToString();
        }
    }
}