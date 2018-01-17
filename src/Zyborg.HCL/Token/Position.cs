namespace Zyborg.HCL.Token
{
    /// Pos describes an arbitrary source position
    /// including the file, line, and column location.
    /// A Position is valid if the line number is > 0.
    public struct Pos
    {
        public Pos(string f, int o, int l, int c)
        {
            Filename = f;
            Offset = o;
            Line = l;
            Column = c;
        }

        /// filename, if any
        public string Filename;

        /// offset, starting at 0
        public int Offset;

        /// line number, starting at 1
        public int Line;

        /// column number, starting at 1 (character count)
        public int Column;

        /// returns true if the position is valid.
        public bool IsValid => Line > 0;

        /// Before reports whether the position p is before u.
        public bool Before(Pos u) => u.Offset > this.Offset || u.Line > this.Line;

        /// After reports whether the position p is after u.
        public bool After(Pos u) => u.Offset < this.Offset || u.Line < this.Line;

        /// String returns a string in one of several forms:
        /// <code>
        ///	file:line:column    valid position with file name
        ///	line:column         valid position without file name
        ///	file                invalid position with file name
        ///	-                   invalid position without file name
        /// </code>
        public override string ToString()
        {
            var s = this.Filename;
            if (this.IsValid)
            {
                if (!string.IsNullOrEmpty(s))
                {
                    s += ":";
                }
                s += $"{Line}:{Column}";
            }
            if (s == "")
            {
                s = "-";
            }
            return s;
        }
    }
}