using System;
using GoBuffer = gozer.bytes.Buffer;
using gozer.io;
using NStack;

namespace gozer.text
{
    /// Package tabwriter implements a write filter (tabwriter.Writer) that
    /// translates tabbed columns in input into properly aligned text.
    ///
    /// The package is using the Elastic Tabstops algorithm described at
    /// http://nickgravgaard.com/elastictabstops/index.html.
    ///
    /// The text/tabwriter package is frozen and is not accepting new features.



    /// A Writer is a filter that inserts padding around tab-delimited
    /// columns in its input to align them in the output.
    ///
    /// The Writer treats incoming bytes as UTF-8-encoded text consisting
    /// of cells terminated by horizontal ('\t') or vertical ('\v') tabs,
    /// and newline ('\n') or formfeed ('\f') characters; both newline and
    /// formfeed act as line breaks.
    ///
    /// Tab-terminated cells in contiguous lines constitute a column. The
    /// Writer inserts padding as needed to make all cells in a column have
    /// the same width, effectively aligning the columns. It assumes that
    /// all characters have the same width, except for tabs for which a
    /// tabwidth must be specified. Column cells must be tab-terminated, not
    /// tab-separated: non-tab terminated trailing text at the end of a line
    /// forms a cell but that cell is not part of an aligned column.
    /// For instance, in this example (where | stands for a horizontal tab):
    ///
    ///	aaaa|bbb|d
    ///	aa  |b  |dd
    ///	a   |
    ///	aa  |cccc|eee
    ///
    /// the b and c are in distinct columns (the b column is not contiguous
    /// all the way). The d and e are not in a column at all (there's no
    /// terminating tab, nor would the column be contiguous).
    ///
    /// The Writer assumes that all Unicode code points have the same width;
    /// this may not be true in some fonts or if the string contains combining
    /// characters.
    ///
    /// If DiscardEmptyColumns is set, empty columns that are terminated
    /// entirely by vertical (or "soft") tabs are discarded. Columns
    /// terminated by horizontal (or "hard") tabs are not affected by
    /// this flag.
    ///
    /// If a Writer is configured to filter HTML, HTML tags and entities
    /// are passed through. The widths of tags and entities are
    /// assumed to be zero (tags) and one (entities) for formatting purposes.
    ///
    /// A segment of text may be escaped by bracketing it with Escape
    /// characters. The tabwriter passes escaped text segments through
    /// unchanged. In particular, it does not interpret any tabs or line
    /// breaks within the segment. If the StripEscape flag is set, the
    /// Escape characters are stripped from the output; otherwise they
    /// are passed through as well. For the purpose of formatting, the
    /// width of the escaped text is always computed excluding the Escape
    /// characters.
    ///
    /// The formfeed character acts like a newline but it also terminates
    /// all columns in the current line (effectively calling Flush). Tab-
    /// terminated cells in the next line start new columns. Unless found
    /// inside an HTML tag or inside an escaped text segment, formfeed
    /// characters appear as newlines in the output.
    ///
    /// The Writer must buffer input internally, because proper spacing
    /// of one line may depend on the cells in future lines. Clients must
    /// call Flush when done calling Write.
    ///
    public class TabWriter : IWriter
    {

        /// A cell represents a segment of text terminated by tabs or line breaks.
        /// The text itself is stored in a separate buffer; cell only describes the
        /// segment's size in bytes, its width in runes, and whether it's an htab
        /// ('\t') terminated cell.
        class Cell
        {
            /// cell size in bytes
            public int size;
            /// cell width in runes
            public int width;
            /// true if the cell is terminated by an htab ('\t')
            public bool htab;
        }

        // configuration
        private IWriter output;
        private int minwidth;
        private int tabwidth;
        private int padding;
        private byte[] padbytes = new byte[8];
        private Formatting flags;
    
        // current state
        private GoBuffer buf = new GoBuffer(); // collected text excluding tabs or line breaks
        private int pos;          // buffer position up to which cell.width of incomplete cell has been computed
        private Cell cell;         // current incomplete cell; cell.width is up to buf[pos] excluding ignored sections
        private byte endChar;         // terminating char of escaped sequence (Escape for escapes, '>', ';' for HTML tags/entities, or 0)
        private slice<slice<Cell>> lines;     // list of lines; each line is a list of cells
        private slice<int> widths;        // list of column widths in runes - re-used during formatting
    

        private void AddLine()
        {
            this.lines = lines.Append(new slice<Cell>());
        }

        /// Reset the current state.
        private void Reset()
        {
	        buf.Reset();
  	        pos = 0;
  	        cell = new Cell();
  	        endChar = 0;
  	        lines = lines.Slice(0, 0);
  	        widths = widths.Slice(0, 0);
  	        AddLine();
        }

        // Internal representation (current state):
        //
        // - all text written is appended to buf; tabs and line breaks are stripped away
        // - at any given time there is a (possibly empty) incomplete cell at the end
        //   (the cell starts after a tab or line break)
        // - cell.size is the number of bytes belonging to the cell so far
        // - cell.width is text width in runes of that cell from the start of the cell to
        //   position pos; html tags and entities are excluded from this width if html
        //   filtering is enabled
        // - the sizes and widths of processed text are kept in the lines list
        //   which contains a list of cells for each line
        // - the widths list is a temporary list with current widths used during
        //   formatting; it is kept in Writer because it's re-used
        //
        //                    |<---------- size ---------->|
        //                    |                            |
        //                    |<- width ->|<- ignored ->|  |
        //                    |           |             |  |
        // [---processed---tab------------<tag>...</tag>...]
        // ^                  ^                         ^
        // |                  |                         |
        // buf                start of incomplete cell  pos
        
        /// Formatting can be controlled with these flags.
        [Flags]
        public enum Formatting
        {
            None = 0,

            // Ignore html tags and treat entities (starting with '&'
            // and ending in ';') as single characters (width = 1).
            FilterHTML = 1,
        
            // Strip Escape characters bracketing escaped text segments
            // instead of passing them through unchanged with the text.
            StripEscape = 2,
        
            // Force right-alignment of cell content.
            // Default is left-alignment.
            AlignRight = 4,
        
            // Handle empty columns as if they were not present in
            // the input in the first place.
            DiscardEmptyColumns = 8,
        
            // Always use tabs for indentation columns (i.e., padding of
            // leading empty cells on the left) independent of padchar.
            TabIndent = 16,
        
            // Print a vertical bar ('|') between columns (after formatting).
            // Discarded columns appear as zero-width columns ("||").
            Debug = 32,
        }

        /// A Writer must be initialized with a call to Init. The first parameter (output)
        /// specifies the filter output. The remaining parameters control the formatting:
        ///
        ///	minwidth	minimal cell width including any padding
        ///	tabwidth	width of tab characters (equivalent number of spaces)
        ///	padding		padding added to a cell before computing its width
        ///	padchar		ASCII char used for padding
        ///			if padchar == '\t', the Writer will assume that the
        ///			width of a '\t' in the formatted output is tabwidth,
        ///			and cells are left-aligned independent of align_left
        ///			(for correct-looking results, tabwidth must correspond
        ///			to the tab width in the viewer displaying the result)
        ///	flags		formatting control
        ///
        public TabWriter Init(IWriter output, int minwidth, int tabwidth, int padding, byte padchar, Formatting flags)
        {
            if (minwidth < 0 || tabwidth < 0 || padding < 0)
                throw new Exception("negative minwidth, tabwidth, or padding");

            this.output = output;
            this.minwidth = minwidth;
            this.tabwidth = tabwidth;
            this.padding = padding;
            foreach (var (i, _) in this.padbytes.Range())
            {
                this.padbytes[i] = padchar;
            }
            
            if (padchar == '\t')
            {
                // tab padding enforces left-alignment
                flags &= ~Formatting.AlignRight;
                //flags &^= AlignRight
            }
            
            this.flags = flags;
        
            Reset();
        
            return this;
        }


        /// debugging support (keep code around)
        private void Dump()
        {
            var pos = 0;
            foreach (var (i, line) in lines.Range())
            {
                Console.Write($"({i}) ");
                foreach (var c in line)
                {
                    Console.Write($"[{buf.Bytes().Slice(pos, pos + c.size).AsString()}]");
                    pos += c.size;
                }
                Console.WriteLine();
            }
            Console.WriteLine();
        }
        
        // local error wrapper so we can distinguish errors we want to return
        // as errors from genuine panics (which we don't want to return as errors)
        class OsError
        {
            public Exception err;
        }
        
        private void Write0(slice<byte> buf)
        {
            buf.WriteTo(output);
        }

        private void WriteN(slice<byte> src, int n)
        {
            while (n > src.Length)
            {
                Write0(src);
                n -= src.Length;
            }
            Write0(src.Slice(0, n));
        }

        
        private slice<byte> newline = slice.From((byte)'\n');
        private slice<byte> tabs = "\t\t\t\t\t\t\t\t".AsByteSlice();
        
        private void WritePadding(int textw, int cellw, bool useTabs)
        {
            if (padbytes[0] == '\t' || useTabs)
            {
                // padding is done with tabs
                if (tabwidth == 0)
                    return; // tabs have no width - can't do any padding

                // make cellw the smallest multiple of b.tabwidth
                cellw = (cellw + tabwidth - 1) / tabwidth * tabwidth;
                var n = cellw - textw; // amount of padding
                if (n < 0)
                    throw new Exception("internal error");

                WriteN(tabs, (n + tabwidth - 1) / tabwidth);
                return;
            }
        
            // padding is done with non-tab characters
            WriteN(padbytes.Slice(0), cellw - textw);
        }
        
        private slice<byte> vbar = slice.From((byte)'|');

        private int WriteLines(int pos0, int line0, int line1)
        {
            var pos = pos0;
            for (var i = line0; i < line1; i++)
            {
                var line = lines[i];
        
                // if TabIndent is set, use tabs to pad leading empty cells
                var useTabs = (flags & Formatting.TabIndent) != 0;
        
                foreach (var (j, c) in line.Range())
                {
                    if (j > 0 && (flags & Formatting.Debug) != 0)
                    {
                        // indicate column break
                        Write0(vbar);
                    }
        
                    if (c.size == 0)
                    {
                        // empty cell
                        if (j < widths.Length)
                        {
                            WritePadding(c.width, widths[j], useTabs);
                        }
                    }
                    else
                    {
                        // non-empty cell
                        useTabs = false;
                        if ((flags & Formatting.AlignRight) == 0)
                        { // align left
                            Write0(buf.Bytes().Slice(pos, pos + c.size));
                            pos += c.size;
                            if (j < widths.Length)
                            {
                                WritePadding(c.width, widths[j], false);
                            }
                        }
                        else
                        { // align right
                            if (j < widths.Length)
                            {
                                WritePadding(c.width, widths[j], false);
                            }
                            Write0(buf.Bytes().Slice(pos, pos + c.size));
                            pos += c.size;
                        }
                    }
                }
        
                if (i + 1 == lines.Length)
                {
                    // last buffered line - we don't have a newline, so just write
                    // any outstanding buffered data
                    Write0(buf.Bytes().Slice(pos, pos + cell.size));
                    pos += cell.size;
                }
                else
                {
                    // not the last line - write newline
                    Write0(newline);
                }
            }
            return pos;
        }

        /// Format the text between line0 and line1 (excluding line1); pos
        /// is the buffer position corresponding to the beginning of line0.
        /// Returns the buffer position corresponding to the beginning of
        /// line1 and an error, if any.
        ///
        private int Format(int pos0, int line0, int line1)
        {
            var pos = pos0;
            var column = widths.Length;
            for (var thisLine = line0; thisLine < line1; thisLine++)
            {
                var line = lines[thisLine];
        
                if (column < line.Length - 1)
                {
                    // cell exists in this column => this line
                    // has more cells than the previous line
                    // (the last cell per line is ignored because cells are
                    // tab-terminated; the last cell per line describes the
                    // text before the newline/formfeed and does not belong
                    // to a column)
        
                    // print unprinted lines until beginning of block
                    pos = WriteLines(pos, line0, thisLine);
                    line0 = thisLine;
        
                    // column block begin
                    var width = minwidth; // minimal column width
                    var discardable = true; // true if all cells in this column are empty and "soft"
                    for (; thisLine < line1; thisLine++)
                    {
                        line = lines[thisLine];
                        if (column < line.Length - 1)
                        {
                            // cell exists in this column
                            var c = line[column];
                            // update width
                            var w = c.width + padding;
                            if (w > width)
                            {
                                width = w;
                            }
                            // update discardable
                            if (c.width > 0 || c.htab)
                            {
                                discardable = false;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    // column block end
        
                    // discard empty columns if necessary
                    if (discardable && (flags & Formatting.DiscardEmptyColumns) != 0)
                    {
                        width = 0;
                    }
        
                    // format and print all columns to the right of this column
                    // (we know the widths of this column and all columns to the left)
                    widths = widths.Append(width); // push width
                    pos = Format(pos, line0, thisLine);
                    widths = widths.Slice(0, widths.Length - 1); // pop width
                    line0 = thisLine;
                }
            }
        
            // print unprinted lines until end
            return WriteLines(pos, line0, line1);
        }
        
        // Append text to current cell.
        private void Append(slice<byte> text)
        {
            buf.Write(text);
            cell.size += text.Length;
        }
        
        // Update the cell width.
        private void UpdateWidth()
        {
            cell.width += Utf8.RuneCount(buf.Bytes().Slice(pos, buf.Len()).ToArray());
            pos = buf.Len();
        }

        /// To escape a text segment, bracket it with Escape characters.
        /// For instance, the tab in this string "Ignore this tab: \xff\t\xff"
        /// does not terminate a cell and constitutes a single character of
        /// width one for formatting purposes.
        ///
        /// The value 0xff was chosen because it cannot appear in a valid UTF-8 sequence.
        ///
        public const byte Escape = (byte)'\xff';
        
        // Start escaped mode.
        private void StartEscape(byte ch)
        {
            switch (ch)
            {
                case Escape:
                    endChar = Escape;
                    break;
                case (byte)'<':
                    endChar = (byte)'>';
                    break;
                case (byte)'&':
                    endChar = (byte)';';
                    break;
            }
        }

        /// Terminate escaped mode. If the escaped text was an HTML tag, its width
        /// is assumed to be zero for formatting purposes; if it was an HTML entity,
        /// its width is assumed to be one. In all other cases, the width is the
        /// unicode width of the text.
        ///
        private void EndEscape()
        {
            switch (endChar)
            {
                case Escape:
                    UpdateWidth();
                    if ((flags & Formatting.StripEscape) == 0)
                    {
                        cell.width -= 2; // don't count the Escape chars
                    }
                    break;
                case (byte)'>': // tag of zero width
                    cell.width++; // entity, count as one rune
                    break;
                case (byte)';':
                    cell.width++; // entity, count as one rune
                    break;
            }
            pos = buf.Len();
            endChar = 0;
        }

        /// Terminate the current cell by adding it to the list of cells of the
        /// current line. Returns the number of cells in that line.
        ///
        private int TerminateCell(bool htab)
        {
            cell.htab = htab;
            var lineIndex = lines.Length - 1;
            lines[lineIndex] = lines[lineIndex].Append(cell);
            cell = new Cell();
            return lines[lineIndex].Length;
        }

        private void HandlePanic(Exception err, string op)
        {
            throw new NotImplementedException($"{op}:  {err}");

            // if e := recover(); e != nil {
            //     if nerr, ok := e.(osError); ok {
            //         *err = nerr.err
            //         return
            //     }
            //     panic("tabwriter: panic during " + op)
            // }
        }


        /// Flush should be called after the last call to Write to ensure
        /// that any data buffered in the Writer is written to output. Any
        /// incomplete escape sequence at the end is considered
        /// complete for formatting purposes.
        public void Flush()
        {
            FlushInternal();
        }
        


        private void FlushInternal()
        {
            using (Defer.Call(() => Reset())) // even in the presence of errors
            {
                try
                {
                    // add current cell if not empty
                    if (cell.size > 0)
                    {
                        if (endChar != 0)
                        {
                            // inside escape - terminate it even if incomplete
                            EndEscape();
                        }
                        TerminateCell(false);
                    }
                
                    // format contents of buffer
                    Format(0, 0, lines.Length);

                }
                catch (Exception ex)
                {
                    HandlePanic(ex, "Flush");                
                }
            }
        }
        
        private slice<byte> hbar = "---\n".AsByteSlice();

        /// Write writes buf to the writer b.
        /// The only errors returned are ones encountered
        /// while writing to the underlying output stream.
        ///
        public int Write(slice<byte> buf)
        {
            // split text into cells
            var n = 0;

            try
            {
                foreach (var (i, ch) in buf.Range())
                {
                    if (endChar == 0)
                    {
                        // outside escape
                        Switch.Begin((char)ch)
                            .Case('\t', '\v', '\n', '\f')
                            .Then(() => {
                                // end of cell
                                Append(buf.Slice(n, i));
                                UpdateWidth();
                                n = i + 1; // ch consumed
                                var ncells = TerminateCell(ch == '\t');
                                if (ch == '\n' || ch == '\f')
                                {
                                    // terminate line
                                    AddLine();
                                    if (ch == '\f' || ncells == 1)
                                    {
                                        // A '\f' always forces a flush. Otherwise, if the previous
                                        // line has only one cell which does not have an impact on
                                        // the formatting of the following lines (the last cell per
                                        // line is ignored by format()), thus we can flush the
                                        // Writer contents.
                                        try { Flush(); } catch (Exception) { return; }
                                        if (ch == '\f' && (flags & Formatting.Debug) != 0)
                                        {
                                            // indicate section break
                                            Write0(hbar);
                                        }
                                    }
                                }
                            })
                            .Case((char)Escape)
                            .Then(() => {
                                // start of escaped sequence
                                Append(buf.Slice(n, i));
                                UpdateWidth();
                                n = i;
                                if ((flags & Formatting.StripEscape) != 0)
                                {
                                    n++; // strip Escape
                                }
                                StartEscape(Escape);
                            })
                            .Case('<', '&')
                            .Then(() => {
                                // possibly an html tag/entity
                                if ((flags & Formatting.FilterHTML) != 0) {
                                    // begin of tag/entity
                                    Append(buf.Slice(n, i));
                                    UpdateWidth();
                                    n = i;
                                    StartEscape(ch);
                                }
                            })
                            .Eval();
            
                    }
                    else
                    {
                        // inside escape
                        if (ch == endChar)
                        {
                            // end of tag/entity
                            var j = i + 1;
                            if (ch == Escape && (flags & Formatting.StripEscape) != 0)
                            {
                                j = i; // strip Escape
                            }
                            Append(buf.Slice(n, j));
                            n = i + 1; // ch consumed
                            EndEscape();
                        }
                    }
                }
            
                // append leftover text
                Append(buf.Slice(n));
                n = buf.Length;
            }
            catch (Exception ex)
            {
                HandlePanic(ex, "Write");
            }
            return n;
        }

        /// NewWriter allocates and initializes a new tabwriter.Writer.
        /// The parameters are the same as for the Init function.
        ///
        public static TabWriter NewWriter(IWriter output, int minwidth, int tabwidth, int padding, byte padchar, Formatting flags)
        {
            return new TabWriter().Init(output, minwidth, tabwidth, padding, padchar, flags);
        }
    }
}
