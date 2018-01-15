using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Zyborg.IO
{
    [TestClass]
    public class sliceTests
    {
        [TestMethod]
        public void TestGoodLowLenCap()
        {
            var array = new byte[100];
            
            var slice = array.slice();
            Assert.AreEqual(0, slice.Lower);
            Assert.AreEqual(100, slice.Length);
            Assert.AreEqual(100, slice.Capacity);

            slice = array.slice(25);
            Assert.AreEqual(25, slice.Lower);
            Assert.AreEqual(75, slice.Length);
            Assert.AreEqual(75, slice.Capacity);

            slice = array.slice(60, 80);
            Assert.AreEqual(60, slice.Lower);
            Assert.AreEqual(20, slice.Length);
            Assert.AreEqual(40, slice.Capacity);

            slice = array.slice(25, 75);
            Assert.AreEqual(25, slice.Lower);
            Assert.AreEqual(50, slice.Length);
            Assert.AreEqual(75, slice.Capacity);

            // slice:
            //    off = 25
            //    len = 50
            //    cap = 75

            var slice2 = slice.slice();
            Assert.AreEqual(25, slice2.Lower);
            Assert.AreEqual(50, slice2.Length);
            Assert.AreEqual(75, slice2.Capacity);

            slice2 = slice.slice(10);
            Assert.AreEqual(35, slice2.Lower);
            Assert.AreEqual(40, slice2.Length);
            Assert.AreEqual(65, slice2.Capacity);

            slice2 = slice.slice(15, 60);
            Assert.AreEqual(40, slice2.Lower);
            Assert.AreEqual(45, slice2.Length);
            Assert.AreEqual(60, slice2.Capacity);
        }

        [TestMethod]
        public void TestBadLowLenCap()
        {
            var array = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            
            var s1 = array.slice();
            Assert.AreEqual(0, s1.Lower);
            Assert.AreEqual(10, s1.Upper);
            Assert.AreEqual(10, s1.Length);
            Assert.AreEqual(10, s1.Capacity);

            
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                    () => array.slice(-1));

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                    () => array.slice(11));

            var s2 = array.slice(3);
            Assert.AreEqual(7, s2.Length);
            Assert.AreEqual(7, s2.Capacity);
            Assert.AreEqual("[3 4 5 6 7 8 9]", s2.ToString());

            var s3 = array.slice(2, 8);
            Assert.AreEqual(6, s3.Length);
            Assert.AreEqual(8, s3.Capacity);
            Assert.AreEqual("[2 3 4 5 6 7]", s3.ToString());

            var s4 = s2.slice(upper: 5);
            Assert.AreEqual(5, s4.Length);
            Assert.AreEqual(7, s4.Capacity);
            Assert.AreEqual("[3 4 5 6 7]", s4.ToString());

            var s5 = s2.slice(2, 5);
            Assert.AreEqual(3, s5.Length);
            Assert.AreEqual(5, s5.Capacity);
            Assert.AreEqual("[5 6 7]", s5.ToString());

            s5 = s5.slice(1, 4);
            Assert.AreEqual(3, s5.Length);
            Assert.AreEqual(4, s5.Capacity);
            Assert.AreEqual("[6 7 8]", s5.ToString());

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                    () => s5.slice(upper: 5));

            s5 = s5.slice(upper: 4);
            Assert.AreEqual(4, s5.Length);
            Assert.AreEqual(4, s5.Capacity);
            Assert.AreEqual("[6 7 8 9]", s5.ToString());

            var s6 = array.slice(upper: 5);
            Assert.AreEqual(5, s6.Length);
            Assert.AreEqual(10, s6.Capacity);
            Assert.AreEqual("[0 1 2 3 4]", s6.ToString());

            s6 = s6.slice(upper: 10);
            Assert.AreEqual(10, s6.Length);
            Assert.AreEqual(10, s6.Capacity);
            Assert.AreEqual("[0 1 2 3 4 5 6 7 8 9]", s6.ToString());

            s6 = s6.slice(upper: 8);
            Assert.AreEqual(8, s6.Length);
            Assert.AreEqual(10, s6.Capacity);
            Assert.AreEqual("[0 1 2 3 4 5 6 7]", s6.ToString());

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                    () => s6.slice(upper: 11));
        }

        [TestMethod]
        public void TestValues()
        {
            var array = new int[] { 0, 1, 2, 3, 4 };

            var s1 = array.slice();
            Assert.AreEqual("[0 1 2 3 4]", s1.ToString());
            var s2 = array.slice(2);
            Assert.AreEqual("[2 3 4]", s2.ToString());
            var s3 = array.slice(upper: 2);
            Assert.AreEqual("[0 1]", s3.ToString());

            var s4 = array.slice(2, 3);
            Assert.AreEqual("[2]", s4.ToString());
            Assert.AreEqual(3, s4.Capacity);
            var s5 = s4.slice(0);
            Assert.AreEqual("[2]", s5.ToString());
            Assert.AreEqual(3, s5.Capacity);
            s5 = s4.slice(0, 3);
            Assert.AreEqual("[2 3 4]", s5.ToString());
            Assert.AreEqual(3, s5.Capacity);
            
            var s6 = s3.slice(2, 5);
            Assert.AreEqual("[2 3 4]", s5.ToString());
            Assert.AreEqual(3, s5.Capacity);
        }

        [TestMethod]
        public void TestValueChanges()
        {
            var array = new int[] { 0, 1, 2, 3, 4 };

            var s1 = array.slice();
            Assert.AreEqual("[0 1 2 3 4]", s1.ToString());
            foreach (var (i, _) in s1.Range())
                s1[i] *= 10;
            Assert.AreEqual("[0 10 20 30 40]", s1.ToString());
            Assert.AreEqual("[0 10 20 30 40]", slice<int>.ToString(array));

            var s2 = array.slice(2);
            Assert.AreEqual("[20 30 40]", s2.ToString());
            foreach (var (i, _) in s2.Range())
                s2[i] += 2;
            Assert.AreEqual("[22 32 42]", s2.ToString());
            Assert.AreEqual("[0 10 22 32 42]", s1.ToString());
            Assert.AreEqual("[0 10 22 32 42]", slice<int>.ToString(array));

            // var s3 = array.slice(upper: 2);
            // Assert.AreEqual("[0 1]", s3.ToString());

            // var s4 = array.slice(2, 3);
            // Assert.AreEqual("[2]", s4.ToString());
            // Assert.AreEqual(3, s4.Capacity);
            // var s5 = s4.slice(0);
            // Assert.AreEqual("[2]", s5.ToString());
            // Assert.AreEqual(3, s5.Capacity);
            // s5 = s4.slice(0, 3);
            // Assert.AreEqual("[2 3 4]", s5.ToString());
            // Assert.AreEqual(3, s5.Capacity);
            
            // var s6 = s3.slice(2, 5);
            // Assert.AreEqual("[2 3 4]", s5.ToString());
            // Assert.AreEqual(3, s5.Capacity);

        }

        [TestMethod]
        public void TestRange()
        {
            var array = new int[] { 10, 20, 30, 40, 50 };
            Assert.AreEqual("[10 20 30 40 50]", slice<int>.ToString(array));

            var buf = "";
            foreach (var f in array.Range())
                buf += $"({f.index},{f.value})";
            Assert.AreEqual("(0,10)(1,20)(2,30)(3,40)(4,50)", buf);

            buf = "";
            foreach (var (_, v) in array.Range(2))
                buf += $"({v})";
            Assert.AreEqual("(30)(40)(50)", buf);

            buf = "";
            foreach (var (i, _) in array.Range(upper: 2))
                buf += $"({i})";
            Assert.AreEqual("(0)(1)", buf);

            buf = "";
            foreach (var (i, v) in array.Range(1, 3))
                buf += $"({i},{v})";
            Assert.AreEqual("(1,20)(2,30)", buf);
        }
    }
}
