using NUnit.Framework;

namespace ProtoTest.DSASM
{
    [TestFixture]
    class MemorySafetyTest : ProtoTestBase
    {
        [Test]
        [Category("FailureNET6")]
        public void TestMemoryAllocation()
        {
            string code = @"
x = [];
x[1000000000] = 21;
";
            thisTest.RunAndVerifyRuntimeWarning(code, ProtoCore.Runtime.WarningID.RunOutOfMemory);
        }
    }
}
