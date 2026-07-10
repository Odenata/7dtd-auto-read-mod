using System;
using System.Reflection;
using Xunit;

namespace AutoReadMod.Tests
{
    /// <summary>
    /// Behavioral tests aligned with the live-stack read path. Production prefers vanilla
    /// ExecuteInstantAction and only reports success when the inventory stack actually moved.
    /// </summary>
    public class AutoReaderBehaviorTests
    {
        /// <summary>
        /// Contract: vanilla ItemActionEat declares consume(ItemActionData); AutoReader falls back to it when ExecuteAction is unavailable.
        /// </summary>
        [Fact]
        public void ItemActionEat_consume_MatchesAutoReadModReflectionContract()
        {
            var method = typeof(ItemActionEat).GetMethod(
                "consume",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(ItemActionData) },
                null);
            Assert.NotNull(method);
            Assert.Equal(typeof(void), method.ReturnType);
        }

        /// <summary>
        /// Contract: primary eat path uses ItemAction.ExecuteAction override with completion flag (same shape as ItemActionGainSkill).
        /// </summary>
        [Fact]
        public void ItemActionEat_ExecuteAction_MatchesAutoReadModReflectionContract()
        {
            var method = typeof(ItemActionEat).GetMethod(
                "ExecuteAction",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(ItemActionData), typeof(bool) },
                null);
            Assert.NotNull(method);
            Assert.Equal(typeof(void), method.ReturnType);
            Assert.True(method.IsVirtual);
        }

        /// <summary>
        /// Contract: ExecuteInstantAction exists on vanilla ItemActionEat (fallback when CreateModifierData paths fail).
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
        public void ApplyReadableEffects_WhenActionsDoNotConsume_DoesNotReportSilentSuccess()
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
        public void ApplyReadableEffects_MockEat_CountsSuccessfulReadIterations()
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
        public void ApplyReadableEffects_WhenInstantActionReturnsFalseButConsumes_CountsObservedConsumption()
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                var stack = CreateBookStack(2, action =>
                {
                    action.ReturnFalseAfterInstantAction = true;
                });
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 2);

                Assert.Equal(2, n);
                Assert.Equal(0, stack.count);
            }
            finally
            {
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void ApplyReadableEffects_WhenActionReportsSuccessButDoesNotConsume_ReturnsZero()
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                var stack = CreateBookStack(2, action =>
                {
                    action.SimulateNoDecrement = true;
                });
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 2);

                Assert.Equal(0, n);
                Assert.Equal(2, stack.count);
            }
            finally
            {
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void ApplyReadableEffects_MagazineWithGainSkill_ConsumesAndAppliesSkillUsingReadableEffectStack()
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                var gainSkill = new ItemActionGainSkill
                {
                    RequireNonEmptyStack = true,
                    RequireEventParms = true,
                    SkillsToGain = new[] { "craftingRepair" },
                };
                var stack = CreateStack("magazineGuns", 2, new ItemActionEat(), gainSkill);
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 2);

                Assert.Equal(2, n);
                Assert.Equal(0, stack.count);
                Assert.Equal(2, gainSkill.ExecuteActionCalls);
                Assert.Equal(2, gainSkill.AppliedSkillCalls);
                Assert.Equal(2, player.Progression.GetProgressionValue("craftingRepair").Level);
            }
            finally
            {
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void ApplyReadableEffects_WhenOneActionConsumesWholeStack_AppliesProgressionForEachConsumedUnit()
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                var gainSkill = new ItemActionGainSkill
                {
                    SkillsToGain = new[] { "craftingRepair" },
                };
                var stack = CreateStack(
                    "magazineGuns",
                    2,
                    new ItemActionEat { ConsumeEntireStack = true },
                    gainSkill);
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 2);

                Assert.Equal(2, n);
                Assert.Equal(0, stack.count);
                Assert.Equal(1, gainSkill.ExecuteActionCalls);
                Assert.Equal(2, player.Progression.GetProgressionValue("craftingRepair").Level);
            }
            finally
            {
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void ApplyReadableEffects_MagazineWithoutSkillsToGain_UsesCraftingSkillGroupProgression()
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                var stack = CreateStack("magazineRepair", 2, new ItemActionEat(), new ItemActionGainSkill());
                stack.itemValue.ItemClass.CraftingSkillGroup = "RepairTools";
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 2);

                Assert.Equal(2, n);
                Assert.Equal(2, player.Progression.GetProgressionValue("craftingrepairtools").Level);
            }
            finally
            {
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void ApplyReadableEffects_WhenStackConsumesWithoutGainAction_ReconcilesProgressionForAllConsumedUnits()
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                var stack = CreateStack("magazineRepair", 2, new ItemActionEat { ConsumeEntireStack = true });
                stack.itemValue.ItemClass.CraftingSkillGroup = "RepairTools";
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 2);

                Assert.Equal(2, n);
                Assert.Equal(0, stack.count);
                Assert.Equal(2, player.Progression.GetProgressionValue("craftingrepairtools").Level);
            }
            finally
            {
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void ApplyReadableEffects_QuickListReconcile_WhenVanillaAddsOneLevelForTwoConsumedItems_RaisesByTwo()
        {
            var originalGm = GameManager.Instance;
            try
            {
                GameManager.Instance = new GameManager();
                var eat = new ItemActionEat
                {
                    ConsumeEntireStack = true,
                    AfterInstantActionHook = ent =>
                    {
                        var p = ent as EntityPlayer;
                        if (p == null)
                        {
                            return;
                        }

                        var pv = p.Progression.GetProgressionValue("craftingrepairtools");
                        pv.Level += 1;
                    },
                };
                var stack = CreateStack("magazineRepair", 2, eat);
                stack.itemValue.ItemClass.CraftingSkillGroup = "RepairTools";
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                player.Progression.GetProgressionValue("craftingrepairtools").Level = 10;

                int n = AutoReader.ApplyReadableEffectsForTest(player, stack, 2);

                Assert.Equal(2, n);
                Assert.Equal(12, player.Progression.GetProgressionValue("craftingrepairtools").Level);
            }
            finally
            {
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void ApplyReadableEffects_WithSlotController_DoesNotManuallyFireItemEvents()
        {
            var originalGm = GameManager.Instance;
            var originalDiagnosticLine = GameReflection.DiagnosticLine;
            try
            {
                GameManager.Instance = new GameManager();
                var logs = new System.Collections.Generic.List<string>();
                GameReflection.DiagnosticLine = msg => logs.Add(msg);

                var stack = CreateStack("magazineRepair", 2, new ItemActionEat());
                stack.itemValue.ItemClass.CraftingSkillGroup = "RepairTools";
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };
                var controller = new XUiC_ItemStack { ItemStack = stack };

                int n = AutoReader.ApplyReadableEffectsForTest(
                    player,
                    stack,
                    2,
                    itemSlotIndex: 0,
                    lootSlotController: controller);

                Assert.Equal(2, n);
                Assert.Contains(logs, line => line.Contains("skipped manual FireStart; vanilla ExecuteInstantAction owns item events"));
                Assert.Contains(logs, line => line.Contains("skipped manual FireEnd; vanilla ExecuteInstantAction owns item events"));
                Assert.DoesNotContain(logs, line => line.Contains("TryFireOnSelfPrimaryActionEnd") && line.Contains("after vanilla read path"));
            }
            finally
            {
                GameManager.Instance = originalGm;
                GameReflection.DiagnosticLine = originalDiagnosticLine;
            }
        }

        [Fact]
        public void ApplyReadableEffects_WhenLocalEntityDiffers_UpdatesContextPlayerProgressionToo()
        {
            var originalGm = GameManager.Instance;
            try
            {
                var local = new EntityPlayer { EntityName = "local", inventory = new Inventory() };
                GameManager.Instance = new GameManager { myEntityPlayerLocal = local };
                var stack = CreateStack("magazineRepair", 1, new ItemActionEat(), new ItemActionGainSkill());
                stack.itemValue.ItemClass.CraftingSkillGroup = "RepairTools";
                var contextPlayer = new EntityPlayer { EntityName = "context", inventory = new Inventory() };

                int n = AutoReader.ApplyReadableEffectsForTest(contextPlayer, stack, 1);

                Assert.Equal(1, n);
                Assert.Equal(1, local.Progression.GetProgressionValue("craftingrepairtools").Level);
                Assert.Equal(1, contextPlayer.Progression.GetProgressionValue("craftingrepairtools").Level);
            }
            finally
            {
                GameManager.Instance = originalGm;
            }
        }

        [Fact]
        public void ProcessOpenedBundle_WasSuccessfulFalse_StillAutoReadsWhenInventoryShowsAdds()
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

                BundleOpenPatchShared.ProcessOpenedBundle(player, snapshot);

                // Diff-only gate: some game builds report ExecuteInstantAction=false while loot was applied.
                Assert.Equal(3, inventory.GetItemStack(0).count);
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
                ContainerOpenPatchShared.SetDeferred(player, container, null, null);

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
        public void ProcessContainer_WhenReadActionOverDecrements_ClearsSlotInsteadOfSavingNegativeCount()
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
                var container = new TileEntityLootContainer(null)
                {
                    items = new[]
                    {
                        CreateStack("magazineRepair", 2, new ItemActionEat { SimulateOverDecrement = true }),
                    },
                };
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                AutoReader.ProcessContainer(player, container);

                Assert.True(container.items[0] == null || container.items[0].IsEmpty());
                Assert.True(GameReflection.GetItemCount(container.items[0]) <= 0);
            }
            finally
            {
                GameManager.Instance = originalGm;
                ModConfig.Reload();
                DrainQueue();
            }
        }

        [Fact]
        public void ProcessEntityLootBag_WhenEnabled_ConsumesReadableBagSlots()
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
                var bag = new Bag();
                bag.SetSlots(new[]
                {
                    CreateStack("bookFiremansAlmanacHeat", 1),
                    null,
                });
                var player = new EntityPlayer { EntityName = "p", inventory = new Inventory() };

                AutoReader.ProcessEntityLootBag(player, bag);

                var slots = bag.GetSlots();
                Assert.True(slots[0] == null || slots[0].IsEmpty());
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
                ContainerOpenPatchShared.SetDeferred(player, container, null, null);

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
            return CreateBookStack(count, action =>
            {
                action.SimulateInstantActionFailure = simulateInstantFailure;
            });
        }

        private static ItemStack CreateBookStack(int count, Action<ItemActionEat> configure)
        {
            var eat = new ItemActionEat();
            configure?.Invoke(eat);
            return new ItemStack(
                new ItemValue("bookTest".GetHashCode())
                {
                    ItemClass = new ItemClass
                    {
                        Name = "bookFiremansAlmanacHeat",
                        Actions = new ItemActionBase[]
                        {
                            eat,
                        },
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
