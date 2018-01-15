using System;
using System.IO;

namespace Zyborg.IO
{
    /// Simple byte buffer for marshaling data.
    /// A Buffer is a variable-sized buffer of bytes with Read and Write methods.
    /// The zero value for Buffer is an empty buffer ready to use.
    public class GoBuffer
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
        private void ErrTooLarge() => throw new IOException("bytes.Buffer: too large");

        // /// Bytes returns a slice of length b.Len() holding the unread portion of the buffer.
        // /// The slice is valid for use only until the next buffer modification (that is,
        // /// only until the next call to a method like Read, Write, Reset, or Truncate).
        // /// The slice aliases the buffer content at least until the next buffer modification,
        // /// so immediate changes to the slice will affect the result of future reads.
        // public byte[] Bytes() => ((Span<byte>)_buf).Slice(_off).ToArray();

        // /// String returns the contents of the unread portion of the buffer
        // /// as a string. If the Buffer is a nil pointer, it returns "&lt;nil&gt;".
        // public override string ToString() => Bytes().ToString();

        // /// Len returns the number of bytes of the unread portion of the buffer;
        // /// b.Len() == len(b.Bytes()).
        // public int Len() => _buf.Length - _off;

        // /// Cap returns the capacity of the buffer's underlying byte slice, that is, the
        // /// total space allocated for the buffer's data.
        // pu8blic int Cap() => 
        // func (b *Buffer) Cap() int { return cap(b.buf) }

        // /// Truncate discards all but the first n unread bytes from the buffer
        // /// but continues to use the same allocated storage.
        // /// It panics if n is negative or greater than the length of the buffer.
        // func (b *Buffer) Truncate(n int) {
        //     if n == 0 {
        //         b.Reset()
        //         return
        //     }
        //     b.lastRead = opInvalid
        //     if n < 0 || n > b.Len() {
        //         panic("bytes.Buffer: truncation out of range")
        //     }
        //     b.buf = b.buf[0 : b.off+n]
        // }



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

}