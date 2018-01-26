using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NStack;

namespace gozer
{
    [TestClass]
    public class OtherGoSliceTests
    {
        public TestContext TestContext
        { get; set; }


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


        static FormattableString sampleIn = $"sam\x00ple {0xff::} str{0xa0fe::2} {0xfafbfcfd::3} {0xfafbfcfd::4} {(byte)0xfd::anything else}";
        static byte[] sampleOut = new [] {
            (byte)'s',
            (byte)'a',
            (byte)'m',
            (byte)0x00,
            (byte)'p',
            (byte)'l',
            (byte)'e',
            (byte)' ',
            (byte)0xff,
            (byte)' ',
            (byte)'s',
            (byte)'t',
            (byte)'r',
            (byte)0xa0,
            (byte)0xfe,
            (byte)' ',
            (byte)0xfb,
            (byte)0xfc,
            (byte)0xfd,
            (byte)' ',
            (byte)0xfa,
            (byte)0xfb,
            (byte)0xfc,
            (byte)0xfd,
            (byte)' ',
            (byte)0xfd,
            };

        [TestMethod]
        public void TestUstringFromStringAndBytes()
        {
            // var s = Ustring.sampleIn.ToString();
            // var b = Encoding.UTF8.GetBytes(s);

            // var bs = b.Slice();
            // var ex = Ustring.sampleOut.Slice();

            sampleIn.Make();
            var s = slice.Make(sampleIn);
            var b = (ustring)sampleOut;

            TestContext.WriteLine("");
            TestContext.WriteLine($"s = [{s}] : [{s.AsString()}] : [{s.AsUstring()}]");
            TestContext.WriteLine($"b = [{b}]");

            Assert.AreEqual(b.ToByteArray().Slice().ToString(), s.ToArray().Slice().ToString());
        }
    }
}