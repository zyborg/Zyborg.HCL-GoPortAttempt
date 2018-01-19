using DeepEqual.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Zyborg.HCL.token
{
    [TestClass]
    public class TokenTests
    {
        [TestMethod]
        public void TestTypeString()
        {
            var tokens = new (TokenType tt, string str)[]
            {
                (TokenType.ILLEGAL, "ILLEGAL"),
                (TokenType.EOF, "EOF"),
                (TokenType.COMMENT, "COMMENT"),
                (TokenType.IDENT, "IDENT"),
                (TokenType.NUMBER, "NUMBER"),
                (TokenType.FLOAT, "FLOAT"),
                (TokenType.BOOL, "BOOL"),
                (TokenType.STRING, "STRING"),
                (TokenType.HEREDOC, "HEREDOC"),
                (TokenType.LBRACK, "LBRACK"),
                (TokenType.LBRACE, "LBRACE"),
                (TokenType.COMMA, "COMMA"),
                (TokenType.PERIOD, "PERIOD"),
                (TokenType.RBRACK, "RBRACK"),
                (TokenType.RBRACE, "RBRACE"),
                (TokenType.ASSIGN, "ASSIGN"),
                (TokenType.ADD, "ADD"),
                (TokenType.SUB, "SUB"),
            };

            foreach (var token in tokens)
            {
                Assert.AreEqual(token.str, token.tt.ToString());
            }
        }

        [TestMethod]
        public void TestTokenValue()
        {
            var tokens = new (Token tt, object v)[]
            {
                (new Token { Type = TokenType.BOOL, Text = "true" }, true),

                ( new Token { Type = TokenType.BOOL, Text = "true"}, true),
                ( new Token { Type = TokenType.BOOL, Text = "false"}, false),
                ( new Token { Type = TokenType.FLOAT, Text = "3.14"}, (double)(3.14)),
                ( new Token { Type = TokenType.NUMBER, Text = "42"}, (long)(42)),
                ( new Token { Type = TokenType.IDENT, Text = "foo"}, "foo"),
                ( new Token { Type = TokenType.STRING, Text = @"""foo"""}, "foo"),
                ( new Token { Type = TokenType.STRING, Text = @"""foo\nbar"""}, "foo\nbar"),
                ( new Token { Type = TokenType.STRING, Text = @"""${file(""foo"")}"""}, @"${file(""foo"")}"),
                (
                    new Token {
                        Type = TokenType.STRING,
                        Text = @"""${replace(""foo"", ""."", ""\\."")}""",
                    },
                    @"${replace(""foo"", ""."", ""\\."")}"),
                (new Token { Type = TokenType.HEREDOC, Text = "<<EOF\nfoo\nbar\nEOF" }, "foo\nbar"),
            };

            foreach (var token in tokens)
            {
                var val = token.tt.Value();
                token.v.ShouldDeepEqual(val);
            }
        }        
    }
}