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
                s = s.AsByteSlice().Slice(n).AsString();
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

                var n = buf.Write(data.AsByteSlice().Slice(0, 1));
                Assert.AreEqual(1, n, $"wrote 1 byte, but n == {n}");
                Check(buf, "a");

                buf.WriteByte(data.AsByteSlice()[1]);
                Check(buf, "ab");

                n = buf.Write(data.AsByteSlice().Slice(2, 26));
                Assert.AreEqual(24, n, $"wrote 25 bytes, but n == {n}");
                Check(buf, data.AsByteSlice().Slice(0, 26).AsString());

                buf.Truncate(26);
                Check(buf, data.AsByteSlice().Slice(0, 26).AsString());

                buf.Truncate(20);
                Check(buf, data.AsByteSlice().Slice(0, 20).AsString());

                Empty(buf, data.AsByteSlice().Slice(0, 20).AsString(), slice<byte>.Make(5));
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
                var s = FillString(buf, "", 5, data.AsByteSlice().Slice(0, data.Length / i).AsString());
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
                var s = FillBytes(buf, "", 5, testBytes.Slice(0, testBytes.Length / i));
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
                    s = FillString(buf, s, 1, data.AsByteSlice().Slice(0, wlen).AsString());
                }
                else
                {
                    s = FillBytes(buf, s, 1, testBytes.Slice(0, wlen));
                }

                var rlen = rng.Next(data.Length);
                var fub = slice<byte>.Make(rlen);
                var (n, _) = buf.Read(fub);
                s = s.AsByteSlice().Slice(n).AsString();
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
                var s = FillBytes(buf, "", 5, testBytes.Slice(0, testBytes.Length / i));
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
                var s = FillBytes(buf, "", 5, testBytes.Slice(0, testBytes.Length / i));
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

            var zeroChar = (char)0;

            for (var r = zeroChar; r < NRune; r++)
            {
                var size = b.Slice(n).EncodeRune(r);
                var nbytes = buf.WriteRune(r);
                Assert.AreEqual(size, nbytes, "WriteRune({0}) expected {1}, got {2}",
                        r, size, nbytes);
                n += size;
            }
            b = b.Slice(0, n);

            // Check the resulting bytes
            Assert.IsTrue(b.Equals(buf.Bytes()),
                    "incorrect result from WriteRune: {0} not {1}", buf.Bytes(), b);

            var p = slice<byte>.Make(GoBuffer.UTFMax);
            // Read it back with ReadRune
            for (var r = zeroChar; r < NRune; r++)
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

            // check not at EOF
            buf.Write(b);
            for (var r = zeroChar; r < NRune; r++)
            {
                var (r1, size, _) = buf.ReadRune();

                buf.UnreadRune();
                var (r2, nbytes, err) = buf.ReadRune();
                Assert.AreEqual(r1, r2,
                        "ReadRune({0}) after UnreadRune got {1},{2} not {3},{4} (err={5})", r, r2, nbytes, r, size, err);
                Assert.AreEqual(r1, r,
                        "ReadRune({0}) after UnreadRune got {1},{2} not {3},{4} (err={5})", r, r2, nbytes, r, size, err);
                Assert.AreEqual(size, nbytes,
                        "ReadRune({0}) after UnreadRune got {1},{2} not {3},{4} (err={5})", r, r2, nbytes, r, size, err);
                Assert.IsFalse(err,
                        "ReadRune({0}) after UnreadRune got {1},{2} not {3},{4} (err={5})", r, r2, nbytes, r, size, err);
            }
        }

        [TestMethod]
        public void TestNext()
        {
            var b = slice<byte>.From(0, 1, 2, 3, 4);
            var tmp = slice<byte>.Make(5);
            for (var i = 0; i <= 5; i++)
            {
                for (var j = i; j <= 5; j++)
                {
                    for (var k = 0; k <= 6; k++)
                    {
                        // 0 <= i <= j <= 5; 0 <= k <= 6
                        // Check that if we start with a buffer
                        // of length j at offset i and ask for
                        // Next(k), we get the right bytes.
                        var buf = GoBuffer.NewBuffer(b.Slice(0, j));
                        var (n, _) = buf.Read(tmp.Slice(0, i));
                        Assert.AreEqual(i, n);
                        var bb = buf.Next(k);
                        var want = k;
                        if (want > j - i)
                            want = j - i;
                        Assert.AreEqual(want, bb.Length);
                        foreach (var (l, v) in bb.Range())
                        {
                            Assert.AreEqual((byte)(l + i), v);
                        }
                    }
                }
            }
        }

        private class ReadBytesTest
        {
            public string buffer;
            public byte delim;
            public slice<string> expected;
            public bool eof;
            public ReadBytesTest(string b, byte d, slice<string> x, bool e)
            {
                buffer = b;
                delim = d;
                expected = x;
                eof = e;
            }
        }

        private ReadBytesTest[] _readBytesTests = new[]
        {
            new ReadBytesTest("", (byte)0, slice<string>.From(""), true),
            new ReadBytesTest("a\x00", (byte)0, slice<string>.From("a\x00"), false),
            new ReadBytesTest("abbbaaaba", (byte)'b', slice<string>.From("ab", "b", "b", "aaab"), false),
            new ReadBytesTest("hello\x01world", (byte)1, slice<string>.From("hello\x01"), false),
            new ReadBytesTest("foo\nbar", (byte)0, slice<string>.From("foo\nbar"), true),
            new ReadBytesTest("alpha\nbeta\ngamma\n", (byte)'\n', slice<string>.From("alpha\n", "beta\n", "gamma\n"), false),
            new ReadBytesTest("alpha\nbeta\ngamma", (byte)'\n', slice<string>.From("alpha\n", "beta\n", "gamma"), true),
        };

        [TestMethod]
        public void TestReadBytes()
        {
            foreach (var test in _readBytesTests)
            {
                var buf = GoBuffer.NewBufferString(test.buffer);
                bool eof = false;

                foreach (var expected in test.expected)
                {
                    slice<byte> bytes;
                    (bytes, eof) = buf.ReadBytes(test.delim);
                    Assert.AreEqual(expected, bytes.AsString());
                    if (eof)
                        break;
                }
                Assert.AreEqual(test.eof, eof);
            }
        }

        [TestMethod]
        public void TestReadString()
        {
            foreach (var test in _readBytesTests)
            {
                var buf = GoBuffer.NewBufferString(test.buffer);
                bool eof = false;

                foreach (var expected in test.expected)
                {
                    string s;
                    (s, eof) = buf.ReadString(test.delim);
                    Assert.AreEqual(expected, s);
                    if (eof)
                        break;
                }
                Assert.AreEqual(test.eof, eof);
            }
        }

        public const int testing_B_N = 10000;

        [TestMethod]
        public void BenchmarkReadString()
        {
            var n = 32 << 10;

            var data = slice<byte>.Make(n);
            data[n - 1] = (byte)'x';

          //b.SetBytes(int64(n))

            for (var i = 0; i < testing_B_N; i++)
            {
                var buf = GoBuffer.NewBuffer(data);
                var (_, eof) = buf.ReadString((byte)'x');
                Assert.IsFalse(eof);
            }
        }

        [TestMethod]
        public void TestGrow()
        {
            var x = slice<byte>.From((byte)'x');
            var y = slice<byte>.From((byte)'y');
            var tmp = slice<byte>.Make(72);

            foreach (var startLen in new []{ 0, 100, 1000, 10000, 100000 })
            {
                var xBytes = x.Repeat(startLen);
                foreach (var growLen in new [] { 0, 100, 1000, 10000, 100000 })
                {
                    var buf = GoBuffer.NewBuffer(xBytes);
                    // If we read, this affects buf.off, which is good to test.
                    var (readBytes, _) = buf.Read(tmp);
                    buf.Grow(growLen);
                    var yBytes = y.Repeat(growLen);
                    // Check no allocation occurs in write, as long as we're single-threaded.
                    // var m1, m2 runtime.MemStats
                    // runtime.ReadMemStats(&m1)
                    buf.Write(yBytes);
                    //runtime.ReadMemStats(&m2)
                    // if runtime.GOMAXPROCS(-1) == 1 && m1.Mallocs != m2.Mallocs {
                    //     t.Errorf("allocation occurred during write")
                    // }
                    // Check that buffer has correct data.
                    Assert.AreEqual(xBytes.Slice(readBytes), buf.Bytes().Slice(0, startLen - readBytes),
                            "bad initial data at {0} {1}", startLen, growLen);

                    Assert.AreEqual(yBytes, (object)buf.Bytes().Slice(startLen - readBytes, startLen - readBytes + growLen),
                            "bad written data at {0} {1}", startLen, growLen);
                }
            }
        }

        // Was a bug: used to give EOF reading empty slice at EOF.
        [TestMethod]
        public void TestReadEmptyAtEOF()
        {
            var b = new GoBuffer();
            var slice = slice<byte>.Make(0);
            var (n, eof) = b.Read(slice);
            Assert.IsFalse(eof);
            Assert.AreEqual(0, n, "wrong count; got {0} want 0", n);
        }

        [TestMethod]
        public void TestUnreadByte()
        {
            var b = new GoBuffer();

            // check at EOF
            Assert.ThrowsException<Exception>(() => b.UnreadByte());
            // if err := b.UnreadByte(); err == nil {
            //     t.Fatal("UnreadByte at EOF: got no error")
            // }
            var (_, eof) = b.ReadByteOrEof();
            Assert.IsTrue(eof, "ReadByte at EOF: got no error");
            // if _, err := b.ReadByte(); err == nil {
            //     t.Fatal("ReadByte at EOF: got no error")
            // }
            Assert.ThrowsException<Exception>(() => b.UnreadByte());
            // if err := b.UnreadByte(); err == nil {
            //     t.Fatal("UnreadByte after ReadByte at EOF: got no error")
            // }

            // check not at EOF
            b.WriteString("abcdefghijklmnopqrstuvwxyz");

            // after unsuccessful read
            var (n, err) = b.Read(slice<byte>.Empty);
            Assert.AreEqual(0, n);
            Assert.IsFalse(err);
            // if n, err := b.Read(nil); n != 0 || err != nil {
            //     t.Fatalf("Read(nil) = %d,%v; want 0,nil", n, err)
            // }
            Assert.ThrowsException<Exception>(() => b.UnreadByte());
            // if err := b.UnreadByte(); err == nil {
            //     t.Fatal("UnreadByte after Read(nil): got no error")
            // }

            // after successful read
            (_, err) = b.ReadBytes((byte)'m');
            Assert.IsFalse(err);
            // if _, err := b.ReadBytes('m'); err != nil {
            //     t.Fatalf("ReadBytes: %v", err)
            // }
            b.UnreadByte();
            // if err := b.UnreadByte(); err != nil {
            //     t.Fatalf("UnreadByte: %v", err)
            // }
            var c = b.ReadByte();
            // c, err := b.ReadByte();
            // if err != nil {
            //     t.Fatalf("ReadByte: %v", err)
            // }
            Assert.AreEqual('m', c, "ReadByte = {0}; want {1}", c, 'm');
            // }
        }

        // Tests that we occasionally compact. Issue 5154.
        [TestMethod]
        public void TestBufferGrowth()
        {
            var b = new GoBuffer();
            var buf = slice<byte>.Make(1024);
            b.Write(buf.Slice(0, 1));
            int cap0 = 0;
            for (var i = 0; i < 5 << 10; i++)
            {
                b.Write(buf);
                b.Read(buf);
                if (i == 0)
                    cap0 = b.Cap();
            }
            var cap1 = b.Cap();
            // (*Buffer).grow allows for 2x capacity slop before sliding,
            // so set our error threshold at 3x.
            Assert.IsFalse(cap0 > cap0 * 3,
                    "buffer cap = {0}; too big (grew from {1})", cap1, cap0);
            // if (cap1 > cap0 * 3) {
            //     t.Errorf("buffer cap = %d; too big (grew from %d)", cap1, cap0)
            // }
        }

        // // Test that tryGrowByReslice is inlined.
        // // Only execute on "linux-amd64" builder in order to avoid breakage.
        // func TestTryGrowByResliceInlined(t *testing.T) {
        //     targetBuilder := "linux-amd64"
        //     if testenv.Builder() != targetBuilder {
        //         t.Skipf("%q gets executed on %q builder only", t.Name(), targetBuilder)
        //     }
        //     t.Parallel()
        //     goBin := testenv.GoToolPath(t)
        //     out, err := exec.Command(goBin, "tool", "nm", goBin).CombinedOutput()
        //     if err != nil {
        //         t.Fatalf("go tool nm: %v: %s", err, out)
        //     }
        //     // Verify this doesn't exist:
        //     sym := "bytes.(*Buffer).tryGrowByReslice"
        //     if Contains(out, []byte(sym)) {
        //         t.Errorf("found symbol %q in cmd/go, but should be inlined", sym)
        //     }
        // }

        // func BenchmarkWriteByte(b *testing.B) {
        //     const n = 4 << 10
        //     b.SetBytes(n)
        //     buf := NewBuffer(make([]byte, n))
        //     for i := 0; i < b.N; i++ {
        //         buf.Reset()
        //         for i := 0; i < n; i++ {
        //             buf.WriteByte('x')
        //         }
        //     }
        // }

        // func BenchmarkWriteRune(b *testing.B) {
        //     const n = 4 << 10
        //     const r = '☺'
        //     b.SetBytes(int64(n * utf8.RuneLen(r)))
        //     buf := NewBuffer(make([]byte, n*utf8.UTFMax))
        //     for i := 0; i < b.N; i++ {
        //         buf.Reset()
        //         for i := 0; i < n; i++ {
        //             buf.WriteRune(r)
        //         }
        //     }
        // }

        // // From Issue 5154.
        // func BenchmarkBufferNotEmptyWriteRead(b *testing.B) {
        //     buf := make([]byte, 1024)
        //     for i := 0; i < b.N; i++ {
        //         var b Buffer
        //         b.Write(buf[0:1])
        //         for i := 0; i < 5<<10; i++ {
        //             b.Write(buf)
        //             b.Read(buf)
        //         }
        //     }
        // }

        // // Check that we don't compact too often. From Issue 5154.
        // func BenchmarkBufferFullSmallReads(b *testing.B) {
        //     buf := make([]byte, 1024)
        //     for i := 0; i < b.N; i++ {
        //         var b Buffer
        //         b.Write(buf)
        //         for b.Len()+20 < b.Cap() {
        //             b.Write(buf[:10])
        //         }
        //         for i := 0; i < 5<<10; i++ {
        //             b.Read(buf[:1])
        //             b.Write(buf[:1])
        //         }
        //     }
        // }
    }
}