using System;
using System.IO;
using gozer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Zyborg.HCL.ast;
using Zyborg.HCL.parser;
using Zyborg.HCL.token;

namespace Zyborg.HCL.parser
{
    [TestClass]
    public class ParserTests
    {
        [TestMethod]
        public void TestType()
        {
            var literals = new (TokenType typ, string src)[]
            {
                (TokenType.STRING, @"foo = ""foo"""),
                (TokenType.NUMBER, @"foo = 123"),
                (TokenType.NUMBER, @"foo = -29"),
                (TokenType.FLOAT,  @"foo = 123.12"),
                (TokenType.FLOAT,  @"foo = -123.12"),
                (TokenType.BOOL,   @"foo = true"),
                (TokenType.HEREDOC, "foo = <<EOF\nHello\nWorld\nEOF"),
            };

            foreach (var l in literals)
            {
                var p = Parser.NewParser(l.src.AsByteSlice());
                var item = p.ObjectItem();

                var lit = (LiteralType)item.Val;

                Assert.AreEqual(l.typ, lit.Token.Type);
            }
        }

        [TestMethod]
        public void TestListType()
        {
            var literals = new (string src, slice<TokenType> tokens)[]
            {
                (
                    @"foo = [""123"", 123]",
                    slice.From(TokenType.STRING, TokenType.NUMBER)
                ),
                (
                    @"foo = [123, ""123"",]",
                    slice.From(TokenType.NUMBER, TokenType.STRING)
                ),
                (
                    @"foo = [false]",
                    slice.From(TokenType.BOOL)
                ),
                (
                    @"foo = []",
                    slice.From<TokenType>()
                ),
                (
                    @"foo = [1,
        ""string"",
        <<EOF
        heredoc contents
        EOF
        ]",
                    slice.From(TokenType.NUMBER, TokenType.STRING, TokenType.HEREDOC)
                ),
            };

            foreach (var l in literals)
            {
                var p = Parser.NewParser(l.src.AsByteSlice());
                var item = p.ObjectItem();

                var list = (ListType)item.Val;

                var tokens = slice<TokenType>.Empty;
                foreach (var li in list.List)
                {
                    if (li is LiteralType tp)
                        tokens = tokens.Append(tp.Token.Type);
                }

                AssertEquals(l.tokens, tokens);
            }
        }

        [TestMethod]
        public void TestListOfMaps()
        {
            var src = @"foo = [
            {key = ""bar""},
            {key = ""baz"", key2 = ""qux""},
        ]";
            var p = Parser.NewParser(src.AsByteSlice());

            var file = p.Parse();

            // Here we make all sorts of assumptions about the input structure w/ type
            // assertions. The intent is only for this to be a "smoke test" ensuring
            // parsing actually performed its duty - giving this test something a bit
            // more robust than _just_ "no error occurred".
            var expected = slice.From(@"""bar""", @"""baz""", @"""qux""");
            var actual = slice.Make<string>(0, 3);
            var ol = (ObjectList)file.Node;
            var objItem = ol.Items[0];
            var list = (ListType)objItem.Val;
            foreach (var node in list.List)
            {
                var obj = (ObjectType)node;
                foreach (var item in obj.List.Items)
                {
                    var val = (LiteralType)item.Val;
                    actual = actual.Append(val.Token.Text);
                }

            }
            AssertEquals(expected, actual);
            // if !reflect.DeepEqual(expected, actual) {
            //     t.Fatalf("Expected: %#v, got %#v", expected, actual)
            // }
        }

        [TestMethod]
        public void TestListOfMaps_requiresComma()
        {
            var src = @"foo = [
            {key = ""bar""}
            {key = ""baz""}
        ]";
            var p = Parser.NewParser(src.AsByteSlice());

            try
            {
                p.Parse();
                Assert.Fail("Expected error, got none!");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("error parsing list, expected comma or list end"));
            }
        }

        [TestMethod]
        public void TestListType_leadComment()
        {
            var literals = new(string src, slice<string> comment)[]
            {
                (
                    @"foo = [
                    1,
                    # bar
                    2,
                    3,
                    ]",
                    slice.From("", "# bar", "")
                ),
            };

            foreach (var l in literals)
            {
                var p = Parser.NewParser(l.src.AsByteSlice());
                var item = p.ObjectItem();

                var list = (ListType)item.Val;

                Assert.AreEqual(l.comment.Length, list.List.Length);

                foreach (var (i, li) in list.List.Range())
                {
                    var lt = (LiteralType)li;
                    var comment = l.comment[i];

                    Assert.AreEqual((comment == ""), (lt.LeadComment == null), $"bad: {lt}");

                    if (comment == "")
                        continue;

                    var actual = lt.LeadComment.List[0].Text;
                    Assert.AreEqual(comment, actual);
                }
            }
        }


        [TestMethod]
        public void TestListType_lineComment()
        {
            var literals = new (string src, slice<string> comment)[]
            {
                (
                    @"foo = [
                    1,
                    2, # bar
                    3,
                    ]",
                    slice.From("", "# bar", "")
                ),
            };

            foreach (var l in literals)
            {
                var p = Parser.NewParser(l.src.AsByteSlice());
                var item =  p.ObjectItem();

                var list = (ListType)item.Val;

                Assert.AreEqual(l.comment.Length, list.List.Length);

                foreach (var (i, li) in list.List.Range())
                {
                    var lt = (LiteralType)li;
                    var comment = l.comment[i];

                    Assert.AreEqual((comment == ""), (lt.LineComment == null), $"bad: {lt}");

                    if (comment == "")
                        continue;

                    var actual = lt.LineComment.List[0].Text;
                    Assert.AreEqual(comment, actual);
                }
            }
        }

        [TestMethod]
        public void TestObjectType()
        {
            var literals = new (string src, slice<INode>? nodeType, int itemLen)[]
            {
                (
                    @"foo = {}",
                    null,
                    0
                ),
                (
                    @"foo = {
                        bar = ""fatih""
                    }",
                    slice.From<INode>(new LiteralType()),
                    1
                ),
                (
                    @"foo = {
                        bar = ""fatih""
                        baz = [""arslan""]
                    }",
                    slice.From<INode>(
                        new LiteralType(),
                        new ListType()
                    ),
                    2
                ),
                (
                    @"foo = {
                        bar {}
                    }",
                    slice.From<INode>(
                        new ObjectType()
                    ),
                    1
                ),
                (
                    @"foo {
                        bar {}
                        foo = true
                    }",
                    slice.From<INode>(
                        new ObjectType(),
                        new LiteralType()
                    ),
                    2
                ),
            };

            foreach (var l in literals)
            {
                //t.Logf("Source: %s", l.src)

                var p = Parser.NewParser(l.src.AsByteSlice());
                // p.enableTrace = true
                var item = p.ObjectItem();
                // if err != nil {
                //     t.Error(err)
                //     continue
                // }

                // we know that the ObjectKey name is foo for all cases, what matters
                // is the object
                var obj = (ObjectType)item.Val;
                // if !ok {
                //     t.Errorf("node should be of type LiteralType, got: %T", item.Val)
                //     continue
                // }

                // check if the total length of items are correct
                AssertEquals(l.itemLen, obj.List.Items.Length);

                // check if the types are correct
                foreach (var (i, item2) in obj.List.Items.Range())
                {
                    AssertEquals(l.nodeType.Value[i].GetType(), item2.Val.GetType());
                }
            }
        }

        [TestMethod]
        public void TestObjectKey()
        {
            var keys = new (slice<TokenType> exp, string src)[]
            {
                (slice.From(TokenType.IDENT), @"foo {}"),
                (slice.From(TokenType.IDENT), @"foo = {}"),
                (slice.From(TokenType.IDENT), @"foo = bar"),
                (slice.From(TokenType.IDENT), @"foo = 123"),
                (slice.From(TokenType.IDENT), @"foo = ""${var.bar}"),
                (slice.From(TokenType.STRING), @"""foo"" {}"),
                (slice.From(TokenType.STRING), @"""foo"" = {}"),
                (slice.From(TokenType.STRING), @"""foo"" = ""${var.bar}"),
                (slice.From(TokenType.IDENT, TokenType.IDENT), @"foo bar {}"),
                (slice.From(TokenType.IDENT, TokenType.STRING), @"foo ""bar"" {}"),
                (slice.From(TokenType.STRING, TokenType.IDENT), @"""foo"" bar {}"),
                (slice.From(TokenType.IDENT, TokenType.IDENT, TokenType.IDENT), @"foo bar baz {}"),
            };

            foreach (var k in keys)
            {
                var p = Parser.NewParser(k.src.AsByteSlice());
                var keys2 = p.ObjectKey();

                var tokens = slice.From<TokenType>();
                foreach (var o in keys2)
                {
                    tokens = tokens.Append(o.Token.Type);
                }

                AssertEquals(k.exp, tokens);
            }

            var errKeys = new string[]
            {
                @"foo 12 {}",
                @"foo bar = {}",
                @"foo []",
                @"12 {}",
            };

            foreach (var k in errKeys)
            {
                var p = Parser.NewParser(k.AsByteSlice());
                Assert.ThrowsException<PosErrorException>(() => p.ObjectKey(),
                        $"case '{k}' should give an error");
            }
        }

        [TestMethod]
        public void TestCommentGroup()
        {
            var cases = new (string src, int groups)[]
            {
                ("# Hello\n# World", 1),
                ("# Hello\r\n# Windows", 1),
            };

            foreach (var tc in cases)
            {
                //t.Run(tc.src, func(t *testing.T) {
                    var p = Parser.NewParser(tc.src.AsByteSlice());
                    var file = p.Parse();

                    Assert.AreEqual(tc.groups, file.Comments.Length);
                //})
            }
        }

        [TestMethod]
        /// Official HCL tests
        public void TestParse()
        {
            var cases = new (string Name, bool Err)[]
            {
                (
                    "assign_colon.hcl",
                    true
                ),
                (
                    "comment.hcl",
                    false
                ),
                (
                    "comment_crlf.hcl",
                    false
                ),
                (
                    "comment_lastline.hcl",
                    false
                ),
                (
                    "comment_single.hcl",
                    false
                ),
                (
                    "empty.hcl",
                    false
                ),
                (
                    "list_comma.hcl",
                    false
                ),
                (
                    "multiple.hcl",
                    false
                ),
                (
                    "object_list_comma.hcl",
                    false
                ),
                (
                    "structure.hcl",
                    false
                ),
                (
                    "structure_basic.hcl",
                    false
                ),
                (
                    "structure_empty.hcl",
                    false
                ),
                (
                    "complex.hcl",
                    false
                ),
                (
                    "complex_crlf.hcl",
                    false
                ),
                (
                    "types.hcl",
                    false
                ),
                (
                    "array_comment.hcl",
                    false
                ),
                (
                    "array_comment_2.hcl",
                    true
                ),
                (
                    "missing_braces.hcl",
                    true
                ),
                (
                    "unterminated_object.hcl",
                    true
                ),
                (
                    "unterminated_object_2.hcl",
                    true
                ),
                (
                    "key_without_value.hcl",
                    true
                ),
                (
                    "object_key_without_value.hcl",
                    true
                ),
                (
                    "object_key_assign_without_value.hcl",
                    true
                ),
                (
                    "object_key_assign_without_value2.hcl",
                    true
                ),
                (
                    "object_key_assign_without_value3.hcl",
                    true
                ),
                (
                    "git_crypt.hcl",
                    true
                )
            };

            var fixtureDir = "./test-fixtures";

            foreach (var tc in cases)
            {
                //t.Run(tc.Name, func(t *testing.T) {
                    var d = System.IO.File.ReadAllText(Path.Combine(fixtureDir, tc.Name));
                    // var d = ioutil.ReadFile(filepath.Join(fixtureDir, tc.Name))
                    // if err != nil {
                    //     t.Fatalf("err: %s", err)
                    // }

                    try
                    {
                        var v = Parser.Parse(d.AsByteSlice());
                        if (tc.Err)
                            Assert.Fail($"Input: {tc.Name}\n\nError: {null}\n\nAST: {v}");
                        
                    }
                    catch (Exception ex)
                    {
                        if (!tc.Err)
                            Assert.Fail($"Input: {tc.Name}\n\nError: {ex}\n\nAST: {null}");
                    }
                    // v, err := Parse(d)
                    // if (err != nil) != tc.Err {
                    //     t.Fatalf("Input: %s\n\nError: %s\n\nAST: %#v", tc.Name, err, v)
                    // }
                //})
            }
        }

        [TestMethod]
        public void TestParse_inline()
        {
            var cases = new (string Value, bool Err)[]
            {
                ("t t e{{}}", true),
                ("o{{}}", true),
                ("t t e d N{{}}", true),
                ("t t e d{{}}", true),
                ("N{}N{{}}", true),
                ("v\nN{{}}", true),
                ("v=/\n[,", true),
                ("v=10kb", true),
                ("v=/foo", true),
            };

            foreach (var tc in cases)
            {
                try
                {
                    var ast = Parser.Parse(tc.Value.AsByteSlice());
                    if (tc.Err)
                        Assert.Fail($"Input: {tc.Value}\n\nError: {null}\n\nAST: {ast}");
                }
                catch (Exception ex)
                {
                    if (!tc.Err)
                        Assert.Fail($"Input: {tc.Value}\n\nError: {ex}\n\nAST: {null}");
                }
                //if (err != nil) != tc.Err {
                //    t.Fatalf("Input: %q\n\nError: %s\n\nAST: %#v", tc.Value, err, ast)
                //}
            }
        }

        /// equals fails the test if exp is not equal to act.
        private static void AssertEquals(object exp, object act)
        {
            var expJson = JsonConvert.SerializeObject(exp);
            var actJson = JsonConvert.SerializeObject(act);
            Assert.AreEqual(expJson, actJson);

            // if !reflect.DeepEqual(exp, act) {
            //     _, file, line, _ := runtime.Caller(1)
            //     fmt.Printf("\033[31m%s:%d:\n\n\texp: %#v\n\n\tgot: %#v\033[39m\n\n", filepath.Base(file), line, exp, act)
            //     tb.FailNow()
            // }
        }
    }
}