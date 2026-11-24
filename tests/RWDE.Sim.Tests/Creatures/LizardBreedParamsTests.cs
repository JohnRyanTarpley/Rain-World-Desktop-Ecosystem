using NUnit.Framework;
using Acc = RWDE.Sim.AItile.Accessibility;
using Type = RWDE.Sim.CreatureTemplate.Type;

namespace RWDE.Sim.Tests.Creatures
{
    // Expected values are read off the game's LizardBreeds.BreedTemplate per-breed blocks.
    public class LizardBreedParamsTests
    {
        private static readonly StaticWorld World = StaticWorld.Init(StaticWorld.Dlc.All);

        private static readonly Type[] Ported =
        {
            Type.GreenLizard, Type.PinkLizard, Type.BlueLizard, Type.YellowLizard, Type.RedLizard, Type.SpitLizard, Type.ZoopLizard
        };

        private static LizardBreedParams P(Type t) => (LizardBreedParams)World.GetCreatureTemplate(t).breedParameters;

        [Test]
        public void TheSevenBreedsHaveParams()
        {
            foreach (var t in Ported)
            {
                var p = P(t);
                Assert.IsNotNull(p, t.ToString());
                Assert.AreEqual(t, p.template);
                Assert.AreEqual(10, p.terrainSpeeds.Length);
                Assert.AreEqual(1f, p.bodyRadFac);
                Assert.AreEqual(1f, p.pullDownFac);
                Assert.AreEqual(1f, p.bodyLengthFac);
                Assert.AreEqual(100, p.shakePrey);
            }
        }

        [Test]
        public void BreedsNotPortedYetHaveNone()
        {
            Assert.IsNull(World.GetCreatureTemplate(Type.WhiteLizard).breedParameters);
            Assert.IsNull(World.GetCreatureTemplate(Type.CyanLizard).breedParameters);
            Assert.IsNull(World.GetCreatureTemplate(Type.Salamander).breedParameters);
        }

        [Test]
        public void Green()
        {
            var p = P(Type.GreenLizard);
            Assert.AreEqual(7.5f, p.bodyMass);
            Assert.AreEqual(6.7f, p.baseSpeed);
            Assert.AreEqual(2f, p.biteDamage);
            Assert.AreEqual(16f, p.maxMusclePower);
            Assert.AreEqual(1f, p.loungeTendensy);
            Assert.AreEqual(Mathf.Lerp(1f, -1f, 5f / 18f), p.periferalVisionAngle);
            Assert.AreEqual(new[] { 1, 1, 1, 1, 1 }, p.headGraphics);
            Assert.IsFalse(p.tongue);
            Assert.IsFalse(p.WallClimber);
        }

        [Test]
        public void Pink()
        {
            var p = P(Type.PinkLizard);
            Assert.AreEqual(new LizardBreedParams.SpeedMultiplier(1f, 1f, 1f, 1f), p.TerrainSpeed(Acc.Floor));
            Assert.AreEqual(new LizardBreedParams.SpeedMultiplier(1.2f, 1f, 0.8f, 1f), p.TerrainSpeed(Acc.Corridor));
            Assert.AreEqual(new LizardBreedParams.SpeedMultiplier(0.5f, 1f, 0.75f, 1f), p.TerrainSpeed(Acc.Climb));
            Assert.AreEqual(new LizardBreedParams.SpeedMultiplier(0.1f, 1f, 1f, 1f), p.TerrainSpeed(Acc.Wall), "unset terrain");
            Assert.AreEqual(2.1f, p.bodyMass);
            Assert.AreEqual(1f / 3f, p.biteDamageChance);
            Assert.AreEqual(0.9f, p.aggressionCurveExponent);
            Assert.AreEqual(new Color(1f, 0f, 1f), p.standardColor);
        }

        [Test]
        public void Blue()
        {
            var p = P(Type.BlueLizard);
            Assert.IsTrue(p.tongue);
            Assert.AreEqual(140f, p.tongueAttackRange);
            Assert.AreEqual(0.25f, p.tongueChance);
            Assert.AreEqual(new LizardBreedParams.SpeedMultiplier(0.6f, 1f, 1f, 1f), p.TerrainSpeed(Acc.Ceiling));
            Assert.AreEqual(4, p.regainFootingCounter);
            Assert.IsTrue(p.WallClimber);
        }

        [Test]
        public void Yellow()
        {
            var p = P(Type.YellowLizard);
            Assert.AreEqual(21, p.biteDelay);
            Assert.AreEqual(1f / 6f, p.biteChance);
            Assert.AreEqual(0.93f, p.bodySizeFac);
            Assert.AreEqual(0.77f, p.aggressionCurveExponent);
            Assert.AreEqual(3f, p.tamingDifficulty);
        }

        [Test]
        public void RedIsAnAlphaRedUnderRemixDefaults()
        {
            var p = P(Type.RedLizard);
            Assert.AreEqual(3f, p.toughness);
            Assert.AreEqual(4f, p.biteDamage);
            Assert.AreEqual(1, p.regainFootingCounter);
            Assert.AreEqual(10, p.idleCounterSubtractWhenCloseToIdlePos);
            Assert.AreEqual(5f, p.biteRadBonus);
            Assert.IsTrue(p.tongue);
            Assert.AreEqual(350f, p.tongueAttackRange);
            Assert.AreEqual(8, p.tongueWarmUp);
        }

        [Test]
        public void CaramelAndStrawberry()
        {
            var spit = P(Type.SpitLizard);
            Assert.AreEqual(0.65f, spit.baseSpeed);
            Assert.AreEqual(1.75f, spit.bodySizeFac);
            Assert.AreEqual(400f, spit.loungeDistance);
            Assert.AreEqual(0.45f, spit.biteDominance);
            Assert.AreEqual(0.46511626f, spit.biteChance);
            Assert.AreEqual(new LizardBreedParams.SpeedMultiplier(2f, 0.9f, 1.4f, 1.9f), spit.TerrainSpeed(Acc.CurvedFloor));
            Assert.AreEqual(new LizardBreedParams.SpeedMultiplier(0.1f, 1f, 1f, 1f), spit.TerrainSpeed(Acc.Corridor));

            var zoop = P(Type.ZoopLizard);
            Assert.IsTrue(zoop.tongue);
            Assert.AreEqual(440f, zoop.tongueAttackRange);
            Assert.AreEqual(140, zoop.tongueWarmUp);
            Assert.AreEqual(70, zoop.regainFootingCounter);
            Assert.AreEqual(0.37f, zoop.aggressionCurveExponent);
            Assert.AreEqual(3, zoop.limbGripDelay);
        }

        [Test]
        public void TemplatesTakeToughnessAndDangerFromTheParams()
        {
            // baseDamageResistance = toughness * 2, baseStunResistance = stunToughness, dangerousToPlayer = danger.
            var expected = new (Type t, float toughness, float danger)[]
            {
                (Type.PinkLizard, 1f, 0.45f), (Type.GreenLizard, 2.5f, 0.45f), (Type.BlueLizard, 0.5f, 0.35f),
                (Type.YellowLizard, 0.8f, 0.4f), (Type.RedLizard, 3f, 0.8f), (Type.SpitLizard, 2.5f, 0.8f), (Type.ZoopLizard, 0.7f, 0.8f),
            };
            foreach (var (t, toughness, danger) in expected)
            {
                var template = World.GetCreatureTemplate(t);
                Assert.AreEqual(toughness * 2f, template.baseDamageResistance, t.ToString());
                Assert.AreEqual(toughness, template.baseStunResistance, t.ToString());
                Assert.AreEqual(danger, template.dangerousToPlayer, t.ToString());
            }
        }

        [Test]
        public void SpeedMultiplierArithmetic()
        {
            var a = new LizardBreedParams.SpeedMultiplier(1f, 2f, 3f, 4f);
            var b = new LizardBreedParams.SpeedMultiplier(0.5f, 1f, 1.5f, 2f);
            Assert.AreEqual(new LizardBreedParams.SpeedMultiplier(1.5f, 3f, 4.5f, 6f), a + b);
            Assert.AreEqual(new LizardBreedParams.SpeedMultiplier(0.5f, 1f, 1.5f, 2f), a / 2f);
        }
    }
}
