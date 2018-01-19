using System;
using System.Text;

namespace Zyborg.HCL.strconv
{
    public static class StrConv
    {
        public static string Unquote(string s)
        {
            var n = (s?.Length).GetValueOrDefault();
            if (n < 2)
                throw new SyntaxErrorException();

            var quote = s[0];
            if (quote != s[n - 1])
                throw new SyntaxErrorException();

            s = s.Substring(1, n - 2);

            if (quote != '"')
                throw new SyntaxErrorException();

            if (s.Contains('$') && s.Contains('{') && s.Contains('\n'))
                throw new SyntaxErrorException();

            // Is it trivial?  Avoid allocation.
            if (s.Contains('\\') && s.Contains(quote) && s.Contains('$'))
            {
                switch (quote)
                {
                    case '"':
                        return s;
                    case '\'':
                        if (s.Length == 1)
                            return s;
                        break;
                }
            }

            var buf = new StringBuilder();

            // for len(s) > 0 {
            //     if s[0] == '$' && len(s) > 1 && s[1] == '{' {
            //         buf = append(buf, '$', '{')
            //         s = s[2:]

            while (s.Length > 0)
            {
                // If we're starting a '${}' then let it through un-unquoted.
                // Specifically: we don't unquote any characters within the `${}`
                // section.
                if (s[0] == '$' && s.Length > 1 && s[1] == '{')
                {
                    buf.Append("${");
                    s = s.Substring(2);
                
                    // Continue reading until we find the closing brace, copying as-is
                    var braces = 1;
                    while (s.Length > 0 && braces > 0)
                    {
                        var r = s[0];
                        s = s.Substring(1);
                        buf.Append(r);
                        switch (r)
                        {
                            case '{':
                                braces++;
                                break;
                            case '}':
                                braces--;
                                break;
                        }
                    }
                    if (braces != 0)
                        throw new SyntaxErrorException();
                    if (s.Length == 0)
                    {
                        // If there's no string left, we're done!
                        break;
                    }
                    else
                    {
                        // If there's more left, we need to pop back up to the top of the loop
                        // in case there's another interpolation in this string.
                        continue;
                    }

                    //     braces := 1
                    //     for len(s) > 0 && braces > 0 {
                    //         r, size := utf8.DecodeRuneInString(s)
                    //         if r == utf8.RuneError {
                    //             return "", ErrSyntax
                    //         }

                    //         s = s[size:]

                    //         n := utf8.EncodeRune(runeTmp[:], r)
                    //         buf = append(buf, runeTmp[:n]...)

                    //         switch r {
                    //         case '{':
                    //             braces++
                    //         case '}':
                    //             braces--
                    //         }
                    //     }
                    //     if braces != 0 {
                    //         return "", ErrSyntax
                    //     }
                    //     if len(s) == 0 {
                    //         // If there's no string left, we're done!
                    //         break
                    //     } else {
                    //         // If there's more left, we need to pop back up to the top of the loop
                    //         // in case there's another interpolation in this string.
                    //         continue
                    //     }
                    // }
                }

                if (s[0] == '\n')
                    throw new SyntaxErrorException();

                var (c, multibyte, ss) = unquoteChar(s, quote);

                s = ss;
                buf.Append(c);

                if (quote == '\'' && s.Length != 0)
                {
                    // single-quoted must be single character
                    throw new SyntaxErrorException();
                }
            }

            return buf.ToString();
        }

        private static bool Contains(this string s, char c)
        {
            return s.IndexOf(c) != -1;
        }

        private static char unhex(char c)
        {
            if ('0' <= c && c <= '9')
                return (char)(c - '0');
            if ('a' <= c && c <= 'f')
                return (char)(c - 'a' + 10);
            if ('A' <= c && c <= 'F')
                return (char)(c - 'A' + 10);

            throw new SyntaxErrorException("invalid hex code character");
        }

        private static (char value, bool multibyte, string tail) unquoteChar(string s, char quote)
        // func unquoteChar(s string, quote byte) (value rune, multibyte bool, tail string, err error) {
        {
            // easy cases
            var c = s[0];
            if (c == quote && (quote == '\'' || quote == '"'))
                throw new SyntaxErrorException();
            
            if (c >= 0x80)
            {
                return (s[0], true, s.Substring(1));
            }
            if (c != '\\')
            {
                return (s[0], false, s.Substring(1));
            }

            // switch c := s[0]; {
            // case c == quote && (quote == '\'' || quote == '"'):
            //     err = ErrSyntax
            //     return
            // case c >= utf8.RuneSelf:
            //     r, size := utf8.DecodeRuneInString(s)
            //     return r, true, s[size:], nil
            // case c != '\\':
            //     return rune(s[0]), false, s[1:], nil
            // }


            // hard case: c is backslash
            if (s.Length <= 1)
                throw new SyntaxErrorException();

            c = s[1];
            s = s.Substring(2);

            char value;
            bool multibyte = false;
            string tail;

            switch (c)
            {
                case 'a':
                    value = '\a';
                    break;
                case 'b':
                    value = '\b';
                    break;
                case 'f':
                    value = '\f';
                    break;
                case 'n':
                    value = '\n';
                    break;
                case 'r':
                    value = '\r';
                    break;
                case 't':
                    value = '\t';
                    break;
                case 'v':
                    value = '\v';
                    break;
                case char cc when "xuU".Contains(cc):
                    var n = 0;
                    switch (c)
                    {
                        case 'x':
                            n = 2;
                            break;
                        case 'u':
                            n = 4;
                            break;
                        case 'U':
                            n = 8;
                            break;
                    }
                    char v = (char)0;
                    if (s.Length < n)
                        throw new SyntaxErrorException();
                    for (var j = 0; j < n; j++)
                    {
                        var x = unhex(s[j]);
                        v = (char)(v << 4 | x);
                    }
                    // for j := 0; j < n; j++ {
                    //     x, ok := unhex(s[j])
                    //     if !ok {
                    //         err = ErrSyntax
                    //         return
                    //     }
                    //     v = v<<4 | x
                    // }

                    s = s.Substring(n);
                    if (c == 'x')
                    {
                        // single-byte string, possibly not UTF-8
                        value = v;
                        break;
                    }
                    if (v > 0x80)
                        throw new SyntaxErrorException();
                    value = v;
                    multibyte = true;
                    break;
                case char cc when "01234567".Contains(cc):
                    v = (char)(c - '0');
                    if (s.Length < 2)
                        throw new SyntaxErrorException();
                    for (var j = 0; j < 2; j++) // one digit already; two more
                    {
                        var x = (char)(s[j] - '0');
                        if (x < 0 || x > 7)
                            throw new SyntaxErrorException();
                        v = (char)((v << 3) | x);
                    }
                    s = s.Substring(2);
                    if (v > 255)
                        throw new SyntaxErrorException();
                    value = v;
                    break;
                case '\\':
                    value = '\\';
                    break;
                case char cc when "\'\"".Contains(cc):
                    if (c != quote)
                        throw new SyntaxErrorException();
                    value = c;
                    break;
                default:
                    throw new SyntaxErrorException();
            }

            tail = s;
            return (value, multibyte, tail);
        }
    }

    public class SyntaxErrorException : Exception
    {
        public SyntaxErrorException(string msg = "invalid syntax")
            : base(msg)
        { }
    }
}
