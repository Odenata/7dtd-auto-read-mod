using System;
using Xunit;

namespace AutoReadMod.Tests
{
    public class InventoryAddModeTests
    {
        [Fact]
        public void ProcessContainer_WhenReadOnInventoryAddFalse_StillReads()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
            try
            {
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadOnInventoryAdd = false,
                    ReadBooks = true,
                    ReadMagazines = true,
                    ReadSchematics = true,
                });
                GameManager.Instance = new GameManager();

                var stack = CreateReadable("bookFiremansAlmanacHeat", 1);
                var container = new TileEntityLootContainer { items = new[] { stack } };
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                AutoReader.ProcessContainer(player, container);

                Assert.Equal(0, stack.count);
            }
            finally
            {
                GameManager.Instance = originalGm;
                ModConfig.Reload();
            }
        }

        [Fact]
        public void ProcessContainer_WhenReadOnInventoryAddTrue_LeavesLootUntouched()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
            try
            {
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadOnInventoryAdd = true,
                    ReadBooks = true,
                    ReadMagazines = true,
                    ReadSchematics = true,
                });
                GameManager.Instance = new GameManager();

                var stack = CreateReadable("bookFiremansAlmanacHeat", 2);
                var container = new TileEntityLootContainer { items = new[] { stack } };
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                AutoReader.ProcessContainer(player, container);

                Assert.Equal(2, stack.count);
            }
            finally
            {
                GameManager.Instance = originalGm;
                ModConfig.Reload();
            }
        }

        [Fact]
        public void ProcessEntityLootBag_WhenReadOnInventoryAddTrue_LeavesBagUntouched()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
            try
            {
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadOnInventoryAdd = true,
                    ReadBooks = true,
                    ReadMagazines = true,
                    ReadSchematics = true,
                });
                GameManager.Instance = new GameManager();

                var stack = CreateReadable("magazineGuns", 3);
                var bag = new Bag();
                bag.SetSlots(new[] { stack });
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory(), bag = bag };

                AutoReader.ProcessEntityLootBag(player, bag);

                Assert.Equal(3, stack.count);
            }
            finally
            {
                GameManager.Instance = originalGm;
                ModConfig.Reload();
            }
        }

        [Fact]
        public void TryAutoReadInventoryIngress_WhenModeOn_ReadsFromPlayerInventory()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
            try
            {
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadOnInventoryAdd = true,
                    ReadBooks = true,
                    ReadMagazines = true,
                    ReadSchematics = true,
                });
                InventoryAddPatchShared.ResetDebounceForTest();
                GameManager.Instance = new GameManager();

                var live = CreateReadable("magazineGuns", 2);
                var inventory = new Inventory();
                inventory.SetSlots(new[] { live }, true);
                var player = new EntityPlayer { EntityName = "p", inventory = inventory };
                GameManager.Instance.myEntityPlayerLocal = player;

                var hint = CreateReadable("magazineGuns", 0);
                hint.itemValue = live.itemValue;

                bool ok = AutoReader.TryAutoReadInventoryIngress(player, hint, preferredSlot: 0, acceptedCount: 2);

                Assert.True(ok);
                Assert.Equal(0, live.count);
            }
            finally
            {
                GameManager.Instance = originalGm;
                InventoryAddPatchShared.ResetDebounceForTest();
                ModConfig.Reload();
            }
        }

        [Fact]
        public void BundleSnapshotPath_WhenReadOnInventoryAdd_DoesNotDoubleProcess()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
            try
            {
                SetConfig(new ModConfig
                {
                    Enabled = true,
                    ReadOnInventoryAdd = true,
                    ReadBooks = true,
                    ReadMagazines = true,
                    ReadSchematics = true,
                });
                InventoryAddPatchShared.ResetDebounceForTest();
                GameManager.Instance = new GameManager();

                var inventory = new Inventory();
                inventory.SetSlots(new[] { ItemStack.Empty, ItemStack.Empty }, true);
                var player = new EntityPlayer { EntityName = "p", inventory = inventory };
                GameManager.Instance.myEntityPlayerLocal = player;

                var snapshot = BundleOpenPatchShared.CaptureInventorySnapshot(player);
                Assert.Null(snapshot);

                var added = CreateReadable("bookFiremansAlmanacHeat", 1);
                inventory.SetItem(0, added);

                // Bundle path must no-op; inventory ingress owns the read once.
                BundleOpenPatchShared.ApplyOpenedBundleEffectsImmediately(player, new InventorySnapshot(
                    InventorySlotSnapshotCollection.Capture(new[] { ItemStack.Empty, ItemStack.Empty }),
                    InventorySlotSnapshotCollection.Capture(null)));
                Assert.Equal(1, added.count);

                bool ok = AutoReader.TryAutoReadInventoryIngress(player, added, preferredSlot: 0, acceptedCount: 1);
                Assert.True(ok);
                Assert.Equal(0, added.count);

                // Second ingress same frame/slot/count is debounced
                inventory.SetItem(0, CreateReadable("bookFiremansAlmanacHeat", 1));
                bool again = AutoReader.TryAutoReadInventoryIngress(
                    player,
                    inventory.GetItemStack(0),
                    preferredSlot: 0,
                    acceptedCount: 1);
                Assert.False(again);
                Assert.Equal(1, inventory.GetItemStack(0).count);
            }
            finally
            {
                GameManager.Instance = originalGm;
                InventoryAddPatchShared.ResetDebounceForTest();
                ModConfig.Reload();
            }
        }

        private static ItemStack CreateReadable(string itemName, int count)
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
