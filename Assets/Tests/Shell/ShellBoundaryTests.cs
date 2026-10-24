using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.Shell.Tests
{
    public class ShellBoundaryTests
    {
        [Test]
        public void AsmdefFollowsDependencyRules()
        {
            AsmdefRules.AssertFollowsRules("RWDE.Shell");
        }
    }
}
