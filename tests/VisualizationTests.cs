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
        public void ProcessOpenedBundle_OnSuccessfulRead_EnqueuesEventWithSourceBundle()
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

                var inventory = CreateInventory(
                    CreateStack("bookFiremansAlmanacHeat", 3),
                    CreateStack("resourceWood", 50));
                var player = CreatePlayer(inventory);
                var snapshot = BundleOpenPatchShared.CaptureInventorySnapshot(player);

                inventory.SetItem(0, CreateStack("bookFiremansAlmanacHeat", 5));

                BundleOpenPatchShared.ProcessOpenedBundle(player, true, snapshot);

                var events = AutoReadVisualizationQueue.DequeueAllForTest();
                Assert.NotEmpty(events);
                var bundleEvent = events.Find(e => e.Source == AutoReadVisualizationSource.Bundle && e.ItemClassName == "bookFiremansAlmanacHeat");
                Assert.NotNull(bundleEvent);
                Assert.Equal(2, bundleEvent.Count);
                Assert.Equal("bookFiremansAlmanacHeat", bundleEvent.ItemClassName);
                Assert.Equal(AutoReader.ReadableItemKind.Book, bundleEvent.Kind);
                Assert.NotNull(bundleEvent.ItemValue);
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

        private static void SetConfig(ModConfig config)
        {
            typeof(ModConfig)
                .GetField("instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .SetValue(null, config);
        }
    }
}
