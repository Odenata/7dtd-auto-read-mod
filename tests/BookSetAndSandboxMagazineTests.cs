using System;
using Xunit;

namespace AutoReadMod.Tests
{
    public class BookSetAndSandboxMagazineTests
    {
        private static readonly string[] FiremanSiblings =
        {
            "perkFiremansAlmanacHeat",
            "perkFiremansAlmanacAxes",
            "perkFiremansAlmanacSpeed",
            "perkFiremansAlmanacMolotov",
            "perkFiremansAlmanacPrevention",
            "perkFiremansAlmanacHarvest",
            "perkFiremansAlmanacEquipment",
        };

        public BookSetAndSandboxMagazineTests()
        {
            AutoReader.ClearSyntheticReadableEffectsCacheForTest();
            GameReflection.ClearCraftingManagerCacheForTest();
            CraftingManager.Instance = new CraftingManager();
        }

        [Fact]
        public void BookSetComplete_TryGrantDirectly_WhenSevenUnlocked()
        {
            InjectFiremanBookEffects("bookFiremansAlmanacHeat");
            var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
            UnlockFiremanSiblingsExcept(player, null);
            var itemClass = new ItemClass { Name = "bookFiremansAlmanacHeat" };

            AutoReader.TryGrantBookSetCompletionIfEligibleForTest(player, itemClass);

            Assert.True(player.Progression.GetProgressionValue("perkFiremansAlmanacComplete").Level >= 1);
        }

        [Fact]
        public void BookSetComplete_SeventhBookFromLoot_GrantsComplete()
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                InjectFiremanBookEffects("bookFiremansAlmanacHeat");

                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                UnlockFiremanSiblingsExcept(player, "perkFiremansAlmanacHeat");

                var stack = CreateBookStack("bookFiremansAlmanacHeat", 1);
                var lootGroup = new XUiC_LootWindowGroup();

                int n = AutoReader.ApplyReadableEffectsForTest(
                    player,
                    stack,
                    1,
                    itemSlotIndex: -1,
                    lootSlotController: null,
                    lootWindowGroup: lootGroup);

                Assert.Equal(1, n);
                Assert.True(player.Progression.GetProgressionValue("perkFiremansAlmanacHeat").Level >= 1);
                Assert.True(player.Progression.GetProgressionValue("perkFiremansAlmanacComplete").Level >= 1);
            }
            finally
            {
                AutoReader.ClearSyntheticReadableEffectsCacheForTest();
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void BookSetComplete_FirstBook_DoesNotGrantComplete()
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                InjectFiremanBookEffects("bookFiremansAlmanacHeat");

                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                var stack = CreateBookStack("bookFiremansAlmanacHeat", 1);
                var lootGroup = new XUiC_LootWindowGroup();

                int n = AutoReader.ApplyReadableEffectsForTest(
                    player,
                    stack,
                    1,
                    itemSlotIndex: -1,
                    lootSlotController: null,
                    lootWindowGroup: lootGroup);

                Assert.Equal(1, n);
                Assert.True(player.Progression.GetProgressionValue("perkFiremansAlmanacHeat").Level >= 1);
                Assert.Equal(0, player.Progression.GetProgressionValue("perkFiremansAlmanacComplete").Level);
            }
            finally
            {
                AutoReader.ClearSyntheticReadableEffectsCacheForTest();
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void BookSetComplete_RepairStuckSevenWithoutComplete()
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                InjectFiremanBookEffects("bookFiremansAlmanacAxes");

                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                UnlockFiremanSiblingsExcept(player, null);
                Assert.Equal(0, player.Progression.GetProgressionValue("perkFiremansAlmanacComplete").Level);

                var stack = CreateBookStack("bookFiremansAlmanacAxes", 1);
                var lootGroup = new XUiC_LootWindowGroup();

                int n = AutoReader.ApplyReadableEffectsForTest(
                    player,
                    stack,
                    1,
                    itemSlotIndex: -1,
                    lootSlotController: null,
                    lootWindowGroup: lootGroup);

                Assert.Equal(1, n);
                Assert.True(player.Progression.GetProgressionValue("perkFiremansAlmanacComplete").Level >= 1);
            }
            finally
            {
                AutoReader.ClearSyntheticReadableEffectsCacheForTest();
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void SkipAlreadyRead_StuckWithoutComplete_StillConsumesRepairBook()
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

                InjectFiremanBookEffects("bookFiremansAlmanacHeat");
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                UnlockFiremanSiblingsExcept(player, null);

                var stack = CreateBookStack("bookFiremansAlmanacHeat", 1);
                var lootGroup = new XUiC_LootWindowGroup();

                int n = AutoReader.ApplyReadableEffectsForTest(
                    player,
                    stack,
                    1,
                    itemSlotIndex: -1,
                    lootSlotController: null,
                    lootWindowGroup: lootGroup);

                Assert.Equal(1, n);
                Assert.True(player.Progression.GetProgressionValue("perkFiremansAlmanacComplete").Level >= 1);
            }
            finally
            {
                AutoReader.ClearSyntheticReadableEffectsCacheForTest();
                GameManager.Instance = originalGm;
                RestoreConfig(originalConfig);
            }
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(3, 13)]
        [InlineData(5, 15)]
        public void SandboxMagazinePoints_LootSyntheticGrant(int pointsPerMagazine, int expectedLevel)
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                AutoReader.SetMagazinePointsPerReadForTest(pointsPerMagazine);
                AutoReader.SetSyntheticReadableEffectsForTest(
                    "repairToolsSkillMagazine",
                    addProgressionNames: new[] { "craftingRepairTools" },
                    addProgressionXmlLevels: new[] { 1 });

                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                var skill = player.Progression.GetProgressionValue("craftingRepairTools");
                skill.Level = 10;
                skill.level = 10;

                var stack = CreateMagazineStack(1);
                var lootGroup = new XUiC_LootWindowGroup();

                int n = AutoReader.ApplyReadableEffectsForTest(
                    player,
                    stack,
                    1,
                    itemSlotIndex: -1,
                    lootSlotController: null,
                    lootWindowGroup: lootGroup);

                Assert.Equal(1, n);
                Assert.Equal(expectedLevel, player.Progression.GetProgressionValue("craftingRepairTools").Level);
            }
            finally
            {
                AutoReader.ClearSyntheticReadableEffectsCacheForTest();
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void SandboxMagazinePoints_BonusProcGrantsFullPointsPerRead()
        {
            try
            {
                AutoReader.SetMagazinePointsPerReadForTest(5);
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

                Assert.Equal(15, player.Progression.GetProgressionValue("craftingRepair").Level);
            }
            finally
            {
                AutoReader.ClearSyntheticReadableEffectsCacheForTest();
            }
        }

        private static void InjectFiremanBookEffects(string itemName)
        {
            AutoReader.SetSyntheticReadableEffectsForTest(
                itemName,
                maxProgressionNames: new[] { itemName.Replace("bookFiremansAlmanac", "perkFiremansAlmanac") },
                completeProgressionName: "perkFiremansAlmanacComplete",
                completeSkillName: "skillFiremansAlmanac",
                completeRequiredCount: 7,
                bookSetSiblingPerkNames: FiremanSiblings);
        }

        private static void UnlockFiremanSiblingsExcept(EntityPlayer player, string exceptPerk)
        {
            for (var i = 0; i < FiremanSiblings.Length; i++)
            {
                var perkName = FiremanSiblings[i];
                if (!string.IsNullOrEmpty(exceptPerk) &&
                    string.Equals(perkName, exceptPerk, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var perk = player.Progression.GetProgressionValue(perkName);
                perk.Level = 1;
                perk.level = 1;
            }
        }

        private static ItemStack CreateBookStack(string itemName, int count)
        {
            return new ItemStack(
                new ItemValue(itemName.GetHashCode())
                {
                    ItemClass = new ItemClass
                    {
                        Name = itemName,
                        Actions = new ItemActionBase[] { new ItemActionEat() },
                    },
                },
                count);
        }

        private static ItemStack CreateMagazineStack(int count)
        {
            return new ItemStack(
                new ItemValue("repairToolsSkillMagazine".GetHashCode())
                {
                    ItemClass = new ItemClass
                    {
                        Name = "repairToolsSkillMagazine",
                        Actions = new ItemActionBase[] { new ItemActionEat() },
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
