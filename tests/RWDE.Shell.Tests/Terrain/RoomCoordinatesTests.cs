using NUnit.Framework;
using RWDE.Shell.Desktop;
using RWDE.Shell.Terrain;
using RWDE.Sim;

namespace RWDE.Shell.Tests.Terrain
{
    public class RoomCoordinatesTests
    {
        [Test]
        public void SizeIsTheScreenInTwentyPixelTilesRoundedUp()
        {
            var uhd = new RoomCoordinates(new ScreenRect(0, 0, 3840, 2160));
            Assert.That((uhd.Width, uhd.Height), Is.EqualTo((192, 108)));

            var odd = new RoomCoordinates(new ScreenRect(0, 0, 410, 305));
            Assert.That((odd.Width, odd.Height), Is.EqualTo((21, 16)));
        }

        [Test]
        public void ScreenToRoomFlipsYAboutTheBottomEdge()
        {
            var coords = new RoomCoordinates(new ScreenRect(-1920, -100, 0, 980));

            Assert.That(coords.ScreenToRoom(-1920, 980), Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(coords.ScreenToRoom(-1920, -100), Is.EqualTo(new Vector2(0f, 1080f)));
            Assert.That(coords.ScreenToRoom(-10, 970), Is.EqualTo(new Vector2(1910f, 10f)));

            coords.RoomToScreen(new Vector2(1910f, 10f), out float sx, out float sy);
            Assert.That((sx, sy), Is.EqualTo((-10f, 970f)));
        }

        [Test]
        public void TopLeftScreenPixelsAreTheTopRowOfTiles()
        {
            var coords = new RoomCoordinates(new ScreenRect(0, 0, 400, 300));

            Assert.That(coords.TryGetTiles(new ScreenRect(0, 0, 40, 40), out var min, out var max), Is.True);
            Assert.That(min, Is.EqualTo(new IntVector2(0, 13)));
            Assert.That(max, Is.EqualTo(new IntVector2(1, 14)));
        }

        [Test]
        public void ATileIsCoveredWhenItsCentreIsInside()
        {
            var coords = new RoomCoordinates(new ScreenRect(0, 0, 400, 300));

            // x [9, 31) holds the centres 10 and 30; x [11, 51) holds 30 and 50.
            coords.TryGetTiles(new ScreenRect(9, 0, 31, 20), out var min, out var max);
            Assert.That((min.x, max.x), Is.EqualTo((0, 1)));
            coords.TryGetTiles(new ScreenRect(11, 0, 51, 20), out min, out max);
            Assert.That((min.x, max.x), Is.EqualTo((1, 2)));
        }

        [Test]
        public void TouchingRectanglesShareNoTileAndLeaveNoGap()
        {
            var coords = new RoomCoordinates(new ScreenRect(0, 0, 400, 300));

            coords.TryGetTiles(new ScreenRect(0, 0, 47, 20), out _, out var upperMax);
            coords.TryGetTiles(new ScreenRect(47, 0, 100, 20), out var lowerMin, out _);
            Assert.That(lowerMin.x, Is.EqualTo(upperMax.x + 1));
        }

        [Test]
        public void ARectangleWithoutACentreStillCoversTheTileUnderIt()
        {
            var coords = new RoomCoordinates(new ScreenRect(0, 0, 400, 300));

            Assert.That(coords.TryGetTiles(new ScreenRect(42, 282, 48, 288), out var min, out var max), Is.True);
            Assert.That(min, Is.EqualTo(new IntVector2(2, 0)));
            Assert.That(max, Is.EqualTo(min));
        }

        [Test]
        public void RectanglesAreClippedToTheRoom()
        {
            var coords = new RoomCoordinates(new ScreenRect(0, 0, 400, 300));

            Assert.That(coords.TryGetTiles(new ScreenRect(-100, -100, 20, 20), out var min, out var max), Is.True);
            Assert.That(min, Is.EqualTo(new IntVector2(0, 14)));
            Assert.That(max, Is.EqualTo(new IntVector2(0, 14)));

            Assert.That(coords.TryGetTiles(new ScreenRect(500, 0, 600, 100), out _, out _), Is.False);
            Assert.That(coords.TryGetTiles(new ScreenRect(10, 10, 10, 50), out _, out _), Is.False);
        }
    }
}
