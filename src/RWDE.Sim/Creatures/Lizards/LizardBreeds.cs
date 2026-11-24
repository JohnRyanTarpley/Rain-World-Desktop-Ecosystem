using System.Collections.Generic;
using Type = RWDE.Sim.CreatureTemplate.Type;
using Acc = RWDE.Sim.AItile.Accessibility;
using Move = RWDE.Sim.MovementConnection.MovementType;

namespace RWDE.Sim
{
    /// <summary>
    /// The game's <c>LizardBreeds.BreedTemplate</c>: each breed's tile and movement costs, the template
    /// fields it sets, and its <see cref="LizardBreedParams"/> (<see cref="BreedParams"/>), which the
    /// template reads <c>toughness</c>, <c>stunToughness</c> and <c>danger</c> from.
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
            var p = BreedParams(type);
            if (p != null)
            {
                b.toughness = p.toughness;
                b.stunToughness = p.stunToughness;
                b.danger = p.danger;
            }
            var t = new CreatureTemplate(type, lizardAncestor, b.tiles, b.connections,
                new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0f), dlc);
            t.breedParameters = p;
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

        /// <summary>
        /// The game's <c>MMF.cfgAlphaRedLizards</c> (Remix "Alpha red lizards", on by default): Red Lizards
        /// get a tongue and a smaller bite radius bonus.
        /// </summary>
        internal const bool AlphaRedLizards = StaticWorld.Remix;

        // The start of BreedTemplate: every terrain speed 0.1 until the breed sets it, body factors 1.
        private static LizardBreedParams NewParams(Type type)
        {
            var p = new LizardBreedParams(type)
            {
                terrainSpeeds = new LizardBreedParams.SpeedMultiplier[System.Enum.GetNames(typeof(Acc)).Length],
                bodyRadFac = 1f,
                pullDownFac = 1f,
                bodyLengthFac = 1f,
            };
            for (int i = 0; i < p.terrainSpeeds.Length; i++)
                p.terrainSpeeds[i] = new LizardBreedParams.SpeedMultiplier(0.1f, 1f, 1f, 1f);
            return p;
        }

        private static LizardBreedParams.SpeedMultiplier S(float speed, float horizontal, float up, float down) =>
            new LizardBreedParams.SpeedMultiplier(speed, horizontal, up, down);

        /// <summary>
        /// The <c>LizardBreedParams</c> half of BreedTemplate's per-breed blocks, or null for breeds not
        /// ported yet. White, Cyan, Black, Salamander and Eel Lizard get theirs with the breed-specials work.
        /// terrainSpeeds indices are the game's literal ones (Caramel and Strawberry set index 2, CurvedFloor,
        /// where the other breeds set 3, Corridor).
        /// </summary>
        public static LizardBreedParams BreedParams(Type type)
        {
            var p = NewParams(type);
            switch (type)
            {
                case Type.PinkLizard: // LizardBreeds.cs:32
                    p.terrainSpeeds[1] = S(1f, 1f, 1f, 1f);
                    p.terrainSpeeds[3] = S(1.2f, 1f, 0.8f, 1f);
                    p.terrainSpeeds[4] = S(0.5f, 1f, 0.75f, 1f);
                    p.biteDelay = 12;
                    p.biteInFront = 25f;
                    p.biteHomingSpeed = 1.7f;
                    p.biteChance = 0.5f;
                    p.attemptBiteRadius = 80f;
                    p.getFreeBiteChance = 0.65f;
                    p.biteDamage = 1f;
                    p.biteDamageChance = 1f / 3f;
                    p.toughness = 1f;
                    p.stunToughness = 1f;
                    p.regainFootingCounter = 10;
                    p.baseSpeed = 4.1f;
                    p.bodyMass = 2.1f;
                    p.bodySizeFac = 1f;
                    p.floorLeverage = 0.5f;
                    p.maxMusclePower = 5f;
                    p.wiggleSpeed = 0.5f;
                    p.wiggleDelay = 15;
                    p.bodyStiffnes = 0.2f;
                    p.swimSpeed = 0.2f;
                    p.idleCounterSubtractWhenCloseToIdlePos = 0;
                    p.danger = 0.45f;
                    p.aggressionCurveExponent = 0.9f;
                    p.headShieldAngle = 100f;
                    p.canExitLounge = false;
                    p.canExitLoungeWarmUp = true;
                    p.findLoungeDirection = 0.5f;
                    p.loungeDistance = 100f;
                    p.preLoungeCrouch = 25;
                    p.preLoungeCrouchMovement = -0.2f;
                    p.loungeSpeed = 1.9f;
                    p.loungeMaximumFrames = 20;
                    p.loungePropulsionFrames = 10;
                    p.loungeJumpyness = 0.5f;
                    p.loungeDelay = 90;
                    p.riskOfDoubleLoungeDelay = 0.1f;
                    p.postLoungeStun = 20;
                    p.loungeTendensy = 0.05f;
                    p.perfectVisionAngle = Mathf.Lerp(1f, -1f, 0f);
                    p.periferalVisionAngle = Mathf.Lerp(1f, -1f, 7f / 12f);
                    p.biteDominance = 0.5f;
                    p.limbSize = 1f;
                    p.limbThickness = 1f;
                    p.stepLength = 0.5f;
                    p.liftFeet = 0.3f;
                    p.feetDown = 0.5f;
                    p.noGripSpeed = 0.1f;
                    p.limbSpeed = 5f;
                    p.limbQuickness = 0.5f;
                    p.limbGripDelay = 1;
                    p.smoothenLegMovement = true;
                    p.legPairDisplacement = 0.2f;
                    p.standardColor = new Color(1f, 0f, 1f);
                    p.walkBob = 4f;
                    p.tailSegments = 5;
                    p.tailStiffness = 250f;
                    p.tailStiffnessDecline = 0.2f;
                    p.tailLengthFactor = 1.2f;
                    p.tailColorationStart = 0.3f;
                    p.tailColorationExponent = 2f;
                    p.headSize = 1f;
                    p.neckStiffness = 0.2f;
                    p.jawOpenAngle = 90f;
                    p.jawOpenLowerJawFac = 2f / 3f;
                    p.jawOpenMoveJawsApart = 23f;
                    p.headGraphics = new int[5];
                    p.framesBetweenLookFocusChange = 80;
                    p.tamingDifficulty = 1f;
                    return p;
                case Type.GreenLizard: // LizardBreeds.cs:114
                    p.terrainSpeeds[1] = S(1f, 1f, 0.8f, 1.7f);
                    p.terrainSpeeds[3] = S(0.8f, 1f, 0.8f, 1.7f);
                    p.biteDelay = 20;
                    p.biteInFront = 40f;
                    p.biteHomingSpeed = 0.7f;
                    p.biteChance = 1f / 3f;
                    p.attemptBiteRadius = 100f;
                    p.getFreeBiteChance = 0.45f;
                    p.biteDamage = 2f;
                    p.biteDamageChance = 0.5f;
                    p.toughness = 2.5f;
                    p.stunToughness = 2.5f;
                    p.regainFootingCounter = 10;
                    p.baseSpeed = 6.7f;
                    p.bodyMass = 7.5f;
                    p.bodySizeFac = 1.2f;
                    p.floorLeverage = 7f;
                    p.maxMusclePower = 16f;
                    p.danger = 0.45f;
                    p.aggressionCurveExponent = 0.95f;
                    p.wiggleSpeed = 0f;
                    p.wiggleDelay = 55;
                    p.bodyStiffnes = 0.5f;
                    p.swimSpeed = 0.6f;
                    p.idleCounterSubtractWhenCloseToIdlePos = 0;
                    p.biteDominance = 0.8f;
                    p.headShieldAngle = 100f;
                    p.canExitLounge = false;
                    p.canExitLoungeWarmUp = false;
                    p.findLoungeDirection = 0f;
                    p.loungeDistance = 170f;
                    p.preLoungeCrouch = 50;
                    p.preLoungeCrouchMovement = -0.3f;
                    p.loungeSpeed = 1f;
                    p.loungeMaximumFrames = 40;
                    p.loungePropulsionFrames = 40;
                    p.loungeJumpyness = 0f;
                    p.loungeDelay = 150;
                    p.riskOfDoubleLoungeDelay = 0.3f;
                    p.postLoungeStun = 40;
                    p.loungeTendensy = 1f;
                    p.perfectVisionAngle = Mathf.Lerp(1f, -1f, 0f);
                    p.periferalVisionAngle = Mathf.Lerp(1f, -1f, 5f / 18f);
                    p.limbSize = 1.4f;
                    p.limbThickness = 1f;
                    p.stepLength = 0.9f;
                    p.liftFeet = 0.5f;
                    p.feetDown = 1f;
                    p.noGripSpeed = 0.05f;
                    p.limbSpeed = 3f;
                    p.limbQuickness = 0.3f;
                    p.limbGripDelay = 1;
                    p.smoothenLegMovement = false;
                    p.legPairDisplacement = 1f;
                    p.standardColor = new Color(0.2f, 1f, 0f);
                    p.walkBob = 4f;
                    p.tailSegments = 7;
                    p.tailStiffness = 300f;
                    p.tailStiffnessDecline = 0.6f;
                    p.tailLengthFactor = 0.9f;
                    p.tailColorationStart = 0.05f;
                    p.tailColorationExponent = 4f;
                    p.headSize = 1f;
                    p.neckStiffness = 1f;
                    p.jawOpenAngle = 50f;
                    p.jawOpenLowerJawFac = 0.5f;
                    p.jawOpenMoveJawsApart = 14f;
                    p.headGraphics = new[] { 1, 1, 1, 1, 1 };
                    p.framesBetweenLookFocusChange = 160;
                    p.tamingDifficulty = 0.8f;
                    return p;
                case Type.BlueLizard: // LizardBreeds.cs:196
                    p.terrainSpeeds[1] = S(1f, 1f, 1f, 1f);
                    p.terrainSpeeds[3] = S(1f, 1f, 1f, 1f);
                    p.terrainSpeeds[4] = S(1f, 1f, 1f, 1f);
                    p.terrainSpeeds[5] = S(0.8f, 1f, 1f, 1f);
                    p.terrainSpeeds[6] = S(0.6f, 1f, 1f, 1f);
                    p.biteDelay = 14;
                    p.biteInFront = 20f;
                    p.biteHomingSpeed = 1.4f;
                    p.biteChance = 0.4f;
                    p.attemptBiteRadius = 90f;
                    p.getFreeBiteChance = 0.5f;
                    p.biteDamage = 0.7f;
                    p.biteDamageChance = 0.2f;
                    p.toughness = 0.5f;
                    p.stunToughness = 0.5f;
                    p.regainFootingCounter = 4;
                    p.baseSpeed = 3.2f;
                    p.bodyMass = 1.4f;
                    p.bodySizeFac = 0.9f;
                    p.floorLeverage = 1f;
                    p.maxMusclePower = 2f;
                    p.danger = 0.35f;
                    p.aggressionCurveExponent = 0.875f;
                    p.wiggleSpeed = 1f;
                    p.wiggleDelay = 15;
                    p.bodyStiffnes = 0f;
                    p.swimSpeed = 0.35f;
                    p.idleCounterSubtractWhenCloseToIdlePos = 0;
                    p.headShieldAngle = 100f;
                    p.canExitLounge = true;
                    p.canExitLoungeWarmUp = true;
                    p.findLoungeDirection = 1f;
                    p.loungeDistance = 80f;
                    p.preLoungeCrouch = 35;
                    p.preLoungeCrouchMovement = -0.3f;
                    p.loungeSpeed = 2.5f;
                    p.loungeMaximumFrames = 20;
                    p.loungePropulsionFrames = 8;
                    p.loungeJumpyness = 0.9f;
                    p.loungeDelay = 310;
                    p.riskOfDoubleLoungeDelay = 0.8f;
                    p.postLoungeStun = 20;
                    p.loungeTendensy = 0.01f;
                    p.perfectVisionAngle = Mathf.Lerp(1f, -1f, 1f / 18f);
                    p.periferalVisionAngle = Mathf.Lerp(1f, -1f, 11f / 24f);
                    p.biteDominance = 0.1f;
                    p.limbSize = 0.9f;
                    p.limbThickness = 1f;
                    p.stepLength = 0.4f;
                    p.liftFeet = 0f;
                    p.feetDown = 0f;
                    p.noGripSpeed = 0.2f;
                    p.limbSpeed = 6f;
                    p.limbQuickness = 0.6f;
                    p.limbGripDelay = 1;
                    p.smoothenLegMovement = true;
                    p.legPairDisplacement = 0f;
                    p.standardColor = new Color(0f, 0.5f, 1f);
                    p.walkBob = 0.4f;
                    p.tailSegments = 4;
                    p.tailStiffness = 400f;
                    p.tailStiffnessDecline = 0.1f;
                    p.tailLengthFactor = 1f;
                    p.tailColorationStart = 0.1f;
                    p.tailColorationExponent = 1.2f;
                    p.headSize = 0.9f;
                    p.neckStiffness = 0f;
                    p.jawOpenAngle = 105f;
                    p.jawOpenLowerJawFac = 0.55f;
                    p.jawOpenMoveJawsApart = 20f;
                    p.headGraphics = new int[5];
                    p.framesBetweenLookFocusChange = 20;
                    p.tongue = true;
                    p.tongueAttackRange = 140f;
                    p.tongueWarmUp = 10;
                    p.tongueSegments = 5;
                    p.tongueChance = 0.25f;
                    p.tamingDifficulty = 1.1f;
                    return p;
                case Type.YellowLizard: // LizardBreeds.cs:293
                    p.terrainSpeeds[1] = S(1f, 1f, 1f, 1f);
                    p.terrainSpeeds[3] = S(1.2f, 1f, 0.8f, 1f);
                    p.terrainSpeeds[4] = S(0.5f, 1f, 0.75f, 1f);
                    p.biteDelay = 21;
                    p.biteInFront = 15f;
                    p.biteHomingSpeed = 1.2f;
                    p.biteChance = 1f / 6f;
                    p.attemptBiteRadius = 40f;
                    p.getFreeBiteChance = 0.65f;
                    p.biteDamage = 0.8f;
                    p.biteDamageChance = 0.25f;
                    p.toughness = 0.8f;
                    p.stunToughness = 0.8f;
                    p.regainFootingCounter = 10;
                    p.baseSpeed = 4.1f;
                    p.bodyMass = 1.7f;
                    p.bodySizeFac = 0.93f;
                    p.floorLeverage = 0.5f;
                    p.maxMusclePower = 5f;
                    p.wiggleSpeed = 0.5f;
                    p.wiggleDelay = 15;
                    p.bodyStiffnes = 0.2f;
                    p.swimSpeed = 0.3f;
                    p.idleCounterSubtractWhenCloseToIdlePos = 0;
                    p.danger = 0.4f;
                    p.aggressionCurveExponent = 0.77f;
                    p.headShieldAngle = 100f;
                    p.canExitLounge = false;
                    p.canExitLoungeWarmUp = true;
                    p.findLoungeDirection = 0.5f;
                    p.loungeDistance = 100f;
                    p.preLoungeCrouch = 25;
                    p.preLoungeCrouchMovement = -0.2f;
                    p.loungeSpeed = 1.9f;
                    p.loungeMaximumFrames = 20;
                    p.loungePropulsionFrames = 10;
                    p.loungeJumpyness = 0.5f;
                    p.loungeDelay = 90;
                    p.riskOfDoubleLoungeDelay = 0.1f;
                    p.postLoungeStun = 20;
                    p.loungeTendensy = 0.05f;
                    p.perfectVisionAngle = Mathf.Lerp(1f, -1f, 1f / 12f);
                    p.periferalVisionAngle = Mathf.Lerp(1f, -1f, 19f / 36f);
                    p.biteDominance = 0.45f;
                    p.limbSize = 1f;
                    p.limbThickness = 1f;
                    p.stepLength = 0.5f;
                    p.liftFeet = 0.3f;
                    p.feetDown = 0.5f;
                    p.noGripSpeed = 0.1f;
                    p.limbSpeed = 5f;
                    p.limbQuickness = 0.5f;
                    p.limbGripDelay = 1;
                    p.smoothenLegMovement = true;
                    p.legPairDisplacement = 0.2f;
                    p.standardColor = new Color(1f, 0.6f, 0f);
                    p.walkBob = 4f;
                    p.tailSegments = 5;
                    p.tailStiffness = 250f;
                    p.tailStiffnessDecline = 0.2f;
                    p.tailLengthFactor = 1.2f;
                    p.tailColorationStart = 0.3f;
                    p.tailColorationExponent = 2f;
                    p.headSize = 1f;
                    p.neckStiffness = 0.2f;
                    p.jawOpenAngle = 90f;
                    p.jawOpenLowerJawFac = 2f / 3f;
                    p.jawOpenMoveJawsApart = 23f;
                    p.headGraphics = new int[5];
                    p.framesBetweenLookFocusChange = 80;
                    p.tamingDifficulty = 3f;
                    return p;
                case Type.RedLizard: // LizardBreeds.cs:472
                    p.terrainSpeeds[1] = S(1f, 1f, 1f, 1f);
                    p.terrainSpeeds[3] = S(1f, 1f, 0.9f, 1f);
                    p.terrainSpeeds[4] = S(0.9f, 1f, 0.6f, 1f);
                    p.biteDelay = 2;
                    p.biteInFront = 20f;
                    p.biteRadBonus = AlphaRedLizards ? 5f : 25f;
                    p.biteHomingSpeed = 4.7f;
                    p.biteChance = 1f;
                    p.attemptBiteRadius = 120f;
                    p.getFreeBiteChance = 1f;
                    p.biteDamage = 4f;
                    p.biteDamageChance = 1f;
                    p.toughness = 3f;
                    p.stunToughness = 3f;
                    p.regainFootingCounter = 1;
                    p.baseSpeed = 5f;
                    p.bodyMass = 3.1f;
                    p.bodySizeFac = 1.2f;
                    p.floorLeverage = 6f;
                    p.maxMusclePower = 9f;
                    p.wiggleSpeed = 0.5f;
                    p.wiggleDelay = 15;
                    p.bodyStiffnes = 0.3f;
                    p.swimSpeed = 1.9f;
                    p.idleCounterSubtractWhenCloseToIdlePos = 10;
                    p.danger = 0.8f;
                    p.aggressionCurveExponent = 0.7f;
                    p.headShieldAngle = 100f;
                    if (AlphaRedLizards)
                    {
                        p.tongue = true;
                        p.tongueAttackRange = 350f;
                        p.tongueWarmUp = 8;
                        p.tongueSegments = 10;
                        p.tongueChance = 0.1f;
                    }
                    p.canExitLounge = false;
                    p.canExitLoungeWarmUp = true;
                    p.findLoungeDirection = 0.5f;
                    p.loungeDistance = 100f;
                    p.preLoungeCrouch = 25;
                    p.preLoungeCrouchMovement = -0.2f;
                    p.loungeSpeed = 1.9f;
                    p.loungeMaximumFrames = 20;
                    p.loungePropulsionFrames = 10;
                    p.loungeJumpyness = 0.5f;
                    p.loungeDelay = 90;
                    p.riskOfDoubleLoungeDelay = 0.1f;
                    p.postLoungeStun = 20;
                    p.loungeTendensy = 0.05f;
                    p.perfectVisionAngle = Mathf.Lerp(1f, -1f, 4f / 9f);
                    p.periferalVisionAngle = Mathf.Lerp(1f, -1f, 7f / 9f);
                    p.biteDominance = 1f;
                    p.limbSize = 1.5f;
                    p.limbThickness = 1f;
                    p.stepLength = 0.8f;
                    p.liftFeet = 0.3f;
                    p.feetDown = 0.5f;
                    p.noGripSpeed = 0.25f;
                    p.limbSpeed = 9f;
                    p.limbQuickness = 0.8f;
                    p.limbGripDelay = 1;
                    p.smoothenLegMovement = true;
                    p.legPairDisplacement = 0.2f;
                    p.standardColor = new Color(1f, 0f, 0f);
                    p.walkBob = 3f;
                    p.tailSegments = 11;
                    p.tailStiffness = 200f;
                    p.tailStiffnessDecline = 0.3f;
                    p.tailLengthFactor = 1.9f;
                    p.tailColorationStart = 0.3f;
                    p.tailColorationExponent = 2f;
                    p.headSize = 1.2f;
                    p.neckStiffness = 0.2f;
                    p.jawOpenAngle = 140f;
                    p.jawOpenLowerJawFac = 2f / 3f;
                    p.jawOpenMoveJawsApart = 23f;
                    p.headGraphics = new int[5];
                    p.framesBetweenLookFocusChange = 20;
                    p.tamingDifficulty = 7f;
                    return p;
                case Type.SpitLizard: // LizardBreeds.cs:830 (Caramel)
                    p.terrainSpeeds[1] = S(3f, 3f, 0.8f, 2f);
                    p.terrainSpeeds[2] = S(2f, 0.9f, 1.4f, 1.9f);
                    p.biteDelay = 6;
                    p.biteInFront = 15f;
                    p.biteHomingSpeed = 1.4f;
                    p.biteChance = 0.46511626f;
                    p.attemptBiteRadius = 90f;
                    p.getFreeBiteChance = 0.72f;
                    p.biteDamage = 1.2f;
                    p.biteDamageChance = 0.5f;
                    p.toughness = 2.5f;
                    p.stunToughness = 2.5f;
                    p.regainFootingCounter = 10;
                    p.baseSpeed = 0.65f;
                    p.bodyMass = 4f;
                    p.bodySizeFac = 1.75f;
                    p.floorLeverage = 0.2f;
                    p.maxMusclePower = 3f;
                    p.danger = 0.8f;
                    p.aggressionCurveExponent = 0.95f;
                    p.wiggleSpeed = 0f;
                    p.wiggleDelay = 55;
                    p.bodyStiffnes = 0.2f;
                    p.swimSpeed = 0.45f;
                    p.idleCounterSubtractWhenCloseToIdlePos = 0;
                    p.biteDominance = 0.4f;
                    p.headShieldAngle = 100f;
                    p.canExitLounge = false;
                    p.canExitLoungeWarmUp = false;
                    p.findLoungeDirection = 0.5f;
                    p.loungeDistance = 400f;
                    p.preLoungeCrouch = 40;
                    p.preLoungeCrouchMovement = -0.2f;
                    p.loungeSpeed = 4.5f;
                    p.loungeMaximumFrames = 20;
                    p.loungePropulsionFrames = 10;
                    p.loungeJumpyness = 2f;
                    p.loungeDelay = 200;
                    p.riskOfDoubleLoungeDelay = 0.1f;
                    p.postLoungeStun = 70;
                    p.loungeTendensy = 0.85f;
                    p.perfectVisionAngle = Mathf.Lerp(1f, -1f, 4f / 9f);
                    p.periferalVisionAngle = Mathf.Lerp(1f, -1f, 7f / 9f);
                    p.biteDominance = 0.45f; // set twice in the game; this later value wins
                    p.limbSize = 1.1f;
                    p.limbThickness = 1.5f;
                    p.stepLength = 0.5f;
                    p.liftFeet = 0.3f;
                    p.feetDown = 0.5f;
                    p.noGripSpeed = 0.1f;
                    p.limbSpeed = 5f;
                    p.limbQuickness = 0.5f;
                    p.limbGripDelay = 1;
                    p.smoothenLegMovement = true;
                    p.legPairDisplacement = 0.2f;
                    p.standardColor = new Color(0.55f, 0.4f, 0.2f);
                    p.walkBob = 4f;
                    p.tailSegments = 4;
                    p.tailStiffness = 250f;
                    p.tailStiffnessDecline = 0.2f;
                    p.tailLengthFactor = 1.2f;
                    p.tailColorationStart = 0.3f;
                    p.tailColorationExponent = 2f;
                    p.headSize = 1.2f;
                    p.neckStiffness = 0.2f;
                    p.jawOpenAngle = 90f;
                    p.jawOpenLowerJawFac = 2f / 3f;
                    p.jawOpenMoveJawsApart = 23f;
                    p.headGraphics = new[] { 1, 1, 1, 1, 1 };
                    p.framesBetweenLookFocusChange = 160;
                    p.tamingDifficulty = 0.3f;
                    return p;
                case Type.ZoopLizard: // LizardBreeds.cs:912 (Strawberry)
                    p.terrainSpeeds[1] = S(1f, 1f, 1f, 1f);
                    p.terrainSpeeds[2] = S(1.2f, 1f, 0.8f, 1f);
                    p.terrainSpeeds[3] = S(0.5f, 1f, 0.75f, 1f);
                    p.biteDelay = 18;
                    p.biteInFront = 15f;
                    p.biteHomingSpeed = 4f;
                    p.biteChance = 0.31f;
                    p.attemptBiteRadius = 80f;
                    p.getFreeBiteChance = 0.85f;
                    p.biteDamage = 0.18f;
                    p.biteDamageChance = 0.65f;
                    p.toughness = 0.7f;
                    p.stunToughness = 0.7f;
                    p.regainFootingCounter = 70;
                    p.baseSpeed = 5.1f;
                    p.bodyMass = 1.4f;
                    p.bodySizeFac = 0.74f;
                    p.floorLeverage = 0.25f;
                    p.maxMusclePower = 9f;
                    p.wiggleSpeed = 0.8f;
                    p.wiggleDelay = 7;
                    p.bodyStiffnes = 0.32f;
                    p.swimSpeed = 0.3f;
                    p.idleCounterSubtractWhenCloseToIdlePos = 0;
                    p.danger = 0.8f;
                    p.aggressionCurveExponent = 0.37f;
                    p.headShieldAngle = 100f;
                    p.canExitLounge = true;
                    p.canExitLoungeWarmUp = false;
                    p.findLoungeDirection = 0.5f;
                    p.loungeDistance = 60f;
                    p.preLoungeCrouch = 25;
                    p.preLoungeCrouchMovement = -0.7f;
                    p.loungeSpeed = 3.1f;
                    p.loungeMaximumFrames = 24;
                    p.loungePropulsionFrames = 5;
                    p.loungeJumpyness = 0.5f;
                    p.loungeDelay = 90;
                    p.riskOfDoubleLoungeDelay = 0.4f;
                    p.postLoungeStun = 20;
                    p.loungeTendensy = 0.1f;
                    p.perfectVisionAngle = Mathf.Lerp(1f, -1f, 1f / 12f);
                    p.periferalVisionAngle = Mathf.Lerp(1f, -1f, 19f / 36f);
                    p.biteDominance = 0.15f;
                    p.limbSize = 0.9f;
                    p.limbThickness = 1f;
                    p.stepLength = 0.8f;
                    p.liftFeet = 0.6f;
                    p.feetDown = 0.2f;
                    p.noGripSpeed = 0.1f;
                    p.limbSpeed = 9f;
                    p.limbQuickness = 0.2f;
                    p.limbGripDelay = 3;
                    p.smoothenLegMovement = true;
                    p.legPairDisplacement = 0.8f;
                    p.standardColor = new Color(0.95f, 0.73f, 0.73f);
                    p.walkBob = 9f;
                    p.tailSegments = 4;
                    p.tailStiffness = 20f;
                    p.tailStiffnessDecline = 0.2f;
                    p.tailLengthFactor = 1.8f;
                    p.tailColorationStart = 0.8f;
                    p.tailColorationExponent = 2f;
                    p.headSize = 0.9f;
                    p.neckStiffness = 0.2f;
                    p.jawOpenAngle = 80f;
                    p.jawOpenLowerJawFac = 2f / 3f;
                    p.jawOpenMoveJawsApart = 23f;
                    p.headGraphics = new int[5];
                    p.framesBetweenLookFocusChange = 130;
                    p.tamingDifficulty = 2f;
                    p.tongue = true;
                    p.tongueAttackRange = 440f;
                    p.tongueWarmUp = 140;
                    p.tongueSegments = 10;
                    p.tongueChance = 0.3f;
                    return p;
                default:
                    return null;
            }
        }

        // The per-breed if-blocks of BreedTemplate, template values only.
        private static Breed BreedData(Type type)
        {
            switch (type)
            {
                case Type.PinkLizard:
                    return new Breed
                    {
                        waterPathingResistance = 3f,
                        visualRadius = 900f, waterVision = 0.4f, throughSurfaceVision = 0.85f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.Climb, 2.5f).C(Move.DropToClimb, 40f);
                case Type.GreenLizard:
                    return new Breed
                    {
                        waterPathingResistance = 1.5f,
                        visualRadius = 850f, waterVision = 0.3f, throughSurfaceVision = 0.85f, movementBasedVision = 0.7f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 2f).C(Move.DropToFloor, 40f).C(Move.LizardTurn, 60f);
                case Type.BlueLizard:
                    return new Breed
                    {
                        waterPathingResistance = 5f,
                        visualRadius = 950f, waterVision = 0.4f, throughSurfaceVision = 0.85f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 1.2f).T(Acc.Climb, 0.8f).T(Acc.Wall, 1f).T(Acc.Ceiling, 1.2f)
                        .C(Move.DropToFloor, 20f).C(Move.DropToClimb, 2f).C(Move.ShortCut, 15f).C(Move.ReachOverGap, 1.1f)
                        .C(Move.ReachUp, 1.1f).C(Move.ReachDown, 1.1f).C(Move.CeilingSlope, 2f);
                case Type.YellowLizard:
                    return new Breed
                    {
                        waterPathingResistance = 2.5f, pathsLikePink = true,
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
                        waterPathingResistance = 2f, pathsLikePink = true,
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
                        waterPathingResistance = 1.5f,
                        visualRadius = 2300f, waterVision = 0.7f, throughSurfaceVision = 0.95f,
                    }.T(Acc.Floor, 1f).T(Acc.Corridor, 2f).C(Move.DropToFloor, 40f).C(Move.LizardTurn, 60f);
                case Type.ZoopLizard:
                    return new Breed
                    {
                        waterPathingResistance = 6.5f, pathsLikePink = true,
                        visualRadius = 1400f, waterVision = 0.25f, throughSurfaceVision = 0.55f,
                    }.T(Acc.Floor, 4f).T(Acc.Corridor, 1f).T(Acc.Climb, 0.2f).C(Move.DropToFloor, 10f).C(Move.DropToClimb, 0.2f);
                default:
                    throw new System.ArgumentException(type + " is not a lizard breed.", nameof(type));
            }
        }
    }
}
