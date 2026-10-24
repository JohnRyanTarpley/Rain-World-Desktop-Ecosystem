using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.UI.Tests
{
    public class UIBoundaryTests
    {
        [Test]
        public void AsmdefFollowsDependencyRules()
        {
            AsmdefRules.AssertFollowsRules("RWDE.UI");
        }
    }
}
