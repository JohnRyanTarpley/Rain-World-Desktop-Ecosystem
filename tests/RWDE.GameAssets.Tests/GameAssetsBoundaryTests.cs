using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.GameAssets.Tests
{
    public class GameAssetsBoundaryTests
    {
        [Test]
        public void ProjectFollowsDependencyRules()
        {
            ProjectRules.AssertFollowsRules("RWDE.GameAssets");
        }
    }
}
