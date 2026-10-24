using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.Sim.Tests
{
    public class SimBoundaryTests
    {
        [Test]
        public void AsmdefFollowsDependencyRules()
        {
            AsmdefRules.AssertFollowsRules("RWDE.Sim");
        }
    }
}
