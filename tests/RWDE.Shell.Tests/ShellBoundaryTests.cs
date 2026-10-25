using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.Shell.Tests
{
    public class ShellBoundaryTests
    {
        [Test]
        public void ProjectFollowsDependencyRules()
        {
            ProjectRules.AssertFollowsRules("RWDE.Shell");
        }
    }
}
