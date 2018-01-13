using System;
using System.Collections.Generic;
using System.Text;

namespace Zyborg.HCL.Token
{
    public enum TokenType
    {
        // Special tokens
        ILLEGAL = 0,
        EOF,
        COMMENT,

        _identifier_beg,
        IDENT, // literals
        _literal_beg,
        NUMBER,  // 12345
        FLOAT,   // 123.45
        BOOL,    // true,false
        STRING,  // abc
        HEREDOC, // <<FOO\nbar\nFOO
        _literal_end,
        _identifier_end,

        _operator_beg,
        LBRACK, // [
        LBRACE, // {
        COMMA,  // ,
        PERIOD, // .

        RBRACK, // ]
        RBRACE, // }

        ASSIGN, // =
        ADD,    // +
        SUB,    // -
        _operator_end,
    }

    /// Token defines a single HCL token which can be obtained via the Scanner
    public class Token
    {
        public TokenType Type
        {get; set; }

        public Pos  Pos
        {get; set; }
        
        public string Text
        {get; set; }
        
        public bool JSON
        {get; set; }

        public static readonly IReadOnlyDictionary<TokenType, string> Tokens = new Dictionary<TokenType, string>
        {
            [TokenType.ILLEGAL] = nameof(TokenType.ILLEGAL),

            [TokenType.EOF] = nameof(TokenType.EOF),
            [TokenType.COMMENT] = nameof(TokenType.COMMENT),

            [TokenType.IDENT] = nameof(TokenType.IDENT),
            [TokenType.NUMBER] = nameof(TokenType.NUMBER),
            [TokenType.FLOAT] = nameof(TokenType.FLOAT),
            [TokenType.BOOL] = nameof(TokenType.BOOL),
            [TokenType.STRING] = nameof(TokenType.STRING),

            [TokenType.LBRACK] = nameof(TokenType.LBRACK),
            [TokenType.LBRACE] = nameof(TokenType.LBRACE),
            [TokenType.COMMA] = nameof(TokenType.COMMA),
            [TokenType.PERIOD] = nameof(TokenType.PERIOD),
            [TokenType.HEREDOC] = nameof(TokenType.HEREDOC),

            [TokenType.RBRACK] = nameof(TokenType.RBRACK),
            [TokenType.RBRACE] = nameof(TokenType.RBRACE),

            [TokenType.ASSIGN] = nameof(TokenType.ASSIGN),
            [TokenType.ADD] = nameof(TokenType.ADD),
            [TokenType.SUB] = nameof(TokenType.SUB),
        };

        public static string ToString(TokenType t)
        {
            var s = string.Empty;
            if (0 <= ((int)t) && ((int)t) < Tokens.Count)
                s = Tokens[t];
            if (s == string.Empty)
                s = $"token({(int)t})";
            return s;
        }

        /// IsIdentifier returns true for tokens corresponding to identifiers and basic
        /// type literals; it returns false otherwise.
        public static bool IsIdentifier(TokenType t) =>
                TokenType._identifier_beg < t && t < TokenType._identifier_end;

        /// IsLiteral returns true for tokens corresponding to basic type literals; it
        /// returns false otherwise.
        public static bool IsLiteral(TokenType t) =>
                TokenType._literal_beg < t && t < TokenType._literal_end;

        /// IsOperator returns true for tokens corresponding to operators and
        /// delimiters; it returns false otherwise.
        public static bool IsOperator(TokenType t) =>
                TokenType._operator_beg < t && t < TokenType._operator_end;

        /// String returns the token's literal text. Note that this is only
        /// applicable for certain token types, such as token.IDENT,
        /// token.STRING, etc..
        public static string ToString(Token t) => $"{t.Pos} {ToString(t.Type)} {t.Text}";


        // TODO: strconv.Unquote:  https://golang.org/pkg/strconv/#Unquote
        public static string StdUnquote(string q) => throw new NotImplementedException();


        /// Returns the properly typed value for this token. The type of
        /// the returned interface{} is guaranteed based on the Type field.
        ///
        /// This can only be called for literal types. If it is called for any other
        /// type, this will panic.
        public object Value()
        {
            switch (this.Type)
            {
                case TokenType.BOOL:
                    if (this.Text == "true") {
                        return true;
                    } else if (this.Text == "false") {
                        return false;
                    }
                    throw new Exception("unknown bool value: " + this.Text);
                case TokenType.FLOAT:
                    return double.Parse(this.Text);
                case TokenType.NUMBER:
                    return long.Parse(this.Text);
                case TokenType.IDENT:
                    return this.Text;
                case TokenType.HEREDOC:
                    return unindentHeredoc(this.Text);
                case TokenType.STRING:
                    // Determine the Unquote method to use. If it came from JSON,
                    // then we need to use the built-in unquote since we have to
                    // escape interpolations there.
                    Func<string, string> f = StrConv.StrConv.Unquote;
                    if (this.JSON)
                    {
                        f = StdUnquote;
                    }

                    // This case occurs if json null is used
                    if (this.Text == "") {
                        return "";
                    }

                    try 
                    {
                        return f(this.Text);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"unquote {this.Text} err: {ex.Message}");
                    }
                default:
                    throw new Exception($"unimplemented Value for type: {this.Type}");
            }
        }

        /// unindentHeredoc returns the string content of a HEREDOC if it is started with <<
        /// and the content of a HEREDOC with the hanging indent removed if it is started with
        /// a &lt;&lt;-, and the terminating line is at least as indented as the least indented line.

        protected string unindentHeredoc(string heredoc)
        {
            // We need to find the end of the marker
            var idx = heredoc.IndexOf('\n');
            if (idx == -1)
            {
                throw new Exception("heredoc doesn't contain newline");
            }

            var unindent = heredoc[2] == '-';

            // We can optimize if the heredoc isn't marked for indentation
            if (!unindent) {
                //return string(heredoc[idx+1 : len(heredoc)-idx+1])
                return heredoc.Substring(idx + 1, (heredoc.Length - idx + 1) - (idx + 1));
            }

            // We need to unindent each line based on the indentation level of the marker
            //lines := strings.Split(string(heredoc[idx+1:len(heredoc)-idx+2]), "\n")
            var lines = heredoc.Substring(idx + 1, (heredoc.Length - idx + 2) - (idx + 1)).Split('\n');
            //whitespacePrefix := lines[len(lines)-1]
            var whitespacePrefix = lines[lines.Length - 1];

            var isIndented = true;
            foreach (var v in lines)
            {
                if (v.StartsWith(whitespacePrefix))
                    continue;

                isIndented = false;
                break;
            }

            // If all lines are not at least as indented as the terminating mark, return the
            // heredoc as is, but trim the leading space from the marker on the final line.
            if (!isIndented)
            {
                //return strings.TrimRight(string(heredoc[idx+1:len(heredoc)-idx+1]), " \t")
                return heredoc.Substring(idx + 1, (heredoc.Length - idx + 1) - (idx + 1)).TrimEnd('\t');
            }

            var unindentedLines = new string[lines.Length];
            for (int k = 0, len = lines.Length, pfx = whitespacePrefix.Length; k < len; k++)
            {
                if (k == len - 1)
                {
                    unindentedLines[k] = "";
                    break;
                }
                unindentedLines[k] = lines[k].Remove(0, pfx);
            }

            return string.Join("\n", unindentedLines);
        }
    }
}