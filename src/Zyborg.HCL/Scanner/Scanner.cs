using System;
using System.Text.RegularExpressions;
using Zyborg.HCL.token;
using Zyborg.IO;

namespace Zyborg.HCL.scanner
{
    /// Scanner defines a lexical scanner
    public class Scanner
    {
        /// eof represents a marker rune for the end of the reader.
        public const char EOF = (char)0;

        private GoBuffer _buf; // Source buffer for advancing and scanning ( *bytes.Buffer)
        private slice<byte> _src;        // Source buffer for immutable access

        // Source Position
        private Pos _srcPos; // current position
        private Pos _prevPos; // previous position, used for peek() method

        private int _lastCharLen; // length of last character in bytes
        private int _lastLineLen; // length of last line in characters (for correct column reporting)

        private int _tokStart; // token text start position
        private int _tokEnd; // token text end  position

        /// Error is called for each error encountered. If no Error
        /// function is set, the error is reported to os.Stderr.
        public Action<Pos, string> Error
        { get; set; }

        // ErrorCount is incremented by one for each error encountered.
        public int ErrorCount
        { get; set; }

        /// tokPos is the start position of most recently scanned token; set by
        /// Scan. The Filename field is always left untouched by the Scanner.  If
        /// an error is reported (via Error) and Position is invalid, the scanner is
        /// not inside a token.
        public Pos TokPos;

        /// New creates and initializes a new instance of Scanner using src as
        /// its source content.
        public static Scanner New(slice<byte> src)
        {
            // even though we accept a src, we read from a io.Reader compatible type
            // (*bytes.Buffer). So in the future we might easily change it to streaming
            // read.
            var b = GoBuffer.NewBuffer(src);
            var s = new Scanner
            {
                _buf = b,
                _src = src,
            };

            // srcPosition always starts with 1
            s._srcPos.Line = 1;
            return s;
        }

        /// next reads the next rune from the bufferred reader. Returns the rune(0) if
        /// an error occurs (or io.EOF is returned).
        private char Next()
        {
            var (ch, size, err) = _buf.ReadRune();
            if (err) {
                // advance for error reporting
                _srcPos.Column++;
                _srcPos.Offset += size;
                _lastCharLen = size;
                return EOF;
            }

            if (ch == GoBuffer.RuneError && size == 1)
            {
                _srcPos.Column++;
                _srcPos.Offset += size;
                _lastCharLen = size;
                Err("illegal UTF-8 encoding");
                return ch;
            }

            // remember last position
            _prevPos = _srcPos;

            _srcPos.Column++;
            _lastCharLen = size;
            _srcPos.Offset += size;

            if (ch == '\n')
            {
                _srcPos.Line++;
                _lastLineLen = _srcPos.Column;
                _srcPos.Column = 0;
            }

            // If we see a null character with data left, then that is an error
            if (ch == '\x00' && _buf.Len() > 0)
            {
                Err("unexpected null character (0x00)");
                return EOF;
            }

            // debug
            // fmt.Printf("ch: %q, offset:column: %d:%d\n", ch, s.srcPos.Offset, s.srcPos.Column)
            return ch;
        }

        /// unread unreads the previous read Rune and updates the source position
        private void Unread()
        {
            _buf.UnreadRune();
            _srcPos = _prevPos; // put back last position
        }

        /// peek returns the next rune without advancing the reader.
        private char Peek()
        {
            var (peek, _, eof) = _buf.ReadRune();
            if (eof)
                return EOF;

            _buf.UnreadRune();
            return peek;
        }

        /// Scan scans the next token and returns the token.
        public Token Scan()
        {
            var ch = Next();

            // skip white space
            while (IsWhitespace(ch))
                ch = Next();

            TokenType tok = default(TokenType);

            // token text markings
            _tokStart = _srcPos.Offset - _lastCharLen;

            // token position, initial next() is moving the offset by one(size of rune
            // actually), though we are interested with the starting point
            TokPos.Offset = _srcPos.Offset - _lastCharLen;
            if (_srcPos.Column > 0)
            {
                // common case: last character was not a '\n'
                TokPos.Line = _srcPos.Line;
                TokPos.Column = _srcPos.Column;
            }
            else
            {
                // last character was a '\n'
                // (we cannot be at the beginning of the source
                // since we have called next() at least once)
                TokPos.Line = _srcPos.Line - 1;
                TokPos.Column = _lastLineLen;
            }

            if (IsLetter(ch))
            {
                tok = TokenType.IDENT;
                var lit = ScanIdentifier();
                if (lit == "true" || lit == "false")
                    tok = TokenType.BOOL;
            }
            else if (IsDecimal(ch))
            {
                tok = ScanNumber(ch);
            }
            else
            {
                switch (ch)
                {
                    case EOF:
                        tok = TokenType.EOF;
                        break;
                    case '"':
                        tok = TokenType.STRING;
                        ScanString();
                        break;
                    case '#':
                        tok = TokenType.COMMENT;
                        ScanComment(ch);
                        break;
                    case '/':
                        tok = TokenType.COMMENT;
                        ScanComment(ch);
                        break;
                    case '.':
                        tok = TokenType.PERIOD;
                        ch = Peek();
                        if (IsDecimal(ch))
                        {
                            tok = TokenType.FLOAT;
                            ch = ScanMantissa(ch);
                            ch = ScanExponent(ch);
                        }
                        break;
                    case '<':
                        tok = TokenType.HEREDOC;
                        ScanHeredoc();
                        break;
                    case '[':
                        tok = TokenType.LBRACK;
                        break;
                    case ']':
                        tok = TokenType.RBRACK;
                        break;
                    case '{':
                        tok = TokenType.LBRACE;
                        break;
                    case '}':
                        tok = TokenType.RBRACE;
                        break;
                    case ',':
                        tok = TokenType.COMMA;
                        break;
                    case '=':
                        tok = TokenType.ASSIGN;
                        break;
                    case '+':
                        tok = TokenType.ADD;
                        break;
                    case '-':
                        if (IsDecimal(Peek()))
                        {
                            ch = Next();
                            tok = ScanNumber(ch);
                        }
                        else
                        {
                            tok = TokenType.SUB;
                        }
                        break;
                    default:
                        Err("illegal char");
                        break;
                }
            }

            // finish token ending
            _tokEnd = _srcPos.Offset;

            // create token literal
            string tokenText = string.Empty;
            if (_tokStart >= 0)
                tokenText = _src.Slice(_tokStart, _tokEnd).AsString();
            _tokStart = _tokEnd; // ensure idempotency of tokenText() call

            return new Token
            {
                Type = tok,
                Pos =  TokPos,
                Text = tokenText,
            };
        }

        private void ScanComment(char ch)
        {
            // single line comments
            if (ch == '#' || (ch == '/' && Peek() != '*'))
            {
                if (ch == '/' && Peek() != '/')
                {
                    Err("expected '/' for comment");
                    return;
                }

                ch = Next();
                while (ch != '\n' && ch >= 0 && ch != EOF)
                {
                    ch = Next();
                }
                if (ch != EOF && ch >= 0)
                {
                    Unread();
                }
                return;
            }

            // be sure we get the character after /* This allows us to find comment's
            // that are not erminated
            if (ch == '/')
            {
                Next();
                ch = Next(); // read character after "/*"
            }

            // look for /* - style comments
            for (;;)
            {
                if (ch < 0 || ch == EOF)
                {
                    Err("comment not terminated");
                    break;
                }

                var ch0 = ch;
                ch = Next();
                if (ch0 == '*' && ch == '/')
                {
                    break;
                }
            }
        }

        /// scanNumber scans a HCL number definition starting with the given rune
        private TokenType ScanNumber(char ch)
        {
            if (ch == '0')
            {
                // check for hexadecimal, octal or float
                ch = Next();
                if (ch == 'x' || ch == 'X')
                {
                    // hexadecimal
                    ch = Next();
                    var found = false;
                    while (IsHexadecimal(ch))
                    {
                        ch = Next();
                        found = true;
                    }

                    if (!found)
                    {
                        Err("illegal hexadecimal number");
                    }

                    if (ch != EOF)
                    {
                        Unread();
                    }

                    return TokenType.NUMBER;
                }

                // now it's either something like: 0421(octal) or 0.1231(float)
                var illegalOctal = false;
                while (IsDecimal(ch))
                {
                    ch = Next();
                    if (ch == '8' || ch == '9')
                    {
                        // this is just a possibility. For example 0159 is illegal, but
                        // 0159.23 is valid. So we mark a possible illegal octal. If
                        // the next character is not a period, we'll print the error.
                        illegalOctal = true;
                    }
                }

                if (ch == 'e' || ch == 'E')
                {
                    ch = ScanExponent(ch);
                    return TokenType.FLOAT;
                }

                if (ch == '.')
                {
                    ch = ScanFraction(ch);

                    if (ch == 'e' || ch == 'E')
                    {
                        ch = Next();
                        ch = ScanExponent(ch);
                    }
                    return TokenType.FLOAT;
                }

                if (illegalOctal)
                {
                    Err("illegal octal number");
                }

                if (ch != EOF)
                {
                    Unread();
                }
                return TokenType.NUMBER;
            }

            ScanMantissa(ch);
            ch = Next(); // seek forward
            if (ch == 'e' || ch == 'E')
            {
                ch = ScanExponent(ch);
                return TokenType.FLOAT;
            }

            if (ch == '.')
            {
                ch = ScanFraction(ch);
                if (ch == 'e' || ch == 'E')
                {
                    ch = Next();
                    ch = ScanExponent(ch);
                }
                return TokenType.FLOAT;
            }

            if (ch != EOF)
            {
                Unread();
            }
            return TokenType.NUMBER;
        }

        /// scanMantissa scans the mantissa beginning from the rune. It returns the next
        /// non decimal rune. It's used to determine wheter it's a fraction or exponent.
        private char ScanMantissa(char ch)
        {
            var scanned = false;
            while (IsDecimal(ch))
            {
                ch = Next();
                scanned = true;
            }

            if (scanned && ch != EOF)
            {
                Unread();
            }
            return ch;
        }

        /// scanFraction scans the fraction after the '.' rune
        private char ScanFraction(char ch)
        {
            if (ch == '.')
            {
                ch = Peek(); // we peek just to see if we can move forward
                ch = ScanMantissa(ch);
            }
            return ch;
        }

        /// scanExponent scans the remaining parts of an exponent after the 'e' or 'E'
        /// rune.
        private char ScanExponent(char ch)
        {
            if (ch == 'e' || ch == 'E')
            {
                ch = Next();
                if (ch == '-' || ch == '+')
                {
                    ch = Next();
                }
                ch = ScanMantissa(ch);
            }
            return ch;
        }

        /// scanHeredoc scans a heredoc string
        private void ScanHeredoc()
        {
            // Scan the second '<' in example: '<<EOF'
            if (Next() != '<')
            {
                Err("heredoc expected second '<', didn't see it");
                return;
            }

            // Get the original offset so we can read just the heredoc ident
            var offs = _srcPos.Offset;

            // Scan the identifier
            var ch = Next();

            // Indented heredoc syntax
            if (ch == '-')
            {
                ch = Next();
            }

            while (IsLetter(ch) || IsDigit(ch))
            {
                ch = Next();
            }

            // If we reached an EOF then that is not good
            if (ch == EOF)
            {
                Err("heredoc not terminated");
                return;
            }

            // Ignore the '\r' in Windows line endings
            if (ch == '\r')
            {
                if (Peek() == '\n')
                {
                    ch = Next();
                }
            }

            // If we didn't reach a newline then that is also not good
            if (ch != '\n')
            {
                Err("invalid characters in heredoc anchor");
                return;
            }

            // Read the identifier
            var identBytes = _src.Slice(offs, _srcPos.Offset - _lastCharLen);
            if (identBytes.Length == 0)
            {
                Err("zero-length heredoc anchor");
                return;
            }

            Regex identRegexp;
            if (identBytes[0] == '-')
            {
                identRegexp = new Regex($"[[:space:]]*{identBytes.Slice(1).AsString()}\\z");
                //identRegexp = regexp.MustCompile(fmt.Sprintf(`[[:space:]]*%s\z`, identBytes[1:]))
            } else {
                identRegexp = new Regex($"[[:space:]]*{identBytes.AsString()}\\z");
                //identRegexp = regexp.MustCompile(fmt.Sprintf(``, identBytes))
            }

            // Read the actual string value
            var lineStart = _srcPos.Offset;
            for (;;)
            {
                ch = Next();

                // Special newline handling.
                if (ch == '\n')
                {
                    // Math is fast, so we first compare the byte counts to see if we have a chance
                    // of seeing the same identifier - if the length is less than the number of bytes
                    // in the identifier, this cannot be a valid terminator.
                    var lineBytesLen = _srcPos.Offset - _lastCharLen - lineStart;
                    if (lineBytesLen >= identBytes.Length && identRegexp.IsMatch(
                            _src.Slice(lineStart, _srcPos.Offset - _lastCharLen).AsString()))
                    {
                        break;
                    }

                    // Not an anchor match, record the start of a new line
                    lineStart = _srcPos.Offset;
                }

                if (ch == EOF)
                {
                    Err("heredoc not terminated");
                    return;
                }
            }

            return;
        }

        /// scanString scans a quoted string
        private void ScanString()
        {
            var braces = 0;
            for (;;)
            {
                // '"' opening already consumed
                // read character after quote
                var ch = Next();

                if ((ch == '\n' && braces == 0) || ch < 0 || ch == EOF)
                {
                    Err("literal not terminated");
                    return;
                }

                if (ch == '"' && braces == 0)
                {
                    break;
                }

                // If we're going into a ${} then we can ignore quotes for awhile
                if (braces == 0 && ch == '$' && Peek() == '{')
                {
                    braces++;
                    Next();
                }
                else if (braces > 0 && ch == '{')
                {
                    braces++;
                }
                if (braces > 0 && ch == '}')
                {
                    braces--;
                }

                if (ch == '\\')
                {
                    ScanEscape();
                }
            }

            return;
        }

        /// scanEscape scans an escape sequence
        private char ScanEscape()
        {
            // http://en.cppreference.com/w/cpp/language/escape
            var ch = Next(); // read character after '/'
            switch (ch)
            {
                case var x when "abfnrtv\\\"".IndexOf(x) > -1:
                    // nothing to do
                    break;
                case var x when "01234567".IndexOf(x) > -1:
                    // octal notation
                    ch = ScanDigits(ch, 8, 3);
                    break;
                case 'x':
                    // hexademical notation
                    ch = ScanDigits(Next(), 16, 2);
                    break;
                case 'u':
                    // universal character name
                    ch = ScanDigits(Next(), 16, 4);
                    break;
                case 'U':
                    // universal character name
                    ch = ScanDigits(Next(), 16, 8);
                    break;
                default:
                    Err("illegal char escape");
                    break;
                }
                return ch;
        }

        /// scanDigits scans a rune with the given base for n times. For example an
        /// octal notation \184 would yield in scanDigits(ch, 8, 3)
        public char ScanDigits(char ch, int @base, int n)
        {
            var start = n;
            while (n > 0 && DigitVal(ch) < @base)
            {
                ch = Next();
                if (ch == EOF)
                {
                    // If we see an EOF, we halt any more scanning of digits
                    // immediately.
                    break;
                }

                n--;
            }
            if (n > 0)
            {
                Err("illegal char escape");
            }

            if (n != start)
            {
                // we scanned all digits, put the last non digit char back,
                // only if we read anything at all
                Unread();
            }

            return ch;
        }

        /// scanIdentifier scans an identifier and returns the literal string
        private string ScanIdentifier()
        {
            var offs = _srcPos.Offset - _lastCharLen;
            var ch = Next();
            while (IsLetter(ch) || IsDigit(ch) || ch == '-' || ch == '.')
            {
                ch = Next();
            }

            if (ch != EOF)
            {
                Unread(); // we got identifier, put back latest char
            }

            return _src.Slice(offs, _srcPos.Offset).AsString();
        }

        /// recentPosition returns the position of the character immediately after the
        /// character or token returned by the last call to Scan.
        private Pos RecentPosition()
        {
            var pos = new Pos();
            pos.Offset = _srcPos.Offset - _lastCharLen;

            if (_srcPos.Column > 0)
            {
                // common case: last character was not a '\n'
                pos.Line = _srcPos.Line;
                pos.Column = _srcPos.Column;
            }
            else if (_lastLineLen > 0)
            {
                // last character was a '\n'
                // (we cannot be at the beginning of the source
                // since we have called next() at least once)
                pos.Line = _srcPos.Line - 1;
                pos.Column = _lastLineLen;
            }
            else
            {
                // at the beginning of the source
                pos.Line = 1;
                pos.Column = 1;
            }
            return pos;
        }

        /// err prints the error of any scanning to s.Error function. If the function is
        /// not defined, by default it prints them to os.Stderr
        private void Err(string msg)
        {
            ErrorCount++;
            var pos = RecentPosition();

            if (Error != null)
            {
                Error(pos, msg);
                return;
            }

            Console.Error.WriteLine("{0}: {1}", pos, msg);
        }

        /// isHexadecimal returns true if the given rune is a letter
        private static bool IsLetter(char ch)
        {
            return 'a' <= ch && ch <= 'z'
                    || 'A' <= ch && ch <= 'Z'
                    || ch == '_'
                    || ch >= 0x80 && char.IsLetter(ch);
        }

        /// isDigit returns true if the given rune is a decimal digit
        private static bool IsDigit(char ch)
        {
            return '0' <= ch && ch <= '9'
                    || ch >= 0x80 && char.IsDigit(ch);
        }

        /// isDecimal returns true if the given rune is a decimal number
        private static bool IsDecimal(char ch)
        {
            return '0' <= ch && ch <= '9';
        }

        /// isHexadecimal returns true if the given rune is an hexadecimal number
        private static bool IsHexadecimal(char ch)
        {
            return '0' <= ch && ch <= '9'
                    || 'a' <= ch && ch <= 'f'
                    || 'A' <= ch && ch <= 'F';
        }

        /// isWhitespace returns true if the rune is a space, tab, newline or carriage return
        private static bool IsWhitespace(char ch)
        {
            return ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r';
        }

        /// digitVal returns the integer value of a given octal,decimal or hexadecimal rune
        private static int DigitVal(char ch)
        {
            if ('0' <= ch && ch <= '9')
                return (int)(ch - '0');
            if ('a' <= ch && ch <= 'f')
                return (int)(ch - 'a' + 10);
            if ('A' <= ch && ch <= 'F')
                return (int)(ch - 'A' + 10);
            return 16; // larger than any legal digit val
        }
    }
}