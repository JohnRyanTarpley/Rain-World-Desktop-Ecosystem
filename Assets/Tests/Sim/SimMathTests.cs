using NUnit.Framework;

namespace RWDE.Sim.Tests
{
    // The engine-free math types must behave like UnityEngine.Vector2 and the game's IntVector2/FloatRect.
    public class SimMathTests
    {
        [Test]
        public void Vector2Basics()
        {
            var v = new Vector2(3f, 4f);

            Assert.AreEqual(5f, v.magnitude, 1e-6f);
            Assert.AreEqual(25f, v.sqrMagnitude);
            Assert.AreEqual(0.6f, v.normalized.x, 1e-6f);
            Assert.AreEqual(Vector2.zero, new Vector2(1e-7f, 0f).normalized);
            Assert.AreEqual(new Vector2(4f, 5f), v + Vector2.one);
            Assert.AreEqual(new Vector2(-3f, -4f), -v);
            Assert.AreEqual(new Vector2(6f, 8f), 2f * v);
            Assert.AreEqual(11f, Vector2.Dot(v, new Vector2(1f, 2f)));
            Assert.AreEqual(5f, Vector2.Distance(Vector2.zero, v), 1e-6f);
            Assert.AreEqual(v, Vector2.Lerp(Vector2.zero, v, 2f), "Lerp clamps t");
            Assert.IsTrue(new Vector2(1f, 1f) == new Vector2(1f + 1e-7f, 1f), "== is approximate, as in Unity");
        }

        [Test]
        public void IntVector2Basics()
        {
            var a = new IntVector2(2, -3);

            Assert.AreEqual(new IntVector2(3, -1), a + new IntVector2(1, 2));
            Assert.AreEqual(new IntVector2(4, -6), a * 2);
            Assert.AreEqual(new IntVector2(-1, 2), IntVector2.FromVector2(new Vector2(-1.9f, 2.9f)), "truncates toward zero");
            Assert.AreEqual(new Vector2(2f, -3f), a.ToVector2());
            Assert.AreEqual(5f, new IntVector2(0, 0).FloatDist(new IntVector2(3, 4)), 1e-6f);
        }

        [Test]
        public void FloatRectBasics()
        {
            var r = new FloatRect(0f, 0f, 20f, 10f);

            Assert.AreEqual(new Vector2(10f, 5f), r.Center);
            Assert.AreEqual(20f, r.Width);
            Assert.AreEqual(10f, r.Height);
            var grown = r.Grow(1f);
            Assert.AreEqual(-1f, grown.left);
            Assert.AreEqual(-1f, r.left, "Grow changes the rect itself, as in the game");
        }
    }
}
