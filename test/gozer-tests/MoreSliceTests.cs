using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace gozer
{
    [TestClass]
    public class OtherGoSliceTests
    {
        [TestMethod]
        public void TestIndexOfC()
        {
            var a1 = "abcdefghijk".AsCharSlice();
            
            Assert.AreEqual(-1, a1.IndexOf('z'));
            Assert.AreEqual( 0, a1.IndexOf('a'));
            Assert.AreEqual( 4, a1.IndexOf('e'));
            Assert.AreEqual(10, a1.IndexOf('k'));

            Assert.AreEqual(-1, a1.IndexOf('a', 1));
            Assert.AreEqual( 1, a1.IndexOf('b', 1));
            Assert.AreEqual(-1, a1.IndexOf('b', a1.Length));
        }

        [TestMethod]
        public void TestIndexOfSep()
        {
            var s1 = "abcdefghijk";
            var s2 = "ABCDEFGHIJK".ToLower();
            var a1 = s1.AsCharSlice();
            
            Assert.AreEqual(-1, a1.IndexOf("x".AsCharSlice()));
            Assert.AreEqual(-1, a1.IndexOf("xy".AsCharSlice()));
            Assert.AreEqual(-1, a1.IndexOf("xyz".AsCharSlice()));

            Assert.AreEqual( 0, a1.IndexOf("a".AsCharSlice()));
            Assert.AreEqual(-1, a1.IndexOf("ax".AsCharSlice()));
            Assert.AreEqual( 0, a1.IndexOf("ab".AsCharSlice()));
            Assert.AreEqual(-1, a1.IndexOf("abx".AsCharSlice()));
            Assert.AreEqual( 0, a1.IndexOf("abc".AsCharSlice()));

            Assert.AreEqual( 2, a1.IndexOf("c".AsCharSlice()));
            Assert.AreEqual(-1, a1.IndexOf("cx".AsCharSlice()));
            Assert.AreEqual( 2, a1.IndexOf("cd".AsCharSlice()));
            Assert.AreEqual(-1, a1.IndexOf("cdx".AsCharSlice()));
            Assert.AreEqual( 2, a1.IndexOf("cde".AsCharSlice()));


            Assert.AreEqual(10, a1.IndexOf("k".AsCharSlice()));
            Assert.AreEqual(10, a1.IndexOf("k".AsCharSlice(), 9));
            Assert.AreEqual(10, a1.IndexOf("k".AsCharSlice(), 10));
            Assert.AreEqual(-1, a1.IndexOf("k".AsCharSlice(), 11));

            Assert.AreNotSame(s1, s2);
            Assert.AreEqual(s1, s2);
            Assert.AreEqual( 0, a1.IndexOf(s2.AsCharSlice()));
            Assert.AreEqual(-1, a1.IndexOf((s2 + "z").AsCharSlice()));
        }
    }
}