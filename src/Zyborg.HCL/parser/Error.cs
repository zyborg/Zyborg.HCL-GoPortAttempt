using System;
using gozer;
using Zyborg.HCL.ast;
using Zyborg.HCL.token;

namespace Zyborg.HCL.parser
{
    public class PosErrorException : Exception
    {
        public PosErrorException(Pos pos, string msg = null, Exception inner = null, slice<ObjectKey>? keys = null)
            : base(msg, inner)
        {
            Pos = pos;
            Keys = keys;
        }

        public Pos Pos
        { get; }

        public slice<ObjectKey>? Keys
        { get; }

        public override string ToString()
        {
            return $"At {Pos}:  {base.ToString()}";
        }
    }

    public class ErrEofTokenException : Exception
    {
        public ErrEofTokenException(slice<ObjectKey>? keys = null)
            : base("EOF token found")
        {
            Keys = keys;
        }

        public slice<ObjectKey>? Keys
        { get; }
    }
}