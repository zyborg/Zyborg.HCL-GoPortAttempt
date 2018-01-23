using Zyborg.IO;

namespace gozer.io
{
    public interface IWriter
    {
         int Write(slice<byte> p);
    }
}