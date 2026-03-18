using System;
using System.Reflection;
using Xunit;

namespace AutoReadMod.Tests
{
    /// <summary>
    /// Behavioral tests aligned with vanilla ItemActionEat.ExecuteInstantAction (isHeldItem:false)
    /// and ContainerOpenPatchShared deferred loot timing.
    /// </summary>
    public class AutoReaderBehaviorTests
    {
        /// <summary>
        /// Contract vs decompiled Assembly-CSharp ItemActionEat (~168947); AutoReader invokes this via reflection.
        /// </summary>
        [Fact]
        public void ItemActionEat_ExecuteInstantAction_MatchesAutoReadModReflectionContract()
        {
            var method = typeof(ItemActionEat).GetMethod(
                "ExecuteInstantAction",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(EntityAlive), typeof(ItemStack), typeof(bool), typeof(XUiC_ItemStack) },
                null);
            Assert.NotNull(method);
            Assert.Equal(typeof(bool), method.ReturnType);
        }

        [Fact]
        public void ApplyReadableEffects_WhenExecuteInstantActionFails_ReturnsZero_PreservesCount()
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                var stack = CreateBookStack(3, simulateInstantFailure: true);
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 3);

                Assert.Equal(0, n);
                Assert.Equal(3, stack.count);
            }
            finally
            {
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void ApplyReadableEffects_MockEat_DecrementsStackPerSuccessfulRead()
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                var stack = CreateBookStack(2, simulateInstantFailure: false);
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 5);

                Assert.Equal(2, n);
                Assert.Equal(0, stack.count);
            }
            finally
            {
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void ProcessOpenedBundle_WasSuccessfulFalse_DoesNotConsumeReadableItems()
        {
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
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

                BundleOpenPatchShared.ProcessOpenedBundle(player, wasSuccessful: false, snapshot);

                Assert.Equal(5, inventory.GetItemStack(0).count);
            }
            finally
            {
                GameManager.Instance = originalGm;
                ModConfig.Reload();
            }
        }

        [Fact]
        public void ProcessDeferredContainer_WhenLootAppearsAfterManyEmptyFrames_StillAutoReads()
        {
            DrainQueue();
            var originalConfig = ModConfig.Instance;
            var originalGm = GameManager.Instance;
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

                var player = new EntityPlayer
                {
                    EntityName = "p",
                    inventory = new Inventory(),
                };
                var container = new TileEntityLootContainer(null)
                {
                    items = new ItemStack[4],
                };
                ContainerOpenPatchShared.SetDeferred(player, container);

                for (int i = 0; i < 20; i++)
                {
                    ContainerOpenPatchShared.ProcessDeferredContainer();
                }

                container.items[0] = CreateStack("bookFiremansAlmanacHeat", 1);
                ContainerOpenPatchShared.ProcessDeferredContainer();

                Assert.True(container.items[0] == null || container.items[0].IsEmpty());
                var events = AutoReadVisualizationQueue.DequeueAllForTest();
                Assert.Single(events);
                Assert.Equal(AutoReadVisualizationSource.Container, events[0].Source);
            }
            finally
            {
                GameManager.Instance = originalGm;
                ModConfig.Reload();
                DrainQueue();
            }
        }

        [Fact]
        public void ProcessDeferredContainer_StopsAfterMaxFramesWithoutLoot()
        {
            var originalConfig = ModConfig.Instance;
            try
            {
                SetConfig(new ModConfig { Enabled = true, ReadBooks = true });
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                var container = new TileEntityLootContainer(null)
                {
                    items = new ItemStack[2],
                };
                ContainerOpenPatchShared.SetDeferred(player, container);

                for (int i = 0; i < ContainerOpenPatchShared.MaxDeferredFrames + 1; i++)
                {
                    ContainerOpenPatchShared.ProcessDeferredContainer();
                }

                // Deferred cleared without processing; further calls are no-ops
                ContainerOpenPatchShared.ProcessDeferredContainer();
            }
            finally
            {
                ModConfig.Reload();
            }
        }

        private static void DrainQueue()
        {
            AutoReadVisualizationQueue.DequeueAllForTest();
        }

        private static ItemStack CreateBookStack(int count, bool simulateInstantFailure)
        {
            return new ItemStack(
                new ItemValue("bookTest".GetHashCode())
                {
                    ItemClass = new ItemClass
                    {
                        Name = "bookFiremansAlmanacHeat",
                        Actions = new ItemActionBase[]
                        {
                            new ItemActionEat { SimulateInstantActionFailure = simulateInstantFailure },
                        },
                    },
                },
                count);
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

        private static Inventory CreateInventory(params ItemStack[] stacks)
        {
            var inventory = new Inventory();
            inventory.SetSlots(stacks, true);
            return inventory;
        }

        private static EntityPlayer CreatePlayer(Inventory inventory)
        {
            return new EntityPlayer { EntityName = "Test", inventory = inventory };
        }

        private static void SetConfig(ModConfig config)
        {
            typeof(ModConfig)
                .GetField("instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .SetValue(null, config);
        }
    }
}
