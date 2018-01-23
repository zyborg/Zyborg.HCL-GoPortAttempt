using System;
using gozer;
using Zyborg.HCL.ast;
using Zyborg.HCL.scanner;
using Zyborg.HCL.token;

namespace Zyborg.HCL.parser
{
    public class Parser
    {
        public Scanner sc;

        // Last read token
        public Token tok;
        public Token commaPrev;

        public slice<CommentGroup> comments;
        public CommentGroup leadComment; // last lead comment
        public CommentGroup lineComment; // last line comment

        public bool enableTrace;
        public int indent;
        public int n; // buffer size (max = 1)

        public static Parser NewParser(slice<byte> src)
        {
            return new Parser
            {
                sc = Scanner.New(src),
            };
        }

        /// Parse returns the fully parsed source and returns the abstract syntax tree.
        public static File Parse(slice<byte> src)
        {
            // normalize all line endings
            // since the scanner and output only work with "\n" line endings, we may
            // end up with dangling "\r" characters in the parsed data.
            src = src.Replace("\r\n".AsByteSlice(), "\n".AsByteSlice());
            //src = bytes.Replace(src, []byte("\r\n"), []byte("\n"), -1)

            var p = NewParser(src);
            return p.Parse();
        }

        /// Parse returns the fully parsed source and returns the abstract syntax tree.
        public File Parse()
        {
            var f = new File();
            Exception scerr = null;
            sc.Error = (pos, msg) =>
            {
                scerr = new PosErrorException(pos, msg);
            };

            f.Node = this.ObjectList(false);
            if (scerr != null)
                throw scerr;


            f.Comments = comments;
            return f;
        }

        /// objectList parses a list of items within an object (generally k/v pairs).
        /// The parameter" obj" tells this whether to we are within an object (braces:
        /// '{', '}') or just at the top level. If we're within an object, we end
        /// at an RBRACE.
        public ObjectList ObjectList(bool obj)
        {
            try
            {
                var node = new ObjectList();

                for (;;)
                {
                    if (obj)
                    {
                        var tok1 = Scan();
                        Unscan();
                        if (tok1.Type == TokenType.RBRACE)
                        {
                            break;
                        }
                    }

                    ObjectItem n;
                    try
                    {
                        n = ObjectItem();
                    }
                    catch (ErrEofTokenException)
                    {
                        break; // we are finished
                    }
                    // catch (Exception)
                    // {
                    //     // we don't return a nil node, because might want to use already
                    //     // collected items.
                    //     if err != nil {
                    //         return node, err
                    //     }
                    // }


                    node.Add(n);

                    // object lists can be optionally comma-delimited e.g. when a list of maps
                    // is being expressed, so a comma is allowed here - it's simply consumed
                    var tok2 = Scan();
                    if (tok2.Type != TokenType.COMMA)
                    {
                        Unscan();
                    }
                }
                return node;
            }
            finally
            {
                // defer un(trace(p, "ParseObjectList"))
            }
        }

        public (Comment comment, int endline) ConsumeComment()
        // func (p *Parser) consumeComment() (comment *ast.Comment, endline int) {
        {
            var endline = tok.Pos.Line;

            // count the endline if it's multiline comment, ie starting with /*
            if (tok.Text.Length > 1 && tok.Text[1] == '*')
            {
                // don't use range here - no need to decode Unicode code points
                for (var i = 0; i < tok.Text.Length; i++)
                {
                    if (tok.Text[i] == '\n')
                    {
                        endline++;
                    }
                }
            }

            var comment = new Comment { Start = tok.Pos, Text = tok.Text };
            tok = sc.Scan();
            return (comment, endline);
        }

        private (CommentGroup comments, int endline) ConsumeCommentGroup(int n)
        {
            var list = new slice<Comment>();
            var endline = tok.Pos.Line;

            while (tok.Type == TokenType.COMMENT && tok.Pos.Line <= endline + n)
            {
                Comment comment;
                (comment, endline) = ConsumeComment();
                list = list.Append(comment);
            }

            // add comment group to the comments list
            var comments = new CommentGroup { List = list };
            this.comments = this.comments.Append(comments);

            return (comments, endline);
        }

        /// objectItem parses a single object item
        public ObjectItem ObjectItem()
        {
            try
            {
                var keys = slice<ObjectKey>.Empty;
                try
                {
                    keys = ObjectKey();
                }
                catch (ErrEofTokenException) when (keys.Length > 0)
                {
                    // We ignore eof token here since it is an error if we didn't
                    // receive a value (but we did receive a key) for the item.
                    //err = nil;
                }
                catch (Exception) when (keys.Length > 0 && tok.Type == TokenType.RBRACE)
                {
                    // This is a strange boolean statement, but what it means is:
                    // We have keys with no value, and we're likely in an object
                    // (since RBrace ends an object). For this, we set err to nil so
                    // we continue and get the error below of having the wrong value
                    // type.
                    //err = nil

                    // Reset the token type so we don't think it completed fine. See
                    // objectType which uses p.tok.Type to check if we're done with
                    // the object.
                    tok.Type = TokenType.EOF;
                }

                var o = new ObjectItem
                {
                    Keys = keys,
                };

                if (leadComment != null)
                {
                    o.LeadComment = leadComment;
                    leadComment = null;
                }

                switch (tok.Type)
                {
                    case TokenType.ASSIGN:
                        o.Assign = tok.Pos;
                        o.Val = Object();
                        break;
                    case TokenType.LBRACE:
                        o.Val = ObjectType();
                        break;
                    default:
                        var keyStr = slice.Make<string>(0, keys.Length);
                        foreach (var k in keys)
                        {
                            keyStr = keyStr.Append(k.Token.Text);
                        }

                        throw new PosErrorException(tok.Pos, 
                                $"key '{string.Join(" ", keyStr)}' expected start of object ('{{') or assignment ('=')");
                }

                // do a look-ahead for line comment
                Scan();
                if (keys.Length > 0&& o.Val.Pos().Line == keys[0].Pos().Line && lineComment != null)
                {
                    o.LineComment = lineComment;
                    lineComment = null;
                }
                Unscan();
                return o;
            }
            finally
            {
                // defer un(trace(p, "ParseObjectItem"))
            }
        }

        /// objectKey parses an object key and returns a ObjectKey AST
        public slice<ObjectKey> ObjectKey()
        {
        	var keyCount = 0;
        	var keys = slice.Make<ObjectKey>(0);

        	for (;;)
            {
        		var tok = Scan();
        		switch (tok.Type)
                {
                    case TokenType.EOF:
                        // It is very important to also return the keys here as well as
                        // the error. This is because we need to be able to tell if we
                        // did parse keys prior to finding the EOF, or if we just found
                        // a bare EOF.
                        //return keys, errEofToken
                        throw new ErrEofTokenException(keys);

                    case TokenType.ASSIGN:
                        // assignment or object only, but not nested objects. this is not
                        // allowed: `foo bar = {}`
                        if (keyCount > 1)
                        {
                            throw new PosErrorException(tok.Pos, $"nested object expected: LBRACE got: {tok.Type}");
                        }

                        if (keyCount == 0)
                        {
                            throw new PosErrorException(tok.Pos, "no object keys found!");
                        }

                        return keys;

                    case TokenType.LBRACE:
                        Exception err = null;

                        // If we have no keys, then it is a syntax error. i.e. {{}} is not
                        // allowed.
                        if (keys.Length == 0)
                        {
                            err = new PosErrorException(tok.Pos,
                                    $"expected: IDENT | STRING got: {tok.Type}",
                                    keys: keys);
                            // err = &PosError{
                            //     Pos: p.tok.Pos,
                            //     Err: fmt.Errorf("expected: IDENT | STRING got: %s", p.tok.Type),
                            // }
                            throw err;
                        }

                        // object
                        //return keys, err
                        return keys;
                    case TokenType.IDENT:
                        keyCount++;
                        keys = keys.Append(new ObjectKey { Token = tok });
                        break;
                    case TokenType.STRING:
                        keyCount++;
                        keys = keys.Append(new ObjectKey { Token = tok });
                        break;
                    case TokenType.ILLEGAL:
                        // return keys, &PosError{
                        //     Pos: p.tok.Pos,
                        //     Err: fmt.Errorf("illegal character"),
                        // }
                        throw new PosErrorException(tok.Pos, "illegal character", keys: keys);
                    default:
                        // return keys, &PosError{
                        //     Pos: p.tok.Pos,
                        //     Err: fmt.Errorf("expected: IDENT | STRING | ASSIGN | LBRACE got: %s", p.tok.Type),
                        // }
                        throw new PosErrorException(tok.Pos,
                                $"expected: IDENT | STRING | ASSIGN | LBRACE got: {tok.Type}", keys: keys);
        		}
        	}
        }

        /// object parses any type of object, such as number, bool, string, object or
        /// list.
        private INode Object()
        {
            try
            {
                var tok = Scan();

                if (tok.Type == TokenType.NUMBER || tok.Type == TokenType.FLOAT
                        || tok.Type == TokenType.BOOL || tok.Type == TokenType.STRING
                        || tok.Type == TokenType.HEREDOC)
                    return LiteralType();

                if (tok.Type == TokenType.LBRACE)
                    return ObjectType();

                if (tok.Type == TokenType.LBRACK)
                    return ListType();

                if (tok.Type == TokenType.COMMENT)
                    // implement comment
                    ;

                if (tok.Type == TokenType.EOF)
                    throw new ErrEofTokenException();

                throw new PosErrorException(tok.Pos, $"Unknown token: {tok}");
            }
            finally
            {
                //defer un(trace(p, "ParseType"))                
            }
        }
        /// objectType parses an object type and returns a ObjectType AST
        private ObjectType ObjectType()
        {
            try
            {
                // we assume that the currently scanned token is a LBRACE
                var o = new ObjectType
                {
                    Lbrace = this.tok.Pos,
                };

                ObjectList l = null;
                try
                {
                    l = ObjectList(true);
                }
                catch (Exception)
                {
                    // if we hit RBRACE, we are good to go (means we parsed all Items), if it's
                    // not a RBRACE, it's an syntax error and we just return it.
                    if (this.tok.Type != TokenType.RBRACE)
                        throw;
                }

                // No error, scan and expect the ending to be a brace
                var tok = Scan();
                if (tok.Type != TokenType.RBRACE)
                {
                    throw new PosErrorException(tok.Pos,
                            $"object expected closing RBRACE got: {tok.Type}");
                }

                o.List = l;
                o.Rbrace = tok.Pos; // advanced via parseObjectList
                return o;
            }
            finally
            {
                // defer un(trace(p, "ParseObjectType"))
            }
        }

        /// listType parses a list type and returns a ListType AST
        private ListType ListType()
        {
            try
            {
                // we assume that the currently scanned token is a LBRACK
                var l = new ListType
                {
                    Lbrack = tok.Pos,
                };

                var needComma = false;
                for (;;)
                {
                    var tok = Scan();
                    if (needComma)
                    {
                        if (tok.Type == TokenType.COMMA || tok.Type == TokenType.RBRACK)
                        {
                            // Do nothing
                        }
                        else
                        {
                            throw new PosErrorException(tok.Pos,
                                    $"error parsing list, expected comma or list end, got: {tok.Type}");
                        }
                    }
                    if (tok.Type == TokenType.BOOL || tok.Type == TokenType.NUMBER
                            || tok.Type == TokenType.FLOAT || tok.Type == TokenType.STRING
                            || tok.Type == TokenType.HEREDOC)
                    {
                        var node = LiteralType();

                        // If there is a lead comment, apply it
                        if (leadComment != null)
                        {
                            node.LeadComment = leadComment;
                            leadComment = null;
                        }

                        l.Add(node);
                        needComma = true;
                    }
                    else if (tok.Type == TokenType.COMMA)
                    {
                        // get next list item or we are at the end
                        // do a look-ahead for line comment
                        Scan();
                        if (lineComment != null && l.List.Length > 0)
                        {
                            var lit = l.List[l.List.Length - 1] as LiteralType;
                            if (lit != null)
                            {
                                lit.LineComment = lineComment;
                                l.List[l.List.Length - 1] = lit;
                                lineComment = null;
                            }
                        }
                        Unscan();

                        needComma = false;
                        continue;
                    }
                    else if (tok.Type == TokenType.LBRACE)
                    {
                        // Looks like a nested object, so parse it out
                        ObjectType node;
                        try
                        {
                            node = ObjectType();
                        }
                        catch (Exception ex)
                        {
                            throw new PosErrorException(tok.Pos,
                                    "error while trying to parse object within list", ex);
                        }
                        l.Add(node);
                        needComma = true;
                    }
                    else if (tok.Type == TokenType.LBRACK)
                    {
                        ListType node;
                        try
                        {
                            node = ListType();
                        }
                        catch (Exception ex)
                        {
                            throw new PosErrorException(tok.Pos,
                                    "error while trying to parse list within list", ex);
                        }
                        l.Add(node);
                    }
                    else if (tok.Type == TokenType.RBRACK)
                    {
                        // finished
                        l.Rbrack = tok.Pos;
                        return l;
                    }
                    else
                    {
                        throw new PosErrorException(tok.Pos,
                                $"unexpected token while parsing list: {tok.Type}");
                    }
                }
            }
            finally
            {
                //defer un(trace(p, "ParseListType"))                
            }
        }

        /// literalType parses a literal type and returns a LiteralType AST
        private LiteralType LiteralType()
        {
            try
            {
                return new LiteralType
                {
                    Token = tok,
                };
            }
            finally
            {
                //defer un(trace(p, "ParseLiteral"))
            }
        }

        /// scan returns the next token from the underlying scanner. If a token has
        /// been unscanned then read that instead. In the process, it collects any
        /// comment groups encountered, and remembers the last lead and line comments.
        private Token Scan()
        {
            // If we have a token on the buffer, then return it.
            if (n != 0)
            {
                n = 0;
                return tok;
            }

            // Otherwise read the next token from the scanner and Save it to the buffer
            // in case we unscan later.
            var prev = tok;
            tok = sc.Scan();

            if (tok.Type == TokenType.COMMENT)
            {
                CommentGroup comment = null;
                int endline;

                // fmt.Printf("p.tok.Pos.Line = %+v prev: %d endline %d \n",
                // p.tok.Pos.Line, prev.Pos.Line, endline)
                if (tok.Pos.Line == prev.Pos.Line)
                {
                    // The comment is on same line as the previous token; it
                    // cannot be a lead comment but may be a line comment.
                    (comment, endline) = ConsumeCommentGroup(0);
                    if (tok.Pos.Line != endline)
                    {
                        // The next token is on a different line, thus
                        // the last comment group is a line comment.
                        lineComment = comment;
                    }
                }

                // consume successor comments, if any
                endline = -1;
                while (tok.Type == TokenType.COMMENT)
                {
                    (comment, endline) = ConsumeCommentGroup(1);
                }

                if (endline + 1 == tok.Pos.Line && tok.Type != TokenType.RBRACE)
                {
                    switch (tok.Type)
                    {
                        // Do not count for these cases
                        case TokenType.RBRACE:
                            break;
                        case TokenType.RBRACK:
                            break;
                            
                        default:
                            // The next token is following on the line immediately after the
                            // comment group, thus the last comment group is a lead comment.
                            leadComment = comment;
                            break;
                    }
                }

            }

            return tok;
        }

        /// unscan pushes the previously read token back onto the buffer.
        private void Unscan()
        {
            n = 1;
        }


        // ----------------------------------------------------------------------------
        // Parsing support

        private void PrintTrace(params object[] a)
        {
            if (!enableTrace)
            {
                return;
            }

            var dots = ". . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . ";
            var n = dots.Length;
            Console.Write("{0}:{1}", tok.Pos.Line, tok.Pos.Column);

            var i = 2 * indent;
            while (i > n)
            {
                Console.Write(dots);
                i -= n;
            }
            // i <= n
            Console.Write(dots.AsCharSlice().Slice(0, i));
            Console.WriteLine(a);
        }

        private static Parser Trace(Parser p, string msg)
        {
            p.PrintTrace(msg, "(");
            p.indent++;
            return p;
        }

        // Usage pattern: defer un(trace(p, "..."))
        private static void Un(Parser p)
        {
            p.indent--;
            p.PrintTrace(")");
        }

    }
}