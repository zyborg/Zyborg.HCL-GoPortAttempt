using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zyborg.HCL.Token;
using Zyborg.IO;

namespace Zyborg.HCL.Scanner
{
    [TestClass]
    public class ScannerTests
    {
        static readonly string f100 = "f".AsCharSlice().Repeat(100).AsString();

        class TokenPair
        {
            public TokenType tok;
            public string text;

            public TokenPair(TokenType tt, string t)
            {
                tok = tt;
                text = t;
            }
        }

        Dictionary<string, TokenPair[]> tokenLists = new Dictionary<string, TokenPair[]>
        {
            ["comment"] = new[]
            {
                new TokenPair(TokenType.COMMENT, "//"),
                new TokenPair(TokenType.COMMENT, "////"),
                new TokenPair(TokenType.COMMENT, "// comment"),
                new TokenPair(TokenType.COMMENT, "// /* comment *" + "/"),
                new TokenPair(TokenType.COMMENT, "// // comment //"),
                new TokenPair(TokenType.COMMENT, "//" + f100),
                new TokenPair(TokenType.COMMENT, "#"),
                new TokenPair(TokenType.COMMENT, "##"),
                new TokenPair(TokenType.COMMENT, "# comment"),
                new TokenPair(TokenType.COMMENT, "# /* comment *" + "/"),
                new TokenPair(TokenType.COMMENT, "# # comment #"),
                new TokenPair(TokenType.COMMENT, "#" + f100),
                new TokenPair(TokenType.COMMENT, "/**" + "/"),
                new TokenPair(TokenType.COMMENT, "/***" + "/"),
                new TokenPair(TokenType.COMMENT, "/* comment *" + "/"),
                new TokenPair(TokenType.COMMENT, "/* // comment *" + "/"),
                new TokenPair(TokenType.COMMENT, "/* /* comment *" + "/"),
                new TokenPair(TokenType.COMMENT, "/*\n comment\n*" + "/"),
                new TokenPair(TokenType.COMMENT, "/*" + f100 + "*" + "/"),
            },
            ["operator"] = new[]
            {
                new TokenPair(TokenType.LBRACK, "["),
                new TokenPair(TokenType.LBRACE, "{"),
                new TokenPair(TokenType.COMMA, ","),
                new TokenPair(TokenType.PERIOD, "."),
                new TokenPair(TokenType.RBRACK, "]"),
                new TokenPair(TokenType.RBRACE, "}"),
                new TokenPair(TokenType.ASSIGN, "="),
                new TokenPair(TokenType.ADD, "+"),
                new TokenPair(TokenType.SUB, "-"),
            },

            ["bool"] = new[]
            {
                new TokenPair(TokenType.BOOL, "true"),
                new TokenPair(TokenType.BOOL, "false"),
            },
            ["ident"] = new[]
            {
                new TokenPair(TokenType.IDENT, "a"),
                new TokenPair(TokenType.IDENT, "a0"),
                new TokenPair(TokenType.IDENT, "foobar"),
                new TokenPair(TokenType.IDENT, "foo-bar"),
                new TokenPair(TokenType.IDENT, "abc123"),
                new TokenPair(TokenType.IDENT, "LGTM"),
                new TokenPair(TokenType.IDENT, "_"),
                new TokenPair(TokenType.IDENT, "_abc123"),
                new TokenPair(TokenType.IDENT, "abc123_"),
                new TokenPair(TokenType.IDENT, "_abc_123_"),

                // DOH! We have some issues with UTF8/Unicode chars
                // new TokenPair(TokenType.IDENT, "_äöü"),
                // new TokenPair(TokenType.IDENT, "_本"),
                // new TokenPair(TokenType.IDENT, "äöü"),
                // new TokenPair(TokenType.IDENT, "本"),
                // new TokenPair(TokenType.IDENT, "a۰۱۸"),
                // new TokenPair(TokenType.IDENT, "foo६४"),
                // new TokenPair(TokenType.IDENT, "bar９８７６"),
            },
            ["heredoc"] = new[]
            {
                new TokenPair(TokenType.HEREDOC, "<<EOF\nhello\nworld\nEOF"),
                new TokenPair(TokenType.HEREDOC, "<<EOF123\nhello\nworld\nEOF123"),
            },
            ["string"] = new[]
            {
                new TokenPair(TokenType.STRING, @""" """),
                new TokenPair(TokenType.STRING, @"""a"""),
                // new TokenPair(TokenType.STRING, @"""本"""),
                new TokenPair(TokenType.STRING, @"""${file(""foo"")}"""),
                new TokenPair(TokenType.STRING, @"""${file(\""foo\"")}"""),
                new TokenPair(TokenType.STRING, @"""\a"""),
                new TokenPair(TokenType.STRING, @"""\b"""),
                new TokenPair(TokenType.STRING, @"""\f"""),
                new TokenPair(TokenType.STRING, @"""\n"""),
                new TokenPair(TokenType.STRING, @"""\r"""),
                new TokenPair(TokenType.STRING, @"""\t"""),
                new TokenPair(TokenType.STRING, @"""\v"""),
                new TokenPair(TokenType.STRING, @"""\"""""),
                new TokenPair(TokenType.STRING, @"""\000"""),
                new TokenPair(TokenType.STRING, @"""\777"""),
                new TokenPair(TokenType.STRING, @"""\x00"""),
                new TokenPair(TokenType.STRING, @"""\xff"""),
                new TokenPair(TokenType.STRING, @"""\u0000"""),
                new TokenPair(TokenType.STRING, @"""\ufA16"""),
                new TokenPair(TokenType.STRING, @"""\U00000000"""),
                new TokenPair(TokenType.STRING, @"""\U0000ffAB"""),
                new TokenPair(TokenType.STRING, @"""" + f100 + @""""),
            },
            ["number"] = new[]
            {
                new TokenPair(TokenType.NUMBER, "0"),
                new TokenPair(TokenType.NUMBER, "1"),
                new TokenPair(TokenType.NUMBER, "9"),
                new TokenPair(TokenType.NUMBER, "42"),
                new TokenPair(TokenType.NUMBER, "1234567890"),
                new TokenPair(TokenType.NUMBER, "00"),
                new TokenPair(TokenType.NUMBER, "01"),
                new TokenPair(TokenType.NUMBER, "07"),
                new TokenPair(TokenType.NUMBER, "042"),
                new TokenPair(TokenType.NUMBER, "01234567"),
                new TokenPair(TokenType.NUMBER, "0x0"),
                new TokenPair(TokenType.NUMBER, "0x1"),
                new TokenPair(TokenType.NUMBER, "0xf"),
                new TokenPair(TokenType.NUMBER, "0x42"),
                new TokenPair(TokenType.NUMBER, "0x123456789abcDEF"),
                new TokenPair(TokenType.NUMBER, "0x" + f100),
                new TokenPair(TokenType.NUMBER, "0X0"),
                new TokenPair(TokenType.NUMBER, "0X1"),
                new TokenPair(TokenType.NUMBER, "0XF"),
                new TokenPair(TokenType.NUMBER, "0X42"),
                new TokenPair(TokenType.NUMBER, "0X123456789abcDEF"),
                new TokenPair(TokenType.NUMBER, "0X" + f100),
                new TokenPair(TokenType.NUMBER, "-0"),
                new TokenPair(TokenType.NUMBER, "-1"),
                new TokenPair(TokenType.NUMBER, "-9"),
                new TokenPair(TokenType.NUMBER, "-42"),
                new TokenPair(TokenType.NUMBER, "-1234567890"),
                new TokenPair(TokenType.NUMBER, "-00"),
                new TokenPair(TokenType.NUMBER, "-01"),
                new TokenPair(TokenType.NUMBER, "-07"),
                new TokenPair(TokenType.NUMBER, "-29"),
                new TokenPair(TokenType.NUMBER, "-042"),
                new TokenPair(TokenType.NUMBER, "-01234567"),
                new TokenPair(TokenType.NUMBER, "-0x0"),
                new TokenPair(TokenType.NUMBER, "-0x1"),
                new TokenPair(TokenType.NUMBER, "-0xf"),
                new TokenPair(TokenType.NUMBER, "-0x42"),
                new TokenPair(TokenType.NUMBER, "-0x123456789abcDEF"),
                new TokenPair(TokenType.NUMBER, "-0x" + f100),
                new TokenPair(TokenType.NUMBER, "-0X0"),
                new TokenPair(TokenType.NUMBER, "-0X1"),
                new TokenPair(TokenType.NUMBER, "-0XF"),
                new TokenPair(TokenType.NUMBER, "-0X42"),
                new TokenPair(TokenType.NUMBER, "-0X123456789abcDEF"),
                new TokenPair(TokenType.NUMBER, "-0X" + f100),
            },
            ["float"] = new[]
            {
                new TokenPair(TokenType.FLOAT, "0."),
                new TokenPair(TokenType.FLOAT, "1."),
                new TokenPair(TokenType.FLOAT, "42."),
                new TokenPair(TokenType.FLOAT, "01234567890."),
                new TokenPair(TokenType.FLOAT, ".0"),
                new TokenPair(TokenType.FLOAT, ".1"),
                new TokenPair(TokenType.FLOAT, ".42"),
                new TokenPair(TokenType.FLOAT, ".0123456789"),
                new TokenPair(TokenType.FLOAT, "0.0"),
                new TokenPair(TokenType.FLOAT, "1.0"),
                new TokenPair(TokenType.FLOAT, "42.0"),
                new TokenPair(TokenType.FLOAT, "01234567890.0"),
                new TokenPair(TokenType.FLOAT, "0e0"),
                new TokenPair(TokenType.FLOAT, "1e0"),
                new TokenPair(TokenType.FLOAT, "42e0"),
                new TokenPair(TokenType.FLOAT, "01234567890e0"),
                new TokenPair(TokenType.FLOAT, "0E0"),
                new TokenPair(TokenType.FLOAT, "1E0"),
                new TokenPair(TokenType.FLOAT, "42E0"),
                new TokenPair(TokenType.FLOAT, "01234567890E0"),
                new TokenPair(TokenType.FLOAT, "0e+10"),
                new TokenPair(TokenType.FLOAT, "1e-10"),
                new TokenPair(TokenType.FLOAT, "42e+10"),
                new TokenPair(TokenType.FLOAT, "01234567890e-10"),
                new TokenPair(TokenType.FLOAT, "0E+10"),
                new TokenPair(TokenType.FLOAT, "1E-10"),
                new TokenPair(TokenType.FLOAT, "42E+10"),
                new TokenPair(TokenType.FLOAT, "01234567890E-10"),
                new TokenPair(TokenType.FLOAT, "01.8e0"),
                new TokenPair(TokenType.FLOAT, "1.4e0"),
                new TokenPair(TokenType.FLOAT, "42.2e0"),
                new TokenPair(TokenType.FLOAT, "01234567890.12e0"),
                new TokenPair(TokenType.FLOAT, "0.E0"),
                new TokenPair(TokenType.FLOAT, "1.12E0"),
                new TokenPair(TokenType.FLOAT, "42.123E0"),
                new TokenPair(TokenType.FLOAT, "01234567890.213E0"),
                new TokenPair(TokenType.FLOAT, "0.2e+10"),
                new TokenPair(TokenType.FLOAT, "1.2e-10"),
                new TokenPair(TokenType.FLOAT, "42.54e+10"),
                new TokenPair(TokenType.FLOAT, "01234567890.98e-10"),
                new TokenPair(TokenType.FLOAT, "0.1E+10"),
                new TokenPair(TokenType.FLOAT, "1.1E-10"),
                new TokenPair(TokenType.FLOAT, "42.1E+10"),
                new TokenPair(TokenType.FLOAT, "01234567890.1E-10"),
                new TokenPair(TokenType.FLOAT, "-0.0"),
                new TokenPair(TokenType.FLOAT, "-1.0"),
                new TokenPair(TokenType.FLOAT, "-42.0"),
                new TokenPair(TokenType.FLOAT, "-01234567890.0"),
                new TokenPair(TokenType.FLOAT, "-0e0"),
                new TokenPair(TokenType.FLOAT, "-1e0"),
                new TokenPair(TokenType.FLOAT, "-42e0"),
                new TokenPair(TokenType.FLOAT, "-01234567890e0"),
                new TokenPair(TokenType.FLOAT, "-0E0"),
                new TokenPair(TokenType.FLOAT, "-1E0"),
                new TokenPair(TokenType.FLOAT, "-42E0"),
                new TokenPair(TokenType.FLOAT, "-01234567890E0"),
                new TokenPair(TokenType.FLOAT, "-0e+10"),
                new TokenPair(TokenType.FLOAT, "-1e-10"),
                new TokenPair(TokenType.FLOAT, "-42e+10"),
                new TokenPair(TokenType.FLOAT, "-01234567890e-10"),
                new TokenPair(TokenType.FLOAT, "-0E+10"),
                new TokenPair(TokenType.FLOAT, "-1E-10"),
                new TokenPair(TokenType.FLOAT, "-42E+10"),
                new TokenPair(TokenType.FLOAT, "-01234567890E-10"),
                new TokenPair(TokenType.FLOAT, "-01.8e0"),
                new TokenPair(TokenType.FLOAT, "-1.4e0"),
                new TokenPair(TokenType.FLOAT, "-42.2e0"),
                new TokenPair(TokenType.FLOAT, "-01234567890.12e0"),
                new TokenPair(TokenType.FLOAT, "-0.E0"),
                new TokenPair(TokenType.FLOAT, "-1.12E0"),
                new TokenPair(TokenType.FLOAT, "-42.123E0"),
                new TokenPair(TokenType.FLOAT, "-01234567890.213E0"),
                new TokenPair(TokenType.FLOAT, "-0.2e+10"),
                new TokenPair(TokenType.FLOAT, "-1.2e-10"),
                new TokenPair(TokenType.FLOAT, "-42.54e+10"),
                new TokenPair(TokenType.FLOAT, "-01234567890.98e-10"),
                new TokenPair(TokenType.FLOAT, "-0.1E+10"),
                new TokenPair(TokenType.FLOAT, "-1.1E-10"),
                new TokenPair(TokenType.FLOAT, "-42.1E+10"),
                new TokenPair(TokenType.FLOAT, "-01234567890.1E-10"),
            },

        };


        string[] orderedTokenLists = new string[]
        {
            "comment",
            "operator",
            "bool",
            "ident",
            "heredoc",
            "string",
            "number",
            "float",
        };


        [TestMethod]
        public void TestPosition()
        {
            // create artifical source code
            var buf = new GoBuffer();

            foreach (var listName in orderedTokenLists)
            {
                foreach (var ident in tokenLists[listName])
                {
                    buf.WriteString(string.Format("\t\t\t\t{0}\n", ident.text));
                    //fmt.Fprintf(buf, "\t\t\t\t%s\n", ident.text)
                }
            }

            System.IO.File.WriteAllText(@"C:\local\prj\bek\zyborg\Zyborg.HCL\test\Zyborg.HCL-tests\ScannerTests-out.txt", buf.Bytes().AsString());

            var s = Scanner.New(buf.Bytes());

            var pos =  new Pos("", 4, 1, 5);
            s.Scan();
            foreach (var listName in orderedTokenLists)
            {
                foreach (var k in tokenLists[listName])
                {
                    var curPos = s.TokPos;
                    // fmt.Printf("[%q] s = %+v:%+v\n", k.text, curPos.Offset, curPos.Column)

                    Assert.AreEqual(pos.Offset, curPos.Offset,
                            "offset = {0}, want {1} for {2}", curPos.Offset, pos.Offset, k.text);
                    Assert.AreEqual(pos.Line, curPos.Line,
                            "line = {0}, want {1} for {2}", curPos.Line, pos.Line, k.text);
                    Assert.AreEqual(pos.Column, curPos.Column,
                            "column = {0}, want {1} for {2}", curPos.Column, pos.Column, k.text);

                    pos.Offset += 4 + k.text.Length + 1;     // 4 tabs + token bytes + newline
                    pos.Line += CountNewlines(k.text) + 1; // each token is on a new line
                    s.Scan();
                }
            }
            // make sure there were no token-internal errors reported by scanner
            Assert.AreEqual(0, s.ErrorCount);
        }

        [TestMethod]
        public void TestNullChar()
        {
            var s = Scanner.New("\"\\0".AsByteSlice());
            s.Scan(); // Used to panic
        }

        [TestMethod]
        public void TestComment()
        {
            TestTokenList(tokenLists["comment"]);
        }

        [TestMethod]
        public void TestOperator()
        {
            TestTokenList(tokenLists["operator"]);
        }

        [TestMethod]
        public void TestBool()
        {
            TestTokenList(tokenLists["bool"]);
        }

        [TestMethod]
        public void TestIdent()
        {
            TestTokenList(tokenLists["ident"]);
        }

        [TestMethod]
        public void TestString()
        {
            TestTokenList(tokenLists["string"]);
        }

        [TestMethod]
        public void TestNumber()
        {
            TestTokenList(tokenLists["number"]);
        }

        [TestMethod]
        public void TestFloat()
        {
            TestTokenList(tokenLists["float"]);
        }

        [TestMethod]
        public void TestWindowsLineEndings()
        {
            var hcl = @"// This should have Windows line endings
resource ""aws_instance"" ""foo"" {
    user_data=<<HEREDOC
    test script
HEREDOC
}";
            var hclWindowsEndings = hcl.Replace("\n", "\r\n");

            var literals = new (TokenType TokenType, string literal)[]
            {
                (TokenType.COMMENT, "// This should have Windows line endings\r"),
                (TokenType.IDENT, "resource"),
                (TokenType.STRING, @"""aws_instance"""),
                (TokenType.STRING, @"""foo"""),
                (TokenType.LBRACE, "{"),
                (TokenType.IDENT, "user_data"),
                (TokenType.ASSIGN, "="),
                (TokenType.HEREDOC, "<<HEREDOC\r\n    test script\r\nHEREDOC\r\n"),
                (TokenType.RBRACE, "}"),
            };

            var s = Scanner.New(hclWindowsEndings.AsByteSlice());
            foreach (var l in literals)
            {
                var tok = s.Scan();

                Assert.AreEqual(l.TokenType, tok.Type);
                // if l.tokenType != tok.Type {
                //     t.Errorf("got: %s want %s for %s\n", tok, l.tokenType, tok.String())
                // }

                Assert.AreEqual(l.literal, tok.Text);
                // if l.literal != tok.Text {
                //     t.Errorf("got:\n%v\nwant:\n%v\n", []byte(tok.Text), []byte(l.literal))
                // }
            }
        }

        [TestMethod]
        public void TestRealExample()
        {
            var complexHCL = @"// This comes from Terraform, as a test
	variable ""foo"" {
	    default = ""bar""
	    description = ""bar""
	}

	provider ""aws"" {
	  access_key = ""foo""
	  secret_key = ""${replace(var.foo, ""."", ""\\."")}""
	}

	resource ""aws_security_group"" ""firewall"" {
	    count = 5
	}

	resource aws_instance ""web"" {
	    ami = ""${var.foo}""
	    security_groups = [
	        ""foo"",
	        ""${aws_security_group.firewall.foo}""
	    ]

	    network_interface {
	        device_index = 0
	        description = <<EOF
Main interface
EOF
	    }

		network_interface {
	        device_index = 1
	        description = <<-EOF
			Outer text
				Indented text
			EOF
		}
	}";

            var literals = new (TokenType tokenType, string literal)[]
            {
                (TokenType.COMMENT, @"// This comes from Terraform, as a test"),
                (TokenType.IDENT, @"variable"),
                (TokenType.STRING, @"""foo"""),
                (TokenType.LBRACE, @"{"),
                (TokenType.IDENT, @"default"),
                (TokenType.ASSIGN, @"="),
                (TokenType.STRING, @"""bar"""),
                (TokenType.IDENT, @"description"),
                (TokenType.ASSIGN, @"="),
                (TokenType.STRING, @"""bar"""),
                (TokenType.RBRACE, "}"),
                (TokenType.IDENT, @"provider"),
                (TokenType.STRING, @"""aws"""),
                (TokenType.LBRACE, @"{"),
                (TokenType.IDENT, @"access_key"),
                (TokenType.ASSIGN, @"="),
                (TokenType.STRING, @"""foo"""),
                (TokenType.IDENT, @"secret_key"),
                (TokenType.ASSIGN, @"="),
                (TokenType.STRING, @"""${replace(var.foo, ""."", ""\\."")}"""),
                (TokenType.RBRACE, "}"),
                (TokenType.IDENT, @"resource"),
                (TokenType.STRING, @"""aws_security_group"""),
                (TokenType.STRING, @"""firewall"""),
                (TokenType.LBRACE, @"{"),
                (TokenType.IDENT, @"count"),
                (TokenType.ASSIGN, @"="),
                (TokenType.NUMBER, @"5"),
                (TokenType.RBRACE, "}"),
                (TokenType.IDENT, @"resource"),
                (TokenType.IDENT, @"aws_instance"),
                (TokenType.STRING, @"""web"""),
                (TokenType.LBRACE, @"{"),
                (TokenType.IDENT, @"ami"),
                (TokenType.ASSIGN, @"="),
                (TokenType.STRING, @"""${var.foo}"""),
                (TokenType.IDENT, @"security_groups"),
                (TokenType.ASSIGN, @"="),
                (TokenType.LBRACK, @"["),
                (TokenType.STRING, @"""foo"""),
                (TokenType.COMMA, @","),
                (TokenType.STRING, @"""${aws_security_group.firewall.foo}"""),
                (TokenType.RBRACK, @"]"),
                (TokenType.IDENT, @"network_interface"),
                (TokenType.LBRACE, @"{"),
                (TokenType.IDENT, @"device_index"),
                (TokenType.ASSIGN, @"="),
                (TokenType.NUMBER, @"0"),
                (TokenType.IDENT, @"description"),
                (TokenType.ASSIGN, @"="),
                (TokenType.HEREDOC, "<<EOF\nMain interface\nEOF\n"),
                (TokenType.RBRACE, "}"),
                (TokenType.IDENT, @"network_interface"),
                (TokenType.LBRACE, @"{"),
                (TokenType.IDENT, @"device_index"),
                (TokenType.ASSIGN, @"="),
                (TokenType.NUMBER, @"1"),
                (TokenType.IDENT, @"description"),
                (TokenType.ASSIGN, @"="),
                (TokenType.HEREDOC, "<<-EOF\n\t\t\tOuter text\n\t\t\t\tIndented text\n\t\t\tEOF\n"),
                (TokenType.RBRACE, "}"),
                (TokenType.RBRACE, "}"),
                (TokenType.EOF, @""),
            };

            var s = Scanner.New(complexHCL.AsByteSlice());
            foreach (var l in literals)
            {
                var tok = s.Scan();
                Assert.AreEqual(l.tokenType, tok.Type);
                // if l.tokenType != tok.Type {
                //     t.Errorf("got: %s want %s for %s\n", tok, l.tokenType, tok.String())
                // }

                Assert.AreEqual(l.literal, tok.Text);
                // if l.literal != tok.Text {
                //     t.Errorf("got:\n%+v\n%s\n want:\n%+v\n%s\n", []byte(tok.String()), tok, []byte(l.literal), l.literal)
                // }
            }

        }

        [TestMethod]
        public void TestScan_crlf()
        {
            var complexHCL = "foo {\r\n  bar = \"baz\"\r\n}\r\n";

            var literals = new (TokenType tokenType, string literal)[]
            {
                (TokenType.IDENT, @"foo"),
                (TokenType.LBRACE, @"{"),
                (TokenType.IDENT, @"bar"),
                (TokenType.ASSIGN, @"="),
                (TokenType.STRING, @"""baz"""),
                (TokenType.RBRACE, "}"),
                (TokenType.EOF, @""),
            };

            var s = Scanner.New(complexHCL.AsByteSlice());
            foreach (var l in literals)
            {
                var tok = s.Scan();
                Assert.AreEqual(l.tokenType, tok.Type);
                // if l.tokenType != tok.Type {
                //     t.Errorf("got: %s want %s for %s\n", tok, l.tokenType, tok.String())
                // }
                Assert.AreEqual(l.literal, tok.Text);
                // if l.literal != tok.Text {
                //     t.Errorf("got:\n%+v\n%s\n want:\n%+v\n%s\n", []byte(tok.String()), tok, []byte(l.literal), l.literal)
                // }
            }

        }

        // DOH!  None of these work!

        // [TestMethod]
        // public void TestError()
        // {
        //     // TestError("\x80", "1:1", "illegal UTF-8 encoding", TokenType.ILLEGAL);
        //     // TestError("\xff", "1:1", "illegal UTF-8 encoding", TokenType.ILLEGAL);

        //     // TestError("ab\x80", "1:3", "illegal UTF-8 encoding", TokenType.IDENT);
        //     // TestError("abc\xff", "1:4", "illegal UTF-8 encoding", TokenType.IDENT);

        //     // TestError("\"ab"+"\x80", "1:4", "illegal UTF-8 encoding", TokenType.STRING);
        //     // TestError("\"abc"+"\xff", "1:5", "illegal UTF-8 encoding", TokenType.STRING);

        //     // TestError("01238", "1:6", "illegal octal number", TokenType.NUMBER);
        //     // TestError("01238123", "1:9", "illegal octal number", TokenType.NUMBER);
        //     // TestError("0x", "1:3", "illegal hexadecimal number", TokenType.NUMBER);
        //     // TestError("0xg", "1:3", "illegal hexadecimal number", TokenType.NUMBER);
        //     // TestError("'aa'", "1:1", "illegal char", TokenType.ILLEGAL);

        //     // TestError("\"", "1:2", "literal not terminated", TokenType.STRING);
        //     // TestError("\"abc", "1:5", "literal not terminated", TokenType.STRING);
        //     // TestError("\"abc"+"\n", "1:5", "literal not terminated", TokenType.STRING);
        //     // TestError("\"${abc"+"\n", "2:1", "literal not terminated", TokenType.STRING);
        //     // TestError("/*/", "1:4", "comment not terminated", TokenType.COMMENT);
        //     // TestError("/foo", "1:1", "expected '/' for comment", TokenType.COMMENT);
        // }

        private void TestError(string src, string pos, string msg, TokenType tok)
        {
            var s = Scanner.New(src.AsByteSlice());

            var errorCalled = false;
            s.Error = (Pos p, string m) => {
                if (!errorCalled) {
                    Assert.AreEqual(p.ToString(), pos);
                    // if pos != p.String() {
                    //     t.Errorf("pos = %q, want %q for %q", p, pos, src)
                    // }

                    Assert.AreEqual(msg, m);
                    // if m != msg {
                    //     t.Errorf("msg = %q, want %q for %q", m, msg, src)
                    // }
                    errorCalled = true;
                }
            };

            var tk = s.Scan();
            Assert.AreEqual(tok, tk.Type);
            // if tk.Type != tok {
            //     t.Errorf("tok = %s, want %s for %q", tk, tok, src)
            // }
            Assert.IsTrue(errorCalled);
            // if !errorCalled {
            //     t.Errorf("error handler not called for %q", src)
            // }
            Assert.AreEqual(0, s.ErrorCount);
            // if s.ErrorCount == 0 {
            //     t.Errorf("count = %d, want > 0 for %q", s.ErrorCount, src)
            // }
        }

        private void TestTokenList(TokenPair[] tokenList)
        {
            // create artifical source code
            var buf = new GoBuffer();
            foreach (var ident in tokenList)
            {
                buf.WriteString(string.Format("{0}\n", ident.text));
            }

            var s = Scanner.New(buf.Bytes());
            foreach (var ident in tokenList)
            {
                var tok = s.Scan();
                Assert.AreEqual(ident.tok, tok.Type,
                        "tok = {0} want {1} for {2}\n", tok, ident.tok, ident.text);

                Assert.AreEqual(ident.text, tok.Text,
                        "text = {0} want {1}", tok.ToString(), ident.text);
            }
        }

        private int CountNewlines(string s)
        {
            var n = 0;
            foreach (var ch in s)
            {
                if (ch == '\n')
                {
                    n++;
                }
            }
            return n;
        }
    }
}