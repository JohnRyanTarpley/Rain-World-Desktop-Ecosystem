using System.Collections.Generic;
using Type = RWDE.Sim.CreatureTemplate.Type;
using Acc = RWDE.Sim.AItile.Accessibility;
using Move = RWDE.Sim.MovementConnection.MovementType;

namespace RWDE.Sim
{
    /// <summary>
    /// The template half of the game's <c>LizardBreeds.BreedTemplate</c>: each breed's tile and
    /// movement costs and the template fields it sets. The <c>LizardBreedParams</c> half (speeds,
    /// bite, tongue, colours) is ported with the lizard body; the few params a template reads
    /// (<c>toughness</c>, <c>stunToughness</c>, <c>danger</c>) are copied here with citations.
    /// </summary>
    internal static class LizardBreeds
    {
        // The per-breed values BreedTemplate reads into the template.
        private sealed class Breed
        {
            public readonly List<TileTypeResistance> tiles = new List<TileTypeResistance>();
            public readonly List<TileConnectionResistance> connections = new List<TileConnectionResistance>();
            public float waterPathingResistance = 3f;
            public bool pathsLikePink; // BreedTemplate's local "flag"
            public float toughness;     // LizardBreedParams.toughness
            public float stunToughness; // LizardBreedParams.stunToughness
            public float danger;        // LizardBreedParams.danger
            public float visualRadius;
            public float waterVision;
            public float throughSurfaceVision;
            public float movementBasedVision = 0.3f;

            public Breed T(Acc a, float resistance)
            {
                tiles.Add(new TileTypeResistance(a, resistance, PathCost.Legality.Allowed));
                return this;
            }

            public Breed C(Move m, float resistance)
            {
                connections.Add(new TileConnectionResistance(m, resistance, PathCost.Legality.Allowed));
                return this;
            }
        }

        // LizardBreeds.BreedTemplate(type, lizardAncestor, pinkTemplate, blueTemplate, greenTemplate)
        public static CreatureTemplate BreedTemplate(Type type, CreatureTemplate lizardAncestor, CreatureTemplate pinkTemplate,
            CreatureTemplate blueTemplate, CreatureTemplate greenTemplate, StaticWorld.Dlc dlc)
        {
            var b = BreedData(type);
            var t = new CreatureTemplate(type, lizardAncestor, b.tiles, b.connections,
                new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0f), dlc);
            t.baseDamageResistance = b.toughness * 2f;
            t.baseStunResistance = b.stunToughness;
            t.damageRestistances[(int)CreatureTemplate.DamageType.Bite, 0] = 2.5f;
            t.damageRestistances[(int)CreatureTemplate.DamageType.Bite, 1] = 3f;
            t.meatPoints = 6;
            switch (type)
            {
                case Type.BlueLizard:
                    t.meatPoints = 5;
                    break;
                case Type.Salamander:
                    t.waterRelationship = CreatureTemplate.WaterRelationship.Amphibious;
                    t.canSwim = StaticWorld.Remix; // only with ModManager.MMF
                    break;
                case Type.GreenLizard:
                    t.wormGrassImmune = true;
                    t.bodySize = 3f;
                    t.meatPoints = 8;
                    break;
                case Type.RedLizard:
                    t.damageRestistances[(int)CreatureTemplate.DamageType.Explosion, 0] = 1.9f;
                    t.bodySize = 3f;
                    t.meatPoints = 9;
                    break;
                case Type.CyanLizard:
                    t.doPreBakedPathing = false;
                    t.preBakedPathingAncestor = blueTemplate;
                    break;
                case Type.SpitLizard:
                    t.bodySize = 1f;
                    t.meatPoints = 10;
                    t.wormGrassImmune = true;
                    break;
                case Type.ZoopLizard:
                    t.meatPoints = 2;
                    break;
            }
            t.visualRadius = b.visualRadius;
            t.waterVision = b.waterVision;
            t.throughSurfaceVision = b.throughSurfaceVision;
            t.movementBasedVision = b.movementBasedVision;
            t.waterPathingResistance = b.waterPathingResistance;
            t.dangerousToPlayer = b.danger;
            if (type == Type.SpitLizard)
            {
                t.doPreBakedPathing = false;
                t.preBakedPathingAncestor = greenTemplate;
            }
            else if (type != Type.CyanLizard)
            {
                if (b.pathsLikePink)
                {
                    t.doPreBakedPathing = false;
                    t.preBakedPathingAncestor = pinkTemplate;
                }
                else
                {
                    t.doPreBakedPathing = true;
                }
            }
            t.virtualCreature = false;
            return t;
        }

        // LizardBreeds.BreedTemplate(type, lizardAncestor, SalamanderTemplate): the Eel Lizard.
        public static CreatureTemplate EelTemplate(CreatureTemplate lizardAncestor, CreatureTemplate salamanderTemplate, StaticWorld.Dlc dlc)
        {
            var b = new Breed
            {
                waterPathingResistance = 0.8f,
                toughness = 0.8f,
                danger = 0.8f,
                visualRadius = 960f,
                waterVision = 1f,
                throughSurfaceVision = 0.5f,
            }
                .T(Acc.Floor, 0.6f).T(Acc.Corridor, 1f).T(Acc.Climb, 0.9f).T(Acc.Wall, 0.8f).T(Acc.Ceiling, 1.2f)
                .C(Move.DropToClimb, 40f).C(Move.DropToWater, 20f).C(Move.DropToFloor, 10f);
            var t = new CreatureTemplate(Type.EelLizard, lizardAncestor, b.tiles, b.connections,
                new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0f), dlc);
            t.baseDamageResistance = b.toughness * 2f;
            t.baseStunResistance = b.toughness; // the game uses toughness here, not stunToughness
            t.damageRestistances[(int)CreatureTemplate.DamageType.Bite, 0] = 2.5f;
            t.damageRestistances[(int)CreatureTemplate.DamageType.Bite, 1] = 3f;
            t.meatPoints = 5;
            t.bodySize = 3f;
            t.canSwim = true;
            t.waterRelationship = CreatureTemplate.WaterRelationship.Amphibious;
            t.visualRadius = b.visualRadius;
            t.waterVision = b.waterVision;
            t.throughSurfaceVision = b.throughSurfaceVision;
            t.movementBasedVision = b.movementBasedVision;
            t.waterPathingResistance = b.waterPathingResistance;
            t.dangerousToPlayer = b.danger;
            t.doPreBakedPathing = false;
            t.preBakedPathingAncestor = salamanderTemplate;
            t.virtualCreature = false;
            t.bodySize = 4f;
            t.meatPoints = 8;
            return t;
        }

        // The per-breed if-blocks of BreedTemplate, template values only.
        private static Breed BreedData(Type type)
        {
            switch (type)
            {
                case Type.PinkLizard:
                    return new Breed
                    {
                        waterPathingResistance = 3f, toughness = 1f, stunToughness = 1f, danger = 0.45f,
                        visualRadius = 900f, waterVision = 0.4f, throughSurfaceVision = 0.85f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.Climb, 2.5f).C(Move.DropToClimb, 40f);
                case Type.GreenLizard:
                    return new Breed
                    {
                        waterPathingResistance = 1.5f, toughness = 2.5f, stunToughness = 2.5f, danger = 0.45f,
                        visualRadius = 850f, waterVision = 0.3f, throughSurfaceVision = 0.85f, movementBasedVision = 0.7f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 2f).C(Move.DropToFloor, 40f).C(Move.LizardTurn, 60f);
                case Type.BlueLizard:
                    return new Breed
                    {
                        waterPathingResistance = 5f, toughness = 0.5f, stunToughness = 0.5f, danger = 0.35f,
                        visualRadius = 950f, waterVision = 0.4f, throughSurfaceVision = 0.85f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 1.2f).T(Acc.Climb, 0.8f).T(Acc.Wall, 1f).T(Acc.Ceiling, 1.2f)
                        .C(Move.DropToFloor, 20f).C(Move.DropToClimb, 2f).C(Move.ShortCut, 15f).C(Move.ReachOverGap, 1.1f)
                        .C(Move.ReachUp, 1.1f).C(Move.ReachDown, 1.1f).C(Move.CeilingSlope, 2f);
                case Type.YellowLizard:
                    return new Breed
                    {
                        waterPathingResistance = 2.5f, pathsLikePink = true, toughness = 0.8f, stunToughness = 0.8f, danger = 0.4f,
                        visualRadius = 900f, waterVision = 0.35f, throughSurfaceVision = 0.85f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.Climb, 2.5f).C(Move.DropToFloor, 4f).C(Move.DropToClimb, 8f);
                case Type.WhiteLizard:
                    return new Breed
                    {
                        waterPathingResistance = 8f, toughness = 0.9f, stunToughness = 0.9f, danger = 0.5f,
                        visualRadius = 1300f, waterVision = 0.4f, throughSurfaceVision = 0.9f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 1.2f).T(Acc.Climb, 0.8f).T(Acc.Wall, 1f)
                        .C(Move.DropToFloor, 10f).C(Move.DropToClimb, 10f).C(Move.ShortCut, 2f).C(Move.ReachOverGap, 1.1f)
                        .C(Move.ReachUp, 1.1f).C(Move.ReachDown, 1.1f).C(Move.CeilingSlope, 20f);
                case Type.RedLizard:
                    return new Breed
                    {
                        waterPathingResistance = 2f, pathsLikePink = true, toughness = 3f, stunToughness = 3f, danger = 0.8f,
                        visualRadius = 2300f, waterVision = 0.7f, throughSurfaceVision = 0.95f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.Climb, 1f).C(Move.DropToClimb, 4f);
                case Type.Salamander:
                    return new Breed
                    {
                        waterPathingResistance = 0.5f, toughness = 1f, stunToughness = 1f, danger = 0.4f,
                        visualRadius = 960f, waterVision = 1f, throughSurfaceVision = 0.5f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.Climb, 2.5f).C(Move.DropToClimb, 40f);
                case Type.CyanLizard:
                    return new Breed
                    {
                        waterPathingResistance = 5f, toughness = 0.9f, stunToughness = 0.9f, danger = 0.65f,
                        visualRadius = 990f, waterVision = 0.4f, throughSurfaceVision = 0.85f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 1.2f).T(Acc.Climb, 0.8f).T(Acc.Wall, 1f).T(Acc.Ceiling, 1.2f)
                        .C(Move.DropToFloor, 2f).C(Move.DropToClimb, 2f).C(Move.ShortCut, 5f).C(Move.ReachOverGap, 1.1f)
                        .C(Move.ReachUp, 1.1f).C(Move.ReachDown, 1.1f).C(Move.CeilingSlope, 2f);
                case Type.SpitLizard:
                    return new Breed
                    {
                        waterPathingResistance = 1.5f, toughness = 2.5f, stunToughness = 2.5f, danger = 0.8f,
                        visualRadius = 2300f, waterVision = 0.7f, throughSurfaceVision = 0.95f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 2f).C(Move.DropToFloor, 40f).C(Move.LizardTurn, 60f);
                case Type.ZoopLizard:
                    return new Breed
                    {
                        waterPathingResistance = 6.5f, pathsLikePink = true, toughness = 0.7f, stunToughness = 0.7f, danger = 0.8f,
                        visualRadius = 1400f, waterVision = 0.25f, throughSurfaceVision = 0.55f,
                    }.T(Acc.Floor, 4f).T(Acc.Corridor, 1f).T(Acc.Climb, 0.2f).C(Move.DropToFloor, 10f).C(Move.DropToClimb, 0.2f);
                default:
                    throw new System.ArgumentException(type + " is not a lizard breed.", nameof(type));
            }
        }
    }
}
