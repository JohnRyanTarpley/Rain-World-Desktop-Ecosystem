using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.Graphics.Tests
{
    public class GraphicsBoundaryTests
    {
        [Test]
        public void AsmdefFollowsDependencyRules()
        {
            AsmdefRules.AssertFollowsRules("RWDE.Graphics");
        }
    }
}
