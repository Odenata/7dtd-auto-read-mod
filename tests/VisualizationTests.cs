using System;
using System.Collections.Generic;
using Xunit;

namespace AutoReadMod.Tests
{
    public class VisualizationTests
    {
        [Fact]
        public void ProcessContainer_OnSuccessfulRead_EnqueuesOneEventWithSourceContainer()
        {
            DrainQueue();
            var originalConfig = ModConfig.Instance;
            var originalGameManager = GameManager.Instance;

            try
            {
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadBooks = true,
                    ReadMagazines = true,
                    ReadSchematics = true,
                });
                GameManager.Instance = new GameManager();

                var container = new TileEntityLootContainer(null)
                {
                    items = new ItemStack[] { CreateStack("bookFiremansAlmanacHeat", 1) },
                };
                var player = CreatePlayer();

                AutoReader.ProcessContainer(player, container);

                var events = AutoReadVisualizationQueue.DequeueAllForTest();
                Assert.Single(events);
                Assert.Equal(AutoReadVisualizationSource.Container, events[0].Source);
                Assert.Equal(1, events[0].Count);
                Assert.Contains("bookfiremansalmanacheat", events[0].ItemKey, StringComparison.OrdinalIgnoreCase);
                Assert.Equal("bookFiremansAlmanacHeat", events[0].ItemClassName);
                Assert.Equal(AutoReader.ReadableItemKind.Book, events[0].Kind);
                Assert.NotNull(events[0].ItemValue);
            }
            finally
            {
                GameManager.Instance = originalGameManager;
                ModConfig.Reload();
                DrainQueue();
            }
        }

        [Fact]
        public void ProcessContainer_WhenOneActionConsumesWholeStack_EnqueuesConsumedUnitCount()
        {
            DrainQueue();
            var originalConfig = ModConfig.Instance;
            var originalGameManager = GameManager.Instance;

            try
            {
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadBooks = true,
                    ReadMagazines = true,
                    ReadSchematics = true,
                });
                GameManager.Instance = new GameManager();

                var container = new TileEntityLootContainer(null)
                {
                    items = new ItemStack[] { CreateStack("magazineGuns", 2, new ItemActionEat { ConsumeEntireStack = true }) },
                };
                var player = CreatePlayer();

                AutoReader.ProcessContainer(player, container);

                var events = AutoReadVisualizationQueue.DequeueAllForTest();
                Assert.Single(events);
                Assert.Equal(AutoReadVisualizationSource.Container, events[0].Source);
                Assert.Equal(2, events[0].Count);
            }
            finally
            {
                GameManager.Instance = originalGameManager;
                ModConfig.Reload();
                DrainQueue();
            }
        }

        [Fact]
        public void ProcessOpenedBundle_EnqueuesDecorateOnlyBundleVisualization()
        {
            DrainQueue();
            var originalGameManager = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                var inventory = CreateInventory(
                    CreateStack("bookFiremansAlmanacHeat", 3),
                    CreateStack("resourceWood", 50));
                var player = CreatePlayer(inventory);
                var snapshot = BundleOpenPatchShared.CaptureInventorySnapshot(player);
                inventory.SetItem(0, CreateStack("bookFiremansAlmanacHeat", 5));
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadBooks = true,
                    ReadMagazines = true,
                    ReadSchematics = true,
                });
                BundleOpenPatchShared.ApplyOpenedBundleEffectsImmediately(player, snapshot);
                var events = AutoReadVisualizationQueue.DequeueAllForTest();
                var decorateEvt = events.Find(e =>
                    e.Source == AutoReadVisualizationSource.Bundle
                    && e.DecorateVanillaEntryOnly
                    && e.ItemClassName == "bookFiremansAlmanacHeat");
                Assert.NotNull(decorateEvt);
                Assert.Equal(2, decorateEvt.Count);
                Assert.Equal(AutoReader.ReadableItemKind.Book, decorateEvt.Kind);
                Assert.DoesNotContain(events, e =>
                    e.Source == AutoReadVisualizationSource.Bundle && !e.DecorateVanillaEntryOnly);
            }
            finally
            {
                GameManager.Instance = originalGameManager;
                ModConfig.Reload();
                DrainQueue();
            }
        }

        [Fact]
        public void VisualizationEvent_HasExpectedProperties()
        {
            var evt = new AutoReadVisualizationEvent(
                "0:booktest",
                "bookTest",
                "Book Test",
                "icon_book",
                2,
                AutoReadVisualizationSource.Container,
                AutoReader.ReadableItemKind.Book);
            Assert.Equal("0:booktest", evt.ItemKey);
            Assert.Equal("bookTest", evt.ItemClassName);
            Assert.Equal("Book Test", evt.LocalizedItemName);
            Assert.Equal("icon_book", evt.IconName);
            Assert.Equal(2, evt.Count);
            Assert.Equal(AutoReadVisualizationSource.Container, evt.Source);
            Assert.Equal(AutoReader.ReadableItemKind.Book, evt.Kind);
            Assert.Null(evt.ItemValue);
            Assert.False(evt.DecorateVanillaEntryOnly);
        }

        private static void DrainQueue()
        {
            while (AutoReadVisualizationQueue.DequeueAllForTest().Count > 0) { }
        }

        private static EntityPlayer CreatePlayer(Inventory inventory = null)
        {
            return new EntityPlayer
            {
                EntityName = "Test Player",
                inventory = inventory ?? CreateInventory(),
            };
        }

        private static Inventory CreateInventory(params ItemStack[] stacks)
        {
            var inventory = new Inventory();
            if (stacks != null && stacks.Length > 0)
            {
                inventory.SetSlots(stacks, true);
            }

            return inventory;
        }

        private static ItemStack CreateStack(string itemName, int count)
        {
            return CreateStack(itemName, count, new ItemActionEat());
        }

        private static ItemStack CreateStack(string itemName, int count, params ItemActionBase[] actions)
        {
            return new ItemStack(
                new ItemValue(itemName.GetHashCode())
                {
                    ItemClass = new ItemClass
                    {
                        Name = itemName,
                        Actions = actions,
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
    }
}
