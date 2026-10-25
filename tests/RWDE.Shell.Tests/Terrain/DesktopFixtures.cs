using System.Collections.Generic;
using System.Text.Json;
using RWDE.Shell.Desktop;
using RWDE.Shell.Terrain;
using RWDE.Tests.Common;

namespace RWDE.Shell.Tests.Terrain
{
    /// <summary>
    /// Loads the invented desktop layouts under <c>tests/Fixtures/desktop/</c>. Each file holds
    /// <c>screen</c>, <c>taskbar</c> (or null) and <c>icons</c>, every rectangle as
    /// <c>[left, top, right, bottom]</c> in virtual-screen pixels.
    /// </summary>
    internal static class DesktopFixtures
    {
        public static DesktopLayout Load(string name)
        {
            using var doc = JsonDocument.Parse(Fixtures.ReadText("desktop/" + name + ".json"));
            var root = doc.RootElement;

            ScreenRect? taskbar = null;
            if (root.TryGetProperty("taskbar", out var bar) && bar.ValueKind != JsonValueKind.Null)
                taskbar = Rect(bar);

            var icons = new List<ScreenRect>();
            foreach (var icon in root.GetProperty("icons").EnumerateArray())
                icons.Add(Rect(icon));

            return new DesktopLayout(Rect(root.GetProperty("screen")), taskbar, icons);
        }

        private static ScreenRect Rect(JsonElement e) =>
            new ScreenRect(e[0].GetInt32(), e[1].GetInt32(), e[2].GetInt32(), e[3].GetInt32());
    }

    /// <summary>Hands out fixed layouts in place of the live desktop, counting reads.</summary>
    internal sealed class FakeDesktopLayoutSource : IDesktopLayoutSource
    {
        private readonly DesktopLayout layout;

        public FakeDesktopLayoutSource(DesktopLayout layout)
        {
            this.layout = layout;
        }

        public int Reads { get; private set; }

        public DesktopLayout Read()
        {
            Reads++;
            return layout;
        }
    }
}
