using System;
using System.Reflection;
using DeepEqual;
using DeepEqual.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Zyborg.HCL.Token;
using Zyborg.IO;

namespace Zyborg.HCL.ast
{
    [TestClass]
    public class AstTests
    {
        [TestMethod]
        public void TestObjectListFilter()
        {
            var cases = new (slice<string> Filter, slice<ObjectItem> Input, slice<ObjectItem> Output)[]
            {
                (
                    slice<string>.From("foo"),
                    slice<ObjectItem>.From(
                        new ObjectItem
                        {
                            Keys = slice<ObjectKey>.From(
                                new ObjectKey
                                {
                                    Token = new Token.Token
                                    {
                                        Type = TokenType.STRING,
                                        Text = @"""foo"""
                                    }
                                }
                            )
                        }
                    ),
                    slice<ObjectItem>.From(
                        new ObjectItem
                        {
                            Keys = slice<ObjectKey>.From()
                        }
                    )
                ),
                (
                    slice<string>.From("foo"),
                    slice<ObjectItem>.From(
                        new ObjectItem
                        {
                            Keys = slice<ObjectKey>.From(
                                new ObjectKey { Token = new Token.Token { Type = TokenType.STRING, Text = @"""foo""" }},
                                new ObjectKey { Token = new Token.Token { Type = TokenType.STRING, Text = @"""bar""" }}
                            )
                        },
                        new ObjectItem
                        {
                            Keys = slice<ObjectKey>.From(
                                new ObjectKey { Token = new Token.Token { Type = TokenType.STRING, Text = @"""baz""" }}
                            )
                        }
                    ),
                    slice<ObjectItem>.From(
                        new ObjectItem
                        {
                            Keys = slice<ObjectKey>.From(
                                new ObjectKey { Token = new Token.Token { Type = TokenType.STRING, Text = @"""bar""" }}
                            )
                        }
                    )
                ),
            };

            foreach (var tc in cases)
            {
                var input = new ObjectList { Items = tc.Input };
                var expected = new ObjectList { Items = tc.Output };
                var actual = input.Filter(tc.Filter.ToArray());

                Assert.AreEqual(expected, actual,
                        $"in order: input, expected, actual\n\n{input}\n\n{expected}\n\n{actual}");
            }
        }


        [TestMethod]
        public void TestWalk()
        {
            var items = slice<ObjectItem>.From(
                new ObjectItem
                {
                    Keys = slice<ObjectKey>.From(
                        new ObjectKey { Token = new Token.Token { Type = TokenType.STRING, Text = @"""foo""" } },
                        new ObjectKey { Token = new Token.Token { Type = TokenType.STRING, Text = @"""bar""" } }
                    ),
                    Val = new LiteralType { Token = new Token.Token { Type = TokenType.STRING, Text = @"""example""" } },
                },
                new ObjectItem
                {
                    Keys = slice<ObjectKey>.From(
                        new ObjectKey { Token = new Token.Token { Type = TokenType.STRING, Text= @"""baz""" } }
                    ),
                }
            );

            var node = new ObjectList { Items = items };

            var order = slice<string>.From(
                $"{typeof(Ast).Namespace}.ObjectList",
                $"{typeof(Ast).Namespace}.ObjectItem",
                $"{typeof(Ast).Namespace}.ObjectKey",
                $"{typeof(Ast).Namespace}.ObjectKey",
                $"{typeof(Ast).Namespace}.LiteralType",
                $"{typeof(Ast).Namespace}.ObjectItem",
                $"{typeof(Ast).Namespace}.ObjectKey"
            );
            var count = 0;

            Ast.Walk(node,  n => {
                if (n == null)
                {
                    return (n, false);
                }

                var typeName = n.GetType().FullName;
                Assert.AreEqual(order[count], typeName);
                count++;
                return (n, true);
            });
        }

        [TestMethod]
        public void TestWalkEquality()
        {
            var items = slice.From(
                new ObjectItem
                {
                    Keys = slice.From(
                        new ObjectKey { Token = new Token.Token { Type = TokenType.STRING, Text = @"""foo""" } }
                    ),
                },
                new ObjectItem
                {
                    Keys = slice.From(
                        new ObjectKey { Token = new Token.Token { Type = TokenType.STRING, Text = @"""bar""" } }
                    ),
                }
            );

            var node = new ObjectList { Items = items };

            var rewritten = Ast.Walk(node, n => (n, true));

            var newNode = (ObjectList)rewritten as ObjectList;

            Assert.IsTrue(node.Equals(newNode), "rewritten node is not equal to the given node");

            Assert.AreEqual(2, newNode.Items.Length, "newNode length");

            var expected = slice.From(
                @"""foo""",
                @"""bar"""
            );

            foreach (var (i, item) in newNode.Items.Range())
            {
                Assert.AreEqual(1, item.Keys.Length, "expected keys newNode length");

                Assert.AreEqual(expected[i], item.Keys[0].Token.Text, "expected key");

                Assert.IsNull(item.Val, "expected item value should be null");
            }
        }

        [TestMethod]
        public void TestWalkRewrite()
        {
            var items = slice.From(
                new ObjectItem
                {
                    Keys = slice.From(
                        new ObjectKey { Token = new Token.Token { Type = TokenType.STRING, Text = @"""foo""" } },
                        new ObjectKey { Token = new Token.Token { Type = TokenType.STRING, Text = @"""bar""" } }
                    ),
                },
                new ObjectItem
                {
                    Keys = slice.From(
                        new ObjectKey { Token = new Token.Token { Type = TokenType.STRING, Text = @"""baz""" } }
                    ),
                }
            );

            var node = new ObjectList { Items = items };

            var suffix = "_example";
            node = (ObjectList)Ast.Walk(node, n => {
                switch (n) {
                case ObjectKey i:
                    i.Token.Text = i.Token.Text + suffix;
                    n = i;
                    break;
                }
                return (n, true);
            });

            Ast.Walk(node, n => {
                switch (n) {
                case ObjectKey i:
                    Assert.IsTrue(i.Token.Text.EndsWith(suffix),
                            "Token '{0}' should have suffix '{1}'", i.Token.Text, suffix);
                    break;
                }
                return (n, true);
            });
        }
    }
}