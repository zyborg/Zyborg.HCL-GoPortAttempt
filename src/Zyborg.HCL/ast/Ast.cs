using System;
using Zyborg.HCL.token;
using Zyborg.IO;

namespace Zyborg.HCL.ast
{

    // Node is an element in the abstract syntax tree.
    public interface INode
    {
        Pos Pos();
        void GetNode();
    }

    /// File represents a single HCL file
    public class File : INode
    {
        public INode Node;            // usually a *ObjectList
        public slice<CommentGroup> Comments; // list of all comments in the source

        public Pos Pos()
        {
            return Node.Pos();
        }


        public void GetNode()
        { }

        public override string ToString()
        {
            return $"{{Node: {Node}, Comments: {Comments}}}";
        }

        public override bool Equals(object obj)
        {
            var that = obj as File;
            return that != null
                    && object.Equals(Node, that.Node)
                    && object.Equals(Comments, that.Comments)
                    ;
        }
    }

    /// ObjectList represents a list of ObjectItems. An HCL file itself is an
    /// ObjectList.
    public class ObjectList : INode
    {
        public slice<ObjectItem> Items;

        public void Add(ObjectItem item) {
            Items = Items.Append(item);
        }

        /// Filter filters out the objects with the given key list as a prefix.
        ///
        /// The returned list of objects contain ObjectItems where the keys have
        /// this prefix already stripped off. This might result in objects with
        /// zero-length key lists if they have no children.
        ///
        /// If no matches are found, an empty ObjectList (non-nil) is returned.
        public ObjectList Filter(params string[] keys)
        {
            var result = new ObjectList();
            foreach (var item in Items)
            {
                // If there aren't enough keys, then ignore this
                if (item.Keys.Length < keys.Length)
                    continue;

                var match = true;
                foreach (var (i, key0) in item.Keys.slice(upper: keys.Length).Range())
                {
                    var key = key0.Token.Value() as string;
                    if (key != keys[i] && !string.Equals(key, keys[i],
                            StringComparison.InvariantCultureIgnoreCase))
                    {
                        match = false;
                        break;
                    }
                }
                if (!match) {
                    continue;
                }

                // Strip off the prefix from the children
                var newItem = item;
                newItem.Keys = newItem.Keys.slice(keys.Length);
                result.Add(newItem);
            }

            return result;
        }

        /// Children returns further nested objects (key length > 0) within this
        /// ObjectList. This should be used with Filter to get at child items.
        public ObjectList Children()
        {
            ObjectList result = new ObjectList();
            foreach (var item in Items)
            {
                if (item.Keys.Length > 0)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        /// Elem returns items in the list that are direct element assignments
        /// (key length == 0). This should be used with Filter to get at elements.
        public ObjectList Elem()
        {
            ObjectList result = new ObjectList();
            foreach (var item in Items)
            {
                if (item.Keys.Length == 0)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        public Pos Pos()
        {
            // always returns the uninitiliazed position
            return Items[0].Pos();
        }

        public void GetNode()
        { }

        public override string ToString()
        {
            return $"{{Items: {Items}}}";
        }


        public override bool Equals(object obj)
        {
            var that = obj as ObjectList;
            return that != null
                    && object.Equals(Items, that.Items)
                    ;
        }
    }


    /// ObjectItem represents a HCL Object Item. An item is represented with a key
    /// (or keys). It can be an assignment or an object (both normal and nested)
    public class ObjectItem : INode
    {
        /// keys is only one length long if it's of type assignment. If it's a
        /// nested object it can be larger than one. In that case "assign" is
        /// invalid as there is no assignments for a nested object.
        public slice<ObjectKey> Keys;

        //; assign contains the position of "=", if any
        public Pos Assign;

        /// val is the item itself. It can be an object,list, number, bool or a
        /// string. If key length is larger than one, val can be only of type
        /// Object.
        public INode Val;

        public CommentGroup LeadComment; // associated lead comment
        public CommentGroup LineComment; // associated line comment

        public Pos Pos()
        {
            // I'm not entirely sure what causes this, but removing this causes
            // a test failure. We should investigate at some point.
            if (Keys.Length == 0)
            {
                return new Pos();
            }

            return Keys[0].Pos();
        }

        public void GetNode()
        { }

        public override string ToString()
        {
            return $"{{Keys: {Keys}, Assign: {Assign}, Val: {Val}, LeadComment: {LeadComment}, LineComment: {LineComment}}}";
        }

        public override bool Equals(object obj)
        {
            var that = obj as ObjectItem;
            return that != null
                    && object.Equals(Keys, that.Keys)
                    && object.Equals(Assign, that.Assign)
                    && object.Equals(Val, that.Val)
                    && object.Equals(LeadComment, that.LeadComment)
                    && object.Equals(LineComment, that.LineComment)
                    ;
        }
    }


    /// ObjectKeys are either an identifier or of type string.
    public class ObjectKey : INode
    {
        public Token Token;

        public Pos Pos()
        {
            return Token.Pos;
        }

        public void GetNode()
        { }

        public override string ToString()
        {
            return $"{{Token: {Token}}}";
        }

        public override bool Equals(object obj)
        {
            var that = obj as ObjectKey;
            return that != null
                    && object.Equals(Token, that.Token)
                    ;
        }
    }

    /// LiteralType represents a literal of basic type. Valid types are:
    /// token.NUMBER, token.FLOAT, token.BOOL and token.STRING
    public class LiteralType : INode
    {
        public Token Token;

        // comment types, only used when in a list
        public CommentGroup LeadComment;
        public CommentGroup LineComment;

        public Pos Pos()
        {
            return Token.Pos;
        }

        public void GetNode()
        { }

        public override string ToString()
        {
            return $"{{Token: {Token}, LeadComment: {LeadComment}, LineComment: {LineComment}}}";
        }

        public override bool Equals(object obj)
        {
            var that = obj as LiteralType;
            return that != null
                    && object.Equals(Token, that.Token)
                    && object.Equals(LeadComment, that.LeadComment)
                    && object.Equals(LineComment, that.LineComment)
                    ;
        }
    }

    /// ListStatement represents a HCL List type
    public class ListType : INode
    {
        public Pos Lbrack; // position of "["
        public Pos Rbrack; // position of "]"
        public slice<INode> List;    // the elements in lexical order

        public Pos Pos()
        {
            return Lbrack;
        }

        public void Add(INode node)
        {
            List = List.Append(node);
        }

        public void GetNode()
        { }

        public override string ToString()
        {
            return $"{{Lbrack: {Lbrack}, Rbrack: {Rbrack}, List: {List}}}";
        }

        public override bool Equals(object obj)
        {
            var that = obj as ListType;
            return that != null
                    && object.Equals(Lbrack, that.Lbrack)
                    && object.Equals(Rbrack, that.Rbrack)
                    && object.Equals(List, that.List)
                    ;
        }
    }

    /// ObjectType represents a HCL Object Type
    public class ObjectType : INode
    {
        public Pos Lbrace;   // position of "{"
        public Pos Rbrace;   // position of "}"
        public ObjectList List; // the nodes in lexical order

        public Pos Pos()
        {
            return Lbrace;
        }

        public void GetNode()
        { }

        public override string ToString()
        {
            return $"{{Lbrace: {Lbrace}, Rbrace: {Rbrace}, List: {List}}}";
        }

        public override bool Equals(object obj)
        {
            var that = obj as ObjectType;
            return that != null
                    && object.Equals(Lbrace, that.Lbrace)
                    && object.Equals(Rbrace, that.Rbrace)
                    && object.Equals(List, that.List)
                    ;
        }
    }

    // Comment node represents a single //, # style or /*- style commment
    public class Comment : INode
    {
        public Pos Start; // position of / or #
        public string Text;

        public Pos Pos()
        {
            return Start;
        }

        public void GetNode()
        { }

        public override string ToString()
        {
            return $"{{Start: {Start}, Text: {Text}}}";
        }

        public override bool Equals(object obj)
        {
            var that = obj as Comment;
            return that != null
                    && object.Equals(Start, that.Start)
                    && object.Equals(Text, that.Text)
                    ;
        }
    }

    // CommentGroup node represents a sequence of comments with no other tokens and
    // no empty lines between.
    public class CommentGroup : INode
    {
        public slice<Comment> List; // len(List) > 0

        public Pos Pos()
        {
            return List[0].Pos();
        }

        public void GetNode()
        { }

        public override string ToString()
        {
            return $"{{List: {List}}}";
        }

        public override bool Equals(object obj)
        {
            var that = obj as CommentGroup;
            return that != null
                    && object.Equals(List, that.List)
                    ;
        }
    }
}