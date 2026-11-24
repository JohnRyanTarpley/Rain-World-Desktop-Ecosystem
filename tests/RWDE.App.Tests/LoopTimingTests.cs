using System;
using NUnit.Framework;

namespace RWDE.App.Tests
{
    public class FixedTickClockTests
    {
        static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

        [Test]
        public void TickIs25Milliseconds()
        {
            Assert.AreEqual(Ms(25), FixedTickClock.TickLength);
        }

        [Test]
        public void FirstAdvanceOnlyStartsTheClock()
        {
            var clock = new FixedTickClock();
            Assert.AreEqual(0, clock.Advance(Ms(1000)));
            Assert.AreEqual(0f, clock.Alpha);
        }

        [Test]
        public void RunsFortyTicksPerSecondAtAnyFrameRate()
        {
            foreach (double frame in new[] { 1000.0 / 60, 1000.0 / 144, 7, 25, 40 })
            {
                var clock = new FixedTickClock();
                clock.Advance(TimeSpan.Zero);
                int ticks = 0;
                for (int i = 1; i * frame <= 10_000 + 1e-6; i++) ticks += clock.Advance(Ms(i * frame));
                Assert.That(ticks, Is.InRange(399, 400), $"frame {frame} ms");
            }
        }

        [Test]
        public void AlphaIsTheFractionOfTheNextTick()
        {
            var clock = new FixedTickClock();
            clock.Advance(TimeSpan.Zero);

            Assert.AreEqual(0, clock.Advance(Ms(10)));
            Assert.AreEqual(0.4f, clock.Alpha, 1e-4f);
            Assert.AreEqual(Ms(15), clock.UntilNextTick);

            Assert.AreEqual(1, clock.Advance(Ms(30)));
            Assert.AreEqual(0.2f, clock.Alpha, 1e-4f);
            Assert.AreEqual(Ms(20), clock.UntilNextTick);
        }

        [Test]
        public void ALongStallRunsAtMostMaxTicksAndDropsTheRest()
        {
            var clock = new FixedTickClock();
            clock.Advance(TimeSpan.Zero);

            Assert.AreEqual(FixedTickClock.MaxTicksPerFrame, clock.Advance(Ms(5000)));
            Assert.AreEqual(0f, clock.Alpha);
            Assert.AreEqual(1, clock.Advance(Ms(5025)));
        }

        [Test]
        public void StoppedTimeIsNotCaughtUp()
        {
            var clock = new FixedTickClock();
            clock.Advance(TimeSpan.Zero);
            clock.Advance(Ms(10));

            clock.Stop();
            Assert.AreEqual(0, clock.Advance(Ms(60_000)), "resuming runs no ticks for the paused minute");
            Assert.AreEqual(0.4f, clock.Alpha, 1e-4f, "the partial tick from before the pause is kept");
            Assert.AreEqual(1, clock.Advance(Ms(60_015)));
        }

        [Test]
        public void TimeGoingBackwardsRunsNothing()
        {
            var clock = new FixedTickClock();
            clock.Advance(Ms(100));
            Assert.AreEqual(0, clock.Advance(Ms(50)));
            Assert.AreEqual(1, clock.Advance(Ms(75)));
        }
    }

    public class RenderGateTests
    {
        [Test]
        public void DrawsTheFirstFrameThenNothingWhileStill()
        {
            var gate = new RenderGate();
            Assert.IsTrue(gate.ShouldRender(animating: false));
            gate.Rendered();
            Assert.IsFalse(gate.ShouldRender(animating: false));
            Assert.IsFalse(gate.ShouldRender(animating: false));
        }

        [Test]
        public void DrawsEveryFrameWhileAnimating()
        {
            var gate = new RenderGate();
            gate.Rendered();
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(gate.ShouldRender(animating: true));
                gate.Rendered();
            }
        }

        [Test]
        public void InvalidateOwesOneFrame()
        {
            var gate = new RenderGate();
            gate.Rendered();
            gate.Invalidate();
            Assert.IsTrue(gate.Stale);
            Assert.IsTrue(gate.ShouldRender(animating: false));
            gate.Rendered();
            Assert.IsFalse(gate.ShouldRender(animating: false));
        }
    }
}
