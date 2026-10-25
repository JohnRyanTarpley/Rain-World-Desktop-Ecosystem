using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.Audio.Tests
{
    public class AudioBoundaryTests
    {
        [Test]
        public void ProjectFollowsDependencyRules()
        {
            ProjectRules.AssertFollowsRules("RWDE.Audio");
        }
    }
}
