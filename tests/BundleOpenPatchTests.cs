using System;
using System.Collections.Generic;
using Xunit;

namespace AutoReadMod.Tests
{
    public class BundleOpenPatchTests
    {
        [Fact]
        public void ClassifyReadableItemName_ExcludesBundlesButRecognizesReadableOutputs()
        {
            Assert.Equal(AutoReader.ReadableItemKind.None, AutoReader.ClassifyReadableItemName("bookBundle"));
            Assert.Equal(AutoReader.ReadableItemKind.None, AutoReader.ClassifyReadableItemName("magazineBundle"));
            Assert.Equal(AutoReader.ReadableItemKind.Book, AutoReader.ClassifyReadableItemName("bookFiremansAlmanacHeat"));
            Assert.Equal(AutoReader.ReadableItemKind.Magazine, AutoReader.ClassifyReadableItemName("magazineGuns"));
            Assert.Equal(AutoReader.ReadableItemKind.Schematic, AutoReader.ClassifyReadableItemName("modGunBarrelExtenderSchematic"));
            Assert.Equal(AutoReader.ReadableItemKind.Recipe, AutoReader.ClassifyReadableItemName("foodCanShamRecipe"));
            // Schematic/recipe checked before magazine so names like gunMagazineSchematic classify as Schematic, not Magazine.
            Assert.Equal(AutoReader.ReadableItemKind.Schematic, AutoReader.ClassifyReadableItemName("gunMagazineSchematic"));
        }

        [Fact]
        public void ShouldAutoReadItem_UsesIndependentBookAndMagazineFlags()
        {
            var originalConfig = ModConfig.Instance;
            try
            {
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadBooks = false,
                    ReadMagazines = true,
                    ReadSchematics = true,
                });

                Assert.False(AutoReader.ShouldAutoReadItem(CreateStack("bookFiremansAlmanacHeat", 1)));
                Assert.True(AutoReader.ShouldAutoReadItem(CreateStack("magazineGuns", 1)));
            }
            finally
            {
                ModConfig.Reload();
            }
        }

        [Fact]
        public void FindReadableSlotIndices_SkipsBundleItemsAndFindsReadableOutputs()
        {
            var items = new[]
            {
                CreateStack("bookBundle", 1),
                CreateStack("bookFiremansAlmanacHeat", 1),
                CreateStack("resourceWood", 50),
                CreateStack("magazineGuns", 2),
            };

            var readableSlots = AutoReader.FindReadableSlotIndices(items);

            Assert.Equal(new List<int> { 1, 3 }, readableSlots);
        }

        [Fact]
        public void FindAddedReadableSlotCounts_TracksNewReadableOutputsOnly()
        {
            var inventory = CreateInventory(
                CreateStack("bookBundle", 1),
                ItemStack.Empty,
                CreateStack("resourceWood", 50));
            var snapshot = InventorySlotSnapshotCollection.Capture(inventory.GetSlots());

            inventory.SetItem(0, ItemStack.Empty);
            inventory.SetItem(1, CreateStack("magazineGuns", 2));
            inventory.SetItem(2, CreateStack("resourceWood", 55));

            var addedCounts = BundleOpenPatchShared.FindAddedReadableSlotCounts(snapshot, inventory.GetSlots());

            Assert.True(addedCounts.TryGetValue(1, out var readCount));
            Assert.Equal(2, readCount);
            Assert.DoesNotContain(2, addedCounts.Keys);
        }

        [Fact]
        public void ProcessOpenedBundle_WhenReadableItemsMergeIntoExistingStack_OnlyConsumesAddedCount()
        {
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

                Assert.Equal(3, inventory.GetItemStack(0).count);
            }
            finally
            {
                GameManager.Instance = originalGameManager;
                ModConfig.Reload();
            }
        }

        [Fact]
        public void ProcessOpenedBundle_LeavesReadableOutputsWhenCategoryDisabled()
        {
            var originalConfig = ModConfig.Instance;
            var originalGameManager = GameManager.Instance;

            try
            {
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadBooks = true,
                    ReadMagazines = false,
                    ReadSchematics = true,
                });
                GameManager.Instance = new GameManager();

                var inventory = CreateInventory(CreateStack("magazineBundle", 1), ItemStack.Empty);
                var player = CreatePlayer(inventory);
                var snapshot = BundleOpenPatchShared.CaptureInventorySnapshot(player);

                inventory.SetItem(0, ItemStack.Empty);
                inventory.SetItem(1, CreateStack("magazineGuns", 2));

                BundleOpenPatchShared.ProcessOpenedBundle(player, true, snapshot);

                Assert.Equal(2, inventory.GetItemStack(1).count);
            }
            finally
            {
                GameManager.Instance = originalGameManager;
                ModConfig.Reload();
            }
        }

        [Fact]
        public void ProcessOpenedBundle_ReadsReadableOutputsFromBackpack()
        {
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

                var inventory = CreateInventory();
                var bag = CreateBag(CreateStack("magazineBundle", 1), ItemStack.Empty);
                var player = CreatePlayer(inventory, bag);
                var snapshot = BundleOpenPatchShared.CaptureInventorySnapshot(player);

                bag.SetSlot(0, ItemStack.Empty, true);
                bag.SetSlot(1, CreateStack("magazineGuns", 2), true);

                BundleOpenPatchShared.ProcessOpenedBundle(player, true, snapshot);

                Assert.Equal(0, GameReflection.GetItemCount(GameReflection.GetBagItemStack(bag, 0)));
                Assert.Equal(0, GameReflection.GetItemCount(GameReflection.GetBagItemStack(bag, 1)));
            }
            finally
            {
                GameManager.Instance = originalGameManager;
                ModConfig.Reload();
            }
        }

        private static EntityPlayer CreatePlayer(Inventory inventory, Bag bag = null)
        {
            return new EntityPlayer
            {
                EntityName = "Test Player",
                bag = bag,
                inventory = inventory,
            };
        }

        private static Inventory CreateInventory(params ItemStack[] stacks)
        {
            var inventory = new Inventory();
            inventory.SetSlots(stacks, true);
            return inventory;
        }

        private static Bag CreateBag(params ItemStack[] stacks)
        {
            var bag = new Bag();
            bag.SetSlots(stacks);
            return bag;
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
                    }
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
