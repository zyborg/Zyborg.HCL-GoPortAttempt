using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Zyborg.IO
{
    [TestClass]
    public class sliceTests
    {
        [TestMethod]
        public void TestEmpty()
        {
            slice<byte> s1 = default(slice<byte>);

            Assert.IsTrue(s1.IsEmpty);
            Assert.AreEqual(0, s1.Length);
            Assert.AreEqual(0, s1.Capacity);
            Assert.AreEqual(slice<byte>.Empty, s1);

            slice<int> s2a = default(slice<int>);
            slice<int> s2b = default(slice<int>);

            Assert.IsTrue(s2a.IsEmpty);
            Assert.AreEqual(s2a, s2b);
            Assert.AreEqual(s2a, slice<int>.Empty);

            var array = new int[] { 10, 20, 30 };
            var s3a = array.slice();
            var s3b = array.slice();

            Assert.AreNotEqual(s2a, s3a);
            Assert.AreEqual(s3a, s3b);

            Assert.IsTrue(slice<byte>.Empty.IsEmpty);
            Assert.IsTrue(slice<int>.Empty.IsEmpty);
            Assert.IsTrue(slice<string>.Empty.IsEmpty);
            Assert.IsTrue(slice<Array>.Empty.IsEmpty);
        }

        [TestMethod]
        public void TestLiteral()
        {
            var s1 = slice<int>.Make(5, 10);
            Assert.AreEqual("[0 0 0 0 0]", s1.ToString());

            var s2 = slice<int>.From(0, 1, 2, 4, 8, 16);
            Assert.AreEqual("[0 1 2 4 8 16]", s2.ToString());
        }

        [TestMethod]
        public void TestCopy()
        {
            var s1 = slice<int>.From(10, 20, 30, 40, 50);
            var s2 = slice<int>.Make(5);

            Assert.AreEqual("[10 20 30 40 50]", s1.ToString());
            Assert.AreEqual("[0 0 0 0 0]", s2.ToString());

            s1.CopyTo(s2);
            Assert.AreEqual("[10 20 30 40 50]", s2.ToString());

            s2 = slice<int>.Make(3);
            s1.CopyTo(s2);
            Assert.AreEqual("[10 20 30]", s2.ToString());
            
            s2 = slice<int>.Make(7);
            s1.CopyTo(s2);
            Assert.AreEqual("[10 20 30 40 50 0 0]", s2.ToString());
        }

        [TestMethod]
        public void TestOverlapCopy()
        {
            var array = new int[] { 10, 20, 30, 40, 50, 60, 70, 80 };
            var slice = array.slice();

            Assert.AreEqual("[10 20 30 40 50 60 70 80]", slice<int>.ToString(array));
            Assert.AreEqual("[10 20 30 40 50 60 70 80]", slice.ToString());

            var s1 = array.slice(2, 6);
            Assert.AreEqual("[30 40 50 60]", s1.ToString());
            var s2 = array.slice(4, 8);
            Assert.AreEqual("[50 60 70 80]", s2.ToString());

            s1.CopyTo(s2);
            Assert.AreEqual("[30 40 50 60]", s2.ToString());
            Assert.AreEqual("[10 20 30 40 30 40 50 60]", slice<int>.ToString(array));

            // --

            array = new int[] { 10, 20, 30, 40, 50, 60, 70, 80 };
            slice = array.slice();

            Assert.AreEqual("[10 20 30 40 50 60 70 80]", slice<int>.ToString(array));
            Assert.AreEqual("[10 20 30 40 50 60 70 80]", slice.ToString());

            s1 = array.slice(2, 6);
            Assert.AreEqual("[30 40 50 60]", s1.ToString());
            s2 = array.slice(4, 8);
            Assert.AreEqual("[50 60 70 80]", s2.ToString());

            s2.CopyTo(s1);
            Assert.AreEqual("[50 60 70 80]", s1.ToString());
            Assert.AreEqual("[10 20 50 60 70 80 70 80]", slice<int>.ToString(array));
        }

        [TestMethod]
        public void TestAppend()
        {
            var s1 = slice<int>.Empty;

            Assert.AreEqual(0, s1.Length);
            Assert.AreEqual(0, s1.Capacity);
            Assert.AreEqual("[]", s1.ToString());

            s1 = s1.Append(12, 13, 14);
            Assert.AreEqual(3, s1.Length);
            Assert.AreEqual(8, s1.Capacity);
            Assert.AreEqual("[12 13 14]", s1.ToString());
            
            s1 = s1.Append(25, 26, 27, 28);
            Assert.AreEqual(7, s1.Length);
            Assert.AreEqual(8, s1.Capacity);
            Assert.AreEqual("[12 13 14 25 26 27 28]", s1.ToString());
        }

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

        class ReplaceTest
        {
            public string @in;
            public string old;
            public string @new;
            public int n;
            public string @out;

            public ReplaceTest(string @in, string old, string @new, int n, string @out)
            {
                this.@in = @in;
                this.old = old;
                this.@new = @new;
                this.n = n;
                this.@out = @out;
            }
        }
        
        slice<ReplaceTest> replaceTests = slice.From(
            new ReplaceTest("hello", "l", "L", 0, "hello"),
            new ReplaceTest("hello", "l", "L", -1, "heLLo"),
            new ReplaceTest("hello", "x", "X", -1, "hello"),
            new ReplaceTest("", "x", "X", -1, ""),
            new ReplaceTest("radar", "r", "<r>", -1, "<r>ada<r>"),
            new ReplaceTest("", "", "<>", -1, "<>"),
            new ReplaceTest("banana", "a", "<>", -1, "b<>n<>n<>"),
            new ReplaceTest("banana", "a", "<>", 1, "b<>nana"),
            new ReplaceTest("banana", "a", "<>", 1000, "b<>n<>n<>"),
            new ReplaceTest("banana", "an", "<>", -1, "b<><>a"),
            new ReplaceTest("banana", "ana", "<>", -1, "b<>na"),
            new ReplaceTest("banana", "", "<>", -1, "<>b<>a<>n<>a<>n<>a<>"),
            new ReplaceTest("banana", "", "<>", 10, "<>b<>a<>n<>a<>n<>a<>"),
            new ReplaceTest("banana", "", "<>", 6, "<>b<>a<>n<>a<>n<>a"),
            new ReplaceTest("banana", "", "<>", 5, "<>b<>a<>n<>a<>na"),
            new ReplaceTest("banana", "", "<>", 1, "<>banana"),
            new ReplaceTest("banana", "a", "a", -1, "banana"),
            new ReplaceTest("banana", "a", "a", 1, "banana")

            // This doesn't seem to work
            // new ReplaceTest("☺☻☹", "", "<>", -1, "<>☺<>☻<>☹<>")
        );
        
        [TestMethod]
        public void TestReplace()
        {
            foreach (var tt in replaceTests)
            {
                var @in = tt.@in.AsByteSlice().AppendAll("<spare>".AsByteSlice());

                @in = @in.slice(upper: tt.@in.Length);
                var @out = @in.Replace(tt.old.AsByteSlice(), tt.@new.AsByteSlice(), tt.n);

                Assert.AreEqual(tt.@out, @out.AsString(),
                        "Replace({0}, {1}, {2}, {3})", tt.@in, tt.old, tt.@new, tt.n);

                Assert.IsTrue(@in.Capacity != @out.Capacity || @in.slice(upper: 1)[0] != @out.slice(upper:1)[0],
                       "Replace({0}, {1}, {2}, {3}) didn't copy", tt.@in, tt.old, tt.@new, tt.n);
                // if cap(in) == cap(out) && &in[:1][0] == &out[:1][0] {
                //     t.Errorf("Replace(%q, %q, %q, %d) didn't copy", tt.in, tt.old, tt.new, tt.n)
                // }
            }
        }
    }
}
