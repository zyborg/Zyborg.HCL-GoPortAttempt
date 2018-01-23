using System;
using Zyborg.IO;

namespace gozer
{
    public class Switch
    {
        public static Switch<T> Begin<T>(T value)
        {
            return new Switch<T>(value);
        }
    }


    public class Switch<T>
    {
        private bool _evaluated = false;

        private T _value;
        private T[] _lastCaseValues;

        private slice<(T[] caseValues, Action thenAction)> _cases;
        private Action _default;

        internal Switch(T value)
        {
            _value = value;
        }

        public Switch<T> Case(params T[] values)
        {
            if (_lastCaseValues != null)
            {
                _cases = _cases.Append((_lastCaseValues, null));
            }
            _lastCaseValues = values;
            return this;
        }

        public Switch<T> Then(Action a)
        {
            _cases = _cases.Append((_lastCaseValues, a));
            _lastCaseValues = null;
            return this;
        }

        public void Default(Action a)
        {
            if (_default != null)
                throw new InvalidOperationException("multiple default actions");
            _default = a;
            Eval();
        }

        public void Eval()
        {
            if (_evaluated)
                return;
            
            try
            {
                foreach (var c in _cases)
                {
                    foreach (var val in c.caseValues)
                    {
                        if (object.Equals(_value, val))
                        {
                            if (c.thenAction != null)
                                c.thenAction();
                            return;
                        }
                    }
                }

                _default?.Invoke();
            }
            finally
            {
                _evaluated = true;
            }
        }
    }}