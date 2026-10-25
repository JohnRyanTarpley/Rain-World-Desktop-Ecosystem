using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.Graphics.Tests
{
    public class GraphicsBoundaryTests
    {
        [Test]
        public void ProjectFollowsDependencyRules()
        {
            ProjectRules.AssertFollowsRules("RWDE.Graphics");
        }
    }
}
