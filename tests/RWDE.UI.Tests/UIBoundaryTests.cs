using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.UI.Tests
{
    public class UIBoundaryTests
    {
        [Test]
        public void ProjectFollowsDependencyRules()
        {
            ProjectRules.AssertFollowsRules("RWDE.UI");
        }
    }
}
