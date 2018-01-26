using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using gozer.io;
using static gozer.text.TabWriter;
using NStack;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Zyborg.MSTest;

namespace gozer.text
{
    [TestClass]
    public class TabWriterTests
    {
        public TestContext TestContext
        { get; set; }

        class Buffer : IWriter
        {
            slice<byte> a;
            
            private void Init(int n)
            {
                a = slice.Make<byte>(0, n);
            }
            
            private void Clear()
            {
                a = a.Slice(0, 0);
            }
            
            public int Write(slice<byte> buf)
            {
                var n = a.Length;
                var m = buf.Length;
                if (n + m <= a.Capacity)
                {
                    a = a.Slice(0, n + m);
                    for (var i = 0; i < m; i++)
                    {
                        a[n + i] = buf[i];
                    }
                }
                else
                {
                    throw new Exception("buffer.Write: buffer too small");
                }
                return buf.Length;
            }

            public ustring ToUstring() => a.AsUstring();

            internal static void Write(string testname, TabWriter w, ustring src)
            {
                w.WriteString(src);
            }
            
            internal static void Verify(TestContext tc, string testname, TabWriter w, Buffer b, ustring src, ustring expected)
            {
                w.Flush();
                var res = b.ToUstring();

                // DBG:
                tc.WriteLine($"  expected = {expected.Slice()}");
                tc.WriteLine($"     found = {res.Slice()}");

                Assert.AreEqual(expected, res,
                        $"--- test: {testname}\n--- src:\n{src}\n--- found:\n{res}\n--- expected:\n{expected}\n");
            }
            
            internal static void Check(TestContext tc, string testname, int minwidth, int tabwidth, int padding, byte padchar,
                    TabWriter.Formatting flags, ustring src, ustring expected)
            {
                var b = new Buffer();
                b.Init(1000);
            
                var w = new TabWriter();
                w.Init(b, minwidth, tabwidth, padding, padchar, flags);
            
                // write all at once
                var title = testname + " (written all at once)";
                b.Clear();
                Write(title, w, src);
                Verify(tc, title, w, b, src, expected);
            
                // write byte-by-byte
                title = testname + " (written byte-by-byte)";
                b.Clear();
                for (var i = 0; i < src.Length; i++)
                {
                    Write(title, w, src.ToByteArray().Slice(i, i + 1).ToArray());
                }
                Verify(tc, title, w, b, src, expected);
            
                // write using Fibonacci slice sizes
                title = testname + " (written in fibonacci slices)";
                b.Clear();
                for (var (i, d) = (0, 0); i < src.Length; )
                {
                    Write(title, w, src.ToByteArray().Slice(i, i + d).ToArray());
                    (i, d) = (i + d, d + 1);
                    if (i + d > src.Length)
                    {
                        d = src.Length - i;
                    }
                }
                Verify(tc, title, w, b, src, expected);
            }
        }

        static (string testname, int minwidth, int tabwidth, int padding, char padchar,
            Formatting flags, ustring src, ustring expected)[] tests = new[]
        {
            (
                "1a",
                8, 0, 1, '.', Formatting.None,
                "",
                ""
            ),
            (
                "1a",
                8, 0, 1, '.', Formatting.None,
                "",
                ""
            ),
        
            (
                "1a debug",
                8, 0, 1, '.', Formatting.Debug,
                "",
                ""
            ),
        
            (
                "1b esc stripped",
                8, 0, 1, '.', Formatting.StripEscape,
                slice.Make($"{0xff::}{0xff::}").AsUstring(),
                (ustring)""
            ),
            (
                "1b esc",
                8, 0, 1, '.', Formatting.None,
                slice.Make($"{0xff::}{0xff::}").AsUstring(),
                slice.Make($"{0xff::}{0xff::}").AsUstring()
            ),

            (
                "1c esc stripped",
                8, 0, 1, '.', Formatting.StripEscape,
                slice.Make($"{0xff::}\t{0xff::}").AsUstring(),
                "\t"
            ),

            (
                "1c esc",
                8, 0, 1, '.', Formatting.None,
                slice.Make($"{0xff::}\t{0xff::}").AsUstring(),
                slice.Make($"{0xff::}\t{0xff::}").AsUstring()
            ),
            (
                "1d esc stripped",
                8, 0, 1, '.', Formatting.StripEscape,
                slice.Make($"{0xff::}\"foo\t\n\tbar\"{0xff::}").AsUstring(),
                "\"foo\t\n\tbar\""
            ),
            (
                "1d esc",
                8, 0, 1, '.', Formatting.None,
                slice.Make($"{0xff::}\"foo\t\n\tbar\"{0xff::}").AsUstring(),
                slice.Make($"{0xff::}\"foo\t\n\tbar\"{0xff::}").AsUstring()
            ),
            (
                "1e esc stripped",
                8, 0, 1, '.', Formatting.StripEscape,
                slice.Make($"abc{0xff::}\tdef").AsUstring(), // unterminated escape
                "abc\tdef"
            ),
            (
                "1e esc",
                8, 0, 1, '.', Formatting.None,
                slice.Make($"abc{0xff::}\tdef").AsUstring(), // unterminated escape
                slice.Make($"abc{0xff::}\tdef").AsUstring()
            ),
        
            (
                "2",
                8, 0, 1, '.', Formatting.None,
                "\n\n\n",
                "\n\n\n"
            ),
        
            (
                "3",
                8, 0, 1, '.', Formatting.None,
                "a\nb\nc",
                "a\nb\nc"
            ),
        
            (
                "4a",
                8, 0, 1, '.', Formatting.None,
                "\t", // '\t' terminates an empty cell on last line - nothing to print
                ""
            ),
        
            (
                "4b",
                8, 0, 1, '.', Formatting.AlignRight,
                "\t", // '\t' terminates an empty cell on last line - nothing to print
                ""
            ),
        
            (
                "5",
                8, 0, 1, '.', Formatting.None,
                "*\t*",
                "*.......*"
            ),
        
            (
                "5b",
                8, 0, 1, '.', Formatting.None,
                "*\t*\n",
                "*.......*\n"
            ),
        
            (
                "5c",
                8, 0, 1, '.', Formatting.None,
                "*\t*\t",
                "*.......*"
            ),
        
            (
                "5c debug",
                8, 0, 1, '.', Formatting.Debug,
                "*\t*\t",
                "*.......|*"
            ),
        
            (
                "5d",
                8, 0, 1, '.', Formatting.AlignRight,
                "*\t*\t",
                ".......**"
            ),
        
            (
                "6",
                8, 0, 1, '.', Formatting.None,
                "\t\n",
                "........\n"
            ),
        
            (
                "7a",
                8, 0, 1, '.', Formatting.None,
                "a) foo",
                "a) foo"
            ),
        
            (
                "7b",
                8, 0, 1, ' ', Formatting.None,
                "b) foo\tbar",
                "b) foo  bar"
            ),
        
            (
                "7c",
                8, 0, 1, '.', Formatting.None,
                "c) foo\tbar\t",
                "c) foo..bar"
            ),
        
            (
                "7d",
                8, 0, 1, '.', Formatting.None,
                "d) foo\tbar\n",
                "d) foo..bar\n"
            ),
        
            (
                "7e",
                8, 0, 1, '.', Formatting.None,
                "e) foo\tbar\t\n",
                "e) foo..bar.....\n"
            ),

            (
                "7f",
                8, 0, 1, '.', Formatting.FilterHTML,
                (ustring)"f) f&lt;o\t<b>bar</b>\t\n",
                (ustring)"f) f&lt;o..<b>bar</b>.....\n"
            ),
        
            (
                "7g",
                8, 0, 1, '.', Formatting.FilterHTML,
                (ustring)"g) f&lt;o\t<b>bar</b>\t non-terminated entity &amp",
                (ustring)"g) f&lt;o..<b>bar</b>..... non-terminated entity &amp"
            ),

            (
                "7g debug",
                8, 0, 1, '.', Formatting.FilterHTML | Formatting.Debug,
                (ustring)"g) f&lt;o\t<b>bar</b>\t non-terminated entity &amp",
                (ustring)"g) f&lt;o..|<b>bar</b>.....| non-terminated entity &amp"
            ),
        
            (
                "8",
                8, 0, 1, '*', Formatting.None,
                (ustring)"Hello, world!\n",
                (ustring)"Hello, world!\n"
            ),
        
            (
                "9a",
                1, 0, 0, '.', Formatting.None,
                (ustring)"1\t2\t3\t4\n" +
                    "11\t222\t3333\t44444\n",
        
                (ustring)"1.2..3...4\n" +
                    "11222333344444\n"
            ),

            (
                "9b",
                1, 0, 0, '.', Formatting.FilterHTML,
                (ustring)"1\t2<!---\f--->\t3\t4\n" + // \f inside HTML is ignored
                    "11\t222\t3333\t44444\n",
        
                (ustring)"1.2<!---\f--->..3...4\n" +
                    "11222333344444\n"
            ),
        
            (
                "9c",
                1, 0, 0, '.', Formatting.None,
                (ustring)"1\t2\t3\t4\f" + // \f causes a newline and flush
                    "11\t222\t3333\t44444\n",
        
                (ustring)"1234\n" +
                    "11222333344444\n"
            ),
        
            (
                "9c debug",
                1, 0, 0, '.', Formatting.Debug,
                (ustring)"1\t2\t3\t4\f" + // \f causes a newline and flush
                    "11\t222\t3333\t44444\n",
        
                (ustring)"1|2|3|4\n" +
                    "---\n" +
                    "11|222|3333|44444\n"
            ),
        
            (
                "10a",
                5, 0, 0, '.', Formatting.None,
                (ustring)"1\t2\t3\t4\n",
                (ustring)"1....2....3....4\n"
            ),
        
            (
                "10b",
                5, 0, 0, '.', Formatting.None,
                (ustring)"1\t2\t3\t4\t\n",
                (ustring)"1....2....3....4....\n"
            ),
        
            (
                "11",
                8, 0, 1, '.', Formatting.None,
                (ustring)"本\tb\tc\n" +
                    "aa\t\u672c\u672c\u672c\tcccc\tddddd\n" +
                    "aaa\tbbbb\n",
        
                (ustring)"本.......b.......c\n" +
                    "aa......本本本.....cccc....ddddd\n" +
                    "aaa.....bbbb\n"
            ),
            (
                "12a",
                8, 0, 1, ' ', Formatting.AlignRight,
                (ustring)"a\tè\tc\t\n" +
                    "aa\tèèè\tcccc\tddddd\t\n" +
                    "aaa\tèèèè\t\n",
        
                (ustring)"       a       è       c\n" +
                    "      aa     èèè    cccc   ddddd\n" +
                    "     aaa    èèèè\n"
            ),
            (
                "12b",
                2, 0, 0, ' ', Formatting.None,
                (ustring)"a\tb\tc\n" +
                    "aa\tbbb\tcccc\n" +
                    "aaa\tbbbb\n",
        
                (ustring)"a  b  c\n" +
                    "aa bbbcccc\n" +
                    "aaabbbb\n"
            ),        
            (
                "12c",
                8, 0, 1, '_', Formatting.None,
                (ustring)"a\tb\tc\n" +
                    "aa\tbbb\tcccc\n" +
                    "aaa\tbbbb\n",
        
                (ustring)"a_______b_______c\n" +
                    "aa______bbb_____cccc\n" +
                    "aaa_____bbbb\n"
            ),
        
            (
                "13a",
                4, 0, 1, '-', Formatting.None,
                (ustring)"4444\t日本語\t22\t1\t333\n" +
                    "999999999\t22\n" +
                    "7\t22\n" +
                    "\t\t\t88888888\n" +
                    "\n" +
                    "666666\t666666\t666666\t4444\n" +
                    "1\t1\t999999999\t0000000000\n",
        
                (ustring)"4444------日本語-22--1---333\n" +
                    "999999999-22\n" +
                    "7---------22\n" +
                    "------------------88888888\n" +
                    "\n" +
                    "666666-666666-666666----4444\n" +
                    "1------1------999999999-0000000000\n"
            ),
        
            (
                "13b",
                4, 0, 3, '.', Formatting.None,
                (ustring)"4444\t333\t22\t1\t333\n" +
                    "999999999\t22\n" +
                    "7\t22\n" +
                    "\t\t\t88888888\n" +
                    "\n" +
                    "666666\t666666\t666666\t4444\n" +
                    "1\t1\t999999999\t0000000000\n",
        
                (ustring)"4444........333...22...1...333\n" +
                    "999999999...22\n" +
                    "7...........22\n" +
                    "....................88888888\n" +
                    "\n" +
                    "666666...666666...666666......4444\n" +
                    "1........1........999999999...0000000000\n"
            ),
        
            (
                "13c",
                8, 8, 1, '\t', Formatting.FilterHTML,
                "4444\t333\t22\t1\t333\n" +
                    "999999999\t22\n" +
                    "7\t22\n" +
                    "\t\t\t88888888\n" +
                    "\n" +
                    "666666\t666666\t666666\t4444\n" +
                    "1\t1\t<font color=red attr=日本語>999999999</font>\t0000000000\n",
        
                "4444\t\t333\t22\t1\t333\n" +
                    "999999999\t22\n" +
                    "7\t\t22\n" +
                    "\t\t\t\t88888888\n" +
                    "\n" +
                    "666666\t666666\t666666\t\t4444\n" +
                    "1\t1\t<font color=red attr=日本語>999999999</font>\t0000000000\n"
            ),
        
            (
                "14",
                1, 0, 2, ' ', Formatting.AlignRight,
                ".0\t.3\t2.4\t-5.1\t\n" +
                    "23.0\t12345678.9\t2.4\t-989.4\t\n" +
                    "5.1\t12.0\t2.4\t-7.0\t\n" +
                    ".0\t0.0\t332.0\t8908.0\t\n" +
                    ".0\t-.3\t456.4\t22.1\t\n" +
                    ".0\t1.2\t44.4\t-13.3\t\t",
        
                "    .0          .3    2.4    -5.1\n" +
                    "  23.0  12345678.9    2.4  -989.4\n" +
                    "   5.1        12.0    2.4    -7.0\n" +
                    "    .0         0.0  332.0  8908.0\n" +
                    "    .0         -.3  456.4    22.1\n" +
                    "    .0         1.2   44.4   -13.3"
            ),
        
            (
                "14 debug",
                1, 0, 2, ' ', Formatting.AlignRight | Formatting.Debug,
                ".0\t.3\t2.4\t-5.1\t\n" +
                    "23.0\t12345678.9\t2.4\t-989.4\t\n" +
                    "5.1\t12.0\t2.4\t-7.0\t\n" +
                    ".0\t0.0\t332.0\t8908.0\t\n" +
                    ".0\t-.3\t456.4\t22.1\t\n" +
                    ".0\t1.2\t44.4\t-13.3\t\t",
        
                "    .0|          .3|    2.4|    -5.1|\n" +
                    "  23.0|  12345678.9|    2.4|  -989.4|\n" +
                    "   5.1|        12.0|    2.4|    -7.0|\n" +
                    "    .0|         0.0|  332.0|  8908.0|\n" +
                    "    .0|         -.3|  456.4|    22.1|\n" +
                    "    .0|         1.2|   44.4|   -13.3|"
            ),
        
            (
                "15a",
                4, 0, 0, '.', Formatting.None,
                "a\t\tb",
                "a.......b"
            ),
        
            (
                "15b",
                4, 0, 0, '.', Formatting.DiscardEmptyColumns,
                "a\t\tb", // htabs - do not discard column
                "a.......b"
            ),
        
            (
                "15c",
                4, 0, 0, '.', Formatting.DiscardEmptyColumns,
                "a\v\vb",
                "a...b"
            ),
        
            (
                "15d",
                4, 0, 0, '.', Formatting.AlignRight | Formatting.DiscardEmptyColumns,
                "a\v\vb",
                "...ab"
            ),

            (
                "16a",
                100, 100, 0, '\t', Formatting.None,
                "a\tb\t\td\n" +
                    "a\tb\t\td\te\n" +
                    "a\n" +
                    "a\tb\tc\td\n" +
                    "a\tb\tc\td\te\n",
        
                "a\tb\t\td\n" +
                    "a\tb\t\td\te\n" +
                    "a\n" +
                    "a\tb\tc\td\n" +
                    "a\tb\tc\td\te\n"
            ),
        
            (
                "16b",
                100, 100, 0, '\t', Formatting.DiscardEmptyColumns,
                "a\vb\v\vd\n" +
                    "a\vb\v\vd\ve\n" +
                    "a\n" +
                    "a\vb\vc\vd\n" +
                    "a\vb\vc\vd\ve\n",
        
                "a\tb\td\n" +
                    "a\tb\td\te\n" +
                    "a\n" +
                    "a\tb\tc\td\n" +
                    "a\tb\tc\td\te\n"
            ),
        
            (
                "16b debug",
                100, 100, 0, '\t', Formatting.DiscardEmptyColumns | Formatting.Debug,
                "a\vb\v\vd\n" +
                    "a\vb\v\vd\ve\n" +
                    "a\n" +
                    "a\vb\vc\vd\n" +
                    "a\vb\vc\vd\ve\n",
        
                "a\t|b\t||d\n" +
                    "a\t|b\t||d\t|e\n" +
                    "a\n" +
                    "a\t|b\t|c\t|d\n" +
                    "a\t|b\t|c\t|d\t|e\n"
            ),
        
            (
                "16c",
                100, 100, 0, '\t', Formatting.DiscardEmptyColumns,
                "a\tb\t\td\n" + // hard tabs - do not discard column
                    "a\tb\t\td\te\n" +
                    "a\n" +
                    "a\tb\tc\td\n" +
                    "a\tb\tc\td\te\n",
        
                "a\tb\t\td\n" +
                    "a\tb\t\td\te\n" +
                    "a\n" +
                    "a\tb\tc\td\n" +
                    "a\tb\tc\td\te\n"
            ),
        
            (
                "16c debug",
                100, 100, 0, '\t', Formatting.DiscardEmptyColumns | Formatting.Debug,
                "a\tb\t\td\n" + // hard tabs - do not discard column
                    "a\tb\t\td\te\n" +
                    "a\n" +
                    "a\tb\tc\td\n" +
                    "a\tb\tc\td\te\n",
        
                "a\t|b\t|\t|d\n" +
                    "a\t|b\t|\t|d\t|e\n" +
                    "a\n" +
                    "a\t|b\t|c\t|d\n" +
                    "a\t|b\t|c\t|d\t|e\n"
            ),
        };

        public static IEnumerable<object[]> GetTestData()
        {
            return tests.Select(x => new object[] { x.testname, x });
        }

        [DataTestMethod]
        [MethodDataSource(typeof(TabWriterTests), nameof(GetTestData), 0)]
        public void Test(string testname, object data)
        {
            var e = ((string testname, int minwidth, int tabwidth, int padding, char padchar,
            Formatting flags, ustring src, ustring expected))data;

            // foreach (var e in tests)
            // {
                // DBG:
                TestContext.WriteLine($"Test: {e.testname}");
                TestContext.WriteLine($"src = {e.src.Slice()}");
                TestContext.WriteLine($"exp = {e.expected.Slice()}");

                Buffer.Check(TestContext, e.testname, e.minwidth, e.tabwidth, e.padding,
                        (byte)e.padchar, e.flags, e.src, e.expected);
            // }
        }
    }
}