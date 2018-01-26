using System.IO;
using IOFile = System.IO.File;
using gozer;
using GoBuffer = gozer.bytes.Buffer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zyborg.HCL.ast;
using Zyborg.HCL.parser;
using System.Collections.Generic;
using Zyborg.MSTest;
using System.Linq;

namespace Zyborg.HCL.printer
{
    [TestClass]
    public class PrinterTests
    {
        public TestContext TestContext
        { get; set; }
        
        //var update = flag.Bool("update", false, "update golden files")
        const bool update = false;

        const string dataDir = "testdata";

        public class Entry
        {
            public string source;
            public string golden;

            public Entry(string s, string g)
            {
                source = s;
                golden = g;
            }
        }

        // Use go test -update to create/update the respective golden files.
        static slice<Entry> data = slice.From(
            // // new Entry("complexhcl.input", "complexhcl.golden"),
            // new Entry("list.input", "list.golden"),
            // new Entry("list_comment.input", "list_comment.golden"),
            // // new Entry("comment.input", "comment.golden"),
            // // new Entry("comment_crlf.input", "comment.golden"),
            // // new Entry("comment_aligned.input", "comment_aligned.golden"),
            // new Entry("comment_array.input", "comment_array.golden"),
            // // new Entry("comment_end_file.input", "comment_end_file.golden"),
            // // new Entry("comment_multiline_indent.input", "comment_multiline_indent.golden"),
            // // new Entry("comment_multiline_no_stanza.input", "comment_multiline_no_stanza.golden"),
            // // new Entry("comment_multiline_stanza.input", "comment_multiline_stanza.golden"),
            new Entry("comment_newline.input", "comment_newline.golden"),
            // new Entry("comment_object_multi.input", "comment_object_multi.golden"),
            // new Entry("comment_standalone.input", "comment_standalone.golden"),
            // new Entry("empty_block.input", "empty_block.golden"),
            // new Entry("list_of_objects.input", "list_of_objects.golden"),
            // new Entry("multiline_string.input", "multiline_string.golden"),
            // new Entry("object_singleline.input", "object_singleline.golden"),
            new Entry("object_with_heredoc.input", "object_with_heredoc.golden")
        );

        static IEnumerable<object[]> TestDataEntries() => data.Select(x => new[] { x });
        static string TestDataEntryName(MethodDataSourceAttribute att, Entry e) =>
                $"{att.TestMethod.Name} / {e.source}";

        [DataTestMethod]
        [MethodDataSource(typeof(PrinterTests),
                nameof(TestDataEntries), nameof(TestDataEntryName))]
        public void TestFiles(Entry e)
        {
            // foreach (var e in data) {
                TestContext.WriteLine($"Testing {e.source}");

                var source = Path.Combine(dataDir, e.source);
                var golden = Path.Combine(dataDir, e.golden);
                
                Check(source, golden);
                // t.Run(e.source, func(t *testing.T) {
                //     check(t, source, golden)
                // })
            // }
        }

        void Check(string source, string golden)
        {
            var src = IOFile.ReadAllBytes(source);

            var res = Format(src.Slice());

            // update golden files if necessary
            if (update) {
                IOFile.WriteAllBytes(golden, res.ToArray());
            }

            // get golden
            var gld = IOFile.ReadAllBytes(golden);

            // formatted source and golden must be the same
            Diff(source, golden, res, gld.Slice());
        }

        /// diff compares a and b.
        void Diff(string aname, string bname, slice<byte> a, slice<byte> b)
        {
            var buf = new GoBuffer(); // holding long error message

            // compare lengths
            if (a.Length != b.Length) {
                buf.WriteString($"\nlength changed: len({aname}) = {a.Length},"
                        + " len({bname}) = {b.Length}");
            }

            // compare contents
            var line = 1;
            var offs = 1;
            for (var i = 0; i < a.Length && i < b.Length; i++) {
                var ch = a[i];
                if (ch != b[i]) {
                    buf.WriteString(string.Format("\n{0}:{1}:{2}: {3}", aname, line, i - offs + 1,
                            LineAt(a, offs)));
                    buf.WriteString(string.Format("\n{0}:{1}:{2}: {3}", bname, line, i - offs + 1,
                            LineAt(b, offs)));
                    buf.WriteString("\n\n");
                    break;
                }
                if (ch == '\n') {
                    line++;
                    offs = i + 1;
                }
            }

            if (buf.Len() > 0) {
                throw new IOException(buf.ToString());
            }
        }

        /// format parses src, prints the corresponding AST, verifies the resulting
        /// src is syntactically correct, and returns the resulting src or an error
        /// if any.
        slice<byte> Format(slice<byte> src)
        {
            var formatted = Printer.Format(src);

            // make sure formatted output is syntactically correct
            Parser.Parse(formatted);

            return formatted;
        }

        /// lineAt returns the line in text starting at offset offs.
        slice<byte> LineAt(slice<byte> text, int offs)
        {
            var i = offs;
            while (i < text.Length && text[i] != '\n') {
                i++;
            }
            return text.Slice(offs, i);
        }
    }
}