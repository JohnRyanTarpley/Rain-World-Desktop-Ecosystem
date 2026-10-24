using NUnit.Framework;
using RWDE.Tests.Common;

namespace RWDE.Audio.Tests
{
    public class AudioBoundaryTests
    {
        [Test]
        public void AsmdefFollowsDependencyRules()
        {
            AsmdefRules.AssertFollowsRules("RWDE.Audio");
        }
    }
}
