using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.Interaction.Tests
{
    public class InteractionBoundaryTests
    {
        [Test]
        public void ProjectFollowsDependencyRules()
        {
            ProjectRules.AssertFollowsRules("RWDE.Interaction");
        }
    }
}
