using System;
using Zyborg.IO;

namespace Zyborg.HCL.ast
{
    public partial class Ast
    {
        /// WalkFunc describes a function to be called for each node during a Walk. The
        /// returned node can be used to rewrite the AST. Walking stops the returned
        /// bool is false.
        public delegate (INode, bool) WalkFunc(INode node);


        // Walk traverses an AST in depth-first order: It starts by calling fn(node);
        // node must not be nil. If fn returns true, Walk invokes fn recursively for
        // each of the non-nil children of node, followed by a call of fn(nil). The
        // returned node of fn can be used to rewrite the passed node to fn.
        public static INode Walk(INode node, WalkFunc fn)
        {
            var (rewritten, ok) = fn(node);
            if (!ok)
            {
                return rewritten;
            }

            switch (node)
            {
                case File n:
                    n.Node = Walk(n.Node, fn);
                    break;
                case ObjectList n:
                    foreach (var (i, item) in n.Items.Range())
                    {
                        n.Items[i] = Walk(item, fn) as ObjectItem;
                    }
                    break;
                case ObjectKey n:
                    // nothing to do
                    break;
                case ObjectItem n:
                    foreach (var (i, k) in n.Keys.Range())
                    {
                        n.Keys[i] = Walk(k, fn) as ObjectKey;
                    }
                    if (n.Val != null)
                    {
                        n.Val = Walk(n.Val, fn);
                    }
                    break;
                case LiteralType n:
                    // nothing to do
                    break;
                case ListType n:
                    foreach (var (i, l) in n.List.Range())
                    {
                        n.List[i] = Walk(l, fn);
                    }
                    break;
                case ObjectType n:
                    n.List = Walk(n.List, fn) as ObjectList;
                    break;
                default:
                    // should we panic here?
                    Console.Error.WriteLine("unknown type: {0}", node.GetType());
                    break;
            }

            fn(null);
            return rewritten;
        }

    }
}