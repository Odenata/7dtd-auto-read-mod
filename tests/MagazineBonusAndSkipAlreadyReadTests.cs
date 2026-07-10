using System;
using Xunit;

namespace AutoReadMod.Tests
{
    public class MagazineBonusAndSkipAlreadyReadTests
    {
        public MagazineBonusAndSkipAlreadyReadTests()
        {
            AutoReader.ClearSyntheticReadableEffectsCacheForTest();
            GameReflection.ClearCraftingManagerCacheForTest();
            CraftingManager.Instance = new CraftingManager();
        }

        [Fact]
        public void TryResolveMagazineDoublePointChance_NerdOutfitCVar_Preferred()
        {
            var player = new EntityPlayer { EntityName = "p" };
            player.Buffs.AddBuff("buffNerdOutfit");
            player.SetCVar("$nerdOutfitSkillPointChance", 25f);
            player.SetCVar("$perkIntellectMasteryPointChance", 50f);

            Assert.True(AutoReader.IsEligibleForMagazineDoublePointBonusForTest(player));
            Assert.True(AutoReader.TryResolveMagazineDoublePointChanceForTest(
                player, out var chance, out var cvar));
            Assert.Equal(25f, chance);
            Assert.Equal("$nerdOutfitSkillPointChance", cvar);
        }

        [Fact]
        public void TryResolveMagazineDoublePointChance_IntMasteryOnly_UsesPerkCVar()
        {
            var player = new EntityPlayer { EntityName = "p" };
            var mastery = player.Progression.GetProgressionValue("perkIntellectMastery");
            mastery.Level = 3;
            mastery.level = 3;
            player.SetCVar("$perkIntellectMasteryPointChance", 50f);

            Assert.True(AutoReader.IsEligibleForMagazineDoublePointBonusForTest(player));
            Assert.True(AutoReader.TryResolveMagazineDoublePointChanceForTest(
                player, out var chance, out var cvar));
            Assert.Equal(50f, chance);
            Assert.Equal("$perkIntellectMasteryPointChance", cvar);
        }

        [Fact]
        public void TryResolveMagazineDoublePointChance_BothPresent_PrefersCombinedCVar()
        {
            var player = new EntityPlayer { EntityName = "p" };
            player.Buffs.AddBuff("buffNerdOutfit");
            var mastery = player.Progression.GetProgressionValue("perkIntellectMastery");
            mastery.Level = 3;
            mastery.level = 3;
            // Vanilla merges both into the combined CVar (e.g. 25 + 50).
            player.SetCVar("$nerdOutfitSkillPointChance", 75f);
            player.SetCVar("$perkIntellectMasteryPointChance", 50f);

            Assert.True(AutoReader.TryResolveMagazineDoublePointChanceForTest(
                player, out var chance, out var cvar));
            Assert.Equal(75f, chance);
            Assert.Equal("$nerdOutfitSkillPointChance", cvar);
        }

        [Fact]
        public void ApplyMagazineDoublePointBonus_IntOnly_GrantsExtraPoint()
        {
            var player = new EntityPlayer { EntityName = "p" };
            var mastery = player.Progression.GetProgressionValue("perkIntellectMastery");
            mastery.Level = 3;
            mastery.level = 3;
            player.SetCVar("$perkIntellectMasteryPointChance", 100f);
            var skill = player.Progression.GetProgressionValue("craftingRepair");
            skill.Level = 10;
            skill.level = 10;

            AutoReader.ApplyMagazineDoublePointBonusForTest(
                player,
                new[] { "craftingRepair" },
                consumedUnits: 1,
                randomSeed: 0);

            Assert.Equal(11, player.Progression.GetProgressionValue("craftingRepair").Level);
        }

        [Fact]
        public void SkipAlreadyRead_MagazineAtMax_ConsumesNothing()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadMagazines = true,
                    SkipAlreadyRead = true,
                });

                var gainSkill = new ItemActionGainSkill { SkillsToGain = new[] { "craftingRepair" } };
                var stack = CreateStack("magazineRepair", 3, new ItemActionEat(), gainSkill);
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                var skill = player.Progression.GetProgressionValue("craftingRepair");
                skill.Level = 100;
                skill.level = 100;

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 3);

                Assert.Equal(0, n);
                Assert.Equal(3, stack.count);
            }
            finally
            {
                GameManager.Instance = originalGm;
                RestoreConfig(originalConfig);
            }
        }

        [Fact]
        public void SkipAlreadyRead_MagazinePartialStack_ConsumesUntilMax()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadMagazines = true,
                    SkipAlreadyRead = true,
                });

                var gainSkill = new ItemActionGainSkill { SkillsToGain = new[] { "craftingRepair" } };
                var stack = CreateStack("magazineRepair", 5, new ItemActionEat(), gainSkill);
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                var skill = player.Progression.GetProgressionValue("craftingRepair");
                skill.Level = 98;
                skill.level = 98;

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 5);

                Assert.Equal(2, n);
                Assert.Equal(3, stack.count);
                Assert.Equal(100, player.Progression.GetProgressionValue("craftingRepair").Level);
            }
            finally
            {
                GameManager.Instance = originalGm;
                RestoreConfig(originalConfig);
            }
        }

        [Fact]
        public void SkipAlreadyRead_KnownSchematic_ConsumesNothing()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                CraftingManager.Instance.UnlockedRecipeList.Add("gunHandgunT1Pistol");
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadSchematics = true,
                    SkipAlreadyRead = true,
                });

                var learn = new ItemActionLearnRecipe
                {
                    RecipesToLearn = new[] { "gunHandgunT1Pistol" },
                };
                var stack = CreateStack("gunHandgunT1PistolSchematic", 2, new ItemActionEat(), learn);
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 2);

                Assert.Equal(0, n);
                Assert.Equal(2, stack.count);
            }
            finally
            {
                GameManager.Instance = originalGm;
                RestoreConfig(originalConfig);
            }
        }

        [Fact]
        public void SkipAlreadyRead_KnownCVarSchematic_ConsumesNothing()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadSchematics = true,
                    SkipAlreadyRead = true,
                });

                AutoReader.SetSyntheticReadableEffectsForTest(
                    "modGunBarrelExtenderSchematic",
                    setCVarNames: new[] { "modGunBarrelExtender" });

                // Eat-only schematic (vanilla layout) — no ItemActionLearnRecipe.
                var stack = CreateStack("modGunBarrelExtenderSchematic", 2, new ItemActionEat());
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                player.SetCVar("modGunBarrelExtender", 1f);

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 2);

                Assert.Equal(0, n);
                Assert.Equal(2, stack.count);
            }
            finally
            {
                AutoReader.ClearSyntheticReadableEffectsCacheForTest();
                GameManager.Instance = originalGm;
                RestoreConfig(originalConfig);
            }
        }

        [Fact]
        public void SkipAlreadyRead_BookPerkAtMax_ConsumesNothing()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadBooks = true,
                    SkipAlreadyRead = true,
                });

                AutoReader.SetSyntheticReadableEffectsForTest(
                    "bookFiremansAlmanacHeat",
                    maxProgressionNames: new[] { "perkFiremansAlmanacHeat" });

                var stack = CreateBookStack(2);
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                var perk = player.Progression.GetProgressionValue("perkFiremansAlmanacHeat");
                perk.Level = 100;
                perk.level = 100;

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 2);

                Assert.Equal(0, n);
                Assert.Equal(2, stack.count);
            }
            finally
            {
                AutoReader.ClearSyntheticReadableEffectsCacheForTest();
                GameManager.Instance = originalGm;
                RestoreConfig(originalConfig);
            }
        }

        [Fact]
        public void SkipAlreadyRead_BookPerkLevelOneWithUnknownMax_ConsumesNothing()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadBooks = true,
                    SkipAlreadyRead = true,
                });

                AutoReader.SetSyntheticReadableEffectsForTest(
                    "bookFiremansAlmanacHeat",
                    maxProgressionNames: new[] { "perkFiremansAlmanacHeat" });

                var stack = CreateBookStack(1);
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                var perk = player.Progression.GetProgressionValue("perkFiremansAlmanacHeat");
                perk.Level = 1;
                perk.level = 1;
                perk.MaxLevelOverride = 0; // unresolved max → book skip uses level ≥ 1

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 1);

                Assert.Equal(0, n);
                Assert.Equal(1, stack.count);
            }
            finally
            {
                AutoReader.ClearSyntheticReadableEffectsCacheForTest();
                GameManager.Instance = originalGm;
                RestoreConfig(originalConfig);
            }
        }

        [Fact]
        public void SkipAlreadyRead_False_StillConsumesMaxedMagazine()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadMagazines = true,
                    SkipAlreadyRead = false,
                });

                var gainSkill = new ItemActionGainSkill { SkillsToGain = new[] { "craftingRepair" } };
                var stack = CreateStack("magazineRepair", 2, new ItemActionEat(), gainSkill);
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                var skill = player.Progression.GetProgressionValue("craftingRepair");
                skill.Level = 100;
                skill.level = 100;

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 2);

                Assert.Equal(2, n);
                Assert.Equal(0, stack.count);
            }
            finally
            {
                GameManager.Instance = originalGm;
                RestoreConfig(originalConfig);
            }
        }

        private static ItemStack CreateBookStack(int count)
        {
            return new ItemStack(
                new ItemValue("bookTest".GetHashCode())
                {
                    ItemClass = new ItemClass
                    {
                        Name = "bookFiremansAlmanacHeat",
                        Actions = new ItemActionBase[] { new ItemActionEat() },
                    },
                },
                count);
        }

        private static ItemStack CreateStack(string itemName, int count, params ItemActionBase[] actions)
        {
            return new ItemStack(
                new ItemValue(itemName.GetHashCode())
                {
                    ItemClass = new ItemClass
                    {
                        Name = itemName,
                        Actions = actions == null || actions.Length == 0
                            ? new ItemActionBase[] { new ItemActionEat() }
                            : actions,
                    },
                },
                count);
        }

        private static void SetConfig(ModConfig config)
        {
            typeof(ModConfig)
                .GetField("instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .SetValue(null, config);
        }

        private static void RestoreConfig(ModConfig original)
        {
            if (original != null)
            {
                SetConfig(original);
            }
            else
            {
                ModConfig.Reload();
            }
        }
    }
}
