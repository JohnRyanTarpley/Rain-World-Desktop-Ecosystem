using RWDE.GameAssets;
using RWDE.Sim;

namespace RWDE.App
{
    /// <summary>
    /// Hands the sim's spawner the region's world file and properties, merged with the installed
    /// DLC's <c>modify</c> files by the game asset cache. The sim never reads the install itself.
    /// </summary>
    public static class OutskirtsSpawnData
    {
        /// <summary>The Outskirts' region folder under <c>world/</c>.</summary>
        public const string Region = "su";

        /// <summary>The region's spawn data, or null when the install has no world file for it.</summary>
        public static RegionSpawnData Load(GameAssetLibrary assets, string region = Region)
        {
            if (!assets.GetWorldFile(region + "/world_" + region + ".txt").TryGet(out var world)) return null;
            var properties = assets.GetWorldFile(region + "/properties.txt");
            return new RegionSpawnData(region, world.Lines, properties.Found ? properties.Value.Lines : null);
        }
    }
}
