using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.Sim.Tests
{
    public class SimBoundaryTests
    {
        [Test]
        public void ProjectFollowsDependencyRules()
        {
            ProjectRules.AssertFollowsRules("RWDE.Sim");
        }
    }
}
