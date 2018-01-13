namespace Zyborg.HCL.Token
{
    /// Pos describes an arbitrary source position
    /// including the file, line, and column location.
    /// A Position is valid if the line number is > 0.
    public class Pos
    {
        /// filename, if any
        public string Filename
        { get; set; }

        /// offset, starting at 0
        public int Offset
        { get; set; }

        /// line number, starting at 1
        public int Line
        { get; set; }

        /// column number, starting at 1 (character count)
        public int Column
        { get; set; }

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
                if (s != "")
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