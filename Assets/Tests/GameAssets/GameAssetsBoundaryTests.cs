using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.GameAssets.Tests
{
    public class GameAssetsBoundaryTests
    {
        [Test]
        public void AsmdefFollowsDependencyRules()
        {
            AsmdefRules.AssertFollowsRules("RWDE.GameAssets");
        }
    }
}
