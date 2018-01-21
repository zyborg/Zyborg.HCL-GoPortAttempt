using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Zyborg.HCL.parser
{
    [TestClass]
    public class ErrorTests
    {
        [TestMethod]
        public void TestPosError_impl()
        {
            var err = new PosErrorException(new token.Pos(), string.Empty);
        }
    }
}