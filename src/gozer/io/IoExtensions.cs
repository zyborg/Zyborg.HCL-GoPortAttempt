using System.IO;
using System.Linq;
using System.Text;

namespace gozer.io
{
    public static class IoExtensions
    {
        public static void WriteTo(this slice<byte> slice, IWriter s)
        {
            s.Write(slice);
        }

        public static void WriteString(this Stream w, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            w.Write(bytes, 0, bytes.Length);
        }

        public static void WriteString(this IWriter w, string s)
        {
            w.Write(s.ToCharArray().Select(x => (byte)x).ToArray().Slice());
            // w.Write(s.AsByteSlice());
        }
    }
}