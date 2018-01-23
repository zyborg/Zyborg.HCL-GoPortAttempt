using System;
using System.Collections.Generic;
using gozer;
using GoBuffer = gozer.bytes.Buffer;
using Zyborg.HCL.ast;
using Zyborg.HCL.token;
using NStack;

namespace Zyborg.HCL.printer
{
    public partial class Printer
    {
        public const byte blank    = (byte)(' ');
        public const byte newline  = (byte)('\n');
        public const byte tab      = (byte)('\t');
        public const int infinity = (int)(1 << 30); // offset or line


        public static readonly slice<byte> unindent = "\uE123".AsByteSlice(); // in the private use space

        private Config cfg;
        private Pos prev;

        private slice<CommentGroup> comments; // may be nil, contains all comments
        private slice<CommentGroup> standaloneComments; // contains all standalone comments (not assigned to any node)

        private bool enableTrace;
        private int indentTrace;

        /// collectComments comments all standalone comments which are not lead or line
        /// comment
        private void CollectComments(INode node)
        {
            // first collect all comments. This is already stored in
            // ast.File.(comments)
            Ast.Walk(node, (INode nn) => {
                switch (nn)
                {
                    case File t:
                        comments = t.Comments;
                        return (nn, true);
                }
                return (nn, false);
            });

            var standaloneComments = new Dictionary<Pos, CommentGroup>();
            //make(map[token.Pos]*ast.CommentGroup, 0)
            foreach (var c in comments)
            {
                standaloneComments[c.Pos()] = c;
            }

            // next remove all lead and line comments from the overall comment map.
            // This will give us comments which are standalone, comments which are not
            // assigned to any kind of node.
            Ast.Walk(node, (INode nn) => {
                switch (nn)
                {
                    case LiteralType t:
                        if (t.LeadComment != null)
                        {
                            foreach (var comment in t.LeadComment.List)
                            {
                                if (standaloneComments.ContainsKey(comment.Pos()))
                                    standaloneComments.Remove(comment.Pos());
                            }
                        }

                        if (t.LineComment != null)
                        {
                            foreach (var comment in t.LineComment.List)
                            {
                                if (standaloneComments.ContainsKey(comment.Pos()))
                                    standaloneComments.Remove(comment.Pos());
                            }
                        }
                        break;
                    case ObjectItem t:
                        if (t.LeadComment != null)
                        {
                            foreach (var comment in t.LeadComment.List)
                            {
                                if (standaloneComments.ContainsKey(comment.Pos()))
                                    standaloneComments.Remove(comment.Pos());
                            }
                        }
                        if (t.LineComment != null)
                        {
                            foreach (var comment in t.LineComment.List)
                            {
                                if (standaloneComments.ContainsKey(comment.Pos()))
                                    standaloneComments.Remove(comment.Pos());
                            }
                        }
                        break;
                }

                return (nn, true);
            });

            foreach (var c in standaloneComments)
            {
                this.standaloneComments.Append(c.Value);
            }

            System.Array.Sort(this.standaloneComments.ToArray());
        }

        /// output prints creates b printable HCL output and returns it.
        private slice<byte> Output(object n)
        {
            var buf = new GoBuffer();

            using (var defer = Defer.Call())
            {
                switch (n)
                {
                case File t:
                    // File doesn't trace so we add the tracing here
                    //defer.Add(() => un(trace(this, "File")));
                    return Output(t.Node);
                case ObjectList t:
                    //defer.Add(() => un(trace(p, "ObjectList")));

                    int index = 0;
                    for (;;)
                    {
                        // Determine the location of the next actual non-comment
                        // item. If we're at the end, the next item is at "infinity"
                        Pos nextItem;
                        if (index != t.Items.Length)
                        {
                            nextItem = t.Items[index].Pos();
                        }
                        else
                        {
                            nextItem = new Pos { Offset = infinity, Line = infinity };
                        }

                        // Go through the standalone comments in the file and print out
                        // the comments that we should be for this object item.
                        foreach (var c in this.standaloneComments)
                        {
                            // Go through all the comments in the group. The group
                            // should be printed together, not separated by double newlines.
                            var printed = false;
                            var newlinePrinted = false;
                            foreach (var comment in c.List)
                            {
                                // We only care about comments after the previous item
                                // we've printed so that comments are printed in the
                                // correct locations (between two objects for example).
                                // And before the next item.
                                if (comment.Pos().After(this.prev) && comment.Pos().Before(nextItem))
                                {
                                    // if we hit the end add newlines so we can print the comment
                                    // we don't do this if prev is invalid which means the
                                    // beginning of the file since the first comment should
                                    // be at the first line.
                                    if (!newlinePrinted && this.prev.IsValid && index == t.Items.Length)
                                    {
                                        buf.Write(slice.From(newline, newline));
                                        newlinePrinted = true;
                                    }

                                    // Write the actual comment.
                                    buf.WriteString(comment.Text);
                                    buf.WriteByte(newline);

                                    // Set printed to true to note that we printed something
                                    printed = true;
                                }
                            }

                            // If we're not at the last item, write a new line so
                            // that there is a newline separating this comment from
                            // the next object.
                            if (printed && index != t.Items.Length)
                            {
                                buf.WriteByte(newline);
                            }
                        }

                        if (index == t.Items.Length)
                        {
                            break;
                        }

                        buf.Write(this.Output(t.Items[index]));
                        if (index != t.Items.Length - 1)
                        {
                            // Always write a newline to separate us from the next item
                            buf.WriteByte(newline);

                            // Need to determine if we're going to separate the next item
                            // with a blank line. The logic here is simple, though there
                            // are a few conditions:
                            //
                            //   1. The next object is more than one line away anyways,
                            //      so we need an empty line.
                            //
                            //   2. The next object is not a "single line" object, so
                            //      we need an empty line.
                            //
                            //   3. This current object is not a single line object,
                            //      so we need an empty line.
                            var current = t.Items[index];
                            var next = t.Items[index + 1];
                            if ((next.Pos().Line != t.Items[index].Pos().Line + 1)
                                    || !this.IsSingleLineObject(next)
                                    || !this.IsSingleLineObject(current))
                            {
                                buf.WriteByte(newline);
                            }
                        }
                        index++;
                    }
                    break;
                case ObjectKey t:
                    buf.WriteString(t.Token.Text);
                    break;
                case ObjectItem t:
                    this.prev = t.Pos();
                    buf.Write(this.ObjectItem(t));
                    break;
                case LiteralType t:
                    buf.Write(this.LiteralType(t));
                    break;
                case ListType t:
                    buf.Write(this.List(t));
                    break;
                case ObjectType t:
                    buf.Write(this.ObjectType(t));
                    break;
                default:
                    Console.WriteLine($"unknown type {n}");
                    break;
                }

                return buf.Bytes();
            }
        }

        private slice<byte> LiteralType(LiteralType lit)
        {
            var result = lit.Token.Text.AsByteSlice();
            switch (lit.Token.Type)
            {
                case TokenType.HEREDOC:
                    // Clear the trailing newline from heredocs
                    if (result[result.Length - 1] == '\n')
                    {
                        result = result.Slice(upper: result.Length - 1);
                    }

                    // Poison lines 2+ so that we don't indent them
                    result = this.HeredocIndent(result);
                    break;
                case TokenType.STRING:
                    // If this is a multiline string, poison lines 2+ so we don't
                    // indent them.
                    
                    if (result.IndexOf((byte)'\n') >= 0)
                    {
                        result = this.HeredocIndent(result);
                    }
                    break;
            }

            return result;
        }


        /// objectItem returns the printable HCL form of an object item. An object type
        /// starts with one/multiple keys and has a value. The value might be of any
        /// type.
        private slice<byte> ObjectItem(ObjectItem o)
        {
            //using (Defer.Call(() => un(trace(p, fmt.Sprintf("ObjectItem: %s", o.Keys[0].Token.Text)))))
            {
                var buf = new GoBuffer();

                if (o.LeadComment != null)
                {
                    foreach (var comment in o.LeadComment.List)
                    {
                        buf.WriteString(comment.Text);
                        buf.WriteByte(newline);
                    }
                }

                foreach (var (i, k) in o.Keys.Range())
                {
                    buf.WriteString(k.Token.Text);
                    buf.WriteByte(blank);

                    // reach end of key
                    if (o.Assign.IsValid && (i == o.Keys.Length - 1) && (o.Keys.Length == 1))
                    {
                        buf.WriteString("=");
                        buf.WriteByte(blank);
                    }
                }

                buf.Write(this.Output(o.Val));

                if ((o.Val.Pos().Line == o.Keys[0].Pos().Line) && (o.LineComment != null))
                {
                    buf.WriteByte(blank);
                    foreach (var comment in o.LineComment.List)
                    {
                        buf.WriteString(comment.Text);
                    }
                }

                return buf.Bytes();
            }
        }


        /// objectType returns the printable HCL form of an object type. An object type
        /// begins with a brace and ends with a brace.
        private slice<byte> ObjectType(ObjectType o)
        {
            //defer un(trace(p, "ObjectType"))

            var buf = new GoBuffer();
            buf.WriteString("{");

            int index = 0;
            Pos nextItem;
            bool commented = false, newlinePrinted = false;
            for (;;)
            {
                // Determine the location of the next actual non-comment
                // item. If we're at the end, the next item is the closing brace
                if (index != o.List.Items.Length)
                {
                    nextItem = o.List.Items[index].Pos();
                }
                else
                {
                    nextItem = o.Rbrace;
                }

                // Go through the standalone comments in the file and print out
                // the comments that we should be for this object item.
                foreach (var c in this.standaloneComments)
                {
                    var printed = false;
                    var lastCommentPos = new Pos();
                    foreach (var comment in c.List)
                    {
                        // We only care about comments after the previous item
                        // we've printed so that comments are printed in the
                        // correct locations (between two objects for example).
                        // And before the next item.
                        if (comment.Pos().After(this.prev) && comment.Pos().Before(nextItem))
                        {
                            // If there are standalone comments and the initial newline has not
                            // been printed yet, do it now.
                            if (!newlinePrinted)
                            {
                                newlinePrinted = true;
                                buf.WriteByte(newline);
                            }

                            // add newline if it's between other printed nodes
                            if (index > 0)
                            {
                                commented = true;
                                buf.WriteByte(newline);
                            }

                            // Store this position
                            lastCommentPos = comment.Pos();

                            // output the comment itself
                            buf.Write(this.Indent(this.HeredocIndent(comment.Text.AsByteSlice())));

                            // Set printed to true to note that we printed something
                            printed = true;

                            /*
                                if index != len(o.List.Items) {
                                    buf.WriteByte(newline) // do not print on the end
                                }
                            */
                        }
                    }

                    // Stuff to do if we had comments
                    if (printed)
                    {
                        // Always write a newline
                        buf.WriteByte(newline);

                        // If there is another item in the object and our comment
                        // didn't hug it directly, then make sure there is a blank
                        // line separating them.
                        if (!object.Equals(nextItem, o.Rbrace) && (nextItem.Line != lastCommentPos.Line + 1))
                        {
                            buf.WriteByte(newline);
                        }
                    }
                }

                if (index == o.List.Items.Length)
                {
                    this.prev = o.Rbrace;
                    break;
                }

                // At this point we are sure that it's not a totally empty block: print
                // the initial newline if it hasn't been printed yet by the previous
                // block about standalone comments.
                if (!newlinePrinted)
                {
                    buf.WriteByte(newline);
                    newlinePrinted = true;
                }

                // check if we have adjacent one liner items. If yes we'll going to align
                // the comments.
                var aligned = slice<ObjectItem>.Empty;
                foreach (var item in o.List.Items.Slice(index))
                {
                    // we don't group one line lists
                    if (o.List.Items.Length == 1)
                    {
                        break;
                    }

                    // one means a oneliner with out any lead comment
                    // two means a oneliner with lead comment
                    // anything else might be something else
                    var cur = Lines(this.ObjectItem(item).AsString());
                    if (cur > 2)
                        break;

                    var curPos = item.Pos();

                    var nextPos = new Pos();
                    if (index != o.List.Items.Length - 1)
                    {
                        nextPos = o.List.Items[index + 1].Pos();
                    }

                    var prevPos = new Pos();
                    if (index != 0)
                    {
                        prevPos = o.List.Items[index - 1].Pos();
                    }

                    // fmt.Println("DEBUG ----------------")
                    // fmt.Printf("prev = %+v prevPos: %s\n", prev, prevPos)
                    // fmt.Printf("cur = %+v curPos: %s\n", cur, curPos)
                    // fmt.Printf("next = %+v nextPos: %s\n", next, nextPos)

                    if ((curPos.Line + 1) == nextPos.Line)
                    {
                        aligned = aligned.Append(item);
                        index++;
                        continue;
                    }

                    if ((curPos.Line - 1) == prevPos.Line)
                    {
                        aligned = aligned.Append(item);
                        index++;

                        // finish if we have a new line or comment next. This happens
                        // if the next item is not adjacent
                        if ((curPos.Line + 1) != nextPos.Line)
                        {
                            break;
                        }
                        continue;
                    }

                    break;
                }

                // put newlines if the items are between other non aligned items.
                // newlines are also added if there is a standalone comment already, so
                // check it too
                if (!commented && (index != aligned.Length))
                {
                    buf.WriteByte(newline);
                }

                if (aligned.Length >= 1)
                {
                    this.prev = aligned[aligned.Length - 1].Pos();

                    var items = this.AlignedItems(aligned);
                    buf.Write(this.Indent(items));
                }
                else
                {
                    this.prev = o.List.Items[index].Pos();

                    buf.Write(this.Indent(this.ObjectItem(o.List.Items[index])));
                    index++;
                }

                buf.WriteByte(newline);
            }

            buf.WriteString("}");
            return buf.Bytes();
        }

        private slice<byte> AlignedItems(slice<ObjectItem> items)
        {
            var buf  = new GoBuffer();

            // find the longest key and value length, needed for alignment
            var longestKeyLen = 0; // longest key length
            var longestValLen = 0; // longest value length
            foreach (var item in items)
            {
                var key = item.Keys[0].Token.Text.Length;
                var val = this.Output(item.Val).Length;

                if (key > longestKeyLen) {
                    longestKeyLen = key;
                }

                if (val > longestValLen) {
                    longestValLen = val;
                }
            }

            foreach (var (i, item) in items.Range()) {
                if (item.LeadComment != null) {
                    foreach (var comment in item.LeadComment.List) {
                        buf.WriteString(comment.Text);
                        buf.WriteByte(newline);
                    }
                }

                foreach (var k in item.Keys) {
                    var keyLen = k.Token.Text.Length;
                    buf.WriteString(k.Token.Text);
                    var ii = 0;
                    for (; ii < longestKeyLen-keyLen + 1; ii++) {
                        buf.WriteByte(blank);
                    }

                    // reach end of key
                    if ((ii == item.Keys.Length - 1) && (item.Keys.Length == 1)) {
                        buf.WriteString("=");
                        buf.WriteByte(blank);
                    }
                }

                var val = this.Output(item.Val);
                var valLen = val.Length;
                buf.Write(val);

                if ((item.Val.Pos().Line == item.Keys[0].Pos().Line) && (item.LineComment != null)) {
                    var ii = 0;
                    for (; ii < longestValLen-valLen + 1; ii++) {
                        buf.WriteByte(blank);
                    }

                    foreach (var comment in item.LineComment.List) {
                        buf.WriteString(comment.Text);
                    }
                }

                // do not print for the last item
                if (i != items.Length - 1) {
                    buf.WriteByte(newline);
                }
            }

            return buf.Bytes();
        }

        /// list returns the printable HCL form of an list type.
        private slice<byte> List(ListType l)
        {
            var buf = new GoBuffer();
            buf.WriteString("[");

            var longestLine = 0;
            foreach (var item in l.List) {
                // for now we assume that the list only contains literal types
                var lit = item as LiteralType;
                if (lit != null) {
                    var lineLen = lit.Token.Text.Length;
                    if (lineLen > longestLine) {  
                        longestLine = lineLen;
                    }
                }
            }

            var insertSpaceBeforeItem = false;
            var lastHadLeadComment = false;
            foreach (var (i, item) in l.List.Range()) {
                // Keep track of whether this item is a heredoc since that has
                // unique behavior.
                var heredoc = false;
                var lit = item as LiteralType;
                if (lit != null  && lit.Token.Type == TokenType.HEREDOC) {
                    heredoc = true;
                }

                if (item.Pos().Line != l.Lbrack.Line) {
                    // multiline list, add newline before we add each item
                    buf.WriteByte(newline);
                    insertSpaceBeforeItem = false;

                    // If we have a lead comment, then we want to write that first
                    var leadComment = false;
                    lit = item as LiteralType;
                    if (lit != null && lit.LeadComment != null) {
                        leadComment = true;

                        // If this isn't the first item and the previous element
                        // didn't have a lead comment, then we need to add an extra
                        // newline to properly space things out. If it did have a
                        // lead comment previously then this would be done
                        // automatically.
                        if (i > 0 && !lastHadLeadComment) {
                            buf.WriteByte(newline);
                        }

                        foreach (var comment in lit.LeadComment.List) {
                            buf.Write(this.Indent(comment.Text.AsByteSlice()));
                            buf.WriteByte(newline);
                        }
                    }

                    // also indent each line
                    var val = this.Output(item);
                    var curLen = val.Length;
                    buf.Write(this.Indent(val));

                    // if this item is a heredoc, then we output the comma on
                    // the next line. This is the only case this happens.
                    var comma = slice.From((byte)',');
                    if (heredoc) {
                        buf.WriteByte(newline);
                        comma = this.Indent(comma);
                    }

                    buf.Write(comma);

                    lit = item as LiteralType;
                    if (lit != null && lit.LineComment != null) {
                        // if the next item doesn't have any comments, do not align
                        buf.WriteByte(blank); // align one space
                        for (var ii = 0; ii < longestLine - curLen; ii++) {
                            buf.WriteByte(blank);
                        }

                        foreach (var comment in lit.LineComment.List) {
                            buf.WriteString(comment.Text);
                        }
                    }

                    var lastItem = i == l.List.Length - 1;
                    if (lastItem) {
                        buf.WriteByte(newline);
                    }

                    if (leadComment && !lastItem) {
                        buf.WriteByte(newline);
                    }

                    lastHadLeadComment = leadComment;
                } else {
                    if (insertSpaceBeforeItem) {
                        buf.WriteByte(blank);
                        insertSpaceBeforeItem = false;
                    }

                    // Output the item itself
                    // also indent each line
                    var val = this.Output(item);
                    var curLen = val.Length;
                    buf.Write(val);

                    // If this is a heredoc item we always have to output a newline
                    // so that it parses properly.
                    if (heredoc) {
                        buf.WriteByte(newline);
                    }

                    // If this isn't the last element, write a comma.
                    if (i != l.List.Length - 1) {
                        buf.WriteString(",");
                        insertSpaceBeforeItem = true;
                    }

                    lit = item as LiteralType;
                    if (lit != null && lit.LineComment != null) {
                        // if the next item doesn't have any comments, do not align
                        buf.WriteByte(blank); // align one space
                        for (var ii = 0; ii < longestLine - curLen; ii++) {
                            buf.WriteByte(blank);
                        }

                        foreach (var comment in lit.LineComment.List) {
                            buf.WriteString(comment.Text);
                        }
                    }
                }

            }

            buf.WriteString("]");
            return buf.Bytes();
        }

        /// indent indents the lines of the given buffer for each non-empty line
        private slice<byte> Indent(slice<byte> buf)
        {
            var prefix = slice<byte>.Empty;
            if (this.cfg.SpacesWidth != 0) {
                for (var i = 0; i < this.cfg.SpacesWidth; i++) {
                    prefix = prefix.Append(blank);
                }
            } else {
                prefix = slice.From(tab);
            }

            var res = slice<byte>.Empty;
            var bol = true;
            foreach (var c in buf) {
                if (bol && c != '\n') {
                    res = res.AppendAll(prefix);
                }

                res = res.Append(c);
                bol = c == '\n';
            }
            return res;
        }

        /// unindent removes all the indentation from the tombstoned lines
        private slice<byte> Unindent(slice<byte> buf)
        {
            var res = slice<byte>.Empty;
            for (var i = 0; i < buf.Length; i++) {
                var skip = buf.Length - i <= unindent.Length;
                if (!skip) {
                    skip = !unindent.Equals(buf.Slice(i, i + unindent.Length));
                }
                if (skip) {
                    res = res.Append(buf[i]);
                    continue;
                }

                // We have a marker. we have to backtrace here and clean out
                // any whitespace ahead of our tombstone up to a \n
                for (var j = res.Length - 1; j >= 0; j--) {
                    if (res[j] == '\n') {
                        break;
                    }

                    res = res.Slice(upper: j);
                }

                // Skip the entire unindent marker
                i += unindent.Length - 1;
            }

            return res;
        }

        /// heredocIndent marks all the 2nd and further lines as unindentable
        private slice<byte> HeredocIndent(slice<byte> buf)
        {
            var res = slice<byte>.Empty;
            var bol = false;
            foreach (var c in buf) {
                if (bol && c != '\n') {
                    res = res.AppendAll(unindent);
                }
                res = res.Append(c);
                bol = c == '\n';
            }
            return res;
        }

        /// isSingleLineObject tells whether the given object item is a single
        /// line object such as "obj {}".
        ///
        /// A single line object:
        ///
        ///   * has no lead comments (hence multi-line)
        ///   * has no assignment
        ///   * has no values in the stanza (within {})
        ///
        private bool IsSingleLineObject(ObjectItem val)
        {
            // If there is a lead comment, can't be one line
            if (val.LeadComment != null) {
                return false;
            }

            // If there is assignment, we always break by line
            if (val.Assign.IsValid) {
                return false;
            }

            // If it isn't an object type, then its not a single line object
            var ot = val.Val as ObjectType;
            if (ot != null) {
                return false;
            }

            // If the object has no items, it is single line!
            return ot.List.Items.Length == 0;
        }

        private static int Lines(string txt)
        {
            var endline = 1;
            for (var i = 0; i < txt.Length; i++) {
                if (txt[i] == '\n') {
                    endline++;
                }
            }
            return endline;
        }
 
        // // ----------------------------------------------------------------------------
        // // Tracing support

        // func (p *printer) printTrace(a ...interface{}) {
        //     if !p.enableTrace {
        //         return
        //     }

        //     const dots = ". . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . "
        //     const n = len(dots)
        //     i := 2 * p.indentTrace
        //     for i > n {
        //         fmt.Print(dots)
        //         i -= n
        //     }
        //     // i <= n
        //     fmt.Print(dots[0:i])
        //     fmt.Println(a...)
        // }

        // func trace(p *printer, msg string) *printer {
        //     p.printTrace(msg, "(")
        //     p.indentTrace++
        //     return p
        // }

        // // Usage pattern: defer un(trace(p, "..."))
        // func un(p *printer) {
        //     p.indentTrace--
        //     p.printTrace(")")
        // }
    }

    public struct ByPosition
    {
        private slice<CommentGroup> _base;

        public int Len() => _base.Length;
        public void Swap(int i, int j) => (_base[i], _base[j]) = (_base[j], _base[i]);
        public bool Less(int i, int j) => _base[i].Pos().Before(_base[j].Pos());
    }
}