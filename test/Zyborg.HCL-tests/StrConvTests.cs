using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zyborg.HCL;

namespace Zyborg.HCL_tests
{
    class QuoteTest
    {
        public string _in;
        public string _out;
        public string _ascii;

        public QuoteTest(string @in, string @out, string ascii)
        {
            _in = @in;
            _out = @out;
            _ascii = ascii;
        }
    }

    class UnquoteTest
    {
        
        public string _in;
        public string _out;

        public UnquoteTest(string @in, string @out)
        {
            _in = @in;
            _out = @out;
        }
    }

 
    /* For reference...
    [Golang Escape Sequences](https://golang.org/ref/spec#Rune_literals):
        \a   U+0007 alert or bell
        \b   U+0008 backspace
        \f   U+000C form feed
        \n   U+000A line feed or newline
        \r   U+000D carriage return
        \t   U+0009 horizontal tab
        \v   U+000b vertical tab
        \\   U+005c backslash
        \'   U+0027 single quote  (valid escape only within rune literals)
        \"   U+0022 double quote  (valid escape only within string literals)

    [C# Escape Sequences](https://msdn.microsoft.com/en-us/library/h21280bw.aspx)
    */


    [TestClass]
    public class StrConvTests
    {
        static QuoteTest[] _quoteTests = new[]
        {
            new QuoteTest("\a\b\f\r\n\t\v", @"""\a\b\f\r\n\t\v""", @"""\a\b\f\r\n\t\v"""),
            new QuoteTest("\\", @"""\\""", @"""\\"""),
            // In C#, the \x escape can take 2 or 4 hex chars afterwards so we need
            // to break up the test strings to match the original intent in golang
            new QuoteTest("abc\xff" + "def", @"""abc\xff" + @"def""", @"""abc\xff" + @"def"""),
            new QuoteTest("\u263a", @"""☺""", @"""\u263a"""),
            // In C#, the \u escape can only support 4 hex chars, whereas
            // in golang it can support 4 or 8, so we need alter its rep
            // in code, as per:  http://www.fileformat.info/info/unicode/char/10ffff/index.htm
            new QuoteTest("\U0010ffff", "\"\U0010ffff\"", @"""\U0010ffff"""),
            new QuoteTest("\x04", @"""\x04""", @"""\x04"""),
        };

        static UnquoteTest[] _unquoteTests = new[]
        {
            new UnquoteTest(@"""""", ""),
            // new UnquoteTest(@"""a""", "a"),
            // new UnquoteTest(@"""abc""", "abc"),
            // new UnquoteTest(@"""☺""", "☺"),
            // new UnquoteTest(@"""hello world""", "hello world"),
            // new UnquoteTest(@"""\xFF""", "\xFF"),
            // new UnquoteTest(@"""\377""", "\0377"),
            // new UnquoteTest(@"""\u1234""", "\u1234"),
            // new UnquoteTest(@"""\U00010111""", "\U00010111"),
            // new UnquoteTest(@"""\U0001011111""", "\U0001011111"),
            // new UnquoteTest(@"""\a\b\f\n\r\t\v\\\""", "\a\b\f\n\r\t\v\\\""),
            // new UnquoteTest(@"""'""", "'"),
            // new UnquoteTest(@"""${file(""foo"")}""", @"${file(""foo"")}"),
            // new UnquoteTest(@"""${file(""\""foo\"""")}""", @"${file(""\""foo\"""")}"),
            // new UnquoteTest(@"""echo ${var.region}${element(split("","",var.zones),0)}""",
            //         @"echo ${var.region}${element(split("","",var.zones),0)}"),
            // new UnquoteTest(@"""${HH\:mm\:ss}""", @"${HH\:mm\:ss}"),
            // new UnquoteTest(@"""${\n}""", @"${\n}"),
        };

        static string[] _misquoted = new[]
        {
            @"",
            @"""",
            @"""a",
            @"""'",
            @"b""",
            @"""\""",
            @"""\9""",
            @"""\19""",
            @"""\129""",
            @"'\'",
            @"'\9'",
            @"'\19'",
            @"'\129'",
            @"'ab'",
            @"""\x1!""",
            @"""\U12345678""",
            @"""\z""",
            "`",
            "`xxx",
            "`\"",
            @"""\'""",
            @"'""'",
            "\"\n\"",
            "\"\\n\n\"",
            "'\n'",
            @"""${""",
            @"""${foo{}""",
            "\"${foo}\n\"",
        };

        [TestMethod]
        public void TestUnquote()
        {
            foreach (var tt in _unquoteTests)
            {
                Console.WriteLine($"{tt._in}:");
                Assert.AreEqual(tt._out, StrConv.Unquote(tt._in));
            }

            foreach (var tt in _quoteTests)
            {
                Assert.AreEqual(tt._in, StrConv.Unquote(tt._out));
            }

            foreach (var s in _misquoted)
            {
                // We expect this to either return an empty string or throw a syntax
                try
                {
                    Assert.AreEqual(string.Empty, StrConv.Unquote(s));
                }
                catch (SyntaxErrorException)
                { }
            }
        }
    }
}
