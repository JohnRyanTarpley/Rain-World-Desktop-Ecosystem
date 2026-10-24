using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.App.Tests
{
    public class AppBoundaryTests
    {
        [Test]
        public void ProjectFollowsDependencyRules()
        {
            ProjectRules.AssertFollowsRules("RWDE.App");
        }
    }
}
