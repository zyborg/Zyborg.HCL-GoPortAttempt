using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace gozer
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            int eval = 0;
            for (int i = 0; i < 10; i++)
            {
                Switch.Begin(i)
                    .Case(0) // No Then, do nothing
                    .Case(1) // Single matching value
                    .Then(() => {
                        Assert.AreEqual(1, i);
                        eval = 1;
                    })
                    .Case(2, 3, 5) // Multiple matching values
                    .Then(() => {
                        Assert.IsTrue(new[] { 2, 3, 5}.First(x => x == i) == i);
                        eval = i;
                    })
                    .Default(() => {
                        Assert.IsTrue(i == 4 || i > 5);
                        eval = i;
                    });

                Assert.AreEqual(i, eval);
            }
        }
    }
}
