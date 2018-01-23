using gozer;
using GoBuffer = gozer.bytes.Buffer;
using gozer.io;
using gozer.text;
using Zyborg.HCL.ast;
using Zyborg.HCL.parser;

namespace Zyborg.HCL.printer
{
    public partial class Printer
    {
        public static readonly Config DefaultConfig = new Config { SpacesWidth = 2 };

        // A Config node controls the output of Fprint.
        public class Config
        {
            /// if set, it will use spaces instead of tabs for alignment
            public int SpacesWidth
            { get; set; }

            public void Fprint(IWriter output, INode node)
            {
                var p = new Printer
                {
                    cfg = this,
                    comments = slice.Make<CommentGroup>(0),
                    standaloneComments = slice.Make<CommentGroup>(0),
                    // enableTrace:        true,
                };

                p.CollectComments(node);

                output.Write(p.Unindent(p.Output(node)));

                // flush tabwriter, if any
                var tw = output as TabWriter;
                if (tw != null)
                    tw.Flush();
            }
        }

        /// Fprint "pretty-prints" an HCL node to output
        /// It calls Config.Fprint with default settings.
        public static void Fprint(IWriter output, INode node)
        {
            DefaultConfig.Fprint(output, node);
        }

        // Format formats src HCL and returns the result.
        public static slice<byte> Format(slice<byte> src)
        {
            var node = Parser.Parse(src);

            var buf = new GoBuffer();
            DefaultConfig.Fprint(buf, node);

            // Add trailing newline to result
            buf.WriteString("\n");
            return buf.Bytes();
        }
    }
}