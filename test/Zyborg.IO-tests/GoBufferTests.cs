using System;
using System.Text;
using DeepEqual.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zyborg.IO;

namespace Zyborg.IO_tests
{
    [TestClass]
    public class GoBufferTests
    {
        // Comparable to the testing.T.Short() flag 
        // TODO:  move this out to an invocation parameter?
        public static readonly bool ShortTest = false;

        public const int N = 10000;      // make this bigger for a larger (and slower) test
        public string data;       // test data for write tests
        public slice<byte> testBytes; // test data; same as data but as a slice.


        [TestInitialize]
        public void Init()
        {
            
            testBytes = slice<byte>.Make(N);
            for (var i = 0; i < N; i++)
            {
                testBytes[i] = (byte)('a' + (byte)(i % 26));
            }
            data = Encoding.UTF8.GetString(testBytes.ToArray());
        }

        /// Verify that contents of buf match the string s.
        private void Check(GoBuffer buf, string s)
        {
            var bytes = buf.Bytes();
            var str = buf.ToString();

            Assert.AreEqual(bytes.Length, buf.Len());
            // if buf.Len() != len(bytes) {
            //     t.Errorf("%s: buf.Len() == %d, len(buf.Bytes()) == %d", testname, buf.Len(), len(bytes))
            // }

            Assert.AreEqual(str.Length, buf.Len());
            // if buf.Len() != len(str) {
            //     t.Errorf("%s: buf.Len() == %d, len(buf.String()) == %d", testname, buf.Len(), len(str))
            // }

            Assert.AreEqual(s.Length, buf.Len());
            // if buf.Len() != len(s) {
            //     t.Errorf("%s: buf.Len() == %d, len(s) == %d", testname, buf.Len(), len(s))
            // }

            Assert.AreEqual(s, Encoding.UTF8.GetString(bytes.ToArray()));
            // if string(bytes) != s {
            //     t.Errorf("%s: string(buf.Bytes()) == %q, s == %q", testname, string(bytes), s)
            // }
        }        

        /// Fill buf through n writes of string fus.
        /// The initial contents of buf corresponds to the string s;
        /// the result is the final contents of buf returned as a string.
        private string FillString(GoBuffer buf, string s, int n, string fus)
        {
            Check(buf, s);
            for (; n > 0; n--)
            {
                var m = buf.WriteString(fus);
                Assert.AreEqual(fus.Length, m);
                s += fus;
                Check(buf, s);
            }
            return s;
        }

        /// Fill buf through n writes of byte slice fub.
        /// The initial contents of buf corresponds to the string s;
        /// the result is the final contents of buf returned as a string.
        private string FillBytes(GoBuffer buf, string s, int n, slice<byte> fub)
        {
            Check(buf, s);
            for (; n > 0; n--)
            {
                var m = buf.Write(fub);
                Assert.AreEqual(fub.Length, m);
                s += fub.AsString();
                Check(buf, s);
            }
            return s;
        }

        [TestMethod]
        public void TestNewBuffer()
        {
            var buf = GoBuffer.NewBuffer(testBytes);
            Check(buf, data);
        }

        [TestMethod]
        public void TestNewBufferString()
        {
            var buf = GoBuffer.NewBufferString(data);
            Check(buf, data);
        }

        /// Empty buf through repeated reads into fub.
        /// The initial contents of buf corresponds to the string s.
        private void Empty(GoBuffer buf, string s, slice<byte> fub)
        {
            Check(buf, s);

            for (;;)
            {
                var (n, eof) = buf.Read(fub);
                if (n == 0)
                    break;
                Assert.IsFalse(eof);
                s = s.AsByteSlice().slice(n).AsString();
                Check(buf, s);
            }

            Check(buf, "");
        }

        [TestMethod]
        public void TestBasicOperations()
        {
            var buf = new GoBuffer();

            for (var i = 0; i < 5; i++)
            {
                Check(buf, "");

                buf.Reset();
                Check(buf, "");

                buf.Truncate(0);
                Check(buf, "");

                var n = buf.Write(data.AsByteSlice().slice(0, 1));
                Assert.AreEqual(1, n, $"wrote 1 byte, but n == {n}");
                Check(buf, "a");

                buf.WriteByte(data.AsByteSlice()[1]);
                Check(buf, "ab");

                n = buf.Write(data.AsByteSlice().slice(2, 26));
                Assert.AreEqual(24, n, $"wrote 25 bytes, but n == {n}");
                Check(buf, data.AsByteSlice().slice(0, 26).AsString());

                buf.Truncate(26);
                Check(buf, data.AsByteSlice().slice(0, 26).AsString());

                buf.Truncate(20);
                Check(buf, data.AsByteSlice().slice(0, 20).AsString());

                Empty(buf, data.AsByteSlice().slice(0, 20).AsString(), slice<byte>.Make(5));
                Empty(buf, "", slice<byte>.Make(100));

                buf.WriteByte(data.AsByteSlice()[1]);
                var (c, eof) = buf.ReadByteOrEof();
                Assert.IsFalse(eof, "ReadByte unexpected eof");
                Assert.AreEqual(data.AsByteSlice()[1], c, $"ReadByte wrong value c={c}");
                (c, eof) = buf.ReadByteOrEof();
                Assert.IsTrue(eof, "ReadByte unexpected not eof");
            }
        }

        [TestMethod]
        public void TestLargeStringWrites()
        {
            var buf = new GoBuffer();
            var limit = 30;

            if (ShortTest)
                limit = 9;

            for (var i = 3; i < limit; i += 3)
            {
                var s = FillString(buf, "", 5, data);
                Empty(buf, s, slice<byte>.Make(data.Length / i));
            }
            Check(buf, "");
        }

        [TestMethod]
        public void TestLargeByteWrites()
        {
            var buf = new GoBuffer();
            var limit = 30;
            if (ShortTest)
                limit = 9;

            for (var i = 3; i < limit; i += 3)
            {
                var s = FillBytes(buf, "", 5, testBytes);
                Empty(buf, s, slice<byte>.Make(data.Length / i));
            }
            Check(buf, "");
        }

        [TestMethod]
        public void TestLargeStringReads()
        {
            var buf = new GoBuffer();
            
            for (var i = 3; i < 30; i += 3)
            {
                var s = FillString(buf, "", 5, data.AsByteSlice().slice(0, data.Length / i).AsString());
                Empty(buf, s, slice<byte>.Make(data.Length));
            }
            Check(buf, "");
        }

        [TestMethod]
        public void TestLargeByteReads()
        {
            var buf = new GoBuffer();

            for (var i = 3; i < 30; i += 3)
            {
                var s = FillBytes(buf, "", 5, testBytes.slice(0, testBytes.Length / i));
                Empty(buf, s, slice<byte>.Make(data.Length));
            }
            Check(buf, "");
        }

        [TestMethod]
        public void TestMixedReadsAndWrites()
        {
            var buf = new GoBuffer();
            var s = "";
            var rng = new System.Random();

            for (var i = 0; i < 50; i++)
            {
                var wlen =  rng.Next(data.Length);

                if (i % 2 == 0)
                {
                    s = FillString(buf, s, 1, data.AsByteSlice().slice(0, wlen).AsString());
                }
                else
                {
                    s = FillBytes(buf, s, 1, testBytes.slice(0, wlen));
                }

                var rlen = rng.Next(data.Length);
                var fub = slice<byte>.Make(rlen);
                var (n, _) = buf.Read(fub);
                s = s.AsByteSlice().slice(n).AsString();
            }
            Empty(buf, s, slice<byte>.Make(buf.Len()));
        }

        [TestMethod]
        public void TestCapWithPreallocatedSlice()
        {
            var buf = GoBuffer.NewBuffer(slice<byte>.Make(10));
            var n = buf.Cap();
            Assert.AreEqual(10, n, "expected 10, got {0}", n);
        }

        [TestMethod]
        public void TestCapWithSliceAndWrittenData()
        {
            var buf = GoBuffer.NewBuffer(slice<byte>.Make(0, 10));
            buf.Write("test".AsByteSlice());
            var n = buf.Cap();
            Assert.AreEqual(10, n, "expected 10, got {0}", n);
        }

        [TestMethod]
        public void TestNil()
        {
            var b = default(GoBuffer);
            Assert.AreEqual("<nil>", b.String(), "expected <nil>; got {0}", b.String());
        }

        [TestMethod]
        public void TestReadFrom()
        {
            var buf = new GoBuffer();
            
            for (var i = 3; i < 30; i += 3)
            {
                var s = FillBytes(buf, "", 5, testBytes.slice(0, testBytes.Length / i));
                var b = new GoBuffer();
                b.ReadFrom(buf);
                Empty(b, s, slice<byte>.Make(data.Length));
            }
        }

        [TestMethod]
        public void TestWriteTo()
        {
            var buf = new GoBuffer();
            for (var i = 3; i < 30; i += 3)
            {
                var s = FillBytes(buf, "", 5, testBytes.slice(0, testBytes.Length / i));
                var b = new GoBuffer();
                buf.WriteTo(b);
                Empty(b, s, slice<byte>.Make(data.Length));
            }
        }

        [TestMethod]
        public void TestRuneIO()
        {
            var NRune = 1000;
            
            // Built a test slice while we write the data
            var b = slice<byte>.Make(GoBuffer.UTFMax * NRune);
            var buf = new GoBuffer();
            var n = 0;
            for (var r = (char)0; r < NRune; r++)
            {
                var size = b.slice(n).EncodeRune(r);
                var nbytes = buf.WriteRune(r);
                Assert.AreEqual(size, nbytes, "WriteRune({0}) expected {1}, got {2}",
                        r, size, nbytes);
                n += size;
            }
            b = b.slice(0, n);

            // Check the resulting bytes
            Assert.IsTrue(b.Equals(buf.Bytes()),
                    "incorrect result from WriteRune: {0} not {1}", buf.Bytes(), b);

            var p = slice<byte>.Make(GoBuffer.UTFMax);
            // Read it back with ReadRune
            for (var r = (char)0; r < NRune; r++)
            {
                var size = p.EncodeRune(r);
                var (nr, nbytes, err) = buf.ReadRune();

                Assert.AreEqual(r, nr,
                        "ReadRune({0}) got {1},{2} not {3},{4} (eof={5})", r, nr, nbytes, r, size, err);
                Assert.AreEqual(size, nbytes,
                        "ReadRune({0}) got {1},{2} not {3},{4} (eof={5})", r, nr, nbytes, r, size, err);
                Assert.IsFalse(err,
                        "ReadRune({0}) got {1},{2} not {3},{4} (eof={5})", (int)r, nr, nbytes, r, size, err);
            }

            // Check that UnreadRune works
            buf.Reset();

            // check at EOF
            Assert.ThrowsException<Exception>(() => buf.UnreadRune(),
                    "UnreadRune at EOF: got no error");
            var (_, _, eof) = buf.ReadRune();
            Assert.IsTrue(eof, "ReadRune at EOF: got no error");
            Assert.ThrowsException<Exception>(() => buf.UnreadRune(),
                    "UnreadRune after ReadRune at EOF: got no error");

            // // check not at EOF
            // buf.Write(b)
            // for r := rune(0); r < NRune; r++ {
            //     r1, size, _ := buf.ReadRune()
            //     if err := buf.UnreadRune(); err != nil {
            //         t.Fatalf("UnreadRune(%U) got error %q", r, err)
            //     }
            //     r2, nbytes, err := buf.ReadRune()
            //     if r1 != r2 || r1 != r || nbytes != size || err != nil {
            //         t.Fatalf("ReadRune(%U) after UnreadRune got %U,%d not %U,%d (err=%s)", r, r2, nbytes, r, size, err)
            //     }
            // }
        }

    }
}