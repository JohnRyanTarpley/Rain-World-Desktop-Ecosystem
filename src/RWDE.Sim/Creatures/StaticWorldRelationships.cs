using Type = RWDE.Sim.CreatureTemplate.Type;
using Rel = RWDE.Sim.CreatureTemplate.Relationship;
using static RWDE.Sim.CreatureTemplate.Relationship.Type;

namespace RWDE.Sim
{
    // The game's relationship tables, restricted to the built types and kept in the game's order:
    // later lines override earlier ones, and each line also reaches the descendants of both types.
    public sealed partial class StaticWorld
    {
        private void R(Type a, Type b, Rel.Type type, float intensity) =>
            EstablishRelationship(a, b, new Rel(type, intensity));

        // StaticWorld.InitStaticWorldRelationships
        private void InitStaticWorldRelationships()
        {
            R(Type.LizardTemplate, Type.LizardTemplate, AgressiveRival, 0.1f);
            R(Type.LizardTemplate, Type.Slugcat, SocialDependent, 0.5f);
            R(Type.LizardTemplate, Type.Vulture, Afraid, 0.9f);
            R(Type.LizardTemplate, Type.KingVulture, Afraid, 1f);
            R(Type.LizardTemplate, Type.Scavenger, Eats, 0.8f);
            R(Type.LizardTemplate, Type.CicadaA, Eats, 0.05f);
            R(Type.LizardTemplate, Type.BigSpider, Eats, 0.35f);
            R(Type.LizardTemplate, Type.EggBug, Eats, 0.45f);
            R(Type.LizardTemplate, Type.Centipede, Eats, 0.8f);
            R(Type.LizardTemplate, Type.BigNeedleWorm, Eats, 0.25f);
            R(Type.LizardTemplate, Type.DropBug, Eats, 0.2f);
            R(Type.LizardTemplate, Type.SmallNeedleWorm, Eats, 0.3f);
            R(Type.LizardTemplate, Type.RedCentipede, Afraid, 0.9f);
            R(Type.LizardTemplate, Type.TentaclePlant, Afraid, 0.2f);
            R(Type.LizardTemplate, Type.Hazer, Eats, 0.15f);
            R(Type.PinkLizard, Type.PinkLizard, AgressiveRival, 0.4f);
            R(Type.PinkLizard, Type.GreenLizard, AgressiveRival, 0.15f);
            R(Type.PinkLizard, Type.BlueLizard, AgressiveRival, 0.2f);
            R(Type.PinkLizard, Type.WhiteLizard, AgressiveRival, 0.25f);
            R(Type.GreenLizard, Type.PinkLizard, AgressiveRival, 0.2f);
            R(Type.GreenLizard, Type.GreenLizard, AgressiveRival, 0.8f);
            R(Type.GreenLizard, Type.BigSpider, Eats, 0.6f);
            R(Type.GreenLizard, Type.WhiteLizard, AgressiveRival, 0.05f);
            R(Type.GreenLizard, Type.BlueLizard, Eats, 0.25f);
            R(Type.GreenLizard, Type.EggBug, Eats, 0.1f);
            R(Type.GreenLizard, Type.KingVulture, Afraid, 0.5f);
            R(Type.GreenLizard, Type.RedCentipede, Afraid, 0.5f);
            R(Type.BlueLizard, Type.PinkLizard, AgressiveRival, 0.3f);
            R(Type.BlueLizard, Type.BigSpider, Afraid, 0.5f);
            R(Type.BlueLizard, Type.BlueLizard, AgressiveRival, 0.45f);
            R(Type.BlueLizard, Type.GreenLizard, Afraid, 0.25f);
            R(Type.BlueLizard, Type.WhiteLizard, Afraid, 0.1f);
            R(Type.BlueLizard, Type.CyanLizard, Afraid, 0.2f);
            R(Type.BlueLizard, Type.CicadaA, Eats, 0.7f);
            R(Type.WhiteLizard, Type.PinkLizard, AgressiveRival, 0.15f);
            R(Type.WhiteLizard, Type.GreenLizard, AgressiveRival, 0.05f);
            R(Type.WhiteLizard, Type.BlueLizard, AgressiveRival, 0.35f);
            R(Type.WhiteLizard, Type.WhiteLizard, AgressiveRival, 0.25f);
            R(Type.WhiteLizard, Type.CicadaA, Eats, 0.7f);
            R(Type.WhiteLizard, Type.EggBug, Eats, 0.7f);
            R(Type.RedLizard, Type.Vulture, Attacks, 0.4f);
            R(Type.RedLizard, Type.KingVulture, Attacks, 0.2f);
            R(Type.RedLizard, Type.RedLizard, Attacks, 1f);
            R(Type.CyanLizard, Type.CyanLizard, AgressiveRival, 0.075f);
            R(Type.CyanLizard, Type.BlueLizard, Eats, 0.15f);
            R(Type.YellowLizard, Type.YellowLizard, Pack, 0.2f);
            R(Type.Fly, Type.CicadaA, Afraid, 0.7f);
            R(Type.Fly, Type.Slugcat, Afraid, 0.5f);
            R(Type.Fly, Type.SmallCentipede, Afraid, 0.5f);
            R(Type.Fly, Type.TentaclePlant, Afraid, 0.05f);
            R(Type.Vulture, Type.CicadaA, Eats, 0.1f);
            R(Type.Vulture, Type.Slugcat, Eats, 0.3f);
            R(Type.Vulture, Type.LizardTemplate, Eats, 0.5f);
            R(Type.Vulture, Type.GreenLizard, Ignores, 0f);
            R(Type.Vulture, Type.Centipede, Eats, 0.5f);
            R(Type.Vulture, Type.RedCentipede, Afraid, 0.2f);
            R(Type.Vulture, Type.Scavenger, Eats, 0.8f);
            R(Type.Vulture, Type.VultureGrub, Eats, 0.01f);
            R(Type.Vulture, Type.BigSpider, Eats, 0.5f);
            R(Type.Vulture, Type.EggBug, Eats, 0.3f);
            R(Type.Vulture, Type.BigNeedleWorm, Eats, 0.15f);
            R(Type.Vulture, Type.SmallNeedleWorm, Eats, 0.05f);
            R(Type.Vulture, Type.DropBug, Eats, 0.4f);
            R(Type.Vulture, Type.TentaclePlant, Afraid, 0.1f);
            R(Type.Vulture, Type.PoleMimic, Afraid, 0.1f);
            R(Type.KingVulture, Type.GreenLizard, Eats, 0.55f);
            R(Type.KingVulture, Type.RedCentipede, Eats, 0.55f);
            R(Type.CicadaA, Type.CicadaA, PlaysWith, 1f);
            R(Type.CicadaA, Type.CicadaB, PlaysWith, 1f);
            R(Type.CicadaA, Type.LizardTemplate, Afraid, 0.3f);
            R(Type.CicadaA, Type.GreenLizard, Antagonizes, 0.1f);
            R(Type.CicadaA, Type.Slugcat, SocialDependent, 1f);
            R(Type.CicadaA, Type.Centipede, Afraid, 0.15f);
            R(Type.CicadaA, Type.Centiwing, Afraid, 0.35f);
            R(Type.CicadaA, Type.SmallCentipede, Eats, 0.15f);
            R(Type.CicadaA, Type.Vulture, Afraid, 0.7f);
            R(Type.CicadaA, Type.KingVulture, Afraid, 0.8f);
            R(Type.CicadaA, Type.WhiteLizard, Afraid, 0.7f);
            R(Type.CicadaA, Type.BlueLizard, Afraid, 0.7f);
            R(Type.CicadaA, Type.Fly, Eats, 0.2f);
            R(Type.CicadaA, Type.BigSpider, Afraid, 0.5f);
            R(Type.CicadaA, Type.BigNeedleWorm, Afraid, 0.15f);
            R(Type.CicadaA, Type.SmallNeedleWorm, Eats, 0.1f);
            R(Type.CicadaA, Type.DropBug, Afraid, 0.5f);
            R(Type.CicadaA, Type.RedCentipede, Afraid, 0.8f);
            R(Type.CicadaA, Type.TentaclePlant, Afraid, 0.5f);
            R(Type.CicadaB, Type.Slugcat, SocialDependent, 1f);
            R(Type.CicadaB, Type.Fly, Eats, 0.4f);
            R(Type.Slugcat, Type.LizardTemplate, Afraid, 1f);
            R(Type.Slugcat, Type.Fly, Eats, 0.5f);
            R(Type.Slugcat, Type.Vulture, Afraid, 1f);
            R(Type.Slugcat, Type.TentaclePlant, Afraid, 1f);
            R(Type.Slugcat, Type.Centipede, Afraid, 0.3f);
            R(Type.Slugcat, Type.Centiwing, Afraid, 0.3f);
            R(Type.Slugcat, Type.SmallCentipede, Ignores, 0.3f);
            R(Type.Slugcat, Type.BigSpider, Afraid, 0.5f);
            R(Type.Slugcat, Type.SpitterSpider, Afraid, 0.65f);
            R(Type.Slugcat, Type.DropBug, Afraid, 0.5f);
            R(Type.Slugcat, Type.RedCentipede, Afraid, 1f);
            R(Type.TentaclePlant, Type.TentaclePlant, Ignores, 0f);
            R(Type.TentaclePlant, Type.PoleMimic, Ignores, 0f);
            R(Type.Scavenger, Type.Vulture, Afraid, 0.75f);
            R(Type.Scavenger, Type.KingVulture, Afraid, 0.95f);
            R(Type.Scavenger, Type.Slugcat, SocialDependent, 1f);
            R(Type.Scavenger, Type.Scavenger, Pack, 0.5f);
            R(Type.Scavenger, Type.LizardTemplate, Afraid, 0.5f);
            R(Type.Scavenger, Type.Centipede, Afraid, 0.4f);
            R(Type.Scavenger, Type.RedCentipede, Afraid, 0.95f);
            R(Type.Scavenger, Type.SmallCentipede, Ignores, 0f);
            R(Type.Scavenger, Type.TentaclePlant, Afraid, 0.65f);
            R(Type.Scavenger, Type.PoleMimic, Afraid, 0.3f);
            R(Type.Scavenger, Type.BigSpider, Afraid, 0.45f);
            R(Type.Scavenger, Type.BigNeedleWorm, Uncomfortable, 0.5f);
            R(Type.Scavenger, Type.SmallNeedleWorm, Ignores, 0f);
            R(Type.Scavenger, Type.DropBug, Afraid, 0.55f);
            R(Type.Centipede, Type.Centipede, Ignores, 0f);
            R(Type.Centipede, Type.TentaclePlant, Afraid, 0.5f);
            R(Type.Centipede, Type.PoleMimic, Ignores, 0f);
            R(Type.Centipede, Type.BigSpider, Eats, 0.25f);
            R(Type.Centipede, Type.VultureGrub, Eats, 0.1f);
            R(Type.Centipede, Type.Fly, Ignores, 0f);
            R(Type.SmallCentipede, Type.Fly, Eats, 0.5f);
            R(Type.SmallCentipede, Type.Vulture, Afraid, 0.5f);
            R(Type.BigSpider, Type.Slugcat, Eats, 0.4f);
            R(Type.BigSpider, Type.LizardTemplate, Eats, 0.3f);
            R(Type.BigSpider, Type.BlueLizard, Eats, 0.5f);
            R(Type.BigSpider, Type.GreenLizard, Afraid, 0.4f);
            R(Type.BigSpider, Type.RedLizard, Afraid, 0.4f);
            R(Type.BigSpider, Type.Vulture, Afraid, 1f);
            R(Type.BigSpider, Type.Centipede, Eats, 0.11f);
            R(Type.BigSpider, Type.SmallCentipede, Eats, 0.2f);
            R(Type.BigSpider, Type.VultureGrub, Eats, 0.1f);
            R(Type.BigSpider, Type.Scavenger, Eats, 0.45f);
            R(Type.BigSpider, Type.TentaclePlant, Afraid, 0.6f);
            R(Type.BigSpider, Type.PoleMimic, Afraid, 0.3f);
            R(Type.BigSpider, Type.Centiwing, Eats, 0.4f);
            R(Type.BigSpider, Type.CicadaA, Eats, 0.4f);
            R(Type.BigSpider, Type.CicadaB, Eats, 0.4f);
            R(Type.BigSpider, Type.BigNeedleWorm, Eats, 0.3f);
            R(Type.BigSpider, Type.SmallNeedleWorm, Eats, 0.15f);
            R(Type.BigSpider, Type.DropBug, Eats, 0.25f);
            R(Type.BigSpider, Type.RedCentipede, Afraid, 0.8f);
            R(Type.BigSpider, Type.EggBug, Eats, 0.2f);
            R(Type.DropBug, Type.RedLizard, Afraid, 0.4f);
            R(Type.DropBug, Type.Vulture, Afraid, 1f);
            R(Type.DropBug, Type.TentaclePlant, Afraid, 0.6f);
            R(Type.DropBug, Type.PoleMimic, Afraid, 0.3f);
            R(Type.DropBug, Type.SmallNeedleWorm, Eats, 0.1f);
            R(Type.DropBug, Type.SmallCentipede, Eats, 0.1f);
            R(Type.DropBug, Type.DropBug, Ignores, 0.25f);
            R(Type.DropBug, Type.RedCentipede, Afraid, 0.8f);
            R(Type.DropBug, Type.Fly, Ignores, 0f);
            R(Type.BigNeedleWorm, Type.BigNeedleWorm, Attacks, 0.9f);
            R(Type.BigNeedleWorm, Type.Vulture, Afraid, 0.5f);
            R(Type.BigNeedleWorm, Type.KingVulture, Afraid, 0.6f);
            R(Type.BigNeedleWorm, Type.TentaclePlant, Afraid, 0.3f);
            R(Type.BigNeedleWorm, Type.VultureGrub, Ignores, 0f);
            R(Type.BigNeedleWorm, Type.Fly, Ignores, 0f);
            R(Type.BigNeedleWorm, Type.RedCentipede, Afraid, 0.8f);
            R(Type.EggBug, Type.Fly, Ignores, 0f);

            // Egg Bugs fear everything by its size. Types are visited in the game's registration order,
            // so a descendant registered before its ancestor (the Infant Noodlefly) ends up with the
            // ancestor's value, as in the game.
            foreach (Type t in System.Enum.GetValues(typeof(Type)))
            {
                var other = GetCreatureTemplate(t);
                if (other != null)
                    R(Type.EggBug, t, Afraid, Mathf.InverseLerp(0f, 8f, other.bodySize));
            }
            R(Type.EggBug, Type.Fly, Ignores, 0f);
            foreach (Type t in System.Enum.GetValues(typeof(Type)))
                R(t, Type.Overseer, Ignores, 0f);
            R(Type.Scavenger, Type.Overseer, Attacks, 1f);
        }

        // The ModManager.DLCShared part of StaticWorld.InitStaticWorldRelationshipsMSC.
        private void InitStaticWorldRelationshipsShared()
        {
            R(Type.Slugcat, Type.MirosVulture, Afraid, 1f);
            R(Type.MirosVulture, Type.Slugcat, Eats, 1f);
            R(Type.LizardTemplate, Type.MirosVulture, Afraid, 1f);
            R(Type.MirosVulture, Type.LizardTemplate, Eats, 0.5f);
            R(Type.MirosVulture, Type.Scavenger, Eats, 0.8f);
            R(Type.MirosVulture, Type.VultureGrub, Eats, 0.01f);
            R(Type.Scavenger, Type.MirosVulture, Afraid, 1f);
            R(Type.MirosVulture, Type.CicadaA, Eats, 0.1f);
            R(Type.CicadaA, Type.MirosVulture, Afraid, 0.7f);
            R(Type.MirosVulture, Type.BigSpider, Eats, 0.5f);
            R(Type.MirosVulture, Type.EggBug, Eats, 0.3f);
            R(Type.MirosVulture, Type.BigNeedleWorm, Eats, 0.15f);
            R(Type.MirosVulture, Type.SmallNeedleWorm, Eats, 0.05f);
            R(Type.MirosVulture, Type.DropBug, Eats, 0.4f);
            R(Type.BigSpider, Type.MirosVulture, Afraid, 1f);
            R(Type.DropBug, Type.MirosVulture, Afraid, 1f);
            R(Type.BigNeedleWorm, Type.MirosVulture, Afraid, 0.5f);
            R(Type.MirosVulture, Type.MirosVulture, Ignores, 0.1f);
            R(Type.MirosVulture, Type.Vulture, Antagonizes, 0.5f);
            R(Type.MirosVulture, Type.KingVulture, Antagonizes, 0.3f);
            R(Type.KingVulture, Type.MirosVulture, Eats, 1f);
            R(Type.Vulture, Type.MirosVulture, Afraid, 1f);
            R(Type.MirosVulture, Type.SmallCentipede, Eats, 0f);
            R(Type.SmallCentipede, Type.MirosVulture, Afraid, 0.7f);
            R(Type.MirosVulture, Type.Fly, Ignores, 0f);
            R(Type.MirosVulture, Type.Centipede, Eats, 0.7f);
            R(Type.Centipede, Type.MirosVulture, Afraid, 0.7f);
            R(Type.MirosVulture, Type.RedCentipede, Eats, 0.7f);
            R(Type.RedCentipede, Type.MirosVulture, Afraid, 0.7f);
            R(Type.TentaclePlant, Type.MirosVulture, Afraid, 1f);
            R(Type.BlueLizard, Type.ZoopLizard, Eats, 0.5f);
            R(Type.BlueLizard, Type.EelLizard, Afraid, 0.25f);
            R(Type.SpitLizard, Type.EelLizard, Eats, 0.25f);
            R(Type.RedLizard, Type.ZoopLizard, Eats, 0.3f);
            R(Type.GreenLizard, Type.ZoopLizard, Eats, 0.3f);
            R(Type.PinkLizard, Type.ZoopLizard, Eats, 0.3f);
            R(Type.YellowLizard, Type.ZoopLizard, Eats, 0.1f);
            R(Type.ZoopLizard, Type.GreenLizard, Afraid, 0.5f);
            R(Type.ZoopLizard, Type.PinkLizard, Afraid, 0.35f);
            R(Type.ZoopLizard, Type.BlueLizard, AgressiveRival, 0.65f);
            R(Type.ZoopLizard, Type.RedLizard, Afraid, 1f);
            R(Type.ZoopLizard, Type.Fly, Eats, 0.5f);
            R(Type.ZoopLizard, Type.BigSpider, Afraid, 0.7f);
            R(Type.BigSpider, Type.ZoopLizard, Eats, 0.6f);
            R(Type.ZoopLizard, Type.SpitLizard, Afraid, 1f);
            R(Type.SpitLizard, Type.ZoopLizard, Eats, 0.3f);
            R(Type.SpitLizard, Type.PinkLizard, AgressiveRival, 0.2f);
            R(Type.SpitLizard, Type.GreenLizard, AgressiveRival, 0.8f);
            R(Type.SpitLizard, Type.WhiteLizard, AgressiveRival, 0.05f);
            R(Type.SpitLizard, Type.BlueLizard, Eats, 0.25f);
            R(Type.SpitLizard, Type.EggBug, Eats, 0.1f);
            R(Type.SpitLizard, Type.KingVulture, Afraid, 0.5f);
            R(Type.SpitLizard, Type.RedCentipede, Afraid, 0.5f);
            R(Type.CicadaA, Type.SpitLizard, Antagonizes, 0.1f);
            R(Type.Vulture, Type.SpitLizard, Ignores, 0f);
            R(Type.PinkLizard, Type.SpitLizard, AgressiveRival, 0.15f);
            R(Type.BlueLizard, Type.SpitLizard, Afraid, 0.25f);
            R(Type.WhiteLizard, Type.SpitLizard, AgressiveRival, 0.05f);
            R(Type.KingVulture, Type.SpitLizard, Eats, 0.55f);
            R(Type.BigSpider, Type.SpitLizard, Afraid, 0.4f);
            R(Type.SpitLizard, Type.BigSpider, Eats, 0.6f);
        }
    }
}
