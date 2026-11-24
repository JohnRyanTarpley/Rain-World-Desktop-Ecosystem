namespace RWDE.Sim
{
    /// <summary>Base of per-breed parameter sets hung on a template (the game's empty <c>BreedParameters</c>).</summary>
    public class BreedParameters
    {
    }

    /// <summary>
    /// What makes one lizard breed differ from another, ported from the game's <c>LizardBreedParams</c>:
    /// speeds per terrain, bite, lunge, vision cone, tongue, and the body and graphics proportions.
    /// One instance per breed, built by <see cref="LizardBreeds"/> and shared through
    /// <see cref="CreatureTemplate.breedParameters"/>. Field names are the game's.
    /// </summary>
    public sealed class LizardBreedParams : BreedParameters
    {
        /// <summary>Speed factors on one kind of terrain (<c>LizardBreedParams.SpeedMultiplier</c>).</summary>
        public struct SpeedMultiplier
        {
            public float speed;
            public float horizontal;
            public float up;
            public float down;

            public SpeedMultiplier(float speed, float horizontal, float up, float down)
            {
                this.speed = speed;
                this.horizontal = horizontal;
                this.up = up;
                this.down = down;
            }

            public static SpeedMultiplier operator +(SpeedMultiplier a, SpeedMultiplier b) =>
                new SpeedMultiplier(a.speed + b.speed, a.horizontal + b.horizontal, a.up + b.up, a.down + b.down);

            public static SpeedMultiplier operator /(SpeedMultiplier a, float b) =>
                new SpeedMultiplier(a.speed / b, a.horizontal / b, a.up / b, a.down / b);
        }

        public LizardBreedParams(CreatureTemplate.Type template)
        {
            this.template = template;
        }

        public CreatureTemplate.Type template;

        public float toughness;
        public float stunToughness;
        public float biteDamage;
        public float biteDamageChance;
        public float aggressionCurveExponent = 0.925f;
        public float danger;
        public int biteDelay;
        public float baseSpeed;
        public float biteInFront;
        public float biteRadBonus;
        public float biteHomingSpeed;
        public float biteChance;
        public float attemptBiteRadius;
        public float getFreeBiteChance;
        public SpeedMultiplier baseSpeedMultiplier;
        public Color standardColor;
        public int regainFootingCounter;
        public float bodyMass;
        public float bodySizeFac;
        public float bodyLengthFac;
        public float bodyRadFac;
        public float pullDownFac;
        public float floorLeverage;
        public float maxMusclePower;
        /// <summary>Indexed by <see cref="AItile.Accessibility"/>; see <see cref="TerrainSpeed"/>.</summary>
        public SpeedMultiplier[] terrainSpeeds;
        public float wiggleSpeed;
        public int wiggleDelay;
        public float bodyStiffnes;
        public float swimSpeed;
        public int idleCounterSubtractWhenCloseToIdlePos;
        public float headShieldAngle;
        public bool canExitLounge;
        public bool canExitLoungeWarmUp;
        public float findLoungeDirection;
        public float loungeDistance;
        public int preLoungeCrouch;
        public float preLoungeCrouchMovement;
        public float loungeSpeed;
        public int loungePropulsionFrames;
        public int loungeMaximumFrames;
        public float loungeJumpyness;
        public int loungeDelay;
        public float riskOfDoubleLoungeDelay;
        public int postLoungeStun;
        public float loungeTendensy;
        public float perfectVisionAngle;
        public float periferalVisionAngle;
        public int shakePrey = 100;
        public float biteDominance;

        // Limb, tail and head proportions: read by graphics only.
        public float limbSize;
        public float limbThickness;
        public float stepLength;
        public float liftFeet;
        public float feetDown;
        public float noGripSpeed;
        public float limbSpeed;
        public float limbQuickness;
        public int limbGripDelay;
        public bool smoothenLegMovement;
        public float legPairDisplacement;
        public float walkBob;
        public int tailSegments;
        public float tailStiffness;
        public float tailStiffnessDecline;
        public float tailLengthFactor;
        public float tailColorationStart;
        public float tailColorationExponent;
        public float headSize;
        public float neckStiffness;
        public float jawOpenAngle;
        public float jawOpenLowerJawFac;
        public float jawOpenMoveJawsApart;
        public int[] headGraphics;
        public int framesBetweenLookFocusChange;

        public bool tongue;
        public float tongueAttackRange;
        public int tongueWarmUp;
        public int tongueSegments;
        public float tongueChance;
        public float tamingDifficulty = 1f;

        /// <summary>Breeds that climb walls (<c>LizardBreedParams.WallClimber</c>): Blue, White and Eel.</summary>
        public bool WallClimber =>
            template == CreatureTemplate.Type.EelLizard || template == CreatureTemplate.Type.BlueLizard || template == CreatureTemplate.Type.WhiteLizard;

        public SpeedMultiplier TerrainSpeed(AItile.Accessibility acc) => terrainSpeeds[(int)acc];
    }
}
