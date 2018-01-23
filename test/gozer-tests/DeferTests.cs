using gozer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace gozer_tests
{
    [TestClass]
    public class DeferTests
    {
        [TestMethod]
        public void TestSimpleDefer()
        {
            var x = 100;

            Assert.AreEqual(100, x);

            using (Defer.Call(() => x--))
            {
                Assert.AreEqual(100, x);
            }

            Assert.AreEqual(99, x);
        }

        [TestMethod]
        public void TestDeferArgEval()
        {
            var i = 0;
            using (Defer.Call(i, (x) => Assert.AreEqual(0, x)))
            {
                i++;
                return;
            }
        }

        [TestMethod]
        public void TestDeferLIFO()
        {
            var s = "";

            using (var d = Defer.Call())
            {
                for (int i = 0; i < 4; i++)
                {
                    d.Add(i, (x) => s += x);
                }
            }

            Assert.AreEqual("3210", s);
        }


        private int DeferredReturn(int n)
        {
            using (Defer.Call(() => n++))
            {
                // We cannot place the return here as its arguments would
                // get evaluated BEFORE the deferred actions are run
            }

            return n;
        }

        [TestMethod]
        public void TestDeferReturnValues()
        {
            Assert.AreEqual(101, DeferredReturn(100));
        }
    }
}