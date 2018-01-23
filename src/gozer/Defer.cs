using System;
using Zyborg.IO;

namespace gozer
{
    /// The <c>Defer</c> class methods, when used in conjunction with a <c>using (...) { }</c>
    /// construct, allows us to approximate the usage and behavior of the Go <c>defer</c>
    /// language flow control.
    ///
    /// 2 of the 3 rules enforced by Go's defer statement are enforced:
    /// 1. A deferred function's arguments are evaluated when the defer statement is evaluated.
    ///    In other words, at the top of the using statement, any parameters are resolved
    ///    and later when the deferred action is invoked, those values will be passed in.
    /// 2. Deferred functions are added in "Last In First Out" order.  If you stack multiple
    ///    deferred calls using multiple <c>using</c> statements, the nature of the using
    ///    statements ensures this.  Additionally, you may append additional calls to a
    ///    single defer resource (using the <c>Add</c> methods) and they will preserve this
    ///    behavior.
    ///
    /// The third rule cannot be enforced as it requires direct access to the call stack
    /// for reading and writing.  This rule IS NOT SUPPORTED:
    /// 3. Deferred functions may read and assign to the returning function's named return
    ///    values.  This can be simulated if the return statement from the enclosing
    ///    function is placed in an order "after" the deferred actions have executed.
    ///
    /// The deferred actions get executed when the Defer object instance is disposed.
    /// As per Go, it's possible for a panic to occur inside of a deferred action
    /// which would preempt all subsequent deferred actions, so an exception thrown
    /// from one deferred action will preempt all the subsequent (outer) actions
    /// within the same Defer object instance.  HOWEVER,
    /// this behavior only works within a Defer object; "stacked" Defer objects
    /// will actually all evaluate just because of the nature of the 'using'
    /// statements.  The only way to guarantee the same behavior with respect
    /// to this panic/exception aspect is to use a single Defer object.

    public class Defer : IDisposable
    {
        private bool _evaluated = false;
        private slice<Action> _actions;

        private Defer()
        {}

        public Defer Add(Action a)
        {
            _actions = _actions.Append(a);
            return this;
        }

        public Defer Add<T1>(T1 p1, Action<T1> a)
        {
            if (a != null)
                _actions = _actions.Append(() => a(p1));
            return this;
        }

        public Defer Add<T1, T2>(T1 p1, T2 p2, Action<T1, T2> a)
        {
            if (a != null)
                _actions = _actions.Append(() => a(p1, p2));
            return this;
        }

        public Defer Add<T1, T2, T3>(T1 p1, T2 p2, T3 p3, Action<T1, T2, T3> a)
        {
            if (a != null)
                _actions = _actions.Append(() => a(p1, p2, p3));
            return this;
        }

        public Defer Add<T1, T2, T3, T4>(T1 p1, T2 p2, T3 p3, T4 p4, Action<T1, T2, T3, T4> a)
        {
            if (a != null)
                _actions = _actions.Append(() => a(p1, p2, p3, p4));
            return this;
        }

        public Defer Add<T1, T2, T3, T4, T5>(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, Action<T1, T2, T3, T4, T5> a)
        {
            if (a != null)
                _actions = _actions.Append(() => a(p1, p2, p3, p4, p5));
            return this;
        }

        public void Dispose()
        {
            if (_evaluated)
                return;

            try
            {
                // As per Go, it's possible for a panic to occur inside of a deferred action
                // which would preempt all subsequent deferred actions, so we don't try/catch
                // anything here to stop the outer propagation of an exception -- HOWEVER,
                // this behavior only works within a Defer object; "stacked" Defer objects
                // will actually all evaluate just because of the nature of the 'using'
                // statements.  The only way to guarantee the behavior wrt/ the panic/exception
                // aspect is to use a single Defer object
                for (int i = _actions.Length - 1; i >= 0; i--)
                {
                    _actions[i]();
                }
            }
            finally
            {
                _evaluated = true;
            }
        }

        public static Defer Call()
        {
            return new Defer();
        }

        public static Defer Call(Action a)
        {
            return new Defer().Add(a);
        }

        public static Defer Call<T1>(T1 p1, Action<T1> a)
        {
            return new Defer().Add(p1, a);
        }

        public static Defer Call<T1, T2>(T1 p1, T2 p2, Action<T1, T2> a)
        {
            return new Defer().Add(p1, p2, a);
        }

        public static Defer Call<T1, T2, T3>(T1 p1, T2 p2, T3 p3, Action<T1, T2, T3> a)
        {
            return new Defer().Add(p1, p2, p3, a);
        }

        public static Defer Call<T1, T2, T3, T4>(T1 p1, T2 p2, T3 p3, T4 p4, Action<T1, T2, T3, T4> a)
        {
            return new Defer().Add(p1, p2, p3, p4, a);
        }

        public static Defer Call<T1, T2, T3, T4, T5>(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, Action<T1, T2, T3, T4, T5> a)
        {
            return new Defer().Add(p1, p2, p3, p4, p5, a);
        }
    }
}