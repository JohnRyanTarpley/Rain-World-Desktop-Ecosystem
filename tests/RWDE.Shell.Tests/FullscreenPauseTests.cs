using System;
using NUnit.Framework;
using RWDE.Shell.Lifecycle;
using RWDE.Shell.Native;

namespace RWDE.Shell.Tests
{
    public class FullscreenPauseTests
    {
        [TestCase(Shell32.QUNS_NOT_PRESENT, true)]
        [TestCase(Shell32.QUNS_BUSY, true)]
        [TestCase(Shell32.QUNS_RUNNING_D3D_FULL_SCREEN, true)]
        [TestCase(Shell32.QUNS_PRESENTATION_MODE, true)]
        [TestCase(Shell32.QUNS_ACCEPTS_NOTIFICATIONS, false)]
        [TestCase(Shell32.QUNS_QUIET_TIME, false)]
        [TestCase(Shell32.QUNS_APP, false)]
        public void PausesOnlyWhileTheDesktopIsHidden(int quns, bool paused)
        {
            Assert.AreEqual(paused, FullscreenPause.ShouldPause(quns));
        }

        [Test]
        public void QueriesAtMostOncePerInterval()
        {
            int queries = 0;
            int? state = Shell32.QUNS_ACCEPTS_NOTIFICATIONS;
            var pause = new FullscreenPause(() => { queries++; return state; }, TimeSpan.FromMilliseconds(500));

            Assert.IsFalse(pause.Update(TimeSpan.Zero));
            state = Shell32.QUNS_BUSY;
            Assert.IsFalse(pause.Update(TimeSpan.FromMilliseconds(499)), "cached until the interval passes");
            Assert.AreEqual(1, queries);

            Assert.IsTrue(pause.Update(TimeSpan.FromMilliseconds(500)));
            Assert.IsTrue(pause.Paused);
            Assert.AreEqual(2, queries);

            state = Shell32.QUNS_ACCEPTS_NOTIFICATIONS;
            Assert.IsFalse(pause.Update(TimeSpan.FromMilliseconds(1000)));
        }

        [Test]
        public void AFailedQueryKeepsRunning()
        {
            var pause = new FullscreenPause(() => null);
            Assert.IsFalse(pause.Update(TimeSpan.Zero));
        }

        [Test]
        public void TheLiveQueryDoesNotThrow()
        {
            Assert.DoesNotThrow(() => FullscreenPause.ForCurrentUser().Update(TimeSpan.Zero));
        }
    }
}
