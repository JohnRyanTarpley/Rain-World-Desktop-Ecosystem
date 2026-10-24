using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.App.Tests
{
    public class AppBoundaryTests
    {
        [Test]
        public void AsmdefFollowsDependencyRules()
        {
            AsmdefRules.AssertFollowsRules("RWDE.App");
        }
    }
}
