using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
namespace AutoReadMod
{
    public static class AutoReader
    {
        private static int _nextLootReadDebugId;
        private static Dictionary<string, SyntheticReadableEffects> _cachedReadableEffectsByItemName;
        private static bool _readableEffectsCacheLoaded;
        private static Dictionary<string, HashSet<string>> _bookSetSiblingPerksBySkill;
        private static int? _magazinePointsPerReadOverrideForTest;
        private static readonly object NerdBonusRandomGate = new object();
        private static Random NerdBonusRandom = new Random();

        static AutoReader()
        {
            // When ModConfig.DebugMode is on, mirror hypothesis lines to Unity so Player.log is useful in-game.
            GameReflection.DiagnosticLine += message =>
            {
                try
                {
                    if (!ModConfig.Instance.DebugMode)
                    {
                        return;
                    }

                    // LogWarning + fixed tag: survives default Unity log filters better than Log alone.
                    UnityEngine.Debug.LogWarning("[AutoReadMod] " + message);
                }
                catch
                {
                    // Unity not in test host
                }
            };
        }

        private static void LogHypothesis(string line)
        {
            GameReflection.EmitHypothesisLine("[AutoReadMod:HYP:AutoReader] " + line);
        }

        private static int NextLootReadDebugId()
        {
            return System.Threading.Interlocked.Increment(ref _nextLootReadDebugId);
        }

        private static string CurrentFrameForDebug()
        {
            try
            {
                var frameProp = typeof(UnityEngine.Time).GetProperty(
                    "frameCount",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var value = frameProp?.GetValue(null, null);
                return value?.ToString() ?? "?";
            }
            catch
            {
                return "?";
            }
        }

        private static void LogLootSnapshot(
            int readId,
            string phase,
            EntityPlayer player,
            ItemStack lootBacking,
            XUiC_ItemStack uiController = null)
        {
            LogHypothesis(
                $"LootDbg#{readId} frame={CurrentFrameForDebug()} {phase}: {GameReflection.FormatLootReadSnapshot(player, lootBacking, uiController)}");
        }

        internal static void EmitDebugFileLocation()
        {
            LogHypothesis($"Diagnostics file: {GameReflection.GetLootDebugFilePath() ?? "(unavailable)"}");
        }

        public enum ReadableItemKind
        {
            None,
            Book,
            Magazine,
            Schematic,
            Recipe,
        }

        public static void ProcessContainer(EntityPlayer player, ITileEntityLootable container, XUiC_LootWindowGroup lootWindowGroup = null, XUiC_LootWindow lootWindow = null)
        {
            GearsModApi.SyncWorldSettingsIfLoaded();
            DebugLog($"ProcessContainer called, Enabled={ModConfig.Instance.Enabled}");

            if (!ModConfig.Instance.Enabled)
            {
                DebugLog("Mod is disabled in config");
                return;
            }

            var items = ContainerOpenPatchShared.TryGetLootItems(container);
            if (player == null || items == null)
            {
                DebugLog($"Null check failed: player={player != null}, items={items != null}");
                return;
            }

            var itemsToRead = FindReadableSlotIndices(items);

            DebugLog($"Found {itemsToRead.Count} readable items");

            // Read items (in reverse to avoid index issues when removing)
            for (int i = itemsToRead.Count - 1; i >= 0; i--)
            {
                int slotIndex = itemsToRead[i];
                ReadStack(
                    player,
                    items[slotIndex],
                    GameReflection.GetItemCount(items[slotIndex]),
                    AutoReadVisualizationSource.Container,
                    updatedStack =>
                {
                    ContainerOpenPatchShared.TryWriteLootItem(container, items, slotIndex, updatedStack);
                },
                    itemSlotIndex: slotIndex,
                    lootWindowGroup: lootWindowGroup);
            }
        }

        internal static List<int> FindReadableSlotIndices(ItemStack[] items)
        {
            var itemsToRead = new List<int>();
            if (items == null)
            {
                return itemsToRead;
            }

            DebugLog($"Scanning {items.Length} slots for readable items");

            for (int i = 0; i < items.Length; i++)
            {
                var itemStack = items[i];
                if (itemStack == null || itemStack.IsEmpty())
                {
                    continue;
                }

                var itemClass = GameReflection.GetItemClass(GameReflection.GetItemValue(itemStack));
                DebugLog($"Slot {i}: {itemClass?.GetLocalizedItemName() ?? "unknown"}");

                if (ShouldAutoReadItem(itemStack))
                {
                    DebugLog($"Slot {i} is readable, adding to list");
                    itemsToRead.Add(i);
                }
            }

            return itemsToRead;
        }

        internal static void ProcessInventorySlots(EntityPlayer player, Inventory inventory, IDictionary<int, int> slotReadCounts)
        {
            GearsModApi.SyncWorldSettingsIfLoaded();
            DebugLog($"ProcessInventorySlots called, Enabled={ModConfig.Instance.Enabled}");

            if (!ModConfig.Instance.Enabled)
            {
                DebugLog("Mod is disabled in config");
                return;
            }

            if (player == null || inventory == null || slotReadCounts == null || slotReadCounts.Count == 0)
            {
                DebugLog("Inventory processing skipped due to missing data");
                return;
            }

            var slotIndices = new List<int>(slotReadCounts.Keys);
            slotIndices.Sort();

            for (int i = slotIndices.Count - 1; i >= 0; i--)
            {
                int slotIndex = slotIndices[i];
                int consumeCount = slotReadCounts[slotIndex];
                var itemStack = inventory.GetItemStack(slotIndex);
                if (!ShouldAutoReadItem(itemStack))
                {
                    continue;
                }

                ReadStack(
                    player,
                    itemStack,
                    consumeCount,
                    AutoReadVisualizationSource.Bundle,
                    updatedStack =>
                {
                    inventory.SetItem(slotIndex, updatedStack);
                    inventory.Changed();
                },
                    itemSlotIndex: slotIndex);
            }
        }

        internal static void ProcessBagSlots(EntityPlayer player, Bag bag, IDictionary<int, int> slotReadCounts)
        {
            GearsModApi.SyncWorldSettingsIfLoaded();
            DebugLog($"ProcessBagSlots called, Enabled={ModConfig.Instance.Enabled}");

            if (!ModConfig.Instance.Enabled)
            {
                DebugLog("Mod is disabled in config");
                return;
            }

            if (player == null || bag == null || slotReadCounts == null || slotReadCounts.Count == 0)
            {
                DebugLog("Bag processing skipped due to missing data");
                return;
            }

            var slotIndices = new List<int>(slotReadCounts.Keys);
            slotIndices.Sort();

            for (int i = slotIndices.Count - 1; i >= 0; i--)
            {
                int slotIndex = slotIndices[i];
                int consumeCount = slotReadCounts[slotIndex];
                var itemStack = GameReflection.GetBagItemStack(bag, slotIndex);
                if (!ShouldAutoReadItem(itemStack))
                {
                    continue;
                }

                ReadStack(
                    player,
                    itemStack,
                    consumeCount,
                    AutoReadVisualizationSource.Bundle,
                    updatedStack =>
                    {
                        GameReflection.SetBagItem(bag, slotIndex, updatedStack);
                    },
                    itemSlotIndex: slotIndex,
                    lootWindowGroup: null,
                    bundleReadFromBag: true);
            }
        }

        /// <summary>
        /// v3 entity loot (death bags, zombie bags, <c>EntityLootContainer</c>) uses <see cref="Bag"/> +
        /// <c>XUiC_BagContainer</c>, not <see cref="ITileEntityLootable"/>. Same synthetic open-loot consume path
        /// as tile chests so player inventory/toolbelt is never treated as the read target.
        /// </summary>
        public static void ProcessEntityLootBag(EntityPlayer player, Bag bag, XUiC_BagContainer bagContainer = null)
        {
            GearsModApi.SyncWorldSettingsIfLoaded();
            DebugLog($"ProcessEntityLootBag called, Enabled={ModConfig.Instance.Enabled}");

            if (!ModConfig.Instance.Enabled)
            {
                DebugLog("Mod is disabled in config");
                return;
            }

            var slots = GameReflection.GetBagSlots(bag);
            if (player == null || slots == null)
            {
                DebugLog($"Null check failed: player={player != null}, slots={slots != null}");
                return;
            }

            var itemsToRead = FindReadableSlotIndices(slots);
            DebugLog($"Found {itemsToRead.Count} readable items in entity loot bag");

            for (int i = itemsToRead.Count - 1; i >= 0; i--)
            {
                int slotIndex = itemsToRead[i];
                var itemStack = slots[slotIndex];
                ReadStack(
                    player,
                    itemStack,
                    GameReflection.GetItemCount(itemStack),
                    AutoReadVisualizationSource.Container,
                    updatedStack =>
                    {
                        GameReflection.SetBagItem(bag, slotIndex, updatedStack);
                    },
                    itemSlotIndex: slotIndex,
                    lootWindowGroup: null,
                    bundleReadFromBag: false,
                    readingFromOpenContainer: true);
            }
        }

        internal static bool ShouldAutoReadItem(ItemStack itemStack)
        {
            GearsModApi.SyncWorldSettingsIfLoaded();
            var itemKind = ClassifyReadableItem(itemStack);
            if (itemKind == ReadableItemKind.None)
            {
                return false;
            }

            var config = ModConfig.Instance;
            switch (itemKind)
            {
                case ReadableItemKind.Book:
                    return config.ReadBooks;
                case ReadableItemKind.Magazine:
                    return config.ReadMagazines;
                case ReadableItemKind.Schematic:
                case ReadableItemKind.Recipe:
                    return config.ReadSchematics;
                default:
                    return false;
            }
        }

        internal static ReadableItemKind ClassifyReadableItem(ItemStack itemStack)
        {
            var itemValue = GameReflection.GetItemValue(itemStack);
            var itemClass = GameReflection.GetItemClass(itemValue);
            if (itemClass == null)
            {
                DebugLog("ClassifyReadableItem: null item");
                return ReadableItemKind.None;
            }

            DebugLog($"Item: {itemClass.GetLocalizedItemName()}");
            DebugLog($"  ItemTags: {GameReflection.GetItemTags(itemClass)}");
            DebugLog($"  Name: {GameReflection.GetItemClassName(itemClass)}");
            return ClassifyReadableItemName(GameReflection.GetItemClassName(itemClass));
        }

        internal static ReadableItemKind ClassifyReadableItemName(string itemName)
        {
            string normalizedName = itemName?.ToLowerInvariant() ?? "";
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return ReadableItemKind.None;
            }

            // Bundles are containers, not readable items; bundle contents are handled separately.
            if (normalizedName.Contains("bundle"))
            {
                return ReadableItemKind.None;
            }

            if (normalizedName.StartsWith("book"))
            {
                return ReadableItemKind.Book;
            }

            if (normalizedName.EndsWith("schematic"))
            {
                return ReadableItemKind.Schematic;
            }

            if (normalizedName.EndsWith("recipe"))
            {
                return ReadableItemKind.Recipe;
            }

            if (normalizedName.Contains("magazine"))
            {
                return ReadableItemKind.Magazine;
            }

            return ReadableItemKind.None;
        }

        internal static string GetItemKey(ItemStack itemStack)
        {
            var itemValue = GameReflection.GetItemValue(itemStack);
            if (itemValue == null)
            {
                return string.Empty;
            }

            string name = GameReflection.GetItemClassName(GameReflection.GetItemClass(itemValue));
            return $"{GameReflection.GetItemType(itemValue)}:{name}".ToLowerInvariant();
        }

        /// <summary>
        /// Verifies that a loot-window <see cref="XUiC_ItemStack"/> is showing the same item as the tile container stack
        /// we intend to read. Index-based controller lookup can return slot 0 from the wrong grid (e.g. toolbelt),
        /// which would make <see cref="ItemActionEat.ExecuteInstantAction"/> consume the hotbar instead of loot.
        /// </summary>
        private static bool LootUiStackMatchesContainerBacking(ItemStack uiStack, ItemStack containerBacking)
        {
            if (containerBacking == null || containerBacking.IsEmpty())
            {
                return false;
            }

            if (uiStack == null || uiStack.IsEmpty())
            {
                return false;
            }

            if (ReferenceEquals(uiStack, containerBacking))
            {
                return true;
            }

            var uiKey = GetItemKey(uiStack);
            var backKey = GetItemKey(containerBacking);
            if (string.IsNullOrEmpty(uiKey) || string.IsNullOrEmpty(backKey) ||
                !string.Equals(uiKey, backKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return GameReflection.GetItemCount(uiStack) == GameReflection.GetItemCount(containerBacking);
        }

        /// <summary>Resolves a loot <see cref="XUiC_ItemStack"/> only when its displayed stack matches the container backing.</summary>
        private static XUiC_ItemStack ResolveSafeLootSlotController(
            XUiC_LootWindowGroup lootWindowGroup,
            ItemStack containerBacking,
            int itemSlotIndex)
        {
            if (lootWindowGroup == null || containerBacking == null)
            {
                return null;
            }

            var byRef = GameReflection.TryGetLootSlotByStackReference(lootWindowGroup, containerBacking);
            if (byRef != null)
            {
                return byRef;
            }

            if (itemSlotIndex < 0)
            {
                return null;
            }

            var byIdx = GameReflection.TryGetLootSlotItemStackController(lootWindowGroup, itemSlotIndex);
            var probeUi = GameReflection.TryGetItemStackFromLootSlotController(byIdx);
            return LootUiStackMatchesContainerBacking(probeUi, containerBacking) ? byIdx : null;
        }

        /// <param name="itemSlotIndex">
        /// The slot this stack occupies in the current inventory, bag, or open loot container array.
        /// ItemActionEat.consume uses ItemInventoryData.slotIdx; passing -1 skips the paths that apply reader perks/recipes in many builds.
        /// </param>
        /// <param name="lootWindowGroup">When non-null (loot UI open), resolves <see cref="XUiC_ItemStack"/> so <c>ExecuteInstantAction</c> matches manual reads.</param>
        /// <param name="bundleReadFromBag">When <paramref name="source"/> is bundle: magazines/backpack slots use backpack UI grid; toolbelt slots use toolbelt grid.</param>
        private static void ReadStack(
            EntityPlayer player,
            ItemStack itemStack,
            int consumeCount,
            AutoReadVisualizationSource source,
            Action<ItemStack> updateStack,
            int itemSlotIndex = -1,
            XUiC_LootWindowGroup lootWindowGroup = null,
            bool bundleReadFromBag = false,
            bool readingFromOpenContainer = false)
        {
            var itemValue = GameReflection.GetItemValue(itemStack);
            var itemClass = GameReflection.GetItemClass(itemValue);
            if (itemClass == null || consumeCount <= 0)
            {
                return;
            }

            int currentCount = GameReflection.GetItemCount(itemStack);
            int targetCount = Math.Min(currentCount, consumeCount);
            if (targetCount <= 0)
            {
                return;
            }

            var itemName = itemClass.GetLocalizedItemName();
            var openContainerRead = readingFromOpenContainer || lootWindowGroup != null;

            SafeLog($"AutoReadMod: {player?.EntityName ?? "player"} reading {targetCount}x {itemName}");

            try
            {
                LogHypothesis(
                    $"ReadStack: item={itemName} currentCount={currentCount} targetCount={targetCount} slot={itemSlotIndex} source={source} playerType={player?.GetType().Name ?? "null"}");
                var lootReadDebugId = openContainerRead ? NextLootReadDebugId() : 0;
                if (lootReadDebugId != 0)
                {
                    LogLootSnapshot(lootReadDebugId, $"ReadStack.begin slot={itemSlotIndex} item={itemName}", player, itemStack);
                }

                // Loot eat/instant paths must not receive XUiC_ItemStack (vanilla maps it to player slots — toolbelt slot 0 bug).
                XUiC_ItemStack lootSlotController = null;
                if (openContainerRead)
                {
                    LogHypothesis(
                        $"ReadStack: open loot slot={itemSlotIndex} — omit slot controller from ApplyReadableEffects (tile ItemStack only)");
                }
                else if (source == AutoReadVisualizationSource.Bundle && itemSlotIndex >= 0)
                {
                    lootSlotController = GameReflection.TryGetPlayerItemSlotControllerForBundleRead(
                        player,
                        itemSlotIndex,
                        itemStack,
                        bundleReadFromBag);
                    var probeUi = lootSlotController != null
                        ? GameReflection.TryGetItemStackFromLootSlotController(lootSlotController)
                        : null;
                    if (lootSlotController != null && (probeUi == null || probeUi.IsEmpty()))
                    {
                        // Keep the controller: vanilla magazine skills often need XUiC_ItemStack even when ItemStack
                        // is not synced yet; ApplyReadableEffects passes backing stack when UI stack is missing.
                        LogHypothesis(
                            "ReadStack: bundle XUiC_ItemStack.ItemStack empty/unset — keeping controller; using backing stack for instant/read path");
                    }

                    LogHypothesis(
                        $"ReadStack: bundleSlotController={(lootSlotController == null ? "null" : lootSlotController.GetType().Name)} bag={bundleReadFromBag} slot={itemSlotIndex}");
                }

                var successfulConsumes = ApplyReadableEffects(
                    player,
                    itemStack,
                    targetCount,
                    itemSlotIndex,
                    lootSlotController,
                    lootWindowGroup,
                    lootReadDebugId,
                    readingFromOpenContainer: openContainerRead);
                DebugLog($"Consumed {successfulConsumes}/{targetCount}x {itemName}, now updating slot");
                if (lootReadDebugId != 0)
                {
                    LogLootSnapshot(lootReadDebugId, $"ReadStack.afterApply successful={successfulConsumes}", player, itemStack);
                }

                if (successfulConsumes <= 0)
                {
                    LogHypothesis("ReadStack: no successful consumptions; slot not updated");
                    return;
                }

                // Open loot uses synthetic consumption and deliberately leaves UI/backing ItemStack counts untouched
                // until the container slot update below. Some generated loot grids still expose the stale UI count
                // here; trusting it re-adds already learned items to non-Untouched containers.
                int remaining;
                if (openContainerRead)
                {
                    remaining = Math.Max(0, currentCount - successfulConsumes);
                }
                else if (lootSlotController != null)
                {
                    var uiStackAfter = GameReflection.TryGetItemStackFromLootSlotController(lootSlotController);
                    remaining = uiStackAfter != null
                        ? GameReflection.GetItemCount(uiStackAfter)
                        : Math.Max(0, currentCount - successfulConsumes);
                }
                else
                {
                    var backingAfter = GameReflection.GetItemCount(itemStack);
                    remaining = Math.Min(backingAfter, Math.Max(0, currentCount - successfulConsumes));
                }

                remaining = Math.Max(0, Math.Min(currentCount, remaining));

                LogHypothesis($"ReadStack: updating stack remaining={remaining} (initialCount={currentCount}, read {successfulConsumes}, lootUi={(lootSlotController != null)})");
                if (lootReadDebugId != 0)
                {
                    LogLootSnapshot(lootReadDebugId, $"ReadStack.beforeUpdateSlot remaining={remaining}", player, itemStack);
                }

                if (remaining == 0)
                {
                    updateStack(GameReflection.GetEmptyItemStack());
                }
                else
                {
                    updateStack(new ItemStack(itemValue, remaining));
                    DebugLog($"Reduced stack to {remaining}");
                }

                if (lootReadDebugId != 0)
                {
                    LogLootSnapshot(lootReadDebugId, "ReadStack.afterUpdateSlot", player, itemStack);
                }

                var kind = ClassifyReadableItemName(GameReflection.GetItemClassName(itemClass));
                EmitVisualizationEvent(itemClass, itemValue, successfulConsumes, source, kind);
            }
            catch (Exception ex)
            {
                SafeLog($"Error reading {itemName}: {ex.Message}");
                DebugLog($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>For unit tests (mock + optional real DLL). Production uses ApplyReadableEffects via ReadStack.</summary>
        internal static int ApplyReadableEffectsForTest(
            EntityPlayer player,
            ItemStack itemStack,
            int consumeCount,
            int itemSlotIndex = -1,
            XUiC_ItemStack lootSlotController = null,
            XUiC_LootWindowGroup lootWindowGroup = null)
        {
            return ApplyReadableEffects(player, itemStack, consumeCount, itemSlotIndex, lootSlotController, lootWindowGroup, 0);
        }

        /// <summary>Hermetic tests for magazine double-point CVar resolution (Nerd Outfit / INT Mastery).</summary>
        internal static bool TryResolveMagazineDoublePointChanceForTest(
            EntityAlive entity,
            out float chancePercent,
            out string chanceCVarName)
        {
            return TryResolveMagazineDoublePointChance(entity, out chancePercent, out chanceCVarName);
        }

        /// <summary>Hermetic tests for Nerd Outfit / INT Mastery eligibility gate.</summary>
        internal static bool IsEligibleForMagazineDoublePointBonusForTest(EntityAlive entity)
        {
            return IsEligibleForMagazineDoublePointBonus(entity);
        }

        /// <summary>
        /// Hermetic tests: apply magazine double-point bonus with a deterministic RNG seed
        /// (seed 0 yields roll≈0 so chance ≥ 1 always grants).
        /// </summary>
        internal static void ApplyMagazineDoublePointBonusForTest(
            EntityAlive entity,
            string[] progressionNames,
            int consumedUnits,
            int randomSeed)
        {
            lock (NerdBonusRandomGate)
            {
                var previous = NerdBonusRandom;
                NerdBonusRandom = new Random(randomSeed);
                try
                {
                    ApplyMagazineDoublePointBonusFromGameCVar(
                        entity,
                        progressionNames,
                        consumedUnits,
                        "testMagazine",
                        1,
                        "test",
                        null);
                }
                finally
                {
                    NerdBonusRandom = previous;
                }
            }
        }

        /// <summary>Hermetic tests: inject XML-style synthetic readable effects for an item name.</summary>
        internal static void SetSyntheticReadableEffectsForTest(
            string itemName,
            string[] addProgressionNames = null,
            string[] maxProgressionNames = null,
            string[] setCVarNames = null,
            int[] addProgressionXmlLevels = null,
            string completeProgressionName = null,
            string completeSkillName = null,
            int completeRequiredCount = 0,
            string[] bookSetSiblingPerkNames = null,
            string readSoundName = null,
            string bookSetCompleteSoundName = null)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return;
            }

            var cache = EnsureReadableEffectsCache();
            cache[itemName] = new SyntheticReadableEffects(
                addProgressionNames ?? new string[0],
                maxProgressionNames ?? new string[0],
                setCVarNames ?? new string[0],
                addProgressionXmlLevels,
                completeProgressionName,
                completeSkillName,
                completeRequiredCount,
                readSoundName,
                bookSetCompleteSoundName);

            if (!string.IsNullOrWhiteSpace(completeSkillName) && bookSetSiblingPerkNames != null)
            {
                EnsureBookSetSiblingCache();
                if (!_bookSetSiblingPerksBySkill.TryGetValue(completeSkillName, out var siblings))
                {
                    siblings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _bookSetSiblingPerksBySkill[completeSkillName] = siblings;
                }

                for (var i = 0; i < bookSetSiblingPerkNames.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(bookSetSiblingPerkNames[i]))
                    {
                        siblings.Add(bookSetSiblingPerkNames[i].Trim());
                    }
                }
            }
        }

        /// <summary>Hermetic tests: override sandbox Magazine Progress points (null clears override).</summary>
        internal static void SetMagazinePointsPerReadForTest(int? pointsPerRead)
        {
            _magazinePointsPerReadOverrideForTest = pointsPerRead;
        }

        /// <summary>Hermetic tests: grant book-set Complete perk when eligible.</summary>
        internal static void TryGrantBookSetCompletionIfEligibleForTest(EntityAlive entity, ItemClass itemClass)
        {
            TryGrantBookSetCompletionIfEligible(entity, itemClass);
        }

        /// <summary>Hermetic tests: clear synthetic readable-effects cache.</summary>
        internal static void ClearSyntheticReadableEffectsCacheForTest()
        {
            _cachedReadableEffectsByItemName = null;
            _readableEffectsCacheLoaded = false;
            _bookSetSiblingPerksBySkill = null;
            _magazinePointsPerReadOverrideForTest = null;
        }

        private static int ApplyReadableEffects(
            EntityPlayer player,
            ItemStack itemStack,
            int consumeCount,
            int itemSlotIndex = -1,
            XUiC_ItemStack lootSlotController = null,
            XUiC_LootWindowGroup lootWindowGroup = null,
            int lootReadDebugId = 0,
            bool readingFromOpenContainer = false)
        {
            var itemValue = GameReflection.GetItemValue(itemStack);
            var itemClass = GameReflection.GetItemClass(itemValue);
            if (itemClass == null)
            {
                return 0;
            }

            var itemName = itemClass.GetLocalizedItemName();

            var itemActions = GameReflection.GetItemActions(itemClass);
            if (itemActions == null || itemActions.Length == 0)
            {
                SafeLog($"No actions found for {itemName}");
                return 0;
            }

            // Match concrete vanilla eat action only (see 09b86f5). ItemActionGainSkill / ItemActionLearnRecipe are separate.
            object eatAction = null;
            var eatActionIndex = -1;
            for (var ai = 0; ai < itemActions.Length; ai++)
            {
                var action = itemActions.GetValue(ai);
                if (action != null && action.GetType().Name == "ItemActionEat")
                {
                    eatAction = action;
                    eatActionIndex = ai;
                    break;
                }
            }

            object gainSkillAction = null;
            var gainSkillActionIndex = -1;
            for (var ai = 0; ai < itemActions.Length; ai++)
            {
                var action = itemActions.GetValue(ai);
                if (action != null && action.GetType().Name == "ItemActionGainSkill")
                {
                    gainSkillAction = action;
                    gainSkillActionIndex = ai;
                    break;
                }
            }

            object learnRecipeAction = null;
            var learnRecipeActionIndex = -1;
            for (var ai = 0; ai < itemActions.Length; ai++)
            {
                var action = itemActions.GetValue(ai);
                if (action != null && action.GetType().Name == "ItemActionLearnRecipe")
                {
                    learnRecipeAction = action;
                    learnRecipeActionIndex = ai;
                    break;
                }
            }

            if (eatAction == null && gainSkillAction == null && learnRecipeAction == null)
            {
                var actionDiag = new System.Text.StringBuilder();
                for (var ai = 0; ai < itemActions.Length; ai++)
                {
                    var a = itemActions.GetValue(ai);
                    if (actionDiag.Length > 0)
                    {
                        actionDiag.Append("; ");
                    }

                    actionDiag.Append(a?.GetType().FullName ?? "null");
                }

                SafeLog($"No ItemActionEat, ItemActionGainSkill, or ItemActionLearnRecipe for {itemName}. Actions[]={actionDiag}");
                return 0;
            }

            var effectiveCount = Math.Min(consumeCount, GameReflection.GetItemCount(itemStack));
            if (effectiveCount <= 0)
            {
                return 0;
            }

            LogHypothesis(
                $"ApplyReadableEffects: {effectiveCount}/{consumeCount} x {itemName} | eatAction={(eatAction == null ? "null" : eatAction.GetType().FullName)} | gainSkillAction={(gainSkillAction == null ? "null" : gainSkillAction.GetType().FullName)} | learnRecipe={(learnRecipeAction == null ? "null" : learnRecipeAction.GetType().FullName)} | slotIdx={itemSlotIndex} eatIdx={eatActionIndex} gainSkillIdx={gainSkillActionIndex} learnIdx={learnRecipeActionIndex}");

            var gameManager = (object)GameReflection.GetGameManagerInstance();
            LogHypothesis(
                $"ApplyReadableEffects: gameManager={((gameManager == null) ? "null" : gameManager.GetType().Name)} | playerType={player?.GetType().Name ?? "null"} | itemValueType={itemValue?.GetType().Name ?? "null"}");
            var effectEntity = GameReflection.ResolveEntityPlayerLocalForInstantActions(GameReflection.ResolveEntityForItemEffects(player));
            LogHypothesis(
                $"ApplyReadableEffects: effectEntity={effectEntity?.GetType().FullName ?? "null"} (need EntityPlayerLocal for ExecuteInstantAction → LocalPlayerUI)");
            if (effectEntity == null)
            {
                SafeLog($"ApplyReadableEffects: no entity for item effects, abort {itemName}");
                return 0;
            }

            // Tile loot / entity bag loot (UI open): never pass loot grid indices as player toolbelt/inventory slotIdx
            // (toolbelt corruption + cast spam). Use -1 for consume/GainSkill/LearnRecipe ItemInventoryData; run
            // ItemActionLearnRecipe / CraftingManager fallback after Eat succeeds.
            //
            // Never pass loot XUiC_ItemStack into ItemActionEat.ExecuteInstantAction / eat paths: vanilla uses that controller to resolve
            // *player* inventory slots. Even when ItemStack args reference the tile entity, slot 0 controllers often map to the toolbelt and
            // consume hotbar slot 0 (e.g. axe). Only mutate the tile/bag backing ItemStack passed into this method.
            var readingFromOpenLoot = readingFromOpenContainer || lootWindowGroup != null;
            if (readingFromOpenLoot)
            {
                lootSlotController = null;
            }

            if (readingFromOpenLoot && player != null &&
                GameReflection.ItemStackAliasesPlayerInventoryOrBag(player, itemStack))
            {
                SafeLog(
                    $"ApplyReadableEffects: refused loot read — ItemStack reference matches a player inventory/bag slot (possible engine alias / hotbar corruption). {itemName}");
                LogHypothesis(
                    $"ApplyReadableEffects: abort loot read for {itemName} — stack reference aliases player grid");
                return 0;
            }

            var vanillaInstantActionOwnsEvents = !readingFromOpenLoot && lootSlotController != null;
            var slotForConsumeAndSkills = readingFromOpenLoot ? -1 : itemSlotIndex;
            var slotForMinEvents = readingFromOpenLoot ? -1 : itemSlotIndex;
            if (slotForConsumeAndSkills < 0 && !readingFromOpenLoot)
            {
                LogHypothesis(
                    "ApplyReadableEffects: WARNING slotIdx=-1 — many builds skip perk/recipe unlock logic when ItemInventoryData.slotIdx is not a real inventory/container slot.");
            }

            if (readingFromOpenLoot)
            {
                LogHypothesis(
                    $"ApplyReadableEffects: loot container UI — tile backing stack only, slotIdx=-1, no XUiC_ItemStack on eat/instant paths; learnRecipeAfterEat={(learnRecipeAction != null)}");
                LogLootSnapshot(lootReadDebugId, "ApplyReadableEffects.begin", player, itemStack, lootSlotController);
            }

            DebugLog(
                $"ApplyReadableEffects: {effectiveCount}/{consumeCount} x {itemName} (slotIdx={itemSlotIndex}, consumeSlot={slotForConsumeAndSkills}), lootUiEatCtrl={(lootSlotController == null ? "no" : "yes")}, Eat/GainSkill/LearnRecipe/FireEvent paths");

            var xmlReadableEffects = ResolveSyntheticReadableEffects(itemClass);
            var xmlProgressionEffectsOwnLearning = readingFromOpenLoot &&
                                                   (xmlReadableEffects.AddProgressionNames.Length > 0 ||
                                                    xmlReadableEffects.MaxProgressionNames.Length > 0);
            var itemKindForLootEvents = ClassifyReadableItemName(GameReflection.GetItemClassName(itemClass));
            var shouldFireOpenLootItemEvents = readingFromOpenLoot &&
                                               ((IsSkillMagazineItemClass(itemClass) &&
                                                 xmlReadableEffects.AddProgressionNames.Length > 0) ||
                                                (itemKindForLootEvents == ReadableItemKind.Book &&
                                                 xmlReadableEffects.MaxProgressionNames.Length > 0));
            var shouldApplyMagazineDoublePointBonus = readingFromOpenLoot &&
                                                      IsSkillMagazineItemClass(itemClass) &&
                                                      xmlReadableEffects.AddProgressionNames.Length > 0;
            var progressionNames = ResolveGainSkillProgressionNames(gainSkillAction, itemClass, xmlReadableEffects);
            if (gainSkillAction != null || readingFromOpenLoot || ModConfig.Instance.SkipAlreadyRead)
            {
                LogHypothesis(
                    $"ApplyReadableEffects: resolved gain-skill progressions for {itemName}: {(progressionNames.Length == 0 ? "(none)" : string.Join(",", progressionNames))}");
                if (xmlReadableEffects.HasAny)
                {
                    LogHypothesis(
                        $"ApplyReadableEffects: XML synthetic effects for {itemName}: add=[{string.Join(",", xmlReadableEffects.AddProgressionNames)}] max=[{string.Join(",", xmlReadableEffects.MaxProgressionNames)}] cvar=[{string.Join(",", xmlReadableEffects.SetCVarNames)}]");
                }
            }

            var stackEffectSkillLevelsBefore = CaptureGainSkillLevels(progressionNames, effectEntity);
            var stackPlayerSkillLevelsBefore = ReferenceEquals(effectEntity, player)
                ? null
                : CaptureGainSkillLevels(progressionNames, player);

            // Snapshot real progression rows (canonical names from ProgressionValueQuickList) so we can fix
            // under-grants when vanilla applies one level but our stack delta consumed N copies.
            var stackCraftingSnapBefore = CaptureCraftingProgressionSnapshot(effectEntity, progressionNames);
            var stackPlayerCraftingSnapBefore = ReferenceEquals(effectEntity, player)
                ? null
                : CaptureCraftingProgressionSnapshot(player, progressionNames);

            int successfulConsumes = 0;
            var itemKindForSkip = ClassifyReadableItemName(GameReflection.GetItemClassName(itemClass));

            for (int i = 0; i < effectiveCount; i++)
            {
                if (successfulConsumes >= effectiveCount)
                {
                    break;
                }

                if (ModConfig.Instance.SkipAlreadyRead &&
                    ShouldSkipReadableUnit(
                        effectEntity,
                        player,
                        gameManager,
                        itemClass,
                        itemActions,
                        itemKindForSkip,
                        progressionNames,
                        xmlReadableEffects,
                        gainSkillAction,
                        learnRecipeAction))
                {
                    LogHypothesis(
                        $"ApplyReadableEffects: SkipAlreadyRead — stop at unit {i + 1}/{effectiveCount} for {itemName} (no remaining benefit)");
                    break;
                }

                DebugLog($"Consuming item {i + 1}/{effectiveCount}");
                try
                {
                    // Keep the live slot stack as the source of truth. Vanilla read actions mutate that stack.
                    // Open loot: always use the tile backing ItemStack only — never swap in loot UI stacks (see readingFromOpenLoot + null controller).
                    ItemStack liveStack = itemStack;
                    if (!readingFromOpenLoot && lootSlotController != null)
                    {
                        var fromUi = GameReflection.TryGetItemStackFromLootSlotController(lootSlotController);
                        if (fromUi != null && !fromUi.IsEmpty() && LootUiStackMatchesContainerBacking(fromUi, itemStack))
                        {
                            liveStack = fromUi;
                        }
                    }

                    if (liveStack == null || liveStack.IsEmpty())
                    {
                        LogHypothesis($"ApplyReadableEffects: stop at unit {i + 1}/{effectiveCount} — stack depleted");
                        break;
                    }

                    // Tile loot must measure/consume the backing stack passed from ProcessContainer (never UI clones).
                    var stackForEatMeasure = readingFromOpenLoot ? itemStack : liveStack;
                    int countStart = GameReflection.GetItemCount(stackForEatMeasure);
                    var effectStack = new ItemStack(itemValue, 1);
                    var effectSkillLevelsBefore = CaptureGainSkillLevels(progressionNames, effectEntity);
                    var playerSkillLevelsBefore = ReferenceEquals(effectEntity, player)
                        ? null
                        : CaptureGainSkillLevels(progressionNames, player);

                    if (!readingFromOpenLoot && !vanillaInstantActionOwnsEvents)
                    {
                        TryFireOnSelfPrimaryActionStart(
                            itemClass,
                            itemValue,
                            gameManager,
                            effectEntity,
                            slotForMinEvents,
                            itemName,
                            i + 1,
                            effectStack);
                    }
                    else if (vanillaInstantActionOwnsEvents)
                    {
                        LogHypothesis(
                            $"ApplyReadableEffects: item {i + 1}: skipped manual FireStart; vanilla ExecuteInstantAction owns item events");
                    }
                    else
                    {
                        LogHypothesis(
                            $"ApplyReadableEffects: item {i + 1}: loot — skipped FireStart; vanilla item events can route to hotbar state");
                    }

                    int consumedThisUnit = 0;
                    if (eatAction != null)
                    {
                        bool MeasureConsumeDelta(out int delta)
                        {
                            ItemStack afterStack;
                            if (readingFromOpenLoot)
                            {
                                afterStack = stackForEatMeasure;
                            }
                            else if (lootSlotController != null)
                            {
                                afterStack = GameReflection.TryGetItemStackFromLootSlotController(lootSlotController) ??
                                             liveStack;
                            }
                            else
                            {
                                afterStack = liveStack;
                            }

                            delta = Math.Max(0, countStart - GameReflection.GetItemCount(afterStack));
                            delta = Math.Min(delta, effectiveCount - successfulConsumes);
                            return delta > 0;
                        }

                        if (readingFromOpenLoot)
                        {
                            // Never call vanilla ItemActionEat paths for tile loot. Logs proved ExecuteAction(true)
                            // clears player hotbar slot 0 while leaving the loot stack unchanged. Treat this unit as
                            // successfully read, then apply explicit progression/recipe fallbacks and update container.
                            consumedThisUnit = Math.Min(1, effectiveCount - successfulConsumes);
                            LogHypothesis(
                                $"ApplyReadableEffects: item {i + 1}: loot — synthetic consume counted {consumedThisUnit}; skipped ItemActionEat ExecuteAction/consume/Completed for {itemName}");
                            LogLootSnapshot(lootReadDebugId, $"after SyntheticLootConsume unit={i + 1} consumed={consumedThisUnit}", player, itemStack, lootSlotController);
                        }
                        else
                        {
                            var stackForInstant = lootSlotController != null
                                ? (GameReflection.TryGetItemStackFromLootSlotController(lootSlotController) ?? liveStack)
                                : liveStack;

                            var instantOk = TryExecuteInstantActionReflection(
                                eatAction,
                                effectEntity,
                                stackForInstant,
                                itemName,
                                i + 1,
                                effectiveCount,
                                lootSlotController,
                                treatSuccessfulInvokeAsSuccess: false);
                            LogHypothesis(
                                $"ApplyReadableEffects: item {i + 1}: ExecuteInstantAction first success={instantOk} uiCtrl={(lootSlotController == null ? "null" : "non-null")} for {itemName}");

                            MeasureConsumeDelta(out consumedThisUnit);

                            if (consumedThisUnit <= 0)
                            {
                                if (TryConsumeEatActionReflection(
                                        eatAction,
                                        itemClass,
                                        liveStack,
                                        gameManager,
                                        effectEntity,
                                        itemValue,
                                        slotForConsumeAndSkills,
                                        eatActionIndex,
                                        itemName,
                                        i + 1,
                                        effectiveCount,
                                        lootReadDebugId,
                                        player,
                                        itemStack,
                                        lootSlotController))
                                {
                                    MeasureConsumeDelta(out consumedThisUnit);
                                }
                            }
                        }

                        if (consumedThisUnit > 0)
                        {
                            LogHypothesis(
                                $"ApplyReadableEffects: item {i + 1}: consumed {consumedThisUnit} via vanilla read path for {itemName}");
                            if (!readingFromOpenLoot && !vanillaInstantActionOwnsEvents)
                            {
                                TryFireOnSelfPrimaryActionEnd(
                                    itemClass,
                                    itemValue,
                                    gameManager,
                                    effectEntity,
                                    slotForMinEvents,
                                    itemName,
                                    i + 1,
                                    "after vanilla read path",
                            effectStack);
                            }
                            else if (vanillaInstantActionOwnsEvents)
                            {
                                LogHypothesis(
                                    $"ApplyReadableEffects: item {i + 1}: skipped manual FireEnd; vanilla ExecuteInstantAction owns item events");
                            }
                            else
                            {
                                LogHypothesis(
                                    shouldFireOpenLootItemEvents
                                        ? $"ApplyReadableEffects: item {i + 1}: loot — defer FireEnd until after event baseline capture"
                                        : $"ApplyReadableEffects: item {i + 1}: loot — skipped FireEnd; synthetic path owns progression/recipe effects");
                            }
                        }
                        else
                        {
                            LogHypothesis(
                                $"ApplyReadableEffects: item {i + 1}/{effectiveCount} no stack decrease for {itemName} (count {countStart}); not counting as consumed");
                        }
                    }
                    else
                    {
                        LogHypothesis($"ApplyReadableEffects: item {i + 1}: no ItemActionEat — gain-skill action alone cannot consume {itemName}");
                    }

                    if (consumedThisUnit > 0 && gainSkillAction != null && !xmlProgressionEffectsOwnLearning)
                    {
                        if (readingFromOpenLoot)
                        {
                            // Avoid ItemActionGainSkill.ExecuteAction for loot: same ItemActionData stack/slot context risk as eat.
                            ApplyMissingGainSkillLevels(
                                effectEntity,
                                progressionNames,
                                effectSkillLevelsBefore,
                                consumedThisUnit,
                                itemName,
                                i + 1,
                                "effect entity loot synthetic gain action");
                            if (!ReferenceEquals(effectEntity, player))
                            {
                                ApplyMissingGainSkillLevels(
                                    player,
                                    progressionNames,
                                    playerSkillLevelsBefore,
                                    consumedThisUnit,
                                    itemName,
                                    i + 1,
                                    "context player loot synthetic gain action");
                            }

                            LogLootSnapshot(lootReadDebugId, $"after SyntheticGainSkill unit={i + 1}", player, itemStack, lootSlotController);
                            LogHypothesis(
                                $"ApplyReadableEffects: item {i + 1}: loot — skipped ItemActionGainSkill.ExecuteAction; fallback progressions={progressionNames.Length}");
                        }
                        else
                        {
                            var skillOk = TryGainSkillExecuteActionReflection(
                                gainSkillAction,
                                itemClass,
                                effectStack,
                                gameManager,
                                effectEntity,
                                itemValue,
                                slotForConsumeAndSkills,
                                gainSkillActionIndex,
                                itemName,
                                i + 1,
                                effectiveCount,
                                progressionNames,
                                effectSkillLevelsBefore,
                                consumedThisUnit);
                            LogHypothesis(
                                $"ApplyReadableEffects: item {i + 1}: ItemActionGainSkill.ExecuteAction={skillOk} after consume for {itemName}");

                            if (!ReferenceEquals(effectEntity, player))
                            {
                                ApplyMissingGainSkillLevels(
                                    player,
                                    progressionNames,
                                    playerSkillLevelsBefore,
                                    consumedThisUnit,
                                    itemName,
                                    i + 1,
                                    "context player");
                            }
                        }
                    }
                    else if (consumedThisUnit > 0 &&
                             gainSkillAction == null &&
                             progressionNames.Length > 0 &&
                             !xmlProgressionEffectsOwnLearning)
                    {
                        // Eat-only magazines (no ItemActionGainSkill): vanilla may not bump crafting tiers with loot slotIdx=-1; mirror bundle reconciliation per unit.
                        ApplyMissingGainSkillLevels(
                            effectEntity,
                            progressionNames,
                            effectSkillLevelsBefore,
                            consumedThisUnit,
                            itemName,
                            i + 1,
                            "effect entity eat-only after loot consume");
                        if (readingFromOpenLoot)
                        {
                            LogLootSnapshot(lootReadDebugId, $"after EatOnlyGainFallback unit={i + 1}", player, itemStack, lootSlotController);
                        }
                        LogHypothesis(
                            $"ApplyReadableEffects: item {i + 1}: GainSkill fallback after eat-only consume for {itemName} (progressions={progressionNames.Length})");

                        if (!ReferenceEquals(effectEntity, player))
                        {
                            ApplyMissingGainSkillLevels(
                                player,
                                progressionNames,
                                playerSkillLevelsBefore,
                                consumedThisUnit,
                                itemName,
                                i + 1,
                                "context player eat-only after loot consume");
                        }
                    }

                    Dictionary<string, int> effectEntityOpenLootEventBaseline = null;
                    if (consumedThisUnit > 0 && shouldFireOpenLootItemEvents)
                    {
                        effectEntityOpenLootEventBaseline = CaptureGainSkillLevels(
                            xmlReadableEffects.AddProgressionNames,
                            effectEntity);
                        TryFireOnSelfPrimaryActionEnd(
                            itemClass,
                            itemValue,
                            gameManager,
                            effectEntity,
                            slotForMinEvents,
                            itemName,
                            i + 1,
                            "open loot conditional item effects",
                            effectStack,
                            eatAction,
                            eatActionIndex);
                        LogLootSnapshot(lootReadDebugId, $"after OpenLootFireEnd unit={i + 1}", player, itemStack, lootSlotController);
                    }

                    if (readingFromOpenLoot && consumedThisUnit > 0 && xmlReadableEffects.HasAny)
                    {
                        ApplySyntheticXmlReadableEffects(
                            effectEntity,
                            xmlReadableEffects,
                            consumedThisUnit,
                            itemName,
                            i + 1,
                            "effect entity",
                            effectEntityOpenLootEventBaseline);
                        if (shouldApplyMagazineDoublePointBonus)
                        {
                            ApplyMagazineDoublePointBonusFromGameCVar(
                                effectEntity,
                                xmlReadableEffects.AddProgressionNames,
                                consumedThisUnit,
                                itemName,
                                i + 1,
                                "effect entity",
                                effectEntityOpenLootEventBaseline);
                        }

                        if (itemKindForSkip == ReadableItemKind.Book)
                        {
                            TryGrantBookSetCompletionIfEligible(effectEntity, itemClass);
                        }

                        if (!ReferenceEquals(effectEntity, player))
                        {
                            ApplySyntheticXmlReadableEffects(
                                player,
                                xmlReadableEffects,
                                consumedThisUnit,
                                itemName,
                                i + 1,
                                "context player");
                            if (shouldApplyMagazineDoublePointBonus)
                            {
                                ApplyMagazineDoublePointBonusFromGameCVar(
                                    player,
                                    xmlReadableEffects.AddProgressionNames,
                                    consumedThisUnit,
                                    itemName,
                                    i + 1,
                                    "context player",
                                    null);
                            }

                            if (itemKindForSkip == ReadableItemKind.Book)
                            {
                                TryGrantBookSetCompletionIfEligible(player, itemClass);
                            }
                        }

                        LogLootSnapshot(lootReadDebugId, $"after XmlSyntheticEffects unit={i + 1}", player, itemStack, lootSlotController);
                    }

                    if (consumedThisUnit > 0 && learnRecipeAction != null)
                    {
                        if (readingFromOpenLoot)
                        {
                            LogHypothesis(
                                $"ApplyReadableEffects: item {i + 1}: loot — skipped ItemActionLearnRecipe.ExecuteAction; CraftingManager fallback will run");
                        }
                        else
                        {
                            var recipeOk = TryLearnRecipeExecuteActionReflection(
                                learnRecipeAction,
                                itemClass,
                                effectStack,
                                gameManager,
                                effectEntity,
                                itemValue,
                                slotForConsumeAndSkills,
                                learnRecipeActionIndex,
                                itemName,
                                i + 1,
                                effectiveCount,
                                consumedThisUnit);
                            LogHypothesis(
                                $"ApplyReadableEffects: item {i + 1}: ItemActionLearnRecipe.ExecuteAction invoked ok={recipeOk} for {itemName}");
                        }
                    }

                    if (consumedThisUnit > 0)
                    {
                        successfulConsumes += consumedThisUnit;
                        TryPlayReadableUnitSound(effectEntity, itemKindForSkip, xmlReadableEffects);
                        DebugLog($"Applied effect for item {i + 1}/{effectiveCount} (stack -{consumedThisUnit})");
                    }

                    // Run after counting success so reflection/search bugs cannot undo a valid consume/stack update.
                    // Always attempt CraftingManager unlock for loot reads: ItemActionLearnRecipe + MoldTarget/name heuristics
                    // populate candidates; harmless no-op when none match. Name-based ReadableItemKind alone can miss mods/vanilla ids.
                    if (readingFromOpenLoot && consumedThisUnit > 0)
                    {
                        try
                        {
                            LogLootSnapshot(lootReadDebugId, $"before CraftingManagerFallback unit={i + 1}", player, itemStack, lootSlotController);
                            TryUnlockLootSchematicRecipesViaCraftingManager(
                                player,
                                gameManager,
                                itemClass,
                                itemActions,
                                itemName,
                                i + 1);
                            LogLootSnapshot(lootReadDebugId, $"after CraftingManagerFallback unit={i + 1}", player, itemStack, lootSlotController);
                        }
                        catch (Exception unlockEx)
                        {
                            var root = UnwrapReflectionException(unlockEx);
                            SafeLog(
                                $"Loot schematic recipe unlock fallback failed for {itemName}: {root.Message}");
                            LogHypothesis(
                                $"LootRecipeFallback exception [{i + 1}] {itemName}: {root.GetType().Name}: {root.Message}");
                        }
                    }
                }
                catch (Exception consumeEx)
                {
                    var rootException = UnwrapReflectionException(consumeEx);
                    SafeLog($"Error consuming {itemName} (item {i + 1}/{effectiveCount}): {rootException.Message}");
                    SafeLog(rootException.StackTrace ?? consumeEx.StackTrace ?? string.Empty);
                }
            }

            if (successfulConsumes > 0 && progressionNames.Length > 0 && !xmlProgressionEffectsOwnLearning)
            {
                ApplyMissingGainSkillLevels(
                    effectEntity,
                    progressionNames,
                    stackEffectSkillLevelsBefore,
                    successfulConsumes,
                    itemName,
                    effectiveCount,
                    "effect entity final stack reconciliation");

                if (!ReferenceEquals(effectEntity, player))
                {
                    ApplyMissingGainSkillLevels(
                        player,
                        progressionNames,
                        stackPlayerSkillLevelsBefore,
                        successfulConsumes,
                        itemName,
                        effectiveCount,
                        "context player final stack reconciliation");
                }
            }

            if (successfulConsumes > 1 && !xmlProgressionEffectsOwnLearning)
            {
                TryReconcileQuickListUnderGranted(
                    effectEntity,
                    stackCraftingSnapBefore,
                    successfulConsumes,
                    progressionNames,
                    itemName,
                    effectiveCount);
                if (!ReferenceEquals(effectEntity, player))
                {
                    TryReconcileQuickListUnderGranted(
                        player,
                        stackPlayerCraftingSnapBefore,
                        successfulConsumes,
                        progressionNames,
                        itemName,
                        effectiveCount);
                }
            }

            return successfulConsumes;
        }

        /// <summary>Fires <see cref="MinEventTypes.onSelfPrimaryActionStart"/> so paired End effects (e.g. magazines) match vanilla ordering.</summary>
        private static void TryFireOnSelfPrimaryActionStart(
            ItemClass itemClass,
            ItemValue itemValue,
            object gameManager,
            EntityAlive effectEntity,
            int itemSlotIndex,
            string itemName,
            int unitIndex,
            ItemStack stackForEvent)
        {
            try
            {
                var eventStack = stackForEvent ?? new ItemStack(itemValue, 1);
                var invData = GameReflection.CreateItemInventoryData(
                    itemClass,
                    eventStack,
                    gameManager,
                    effectEntity,
                    itemSlotIndex,
                    itemValue);
                if (invData == null)
                {
                    LogHypothesis($"TryFireOnSelfPrimaryActionStart [{unitIndex}] {itemName}: skipped (no ItemInventoryData)");
                    return;
                }

                var evtParams = GameReflection.CreateMinEventParams(effectEntity, itemValue, invData);
                if (evtParams == null)
                {
                    LogHypothesis($"TryFireOnSelfPrimaryActionStart [{unitIndex}] {itemName}: skipped (no MinEventParams)");
                    return;
                }

                itemClass.FireEvent(MinEventTypes.onSelfPrimaryActionStart, evtParams);
                LogHypothesis($"TryFireOnSelfPrimaryActionStart [{unitIndex}] {itemName}: fired onSelfPrimaryActionStart");
            }
            catch (Exception ex)
            {
                var root = UnwrapReflectionException(ex);
                SafeLog($"TryFireOnSelfPrimaryActionStart failed {itemName}: {root.Message}");
            }
        }

        /// <summary>
        /// Fires item <c>effect_groups</c> bound to <see cref="MinEventTypes.onSelfPrimaryActionEnd"/> (same as manual read completion).
        /// </summary>
        private static void TryFireOnSelfPrimaryActionEnd(
            ItemClass itemClass,
            ItemValue itemValue,
            object gameManager,
            EntityAlive effectEntity,
            int itemSlotIndex,
            string itemName,
            int unitIndex,
            string reason,
            ItemStack stackForEvent,
            object eventAction = null,
            int eventActionIndex = -1)
        {
            try
            {
                var eventStack = stackForEvent ?? new ItemStack(itemValue, 1);
                var invData = GameReflection.CreateItemInventoryData(
                    itemClass,
                    eventStack,
                    gameManager,
                    effectEntity,
                    itemSlotIndex,
                    itemValue);
                if (invData == null)
                {
                    LogHypothesis($"TryFireOnSelfPrimaryActionEnd [{unitIndex}] {itemName}: skipped (no ItemInventoryData) reason={reason}");
                    return;
                }

                var evtParams = GameReflection.CreateMinEventParams(effectEntity, itemValue, invData);
                if (evtParams == null)
                {
                    LogHypothesis($"TryFireOnSelfPrimaryActionEnd [{unitIndex}] {itemName}: skipped (no MinEventParams) reason={reason}");
                    return;
                }

                if (eventAction != null)
                {
                    object actionData = null;
                    var actionIndex = eventActionIndex >= 0 ? eventActionIndex : 0;
                    if (GameReflection.TryInvokeInstanceMethod(eventAction, "CreateModifierData", out actionData, invData, actionIndex) &&
                        actionData != null)
                    {
                        GameReflection.SetMemberValue(actionData, "EventParms", evtParams);
                        GameReflection.SetMemberValue(actionData, "eventParms", evtParams);
                        GameReflection.SetMemberValue(actionData, "EventParams", evtParams);
                        GameReflection.SetMemberValue(actionData, "invData", invData);
                        GameReflection.SetMemberValue(actionData, "indexInEntityOfAction", actionIndex);
                        GameReflection.SetMemberValue(evtParams, "ItemActionData", actionData);
                        GameReflection.SetMemberValue(evtParams, "itemActionData", actionData);
                        LogHypothesis($"TryFireOnSelfPrimaryActionEnd [{unitIndex}] {itemName}: attached actionData={actionData.GetType().Name} reason={reason}");
                    }
                    else
                    {
                        LogHypothesis($"TryFireOnSelfPrimaryActionEnd [{unitIndex}] {itemName}: no actionData for event action reason={reason}");
                    }
                }

                itemClass.FireEvent(MinEventTypes.onSelfPrimaryActionEnd, evtParams);
                LogHypothesis($"TryFireOnSelfPrimaryActionEnd [{unitIndex}] {itemName}: fired onSelfPrimaryActionEnd reason={reason}");
            }
            catch (Exception ex)
            {
                var root = UnwrapReflectionException(ex);
                SafeLog($"TryFireOnSelfPrimaryActionEnd failed ({reason}) {itemName}: {root.Message}");
            }
        }

        /// <summary>
        /// Prefers <c>ItemActionEat.ExecuteAction(ItemActionData, true)</c> (vanilla override of abstract <c>ItemAction.ExecuteAction</c>).
        /// Falls back to <c>consume</c> for older/stub types. <paramref name="stackForConsume"/> should be the live slot stack when possible.
        /// </summary>
        private static bool TryConsumeEatActionReflection(
            object eatAction,
            ItemClass itemClass,
            ItemStack stackForConsume,
            object gameManager,
            EntityAlive effectEntity,
            ItemValue itemValue,
            int itemSlotIndex,
            int eatActionIndex,
            string itemName,
            int unitIndex,
            int effectiveCount,
            int lootReadDebugId = 0,
            EntityPlayer debugPlayer = null,
            ItemStack debugLootBacking = null,
            XUiC_ItemStack debugUiController = null)
        {
            try
            {
                if (lootReadDebugId != 0)
                {
                    LogLootSnapshot(lootReadDebugId, $"TryConsume.enter unit={unitIndex}", debugPlayer, debugLootBacking, debugUiController);
                }

                var invData = GameReflection.CreateItemInventoryData(
                    itemClass,
                    stackForConsume,
                    gameManager,
                    effectEntity,
                    itemSlotIndex,
                    itemValue);
                if (invData == null)
                {
                    LogHypothesis($"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: CreateItemInventoryData returned null (stackForConsume.count={GameReflection.GetItemCount(stackForConsume)})");
                    return false;
                }
                if (lootReadDebugId != 0)
                {
                    LogHypothesis(
                        $"LootDbg#{lootReadDebugId} TryConsume.invData unit={unitIndex}: invDataRef={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(invData)} invStackRef={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(stackForConsume)} slotIdx={itemSlotIndex} holder={effectEntity?.GetType().FullName ?? "null"}");
                    LogLootSnapshot(lootReadDebugId, $"TryConsume.afterCreateInvData unit={unitIndex}", debugPlayer, debugLootBacking, debugUiController);
                }

                LogHypothesis(
                    $"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: invDataType={invData.GetType().Name} slotIdx={itemSlotIndex} will try CreateModifierData then ExecuteAction(true), else consume");

                object actionData = null;
                if (lootReadDebugId != 0)
                {
                    LogLootSnapshot(lootReadDebugId, $"TryConsume.beforeCreateModifierData unit={unitIndex}", debugPlayer, debugLootBacking, debugUiController);
                }
                if (!GameReflection.TryInvokeInstanceMethod(eatAction, "CreateModifierData", out actionData, invData, eatActionIndex) || actionData == null)
                {
                    if (eatActionIndex == 0)
                    {
                        LogHypothesis(
                            $"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: CreateModifierData(idx=0) failed; abort TryConsume");
                        return false;
                    }

                    if (!GameReflection.TryInvokeInstanceMethod(eatAction, "CreateModifierData", out actionData, invData, 0) || actionData == null)
                    {
                        LogHypothesis(
                            $"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: CreateModifierData(idx={eatActionIndex} and idx=0) both failed; abort TryConsume");
                        return false;
                    }
                }
                if (lootReadDebugId != 0)
                {
                    LogHypothesis(
                        $"LootDbg#{lootReadDebugId} TryConsume.actionData unit={unitIndex}: actionDataType={actionData?.GetType().FullName ?? "null"} actionDataRef={(actionData == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(actionData))}");
                    LogLootSnapshot(lootReadDebugId, $"TryConsume.afterCreateModifierData unit={unitIndex}", debugPlayer, debugLootBacking, debugUiController);
                }

                // Books/mags often apply perks via effect_groups that expect EventParms on ItemActionData (same as GainSkill/LearnRecipe paths).
                AttachEventParamsToActionData(
                    actionData,
                    itemClass,
                    itemValue,
                    invData,
                    gameManager,
                    effectEntity,
                    itemSlotIndex,
                    itemName,
                    unitIndex);

                // Vanilla ItemActionEat: ExecuteAction(released) may apply read logic without decrementing the stack;
                // if the stack count is unchanged, still invoke consume() so the item is actually removed (see mod API notes).
                var countBeforeEat = GameReflection.GetItemCount(stackForConsume);
                var invokedEatStep = false;
                if (lootReadDebugId != 0)
                {
                    LogLootSnapshot(lootReadDebugId, $"TryConsume.beforeExecuteAction unit={unitIndex} stackCount={countBeforeEat}", debugPlayer, debugLootBacking, debugUiController);
                }
                if (GameReflection.TryInvokeInstanceMethod(eatAction, "ExecuteAction", out var _, actionData, true))
                {
                    invokedEatStep = true;
                    LogHypothesis(
                        $"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: ExecuteAction(true) ran actionDataType={actionData?.GetType().Name ?? "null"}");
                }
                if (lootReadDebugId != 0)
                {
                    LogLootSnapshot(lootReadDebugId, $"TryConsume.afterExecuteAction unit={unitIndex} stackCount={GameReflection.GetItemCount(stackForConsume)}", debugPlayer, debugLootBacking, debugUiController);
                }

                var countAfterExecuteAction = GameReflection.GetItemCount(stackForConsume);
                if (countAfterExecuteAction >= countBeforeEat &&
                    GameReflection.TryInvokeInstanceMethod(eatAction, "consume", out var _, actionData))
                {
                    invokedEatStep = true;
                    LogHypothesis(
                        $"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: consume() ran (stack unchanged after ExecuteAction or no ExecuteAction) actionDataType={actionData?.GetType().Name ?? "null"}");
                }
                if (lootReadDebugId != 0)
                {
                    LogLootSnapshot(lootReadDebugId, $"TryConsume.afterConsume unit={unitIndex} stackCount={GameReflection.GetItemCount(stackForConsume)}", debugPlayer, debugLootBacking, debugUiController);
                }

                if (!invokedEatStep)
                {
                    LogHypothesis(
                        $"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: neither ExecuteAction(true) nor consume invoked actionDataType={actionData?.GetType().Name ?? "null"}");
                    return false;
                }

                if (GameReflection.TryInvokeInstanceMethod(eatAction, "Completed", out var _, actionData))
                {
                    LogHypothesis($"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: Completed() ran after eat action");
                }
                if (lootReadDebugId != 0)
                {
                    LogLootSnapshot(lootReadDebugId, $"TryConsume.afterCompleted unit={unitIndex}", debugPlayer, debugLootBacking, debugUiController);
                }

                var countEnd = GameReflection.GetItemCount(stackForConsume);
                if (countEnd >= countBeforeEat)
                {
                    LogHypothesis(
                        $"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: stack did not decrease (before={countBeforeEat}, after={countEnd}); caller may fall back to instant action");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                var root = UnwrapReflectionException(ex);
                LogHypothesis(
                    $"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: exception → {root.GetType().Name}: {root.Message}");
                return false;
            }
        }

        /// <summary>
        /// Vanilla read-from-ui path often uses ExecuteInstantAction(ent, stack, isHeldItem:false, stackController).
        /// Call when CreateModifierData+consume fails; see DESIGN.md (still FireEvent afterward when controller is null).
        /// </summary>
        private static bool TryExecuteInstantActionReflection(
            object eatAction,
            EntityAlive effectEntity,
            ItemStack stackForAction,
            string itemName,
            int unitIndex,
            int effectiveCount,
            XUiC_ItemStack stackController,
            bool treatSuccessfulInvokeAsSuccess)
        {
            try
            {
                if (effectEntity == null || eatAction == null)
                {
                    LogHypothesis(
                        $"ExecuteInstantAction [{unitIndex}/{effectiveCount}] {itemName}: skipped (entity or action null)");
                    return false;
                }

                EntityAlive alive = effectEntity;
                if (!GameReflection.TryInvokeInstanceMethod(
                        eatAction,
                        "ExecuteInstantAction",
                        out var ret,
                        alive,
                        stackForAction,
                        false,
                        stackController))
                {
                    LogHypothesis(
                        $"ExecuteInstantAction [{unitIndex}/{effectiveCount}] {itemName}: method not found or no matching overload");
                    return false;
                }

                var retBool = ret is bool b && b;
                LogHypothesis(
                    $"ExecuteInstantAction [{unitIndex}/{effectiveCount}] {itemName}: uiCtrl={(stackController == null ? "null" : "non-null")} retBool={retBool} treatInvokeOk={treatSuccessfulInvokeAsSuccess}");
                if (treatSuccessfulInvokeAsSuccess)
                {
                    // Vanilla often returns false when the stack is not fully depleted (count > 1) even after applying magazine/recipe effects.
                    return true;
                }

                return retBool;
            }
            catch (Exception ex)
            {
                var root = UnwrapReflectionException(ex);
                LogHypothesis(
                    $"ExecuteInstantAction [{unitIndex}/{effectiveCount}] {itemName}: exception → {root.GetType().Name}: {root.Message}");
                return false;
            }
        }

        /// <summary>
        /// Magazines and some books use <see cref="ItemActionGainSkill"/> for crafting-skill / perk application (see API ref).
        /// Uses <c>CreateModifierData</c> + <c>ExecuteAction(ItemActionData, bool)</c>.
        /// </summary>
        private static bool TryGainSkillExecuteActionReflection(
            object gainSkillAction,
            ItemClass itemClass,
            ItemStack stackForSkill,
            object gameManager,
            EntityAlive effectEntity,
            ItemValue itemValue,
            int itemSlotIndex,
            int gainSkillActionIndex,
            string itemName,
            int unitIndex,
            int effectiveCount,
            string[] progressionNames,
            Dictionary<string, int> skillLevelsBefore,
            int consumedUnits)
        {
            try
            {
                if (gainSkillAction == null)
                {
                    ApplyMissingGainSkillLevels(effectEntity, progressionNames, skillLevelsBefore, consumedUnits, itemName, unitIndex, "effect entity without gain action");
                    return false;
                }

                var invData = GameReflection.CreateItemInventoryData(
                    itemClass,
                    stackForSkill,
                    gameManager,
                    effectEntity,
                    itemSlotIndex,
                    itemValue);
                if (invData == null)
                {
                    LogHypothesis($"GainSkill [{unitIndex}/{effectiveCount}] {itemName}: CreateItemInventoryData returned null");
                    ApplyMissingGainSkillLevels(effectEntity, progressionNames, skillLevelsBefore, consumedUnits, itemName, unitIndex, "effect entity after missing ItemInventoryData");
                    return false;
                }

                object actionData = null;
                if (!GameReflection.TryInvokeInstanceMethod(gainSkillAction, "CreateModifierData", out actionData, invData, gainSkillActionIndex) ||
                    actionData == null)
                {
                    if (gainSkillActionIndex == 0)
                    {
                        LogHypothesis($"GainSkill [{unitIndex}/{effectiveCount}] {itemName}: CreateModifierData(idx=0) failed");
                        ApplyMissingGainSkillLevels(effectEntity, progressionNames, skillLevelsBefore, consumedUnits, itemName, unitIndex, "effect entity after failed CreateModifierData");
                        return false;
                    }

                    if (!GameReflection.TryInvokeInstanceMethod(gainSkillAction, "CreateModifierData", out actionData, invData, 0) || actionData == null)
                    {
                        LogHypothesis(
                            $"GainSkill [{unitIndex}/{effectiveCount}] {itemName}: CreateModifierData(idx={gainSkillActionIndex} and idx=0) both failed");
                        ApplyMissingGainSkillLevels(effectEntity, progressionNames, skillLevelsBefore, consumedUnits, itemName, unitIndex, "effect entity after failed CreateModifierData");
                        return false;
                    }
                }

                AttachEventParamsToActionData(
                    actionData,
                    itemClass,
                    itemValue,
                    invData,
                    gameManager,
                    effectEntity,
                    itemSlotIndex,
                    itemName,
                    unitIndex);

                if (GameReflection.TryInvokeInstanceMethod(gainSkillAction, "ExecuteAction", out var _, actionData, true))
                {
                    LogHypothesis($"GainSkill [{unitIndex}/{effectiveCount}] {itemName}: ExecuteAction(released=true) invoked");
                    ApplyMissingGainSkillLevels(effectEntity, progressionNames, skillLevelsBefore, consumedUnits, itemName, unitIndex, "effect entity");
                    return true;
                }

                if (GameReflection.TryInvokeInstanceMethod(gainSkillAction, "ExecuteAction", out var _, actionData, false))
                {
                    LogHypothesis($"GainSkill [{unitIndex}/{effectiveCount}] {itemName}: ExecuteAction(released=false) invoked");
                    ApplyMissingGainSkillLevels(effectEntity, progressionNames, skillLevelsBefore, consumedUnits, itemName, unitIndex, "effect entity");
                    return true;
                }

                LogHypothesis($"GainSkill [{unitIndex}/{effectiveCount}] {itemName}: ExecuteAction not invoked");
                ApplyMissingGainSkillLevels(effectEntity, progressionNames, skillLevelsBefore, consumedUnits, itemName, unitIndex, "effect entity after failed ExecuteAction");
                return false;
            }
            catch (Exception ex)
            {
                var root = UnwrapReflectionException(ex);
                LogHypothesis(
                    $"GainSkill [{unitIndex}/{effectiveCount}] {itemName}: exception → {root.GetType().Name}: {root.Message}");
                ApplyMissingGainSkillLevels(effectEntity, progressionNames, skillLevelsBefore, consumedUnits, itemName, unitIndex, "effect entity after exception");
                return false;
            }
        }

        /// <summary>
        /// Schematics/recipes use <see cref="ItemActionLearnRecipe"/> after <see cref="ItemActionEat"/> consumes.
        /// Required when <c>ItemInventoryData.slotIdx</c> is <c>-1</c> for open-loot reads (safe slot mapping).
        /// </summary>
        private static bool TryLearnRecipeExecuteActionReflection(
            object learnRecipeAction,
            ItemClass itemClass,
            ItemStack stackForRecipe,
            object gameManager,
            EntityAlive effectEntity,
            ItemValue itemValue,
            int itemSlotIndex,
            int learnRecipeActionIndex,
            string itemName,
            int unitIndex,
            int effectiveCount,
            int consumedUnits)
        {
            _ = consumedUnits;
            try
            {
                if (learnRecipeAction == null)
                {
                    return false;
                }

                var invData = GameReflection.CreateItemInventoryData(
                    itemClass,
                    stackForRecipe,
                    gameManager,
                    effectEntity,
                    itemSlotIndex,
                    itemValue);
                if (invData == null)
                {
                    LogHypothesis($"LearnRecipe [{unitIndex}/{effectiveCount}] {itemName}: CreateItemInventoryData returned null");
                    return false;
                }

                object actionData = null;
                if (!GameReflection.TryInvokeInstanceMethod(learnRecipeAction, "CreateModifierData", out actionData, invData, learnRecipeActionIndex) ||
                    actionData == null)
                {
                    if (learnRecipeActionIndex == 0)
                    {
                        LogHypothesis($"LearnRecipe [{unitIndex}/{effectiveCount}] {itemName}: CreateModifierData(idx=0) failed");
                        return false;
                    }

                    if (!GameReflection.TryInvokeInstanceMethod(learnRecipeAction, "CreateModifierData", out actionData, invData, 0) || actionData == null)
                    {
                        LogHypothesis(
                            $"LearnRecipe [{unitIndex}/{effectiveCount}] {itemName}: CreateModifierData(idx={learnRecipeActionIndex} and idx=0) both failed");
                        return false;
                    }
                }

                AttachEventParamsToActionData(
                    actionData,
                    itemClass,
                    itemValue,
                    invData,
                    gameManager,
                    effectEntity,
                    itemSlotIndex,
                    itemName,
                    unitIndex);

                if (GameReflection.TryInvokeInstanceMethod(learnRecipeAction, "ExecuteAction", out var _, actionData, true))
                {
                    LogHypothesis($"LearnRecipe [{unitIndex}/{effectiveCount}] {itemName}: ExecuteAction(released=true) invoked");
                    return true;
                }

                if (GameReflection.TryInvokeInstanceMethod(learnRecipeAction, "ExecuteAction", out var _, actionData, false))
                {
                    LogHypothesis($"LearnRecipe [{unitIndex}/{effectiveCount}] {itemName}: ExecuteAction(released=false) invoked");
                    return true;
                }

                LogHypothesis($"LearnRecipe [{unitIndex}/{effectiveCount}] {itemName}: ExecuteAction not invoked");
                return false;
            }
            catch (Exception ex)
            {
                var root = UnwrapReflectionException(ex);
                LogHypothesis(
                    $"LearnRecipe [{unitIndex}/{effectiveCount}] {itemName}: exception → {root.GetType().Name}: {root.Message}");
                return false;
            }
        }

        /// <summary>
        /// When <see cref="ItemActionLearnRecipe.ExecuteAction"/> does not unlock (e.g. <c>slotIdx=-1</c> loot reads),
        /// mirror vanilla by calling <see cref="CraftingManager.UnlockRecipe(string, EntityPlayer)"/> for known recipe names.
        /// </summary>
        private static void TryUnlockLootSchematicRecipesViaCraftingManager(
            EntityPlayer player,
            object gameManager,
            ItemClass itemClass,
            System.Array itemActions,
            string itemName,
            int unitIndex)
        {
            if (player == null)
            {
                LogHypothesis($"LootRecipeFallback [{unitIndex}] {itemName}: skipped (no player)");
                return;
            }

            // UnlockRecipe is authored for the local player in SP; context patch player may differ on some paths.
            var unlockPlayer = GameReflection.TryGetLocalEntityPlayer() ?? player;

            var craftingManager = GameReflection.TryResolveCraftingManager(gameManager, unlockPlayer);
            if (craftingManager == null)
            {
                LogHypothesis($"LootRecipeFallback [{unitIndex}] {itemName}: CraftingManager not resolved");
                return;
            }

            var recipeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (itemActions != null)
            {
                for (var ai = 0; ai < itemActions.Length; ai++)
                {
                    var action = itemActions.GetValue(ai);
                    if (action == null || action.GetType().Name != "ItemActionLearnRecipe")
                    {
                        continue;
                    }

                    CollectRecipesToLearnFromLearnRecipeAction(action, recipeNames);
                }
            }

            AddSchematicRecipeNameHeuristics(itemClass, recipeNames);

            if (recipeNames.Count == 0)
            {
                LogHypothesis($"LootRecipeFallback [{unitIndex}] {itemName}: no candidate recipe names");
                return;
            }

            foreach (var recipe in recipeNames)
            {
                if (string.IsNullOrWhiteSpace(recipe))
                {
                    continue;
                }

                if (TryCraftingManagerUnlockRecipeByNameOrObject(craftingManager, recipe, unlockPlayer, unitIndex, itemName))
                {
                    LogHypothesis($"LootRecipeFallback [{unitIndex}] {itemName}: UnlockRecipe for \"{recipe}\" invoked");
                }
                else
                {
                    LogHypothesis(
                        $"LootRecipeFallback [{unitIndex}] {itemName}: UnlockRecipe for \"{recipe}\" not applied (no matching overload or GetRecipe miss)");
                }
            }
        }

        /// <summary>Tries <c>UnlockRecipe(string, EntityPlayer)</c> then <c>GetRecipe(string)</c> + <c>UnlockRecipe(Recipe, EntityPlayer)</c>.</summary>
        private static bool TryCraftingManagerUnlockRecipeByNameOrObject(
            object craftingManager,
            string recipeName,
            EntityPlayer unlockPlayer,
            int unitIndex,
            string itemName)
        {
            if (craftingManager == null || string.IsNullOrWhiteSpace(recipeName) || unlockPlayer == null)
            {
                return false;
            }

            if (GameReflection.TryInvokeInstanceMethod(craftingManager, "UnlockRecipe", out var _, recipeName, unlockPlayer))
            {
                return true;
            }

            if (!GameReflection.TryInvokeInstanceMethod(craftingManager, "GetRecipe", out var recipeObj, recipeName) ||
                recipeObj == null)
            {
                return false;
            }

            return GameReflection.TryInvokeInstanceMethod(craftingManager, "UnlockRecipe", out var _, recipeObj, unlockPlayer);
        }

        private static void CollectRecipesToLearnFromLearnRecipeAction(object learnRecipeAction, HashSet<string> recipeNames)
        {
            if (learnRecipeAction == null)
            {
                return;
            }

            try
            {
                AddRecipeNameStringsFromArrayMember(learnRecipeAction, "RecipesToLearn", recipeNames);
                AddRecipeNameStringsFromArrayMember(learnRecipeAction, "recipesToLearn", recipeNames);

                var t = learnRecipeAction.GetType();
                foreach (var fi in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (fi.FieldType != typeof(string[]))
                    {
                        continue;
                    }

                    AddRecipeNameStringsFromArray(fi.GetValue(learnRecipeAction) as string[], recipeNames);
                }

                foreach (var pi in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (pi.PropertyType != typeof(string[]) || !pi.CanRead || pi.GetIndexParameters().Length != 0)
                    {
                        continue;
                    }

                    AddRecipeNameStringsFromArray(pi.GetValue(learnRecipeAction, null) as string[], recipeNames);
                }
            }
            catch
            {
                // ignore
            }
        }

        private static void AddRecipeNameStringsFromArrayMember(object instance, string memberName, HashSet<string> recipeNames)
        {
            try
            {
                var arr = GameReflection.GetMemberValue<string[]>(instance, memberName);
                AddRecipeNameStringsFromArray(arr, recipeNames);
            }
            catch
            {
                // ignore
            }
        }

        private static void AddRecipeNameStringsFromArray(string[] arr, HashSet<string> recipeNames)
        {
            if (arr == null || recipeNames == null)
            {
                return;
            }

            for (var i = 0; i < arr.Length; i++)
            {
                var entry = arr[i];
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    recipeNames.Add(entry.Trim());
                }
            }
        }

        private static void AddSchematicRecipeNameHeuristics(ItemClass itemClass, HashSet<string> recipeNames)
        {
            if (itemClass == null)
            {
                return;
            }

            try
            {
                var moldObj = GameReflection.GetMemberValue<object>(itemClass, "MoldTarget");
                if (moldObj is ItemClass moldTarget)
                {
                    var moldName = GameReflection.GetItemClassName(moldTarget);
                    if (!string.IsNullOrWhiteSpace(moldName))
                    {
                        recipeNames.Add(moldName.Trim());
                    }
                }
            }
            catch
            {
                // ignore
            }

            var className = GameReflection.GetItemClassName(itemClass);
            if (string.IsNullOrEmpty(className))
            {
                return;
            }

            const string schematicSuffix = "schematic";
            if (className.EndsWith(schematicSuffix, StringComparison.OrdinalIgnoreCase) &&
                className.Length > schematicSuffix.Length)
            {
                recipeNames.Add(className.Substring(0, className.Length - schematicSuffix.Length));
            }

            const string recipeSuffix = "recipe";
            if (className.EndsWith(recipeSuffix, StringComparison.OrdinalIgnoreCase) &&
                className.Length > recipeSuffix.Length)
            {
                recipeNames.Add(className.Substring(0, className.Length - recipeSuffix.Length));
            }
        }

        private static Dictionary<string, int> CaptureGainSkillLevels(string[] progressionNames, EntityAlive effectEntity)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (progressionNames == null || progressionNames.Length == 0)
            {
                return result;
            }

            for (var i = 0; i < progressionNames.Length; i++)
            {
                var skill = progressionNames[i];
                if (string.IsNullOrWhiteSpace(skill) || result.ContainsKey(skill))
                {
                    continue;
                }

                result[skill] = GetProgressionLevel(effectEntity, skill);
            }

            return result;
        }

        private static void ApplyMissingGainSkillLevels(
            EntityAlive effectEntity,
            string[] progressionNames,
            Dictionary<string, int> beforeLevels,
            int consumedUnits,
            string itemName,
            int unitIndex,
            string targetLabel)
        {
            if (beforeLevels == null || beforeLevels.Count == 0 || consumedUnits <= 0)
            {
                LogHypothesis(
                    $"GainSkillFallback [{unitIndex}] {itemName}: skipped for {targetLabel} (progressions={progressionNames?.Length ?? 0}, before={beforeLevels?.Count ?? 0}, consumed={consumedUnits})");
                return;
            }

            if (progressionNames == null || progressionNames.Length == 0)
            {
                return;
            }

            for (var i = 0; i < progressionNames.Length; i++)
            {
                var skill = progressionNames[i];
                if (string.IsNullOrWhiteSpace(skill) || !beforeLevels.TryGetValue(skill, out var beforeLevel))
                {
                    continue;
                }

                var afterLevel = GetProgressionLevel(effectEntity, skill);
                var pointsPerRead = GetMagazinePointsPerRead();
                var targetLevel = beforeLevel + (consumedUnits * pointsPerRead);
                var maxLevel = TryGetProgressionMaxLevel(GetProgressionValue(effectEntity, skill), effectEntity);
                if (maxLevel > 0)
                {
                    targetLevel = Math.Min(targetLevel, maxLevel);
                }

                if (afterLevel >= targetLevel)
                {
                    LogHypothesis($"GainSkillFallback [{unitIndex}] {itemName}: vanilla raised {skill} {beforeLevel}->{afterLevel} on {targetLabel}");
                    continue;
                }

                if (TrySetProgressionLevel(effectEntity, skill, targetLevel))
                {
                    LogHypothesis($"GainSkillFallback [{unitIndex}] {itemName}: directly raised {skill} {afterLevel}->{targetLevel} on {targetLabel}");
                }
                else
                {
                    LogHypothesis($"GainSkillFallback [{unitIndex}] {itemName}: failed to directly raise {skill} on {targetLabel}");
                }
            }
        }

        private sealed class SyntheticReadableEffects
        {
            internal static readonly SyntheticReadableEffects Empty = new SyntheticReadableEffects(
                new string[0],
                new string[0],
                new string[0]);

            internal SyntheticReadableEffects(
                string[] addProgressionNames,
                string[] maxProgressionNames,
                string[] setCVarNames,
                int[] addProgressionXmlLevels = null,
                string completeProgressionName = null,
                string completeSkillName = null,
                int completeRequiredCount = 0,
                string readSoundName = null,
                string bookSetCompleteSoundName = null)
            {
                AddProgressionNames = addProgressionNames ?? new string[0];
                MaxProgressionNames = maxProgressionNames ?? new string[0];
                SetCVarNames = setCVarNames ?? new string[0];
                AddProgressionXmlLevels = NormalizeAddProgressionXmlLevels(AddProgressionNames.Length, addProgressionXmlLevels);
                CompleteProgressionName = string.IsNullOrWhiteSpace(completeProgressionName)
                    ? null
                    : completeProgressionName.Trim();
                CompleteSkillName = string.IsNullOrWhiteSpace(completeSkillName)
                    ? null
                    : completeSkillName.Trim();
                CompleteRequiredCount = completeRequiredCount > 0 ? completeRequiredCount : 0;
                ReadSoundName = string.IsNullOrWhiteSpace(readSoundName) ? null : readSoundName.Trim();
                BookSetCompleteSoundName = string.IsNullOrWhiteSpace(bookSetCompleteSoundName)
                    ? null
                    : bookSetCompleteSoundName.Trim();
            }

            internal string[] AddProgressionNames { get; }
            internal int[] AddProgressionXmlLevels { get; }
            internal string[] MaxProgressionNames { get; }
            internal string[] SetCVarNames { get; }
            internal string CompleteProgressionName { get; }
            internal string CompleteSkillName { get; }
            internal int CompleteRequiredCount { get; }
            internal string ReadSoundName { get; }
            internal string BookSetCompleteSoundName { get; }
            internal bool HasAny =>
                AddProgressionNames.Length > 0 || MaxProgressionNames.Length > 0 || SetCVarNames.Length > 0;
            internal bool HasBookSetCompletion =>
                !string.IsNullOrWhiteSpace(CompleteProgressionName) &&
                !string.IsNullOrWhiteSpace(CompleteSkillName) &&
                CompleteRequiredCount > 0;

            internal int GetAddProgressionXmlLevel(int index)
            {
                if (index < 0 || index >= AddProgressionXmlLevels.Length)
                {
                    return 1;
                }

                return Math.Max(0, AddProgressionXmlLevels[index]);
            }
        }

        private static int[] NormalizeAddProgressionXmlLevels(int nameCount, int[] levels)
        {
            var result = new int[Math.Max(0, nameCount)];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = 1;
            }

            if (levels == null)
            {
                return result;
            }

            for (var i = 0; i < result.Length && i < levels.Length; i++)
            {
                result[i] = levels[i] > 0 ? levels[i] : 1;
            }

            return result;
        }

        private static string[] ResolveGainSkillProgressionNames(
            object gainSkillAction,
            ItemClass itemClass,
            SyntheticReadableEffects syntheticReadableEffects = null)
        {
            var names = new List<string>();
            AddProgressionNames(names, GetSkillsToGain(gainSkillAction));

            var craftingSkillGroup = GameReflection.GetMemberValue<string>(itemClass, "CraftingSkillGroup");
            if (string.IsNullOrWhiteSpace(craftingSkillGroup))
            {
                craftingSkillGroup = GameReflection.GetMemberValue<string>(itemClass, "PropCraftingSkillGroup");
            }

            if (!string.IsNullOrWhiteSpace(craftingSkillGroup))
            {
                AddProgressionName(names, MapCraftingSkillGroupToProgressionName(craftingSkillGroup));
            }

            if (syntheticReadableEffects != null)
            {
                AddProgressionNames(names, syntheticReadableEffects.AddProgressionNames);
            }

            return names.ToArray();
        }

        private static bool IsSkillMagazineItemClass(ItemClass itemClass)
        {
            var itemClassName = GameReflection.GetItemClassName(itemClass);
            return !string.IsNullOrWhiteSpace(itemClassName) &&
                   itemClassName.EndsWith("SkillMagazine", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplySyntheticXmlReadableEffects(
            EntityAlive entity,
            SyntheticReadableEffects effects,
            int consumedUnits,
            string itemName,
            int unitIndex,
            string targetLabel,
            Dictionary<string, int> addProgressionBaseLevels = null)
        {
            if (entity == null || effects == null || consumedUnits <= 0 || !effects.HasAny)
            {
                return;
            }

            var sandboxPoints = GetMagazinePointsPerRead();

            for (var i = 0; i < effects.AddProgressionNames.Length; i++)
            {
                var skill = effects.AddProgressionNames[i];
                var before = GetProgressionLevel(entity, skill);
                var baseLevel = before;
                if (addProgressionBaseLevels != null &&
                    addProgressionBaseLevels.TryGetValue(skill, out var capturedBaseLevel))
                {
                    baseLevel = capturedBaseLevel;
                }

                var xmlLevel = effects.GetAddProgressionXmlLevel(i);
                var pointsPerRead = Math.Max(0, xmlLevel * sandboxPoints);
                var target = baseLevel + (consumedUnits * pointsPerRead);
                if (before >= target)
                {
                    LogHypothesis(
                        $"XmlSyntheticEffects [{unitIndex}] {itemName}: {skill} already satisfied at {before} (target {target}, base {baseLevel}, pointsPerRead {pointsPerRead}) on {targetLabel}");
                    continue;
                }

                // Vanilla only substitutes PointsPerMagazine when AddProgressionLevel level==1.
                // Prefer one level=1 call per remaining magazine-read so Execute applies sandbox.
                var remainingPoints = target - before;
                var remainingReads = pointsPerRead > 0
                    ? Math.Max(1, (int)Math.Ceiling(remainingPoints / (double)pointsPerRead))
                    : remainingPoints;
                var minEventOk = false;
                for (var read = 0; read < remainingReads; read++)
                {
                    if (TryExecuteProgressionLevelMinEvent(
                            entity,
                            "MinEventActionAddProgressionLevel",
                            skill,
                            Math.Max(1, xmlLevel)))
                    {
                        minEventOk = true;
                    }
                }

                var afterMinEvent = GetProgressionLevel(entity, skill);
                if (minEventOk && afterMinEvent >= target)
                {
                    LogHypothesis(
                        $"XmlSyntheticEffects [{unitIndex}] {itemName}: min-event raised {skill} {before}->{afterMinEvent} on {targetLabel} (pointsPerRead={pointsPerRead})");
                }
                else if (TrySetProgressionLevel(entity, skill, target))
                {
                    LogHypothesis(
                        $"XmlSyntheticEffects [{unitIndex}] {itemName}: raised {skill} {before}->{GetProgressionLevel(entity, skill)} on {targetLabel} (pointsPerRead={pointsPerRead})");
                }
                else
                {
                    LogHypothesis(
                        $"XmlSyntheticEffects [{unitIndex}] {itemName}: failed raising {skill} from {before} on {targetLabel}");
                }
            }

            for (var i = 0; i < effects.MaxProgressionNames.Length; i++)
            {
                var skill = effects.MaxProgressionNames[i];
                var before = GetProgressionLevel(entity, skill);
                var progressionValue = GetProgressionValue(entity, skill);
                var max = TryGetProgressionMaxLevel(progressionValue, entity);
                var target = max > 0 ? max : Math.Max(before, 1);
                var minEventOk = TryExecuteProgressionLevelMinEvent(
                    entity,
                    "MinEventActionSetProgressionLevel",
                    skill,
                    -1);
                var afterMinEvent = GetProgressionLevel(entity, skill);
                if (minEventOk && afterMinEvent > before)
                {
                    LogHypothesis(
                        $"XmlSyntheticEffects [{unitIndex}] {itemName}: min-event set {skill} {before}->{afterMinEvent} on {targetLabel}");
                }
                else if (TrySetProgressionLevel(entity, skill, target))
                {
                    LogHypothesis(
                        $"XmlSyntheticEffects [{unitIndex}] {itemName}: set {skill} {before}->{GetProgressionLevel(entity, skill)} on {targetLabel}");
                }
                else
                {
                    LogHypothesis(
                        $"XmlSyntheticEffects [{unitIndex}] {itemName}: failed setting {skill} from {before} on {targetLabel}");
                }
            }

            for (var i = 0; i < effects.SetCVarNames.Length; i++)
            {
                var cvar = effects.SetCVarNames[i];
                if (TrySetEntityCVar(entity, cvar, 1f))
                {
                    LogHypothesis(
                        $"XmlSyntheticEffects [{unitIndex}] {itemName}: set cvar {cvar}=1 on {targetLabel}");
                }
                else
                {
                    LogHypothesis(
                        $"XmlSyntheticEffects [{unitIndex}] {itemName}: failed setting cvar {cvar}=1 on {targetLabel}");
                }
            }
        }

        /// <summary>
        /// Magazine double skill-point roll for Nerd Outfit and/or INT Mastery Photographic Memory.
        /// Vanilla merges both into <c>$nerdOutfitSkillPointChance</c>; INT-only players may only have
        /// <c>$perkIntellectMasteryPointChance</c> populated.
        /// </summary>
        private static void ApplyMagazineDoublePointBonusFromGameCVar(
            EntityAlive entity,
            string[] progressionNames,
            int consumedUnits,
            string itemName,
            int unitIndex,
            string targetLabel,
            Dictionary<string, int> baseLevels)
        {
            if (entity == null || progressionNames == null || progressionNames.Length == 0 || consumedUnits <= 0)
            {
                return;
            }

            if (!IsEligibleForMagazineDoublePointBonus(entity))
            {
                LogHypothesis(
                    $"MagDoublePoint [{unitIndex}] {itemName}: skipped on {targetLabel} (no buffNerdOutfit and perkIntellectMastery < 3)");
                return;
            }

            if (!TryResolveMagazineDoublePointChance(entity, out var chancePercent, out var chanceCVarName) ||
                chancePercent <= 0f)
            {
                LogHypothesis(
                    $"MagDoublePoint [{unitIndex}] {itemName}: skipped on {targetLabel} (no positive chance CVar)");
                return;
            }

            // The game owns outfit quality and other modifiers through this CVar; the mod only consumes the already-computed chance.
            if (chancePercent <= 1f)
            {
                chancePercent *= 100f;
            }

            chancePercent = Math.Max(0f, Math.Min(100f, chancePercent));

            var pointsPerRead = GetMagazinePointsPerRead();
            if (pointsPerRead <= 0)
            {
                LogHypothesis(
                    $"MagDoublePoint [{unitIndex}] {itemName}: skipped on {targetLabel} (PointsPerMagazine={pointsPerRead})");
                return;
            }

            var expectedBaseDelta = consumedUnits * pointsPerRead;

            for (var i = 0; i < progressionNames.Length; i++)
            {
                var skill = progressionNames[i];
                if (string.IsNullOrWhiteSpace(skill))
                {
                    continue;
                }

                var before = GetProgressionLevel(entity, skill);
                if (baseLevels != null &&
                    baseLevels.TryGetValue(skill, out var baseLevel) &&
                    before > baseLevel + expectedBaseDelta)
                {
                    LogHypothesis(
                        $"MagDoublePoint [{unitIndex}] {itemName}: skipped {skill} on {targetLabel}; game event already granted extra ({baseLevel}->{before}, expectedBaseDelta={expectedBaseDelta})");
                    continue;
                }

                double roll;
                lock (NerdBonusRandomGate)
                {
                    roll = NerdBonusRandom.NextDouble() * 100.0;
                }

                if (roll > chancePercent)
                {
                    LogHypothesis(
                        $"MagDoublePoint [{unitIndex}] {itemName}: missed {skill} on {targetLabel} roll={roll:0.##} chance={chancePercent:0.##} cvar={chanceCVarName}");
                    continue;
                }

                // Pass XML level=1 so vanilla Execute can apply sandbox; fallback uses full pointsPerRead.
                var minEventOk = TryExecuteProgressionLevelMinEvent(
                    entity,
                    "MinEventActionAddProgressionLevel",
                    skill,
                    1);
                var after = GetProgressionLevel(entity, skill);
                if (minEventOk && after >= before + pointsPerRead)
                {
                    LogHypothesis(
                        $"MagDoublePoint [{unitIndex}] {itemName}: granted {skill} {before}->{after} on {targetLabel} roll={roll:0.##} chance={chancePercent:0.##} cvar={chanceCVarName} points={pointsPerRead}");
                }
                else if (TrySetProgressionLevel(entity, skill, before + pointsPerRead))
                {
                    LogHypothesis(
                        $"MagDoublePoint [{unitIndex}] {itemName}: fallback granted {skill} {before}->{GetProgressionLevel(entity, skill)} on {targetLabel} roll={roll:0.##} chance={chancePercent:0.##} cvar={chanceCVarName} points={pointsPerRead}");
                }
                else
                {
                    LogHypothesis(
                        $"MagDoublePoint [{unitIndex}] {itemName}: failed grant {skill} on {targetLabel} roll={roll:0.##} chance={chancePercent:0.##} cvar={chanceCVarName}");
                }
            }
        }

        private static bool IsEligibleForMagazineDoublePointBonus(EntityAlive entity)
        {
            if (entity == null)
            {
                return false;
            }

            if (EntityHasBuff(entity, "buffNerdOutfit"))
            {
                return true;
            }

            return GetProgressionLevel(entity, "perkIntellectMastery") >= 3;
        }

        private static bool TryResolveMagazineDoublePointChance(
            EntityAlive entity,
            out float chancePercent,
            out string chanceCVarName)
        {
            chancePercent = 0f;
            chanceCVarName = "$nerdOutfitSkillPointChance";
            if (TryGetEntityCVar(entity, chanceCVarName, out chancePercent) && chancePercent > 0f)
            {
                return true;
            }

            chanceCVarName = "nerdOutfitSkillPointChance";
            if (TryGetEntityCVar(entity, chanceCVarName, out chancePercent) && chancePercent > 0f)
            {
                return true;
            }

            if (GetProgressionLevel(entity, "perkIntellectMastery") >= 3)
            {
                chanceCVarName = "$perkIntellectMasteryPointChance";
                if (TryGetEntityCVar(entity, chanceCVarName, out chancePercent) && chancePercent > 0f)
                {
                    return true;
                }

                chanceCVarName = "perkIntellectMasteryPointChance";
                if (TryGetEntityCVar(entity, chanceCVarName, out chancePercent) && chancePercent > 0f)
                {
                    return true;
                }
            }

            chancePercent = 0f;
            return false;
        }

        private static bool EntityHasBuff(EntityAlive entity, string buffName)
        {
            if (entity == null || string.IsNullOrWhiteSpace(buffName))
            {
                return false;
            }

            try
            {
                var buffs = GameReflection.GetMemberValue<object>(entity, "Buffs")
                            ?? GameReflection.GetMemberValue<object>(entity, "buffs");
                if (buffs == null)
                {
                    return false;
                }

                if (GameReflection.TryInvokeInstanceMethod(buffs, "HasBuff", out var hasBuffResult, buffName) &&
                    hasBuffResult is bool hasBuff)
                {
                    return hasBuff;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        /// <summary>
        /// When <see cref="ModConfig.SkipAlreadyRead"/> is on: true if this unit would grant no progression/recipe benefit.
        /// Unresolved names / missing CraftingManager → false (do not skip).
        /// </summary>
        private static bool ShouldSkipReadableUnit(
            EntityAlive effectEntity,
            EntityPlayer player,
            object gameManager,
            ItemClass itemClass,
            System.Array itemActions,
            ReadableItemKind itemKind,
            string[] progressionNames,
            SyntheticReadableEffects xmlReadableEffects,
            object gainSkillAction,
            object learnRecipeAction)
        {
            if (effectEntity == null)
            {
                return false;
            }

            var effects = xmlReadableEffects ?? SyntheticReadableEffects.Empty;

            switch (itemKind)
            {
                case ReadableItemKind.Magazine:
                    return AreAllAddProgressionsAtMax(effectEntity, progressionNames, effects.AddProgressionNames);

                case ReadableItemKind.Book:
                    return IsBookAlreadyRead(effectEntity, effects);

                case ReadableItemKind.Schematic:
                case ReadableItemKind.Recipe:
                    return IsSchematicAlreadyKnown(player, gameManager, itemClass, itemActions, learnRecipeAction, effects);

                default:
                    return false;
            }
        }

        private static bool AreAllAddProgressionsAtMax(
            EntityAlive entity,
            string[] progressionNames,
            string[] xmlAddProgressionNames)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (progressionNames != null)
            {
                for (var i = 0; i < progressionNames.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(progressionNames[i]))
                    {
                        names.Add(progressionNames[i]);
                    }
                }
            }

            if (xmlAddProgressionNames != null)
            {
                for (var i = 0; i < xmlAddProgressionNames.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(xmlAddProgressionNames[i]))
                    {
                        names.Add(xmlAddProgressionNames[i]);
                    }
                }
            }

            if (names.Count == 0)
            {
                return false;
            }

            foreach (var skill in names)
            {
                // Magazines: only skip when max is known and reached. Unknown max → do not skip.
                if (!IsProgressionAtKnownMax(entity, skill))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBookAlreadyRead(EntityAlive entity, SyntheticReadableEffects effects)
        {
            if (effects == null)
            {
                return false;
            }

            var hasMax = effects.MaxProgressionNames != null && effects.MaxProgressionNames.Length > 0;
            var hasCVars = effects.SetCVarNames != null && effects.SetCVarNames.Length > 0;
            if (!hasMax && !hasCVars)
            {
                return false;
            }

            if (hasMax)
            {
                for (var i = 0; i < effects.MaxProgressionNames.Length; i++)
                {
                    var skill = effects.MaxProgressionNames[i];
                    if (string.IsNullOrWhiteSpace(skill))
                    {
                        continue;
                    }

                    // Book perks are usually max_level=1; if max cannot be resolved, level ≥ 1 means unlocked.
                    if (!IsBookProgressionAlreadyUnlocked(entity, skill))
                    {
                        return false;
                    }
                }
            }

            if (hasCVars)
            {
                for (var i = 0; i < effects.SetCVarNames.Length; i++)
                {
                    var cvar = effects.SetCVarNames[i];
                    if (string.IsNullOrWhiteSpace(cvar))
                    {
                        continue;
                    }

                    if (!TryGetEntityCVar(entity, cvar, out var value) || value < 1f)
                    {
                        return false;
                    }
                }
            }

            if (effects.HasBookSetCompletion)
            {
                if (!IsBookPerkUnlocked(entity, effects.CompleteProgressionName))
                {
                    var unlocked = CountBookSetUnlockedPerks(entity, effects.CompleteSkillName);
                    if (unlocked >= effects.CompleteRequiredCount)
                    {
                        // Stuck 7/7 without Complete — still need a matching book to repair.
                        return false;
                    }
                }
            }

            return true;
        }

        private static int GetMagazinePointsPerRead()
        {
            if (_magazinePointsPerReadOverrideForTest.HasValue)
            {
                return Math.Max(0, _magazinePointsPerReadOverrideForTest.Value);
            }

            try
            {
                // Vanilla MinEventActionAddProgressionLevel calls:
                //   SandboxOptionManager.GetInt(SandboxOptions.PointsPerMagazine)
                // as a *static* method (see IL: call, not callvirt).
                var managerType = FindLoadedType("SandboxOptions.SandboxOptionManager")
                                  ?? FindLoadedType("SandboxOptionManager");
                if (managerType == null)
                {
                    LogHypothesis("PointsPerMagazine: SandboxOptionManager type not found; defaulting to 1");
                    return 1;
                }

                var optionsType = FindLoadedType("SandboxOptions.SandboxOptions")
                                  ?? FindLoadedType("SandboxOptions");
                if (optionsType == null || !optionsType.IsEnum)
                {
                    LogHypothesis("PointsPerMagazine: SandboxOptions enum not found; defaulting to 1");
                    return 1;
                }

                object pointsPerMagazineEnum;
                try
                {
                    pointsPerMagazineEnum = Enum.Parse(optionsType, "PointsPerMagazine");
                }
                catch
                {
                    LogHypothesis("PointsPerMagazine: enum value missing; defaulting to 1");
                    return 1;
                }

                var getInt = managerType.GetMethod(
                    "GetInt",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { optionsType },
                    null);
                if (getInt == null)
                {
                    // Fallback: any static GetInt(enum)
                    var candidates = managerType.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    for (var i = 0; i < candidates.Length; i++)
                    {
                        var m = candidates[i];
                        if (m.Name != "GetInt")
                        {
                            continue;
                        }

                        var ps = m.GetParameters();
                        if (ps.Length == 1 && ps[0].ParameterType == optionsType)
                        {
                            getInt = m;
                            break;
                        }
                    }
                }

                if (getInt == null)
                {
                    LogHypothesis("PointsPerMagazine: static GetInt not found; defaulting to 1");
                    return 1;
                }

                var raw = getInt.Invoke(null, new[] { pointsPerMagazineEnum });
                if (raw != null)
                {
                    var points = Convert.ToInt32(raw);
                    return Math.Max(0, points);
                }
            }
            catch (Exception ex)
            {
                LogHypothesis($"PointsPerMagazine: read failed ({ex.GetType().Name}: {ex.Message}); defaulting to 1");
            }

            return 1;
        }

        private static void TryGrantBookSetCompletionIfEligible(EntityAlive entity, ItemClass itemClass)
        {
            if (entity == null || itemClass == null)
            {
                return;
            }

            var effects = ResolveSyntheticReadableEffects(itemClass);
            if (effects == null || !effects.HasBookSetCompletion)
            {
                return;
            }

            if (IsBookPerkUnlocked(entity, effects.CompleteProgressionName))
            {
                return;
            }

            var unlocked = CountBookSetUnlockedPerks(entity, effects.CompleteSkillName);
            if (unlocked < effects.CompleteRequiredCount)
            {
                LogHypothesis(
                    $"BookSetComplete: {effects.CompleteProgressionName} not eligible ({unlocked}/{effects.CompleteRequiredCount} under {effects.CompleteSkillName})");
                return;
            }

            var before = GetProgressionLevel(entity, effects.CompleteProgressionName);
            var progressionValue = GetProgressionValue(entity, effects.CompleteProgressionName);
            var max = TryGetProgressionMaxLevel(progressionValue, entity);
            // Vanilla Complete uses SetProgressionLevel level=-1 (max). Prefer max when known and small
            // (book perks are usually max_level=1); otherwise unlock at least level 1.
            var target = max > 0 && max <= 10 ? max : Math.Max(before, 1);
            var minEventOk = TryExecuteProgressionLevelMinEvent(
                entity,
                "MinEventActionSetProgressionLevel",
                effects.CompleteProgressionName,
                -1);
            var afterMinEvent = GetProgressionLevel(entity, effects.CompleteProgressionName);
            var granted = false;
            if (minEventOk && afterMinEvent > before)
            {
                granted = true;
                LogHypothesis(
                    $"BookSetComplete: min-event set {effects.CompleteProgressionName} {before}->{afterMinEvent}");
            }
            else if (TrySetProgressionLevel(entity, effects.CompleteProgressionName, target))
            {
                granted = IsBookPerkUnlocked(entity, effects.CompleteProgressionName);
                LogHypothesis(
                    $"BookSetComplete: set {effects.CompleteProgressionName} {before}->{GetProgressionLevel(entity, effects.CompleteProgressionName)}");
            }
            else
            {
                LogHypothesis(
                    $"BookSetComplete: failed setting {effects.CompleteProgressionName} from {before}");
            }

            if (granted)
            {
                TryPlayBookSetCompleteSound(entity, effects);
            }
        }

        private static void TryPlayReadableUnitSound(
            EntityAlive entity,
            ReadableItemKind itemKind,
            SyntheticReadableEffects effects)
        {
            if (!ModConfig.Instance.PlayReadSounds)
            {
                return;
            }

            var sound = effects?.ReadSoundName;
            if (string.IsNullOrWhiteSpace(sound))
            {
                switch (itemKind)
                {
                    case ReadableItemKind.Book:
                        sound = "read_skillbook";
                        break;
                    case ReadableItemKind.Magazine:
                    case ReadableItemKind.Schematic:
                    case ReadableItemKind.Recipe:
                        sound = "read_mod";
                        break;
                    default:
                        return;
                }
            }

            TryPlayReadableSound(entity, sound);
        }

        private static void TryPlayBookSetCompleteSound(EntityAlive entity, SyntheticReadableEffects effects)
        {
            if (!ModConfig.Instance.PlayBookSetCompleteSound)
            {
                return;
            }

            var sound = effects?.BookSetCompleteSoundName;
            if (string.IsNullOrWhiteSpace(sound))
            {
                sound = "read_skillbook_final";
            }

            TryPlayReadableSound(entity, sound);
        }

        /// <summary>
        /// Play a one-shot readable cue. Vanilla non-loop PlaySound / Sound_in_head uses
        /// <c>Audio.Manager.Play(entity, sound, 1f, false)</c>. Do not use PlayInsidePlayerHead
        /// for these — that API expects looping <c>*_lp</c> pairs and silently returns otherwise.
        /// </summary>
        private static void TryPlayReadableSound(EntityAlive entity, string soundName)
        {
            if (string.IsNullOrWhiteSpace(soundName))
            {
                return;
            }

            try
            {
                var managerType = FindLoadedType("Audio.Manager");
                if (managerType == null)
                {
                    LogHypothesis($"Audio: Audio.Manager type not found for {soundName}");
                    return;
                }

                // Prefer entity-attached Play (matches MinEventActionPlaySound non-loop path).
                if (entity != null)
                {
                    MethodInfo playEntity = null;
                    var methods = managerType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    for (var i = 0; i < methods.Length; i++)
                    {
                        var method = methods[i];
                        if (method.Name != "Play")
                        {
                            continue;
                        }

                        var parameters = method.GetParameters();
                        if (parameters.Length == 4 &&
                            typeof(EntityAlive).IsAssignableFrom(parameters[0].ParameterType) &&
                            parameters[1].ParameterType == typeof(string) &&
                            parameters[2].ParameterType == typeof(float) &&
                            parameters[3].ParameterType == typeof(bool))
                        {
                            playEntity = method;
                            break;
                        }
                    }

                    // Game API declares Play(Entity, ...) — EntityAlive is assignable at invoke time.
                    if (playEntity == null)
                    {
                        for (var i = 0; i < methods.Length; i++)
                        {
                            var method = methods[i];
                            if (method.Name != "Play")
                            {
                                continue;
                            }

                            var parameters = method.GetParameters();
                            if (parameters.Length == 4 &&
                                parameters[0].ParameterType.Name == "Entity" &&
                                parameters[1].ParameterType == typeof(string) &&
                                parameters[2].ParameterType == typeof(float) &&
                                parameters[3].ParameterType == typeof(bool))
                            {
                                playEntity = method;
                                break;
                            }
                        }
                    }

                    if (playEntity != null)
                    {
                        playEntity.Invoke(null, new object[] { entity, soundName, 1f, false });
                        LogHypothesis($"Audio: Play(entity,{soundName})");
                        return;
                    }
                }

                var broadcastLocal = managerType.GetMethod(
                    "BroadcastPlayByLocalPlayer",
                    BindingFlags.Public | BindingFlags.Static);
                if (broadcastLocal != null && entity != null)
                {
                    var position = GameReflection.GetMemberValue<object>(entity, "position")
                                   ?? GameReflection.GetMemberValue<object>(entity, "Position");
                    if (position != null)
                    {
                        broadcastLocal.Invoke(null, new[] { position, soundName });
                        LogHypothesis($"Audio: BroadcastPlayByLocalPlayer({soundName})");
                        return;
                    }
                }

                var broadcast = managerType.GetMethod(
                    "BroadcastPlay",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null);
                if (broadcast != null)
                {
                    broadcast.Invoke(null, new object[] { soundName });
                    LogHypothesis($"Audio: BroadcastPlay({soundName})");
                    return;
                }

                LogHypothesis($"Audio: no usable play method for {soundName}");
            }
            catch (Exception ex)
            {
                var root = UnwrapReflectionException(ex);
                LogHypothesis($"Audio: play failed for {soundName} ({root.GetType().Name}: {root.Message})");
            }
        }

        private static int CountBookSetUnlockedPerks(EntityAlive entity, string skillName)
        {
            if (entity == null || string.IsNullOrWhiteSpace(skillName))
            {
                return 0;
            }

            EnsureBookSetSiblingCache();
            if (_bookSetSiblingPerksBySkill == null ||
                !_bookSetSiblingPerksBySkill.TryGetValue(skillName, out var siblings) ||
                siblings == null ||
                siblings.Count == 0)
            {
                return 0;
            }

            var unlocked = 0;
            foreach (var perk in siblings)
            {
                if (IsBookPerkUnlocked(entity, perk))
                {
                    unlocked++;
                }
            }

            return unlocked;
        }

        /// <summary>
        /// Book-set / PerksUnlocked semantics: any level ≥ 1 counts as unlocked
        /// (vanilla book perks are typically max_level=1).
        /// </summary>
        private static bool IsBookPerkUnlocked(EntityAlive entity, string skill)
        {
            return GetProgressionLevel(entity, skill) >= 1;
        }

        private static void EnsureBookSetSiblingCache()
        {
            if (_bookSetSiblingPerksBySkill == null)
            {
                _bookSetSiblingPerksBySkill = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void RegisterBookSetSiblingPerks(string skillName, string[] perkNames)
        {
            if (string.IsNullOrWhiteSpace(skillName) || perkNames == null || perkNames.Length == 0)
            {
                return;
            }

            EnsureBookSetSiblingCache();
            if (!_bookSetSiblingPerksBySkill.TryGetValue(skillName, out var siblings))
            {
                siblings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _bookSetSiblingPerksBySkill[skillName] = siblings;
            }

            for (var i = 0; i < perkNames.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(perkNames[i]))
                {
                    siblings.Add(perkNames[i].Trim());
                }
            }
        }

        private static bool IsProgressionAtKnownMax(EntityAlive entity, string skill)
        {
            var pv = GetProgressionValue(entity, skill);
            var level = GetProgressionLevel(entity, skill);
            var max = TryGetProgressionMaxLevel(pv, entity);
            return max > 0 && level >= max;
        }

        private static bool IsBookProgressionAlreadyUnlocked(EntityAlive entity, string skill)
        {
            var pv = GetProgressionValue(entity, skill);
            var level = GetProgressionLevel(entity, skill);
            var max = TryGetProgressionMaxLevel(pv, entity);
            if (max > 0)
            {
                return level >= max;
            }

            return level >= 1;
        }

        private static bool IsSchematicAlreadyKnown(
            EntityPlayer player,
            object gameManager,
            ItemClass itemClass,
            System.Array itemActions,
            object learnRecipeAction,
            SyntheticReadableEffects effects)
        {
            // Vanilla mod/schematic items usually unlock via CVar (Unlocks / ModifyCVar), not CraftingManager.
            // Check CVars first so name heuristics cannot force a false CraftingManager miss.
            if (AreAllSchematicCVarsKnown(player, effects))
            {
                return true;
            }

            // Prefer explicit LearnRecipe action recipes over name heuristics.
            var recipeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (learnRecipeAction != null)
            {
                CollectRecipesToLearnFromLearnRecipeAction(learnRecipeAction, recipeNames);
            }

            if (itemActions != null)
            {
                for (var ai = 0; ai < itemActions.Length; ai++)
                {
                    var action = itemActions.GetValue(ai);
                    if (action == null || action.GetType().Name != "ItemActionLearnRecipe")
                    {
                        continue;
                    }

                    CollectRecipesToLearnFromLearnRecipeAction(action, recipeNames);
                }
            }

            var hadExplicitRecipes = recipeNames.Count > 0;
            if (!hadExplicitRecipes)
            {
                AddSchematicRecipeNameHeuristics(itemClass, recipeNames);
            }

            if (recipeNames.Count == 0)
            {
                return false;
            }

            var unlockPlayer = GameReflection.TryGetLocalEntityPlayer() ?? player;
            var craftingManager = GameReflection.TryResolveCraftingManager(gameManager, unlockPlayer);
            if (craftingManager == null)
            {
                return false;
            }

            var unlocked = GameReflection.GetMemberValue<object>(craftingManager, "UnlockedRecipeList") as System.Collections.IEnumerable;
            if (unlocked == null)
            {
                return false;
            }

            var unlockedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in unlocked)
            {
                if (entry is string s && !string.IsNullOrWhiteSpace(s))
                {
                    unlockedSet.Add(s);
                }
            }

            foreach (var recipe in recipeNames)
            {
                if (string.IsNullOrWhiteSpace(recipe) || !unlockedSet.Contains(recipe))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreAllSchematicCVarsKnown(EntityAlive entity, SyntheticReadableEffects effects)
        {
            if (entity == null || effects?.SetCVarNames == null || effects.SetCVarNames.Length == 0)
            {
                return false;
            }

            var sawAny = false;
            for (var i = 0; i < effects.SetCVarNames.Length; i++)
            {
                var cvar = effects.SetCVarNames[i];
                if (string.IsNullOrWhiteSpace(cvar))
                {
                    continue;
                }

                sawAny = true;
                if (!TryGetEntityCVar(entity, cvar, out var value) || value < 1f)
                {
                    return false;
                }
            }

            return sawAny;
        }

        private static bool TryExecuteProgressionLevelMinEvent(
            EntityAlive entity,
            string actionTypeName,
            string progressionName,
            int level)
        {
            if (entity == null || string.IsNullOrWhiteSpace(actionTypeName) || string.IsNullOrWhiteSpace(progressionName))
            {
                return false;
            }

            try
            {
                var actionType = FindLoadedType(actionTypeName);
                if (actionType == null)
                {
                    return false;
                }

                var action = Activator.CreateInstance(actionType);
                if (action == null)
                {
                    return false;
                }

                GameReflection.SetMemberValue(action, "progressionName", progressionName);
                GameReflection.SetMemberValue(action, "<progressionName>k__BackingField", progressionName);
                GameReflection.SetMemberValue(action, "level", level);
                GameReflection.SetMemberValue(action, "<level>k__BackingField", level);
                GameReflection.SetMemberValue(action, "showMessage", false);

                var eventParams = GameReflection.CreateMinEventParams(entity, null, null);
                return eventParams != null && GameReflection.TryInvokeInstanceMethod(action, "Execute", out var _, eventParams);
            }
            catch
            {
                return false;
            }
        }

        private static Type FindLoadedType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                Type type = null;
                try
                {
                    type = assemblies[i].GetType(typeName, false);
                }
                catch
                {
                    // ignore this assembly
                }

                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static bool TrySetEntityCVar(EntityAlive entity, string cvarName, float value)
        {
            if (entity == null || string.IsNullOrWhiteSpace(cvarName))
            {
                return false;
            }

            try
            {
                if (GameReflection.TryInvokeInstanceMethod(entity, "SetCVar", out var _, cvarName, value))
                {
                    return true;
                }
            }
            catch
            {
                // fall through
            }

            try
            {
                var buffManager = GameReflection.GetMemberValue<object>(entity, "Buffs")
                                  ?? GameReflection.GetMemberValue<object>(entity, "buffs");
                if (buffManager != null &&
                    GameReflection.TryInvokeInstanceMethod(buffManager, "SetCustomVar", out var _, cvarName, value, true, null))
                {
                    return true;
                }
            }
            catch
            {
                // fall through
            }

            return false;
        }

        private static bool TryGetEntityCVar(EntityAlive entity, string cvarName, out float value)
        {
            value = 0f;
            if (entity == null || string.IsNullOrWhiteSpace(cvarName))
            {
                return false;
            }

            try
            {
                var result = GameReflection.InvokeMethod(entity, "GetCVar", cvarName);
                if (TryConvertToFloat(result, out value))
                {
                    return true;
                }
            }
            catch
            {
                // fall through
            }

            try
            {
                var buffManager = GameReflection.GetMemberValue<object>(entity, "Buffs")
                                  ?? GameReflection.GetMemberValue<object>(entity, "buffs");
                if (buffManager != null &&
                    GameReflection.TryInvokeInstanceMethod(buffManager, "GetCustomVar", out var customVar, cvarName) &&
                    TryConvertToFloat(customVar, out value))
                {
                    return true;
                }
            }
            catch
            {
                // fall through
            }

            return false;
        }

        private static bool TryConvertToFloat(object value, out float result)
        {
            result = 0f;
            if (value == null)
            {
                return false;
            }

            try
            {
                result = Convert.ToSingle(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static SyntheticReadableEffects ResolveSyntheticReadableEffects(ItemClass itemClass)
        {
            var itemClassName = GameReflection.GetItemClassName(itemClass);
            if (string.IsNullOrWhiteSpace(itemClassName))
            {
                return SyntheticReadableEffects.Empty;
            }

            var effectsByName = EnsureReadableEffectsCache();
            if (effectsByName != null && effectsByName.TryGetValue(itemClassName, out var xmlEffects))
            {
                return xmlEffects;
            }

            // Fallback for tests, missing game XML, or modded items following vanilla naming conventions.
            var addProgressions = new List<string>();
            var maxProgressions = new List<string>();
            var setCVars = new List<string>();
            AddVanillaNameHeuristicEffects(itemClassName, addProgressions, maxProgressions, setCVars);
            return new SyntheticReadableEffects(
                UniqueStrings(addProgressions),
                UniqueStrings(maxProgressions),
                UniqueStrings(setCVars, normalizeToLower: false));
        }

        private static Dictionary<string, SyntheticReadableEffects> EnsureReadableEffectsCache()
        {
            if (_readableEffectsCacheLoaded)
            {
                return _cachedReadableEffectsByItemName;
            }

            _readableEffectsCacheLoaded = true;
            _cachedReadableEffectsByItemName = new Dictionary<string, SyntheticReadableEffects>(StringComparer.OrdinalIgnoreCase);
            EnsureBookSetSiblingCache();
            _bookSetSiblingPerksBySkill.Clear();

            var itemsXmlPath = FindGameItemsXmlPath();
            if (string.IsNullOrEmpty(itemsXmlPath))
            {
                LogHypothesis("XmlSyntheticEffects: Data/Config/items.xml not found; using naming fallbacks only");
                return _cachedReadableEffectsByItemName;
            }

            try
            {
                var doc = new XmlDocument();
                doc.Load(itemsXmlPath);
                var items = doc.SelectNodes("/items/item");
                if (items == null)
                {
                    return _cachedReadableEffectsByItemName;
                }

                foreach (XmlElement item in items)
                {
                    var name = item.GetAttribute("name");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var addProgressions = new List<string>();
                    var addProgressionLevelByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var maxProgressions = new List<string>();
                    var setCVars = new List<string>();
                    string completeProgressionName = null;
                    string completeSkillName = null;
                    var completeRequiredCount = 0;
                    string readSoundName = null;
                    string bookSetCompleteSoundName = null;
                    AddXmlReadableEffects(
                        item,
                        addProgressions,
                        addProgressionLevelByName,
                        maxProgressions,
                        setCVars,
                        ref completeProgressionName,
                        ref completeSkillName,
                        ref completeRequiredCount,
                        ref readSoundName,
                        ref bookSetCompleteSoundName);
                    AddVanillaNameHeuristicEffects(name, addProgressions, maxProgressions, setCVars);

                    var uniqueAdds = UniqueStrings(addProgressions);
                    var addLevels = new int[uniqueAdds.Length];
                    for (var i = 0; i < uniqueAdds.Length; i++)
                    {
                        addLevels[i] = addProgressionLevelByName.TryGetValue(uniqueAdds[i], out var xmlLevel) && xmlLevel > 0
                            ? xmlLevel
                            : 1;
                    }

                    var effects = new SyntheticReadableEffects(
                        uniqueAdds,
                        UniqueStrings(maxProgressions),
                        UniqueStrings(setCVars, normalizeToLower: false),
                        addLevels,
                        completeProgressionName,
                        completeSkillName,
                        completeRequiredCount,
                        readSoundName,
                        bookSetCompleteSoundName);
                    if (effects.HasBookSetCompletion)
                    {
                        RegisterBookSetSiblingPerks(effects.CompleteSkillName, effects.MaxProgressionNames);
                    }

                    if (effects.HasAny || effects.HasBookSetCompletion)
                    {
                        _cachedReadableEffectsByItemName[name.Trim()] = effects;
                    }
                }

                LogHypothesis(
                    $"XmlSyntheticEffects: loaded {_cachedReadableEffectsByItemName.Count} readable item effect rows from {itemsXmlPath}");
            }
            catch (Exception ex)
            {
                var root = UnwrapReflectionException(ex);
                LogHypothesis(
                    $"XmlSyntheticEffects: failed loading items.xml ({root.GetType().Name}: {root.Message}); using naming fallbacks only");
            }

            return _cachedReadableEffectsByItemName;
        }

        private static string FindGameItemsXmlPath()
        {
            var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<string>();
            AddItemsXmlCandidate(candidates, seenRoots, Directory.GetCurrentDirectory());
            AddItemsXmlCandidate(candidates, seenRoots, AppDomain.CurrentDomain.BaseDirectory);

            try
            {
                var asmDir = Path.GetDirectoryName(typeof(AutoReader).Assembly.Location);
                var dir = asmDir;
                for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
                {
                    AddItemsXmlCandidate(candidates, seenRoots, dir);
                    dir = Directory.GetParent(dir)?.FullName;
                }
            }
            catch
            {
                // ignore
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return null;
        }

        private static void AddItemsXmlCandidate(List<string> candidates, HashSet<string> seenRoots, string root)
        {
            if (candidates == null || seenRoots == null || string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            try
            {
                var fullRoot = Path.GetFullPath(root);
                if (!seenRoots.Add(fullRoot))
                {
                    return;
                }

                candidates.Add(Path.Combine(fullRoot, "Data", "Config", "items.xml"));
            }
            catch
            {
                // ignore
            }
        }

        private static void AddXmlReadableEffects(
            XmlElement item,
            List<string> addProgressions,
            Dictionary<string, int> addProgressionLevelByName,
            List<string> maxProgressions,
            List<string> setCVars,
            ref string completeProgressionName,
            ref string completeSkillName,
            ref int completeRequiredCount,
            ref string readSoundName,
            ref string bookSetCompleteSoundName)
        {
            foreach (XmlElement property in item.SelectNodes("property"))
            {
                var propertyName = property.GetAttribute("name");
                if (!string.Equals(propertyName, "Unlocks", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var unlocks = property.GetAttribute("value") ?? string.Empty;
                var parts = unlocks.Split(',');
                for (var i = 0; i < parts.Length; i++)
                {
                    var unlock = parts[i].Trim();
                    if (unlock.StartsWith("crafting", StringComparison.OrdinalIgnoreCase))
                    {
                        AddProgressionNameWithXmlLevel(addProgressions, addProgressionLevelByName, unlock, 1);
                    }
                    else if (unlock.StartsWith("perk", StringComparison.OrdinalIgnoreCase))
                    {
                        AddProgressionName(maxProgressions, unlock);
                    }
                    else if (!string.IsNullOrWhiteSpace(unlock))
                    {
                        AddCVarName(setCVars, unlock);
                    }
                }
            }

            foreach (XmlElement action in item.SelectNodes("property[@class='Action0']/property"))
            {
                if (string.Equals(action.GetAttribute("name"), "Sound_start", StringComparison.OrdinalIgnoreCase))
                {
                    var sound = action.GetAttribute("value");
                    if (!string.IsNullOrWhiteSpace(sound))
                    {
                        readSoundName = sound.Trim();
                    }
                }
            }

            foreach (XmlElement effectGroup in item.SelectNodes("effect_group"))
            {
                // Skip conditional groups for unconditional synthetic grants, but still parse nested
                // Complete / PerksUnlocked metadata (book set 8th perk).
                var skipUnconditionalGrants = EffectGroupHasTopLevelRequirement(effectGroup);

                foreach (XmlElement effect in effectGroup.SelectNodes("triggered_effect"))
                {
                    var trigger = effect.GetAttribute("trigger");
                    if (!string.Equals(trigger, "onSelfPrimaryActionEnd", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    TryExtractBookSetCompleteMetadata(
                        effect,
                        ref completeProgressionName,
                        ref completeSkillName,
                        ref completeRequiredCount);

                    var action = effect.GetAttribute("action");
                    if (string.Equals(action, "PlaySound", StringComparison.OrdinalIgnoreCase) &&
                        TriggeredEffectHasPerksUnlockedRequirement(effect))
                    {
                        var sound = effect.GetAttribute("sound");
                        if (!string.IsNullOrWhiteSpace(sound))
                        {
                            bookSetCompleteSoundName = sound.Trim();
                        }
                    }

                    if (skipUnconditionalGrants)
                    {
                        continue;
                    }

                    if (string.Equals(action, "AddProgressionLevel", StringComparison.OrdinalIgnoreCase))
                    {
                        var progressionName = effect.GetAttribute("progression_name");
                        if (string.IsNullOrWhiteSpace(progressionName))
                        {
                            continue;
                        }

                        AddProgressionNameWithXmlLevel(
                            addProgressions,
                            addProgressionLevelByName,
                            progressionName,
                            ParsePositiveIntAttribute(effect.GetAttribute("level"), 1));
                    }
                    else if (string.Equals(action, "SetProgressionLevel", StringComparison.OrdinalIgnoreCase))
                    {
                        // Never treat Complete (gated by PerksUnlocked) as an unconditional max unlock.
                        if (TriggeredEffectHasPerksUnlockedRequirement(effect))
                        {
                            continue;
                        }

                        AddProgressionName(maxProgressions, effect.GetAttribute("progression_name"));
                    }
                    else if (string.Equals(action, "ModifyCVar", StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(effect.GetAttribute("operation"), "set", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = effect.GetAttribute("value") ?? string.Empty;
                        if (value.Trim() == "1")
                        {
                            AddCVarName(setCVars, effect.GetAttribute("cvar"));
                        }
                    }
                }
            }
        }

        private static void AddProgressionNameWithXmlLevel(
            List<string> names,
            Dictionary<string, int> levelByName,
            string candidate,
            int xmlLevel)
        {
            if (names == null || string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            var beforeCount = names.Count;
            AddProgressionName(names, candidate);
            if (names.Count == beforeCount || levelByName == null)
            {
                return;
            }

            var key = names[names.Count - 1];
            levelByName[key] = xmlLevel > 0 ? xmlLevel : 1;
        }

        private static void TryExtractBookSetCompleteMetadata(
            XmlElement effect,
            ref string completeProgressionName,
            ref string completeSkillName,
            ref int completeRequiredCount)
        {
            if (effect == null)
            {
                return;
            }

            var action = effect.GetAttribute("action");
            if (!string.Equals(action, "SetProgressionLevel", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var progressionName = effect.GetAttribute("progression_name");
            if (string.IsNullOrWhiteSpace(progressionName))
            {
                return;
            }

            foreach (XmlElement requirement in effect.SelectNodes("requirement"))
            {
                if (!string.Equals(requirement.GetAttribute("name"), "PerksUnlocked", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var skillName = requirement.GetAttribute("skill_name");
                var required = ParsePositiveIntAttribute(requirement.GetAttribute("value"), 0);
                if (string.IsNullOrWhiteSpace(skillName) || required <= 0)
                {
                    continue;
                }

                completeProgressionName = progressionName.Trim();
                completeSkillName = skillName.Trim();
                completeRequiredCount = required;
                return;
            }
        }

        private static bool TriggeredEffectHasPerksUnlockedRequirement(XmlElement effect)
        {
            if (effect == null)
            {
                return false;
            }

            foreach (XmlElement requirement in effect.SelectNodes("requirement"))
            {
                if (string.Equals(requirement.GetAttribute("name"), "PerksUnlocked", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ParsePositiveIntAttribute(string raw, int fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            if (int.TryParse(raw.Trim(), out var parsed) && parsed > 0)
            {
                return parsed;
            }

            return fallback;
        }

        private static bool EffectGroupHasTopLevelRequirement(XmlElement effectGroup)
        {
            if (effectGroup == null)
            {
                return false;
            }

            for (var i = 0; i < effectGroup.ChildNodes.Count; i++)
            {
                if (!(effectGroup.ChildNodes[i] is XmlElement child))
                {
                    continue;
                }

                if (string.Equals(child.Name, "requirement", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(child.Name, "requirement_group", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddVanillaNameHeuristicEffects(
            string itemClassName,
            List<string> addProgressions,
            List<string> maxProgressions,
            List<string> setCVars)
        {
            if (string.IsNullOrWhiteSpace(itemClassName))
            {
                return;
            }

            var progression = MapVanillaSkillMagazineToProgressionName(itemClassName);
            if (!string.IsNullOrWhiteSpace(progression))
            {
                AddProgressionName(addProgressions, progression);
                return;
            }

            var lower = itemClassName.Trim().ToLowerInvariant();
            if (lower.EndsWith("schematic", StringComparison.OrdinalIgnoreCase) && itemClassName.Length > "schematic".Length)
            {
                AddCVarName(setCVars, itemClassName.Substring(0, itemClassName.Length - "schematic".Length));
            }
            else if (lower.EndsWith("recipe", StringComparison.OrdinalIgnoreCase) && itemClassName.Length > "recipe".Length)
            {
                AddCVarName(setCVars, itemClassName.Substring(0, itemClassName.Length - "recipe".Length));
            }
        }

        private static string MapVanillaSkillMagazineToProgressionName(string itemClassName)
        {
            if (string.IsNullOrWhiteSpace(itemClassName) ||
                !itemClassName.EndsWith("SkillMagazine", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var prefix = itemClassName.Substring(0, itemClassName.Length - "SkillMagazine".Length);
            switch (prefix)
            {
                case "harvestingTools": return "craftingHarvestingTools";
                case "repairTools": return "craftingRepairTools";
                case "salvageTools": return "craftingSalvageTools";
                case "knuckles": return "craftingKnuckles";
                case "blades": return "craftingBlades";
                case "clubs": return "craftingClubs";
                case "sledgehammers": return "craftingSledgehammers";
                case "spears": return "craftingSpears";
                case "bows": return "craftingBows";
                case "handguns": return "craftingHandguns";
                case "shotguns": return "craftingShotguns";
                case "rifles": return "craftingRifles";
                case "machineGuns": return "craftingMachineGuns";
                case "explosives": return "craftingExplosives";
                case "robotics": return "craftingRobotics";
                case "armor": return "craftingArmor";
                case "medical": return "craftingMedical";
                case "food": return "craftingFood";
                case "seed": return "craftingSeeds";
                case "electrician": return "craftingElectrician";
                case "traps": return "craftingTraps";
                case "workstation": return "craftingWorkstations";
                case "vehicles": return "craftingVehicles";
                default: return null;
            }
        }

        private static string[] UniqueStrings(List<string> values, bool normalizeToLower = true)
        {
            if (values == null || values.Count == 0)
            {
                return new string[0];
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var normalized = normalizeToLower ? value.Trim().ToLowerInvariant() : value.Trim();
                if (seen.Add(normalized))
                {
                    result.Add(normalized);
                }
            }

            return result.ToArray();
        }

        private static string[] GetSkillsToGain(object gainSkillAction)
        {
            if (gainSkillAction == null)
            {
                return null;
            }

            return GameReflection.GetMemberValue<string[]>(gainSkillAction, "SkillsToGain")
                   ?? GameReflection.GetMemberValue<string[]>(gainSkillAction, "skillsToGain");
        }

        private static void AddProgressionNames(List<string> names, string[] candidates)
        {
            if (candidates == null)
            {
                return;
            }

            for (var i = 0; i < candidates.Length; i++)
            {
                AddProgressionName(names, candidates[i]);
            }
        }

        private static void AddProgressionName(List<string> names, string candidate)
        {
            if (names == null || string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            var normalized = candidate.Trim().ToLowerInvariant();
            for (var i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                names.Add(normalized);
            }
        }

        private static void AddCVarName(List<string> names, string candidate)
        {
            if (names == null || string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            var normalized = candidate.Trim();
            for (var i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            names.Add(normalized);
        }

        private static string MapCraftingSkillGroupToProgressionName(string craftingSkillGroup)
        {
            if (string.IsNullOrWhiteSpace(craftingSkillGroup))
            {
                return null;
            }

            var compact = craftingSkillGroup.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            if (compact == "clothing")
            {
                return "craftingarmor";
            }

            if (compact == "tools")
            {
                return "craftingharvestingtools";
            }

            if (compact == "repair")
            {
                return "craftingrepairtools";
            }

            return compact.StartsWith("crafting", StringComparison.OrdinalIgnoreCase)
                ? compact
                : "crafting" + compact;
        }

        private static object GetProgressionValue(EntityAlive effectEntity, string skill)
        {
            if (effectEntity == null || string.IsNullOrWhiteSpace(skill))
            {
                return null;
            }

            var progression = GameReflection.GetMemberValue<object>(effectEntity, "Progression")
                              ?? GameReflection.GetMemberValue<object>(effectEntity, "progression");
            if (progression == null)
            {
                return null;
            }

            return GameReflection.InvokeMethod(progression, "GetProgressionValue", skill);
        }

        private static int GetProgressionLevel(EntityAlive effectEntity, string skill)
        {
            var progressionValue = GetProgressionValue(effectEntity, skill);
            if (progressionValue == null)
            {
                return 0;
            }

            try
            {
                var m = progressionValue.GetType().GetMethod(
                    "CalculatedLevel",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(EntityAlive) },
                    null);
                if (m != null)
                {
                    var ret = m.Invoke(progressionValue, new object[] { effectEntity });
                    if (ret is int cl)
                    {
                        return cl;
                    }
                }
            }
            catch
            {
                // fall through to field reads
            }

            var level = GameReflection.GetMemberValue<int>(progressionValue, "Level");
            if (level > 0)
            {
                return level;
            }

            return GameReflection.GetMemberValue<int>(progressionValue, "level");
        }

        /// <summary>
        /// Levels for all <c>crafting*</c> progressions in <see cref="Progression.ProgressionValueQuickList"/>,
        /// plus any explicit names we expect from the item. Uses canonical names from the list so lookups match live XML.
        /// </summary>
        private static Dictionary<string, int> CaptureCraftingProgressionSnapshot(EntityAlive entity, string[] ensureNames)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (entity == null)
            {
                return result;
            }

            var progression = GameReflection.GetMemberValue<object>(entity, "Progression")
                              ?? GameReflection.GetMemberValue<object>(entity, "progression");
            if (progression == null)
            {
                return result;
            }

            var listObj = GameReflection.GetMemberValue<object>(progression, "ProgressionValueQuickList");
            if (listObj is System.Collections.IEnumerable enumerable)
            {
                foreach (var pvObj in enumerable)
                {
                    if (pvObj == null)
                    {
                        continue;
                    }

                    var name = GameReflection.GetMemberValue<string>(pvObj, "Name")
                               ?? GameReflection.GetMemberValue<string>(pvObj, "name");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var lower = name.Trim().ToLowerInvariant();
                    if (!lower.StartsWith("crafting", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    result[lower] = ReadProgressionValueLevel(pvObj, entity);
                }
            }

            if (ensureNames != null)
            {
                for (var i = 0; i < ensureNames.Length; i++)
                {
                    var n = ensureNames[i];
                    if (string.IsNullOrWhiteSpace(n))
                    {
                        continue;
                    }

                    var key = n.Trim().ToLowerInvariant();
                    if (!result.ContainsKey(key))
                    {
                        result[key] = GetProgressionLevel(entity, key);
                    }
                }
            }

            return result;
        }

        private static int ReadProgressionValueLevel(object progressionValue, EntityAlive entity)
        {
            if (progressionValue == null || entity == null)
            {
                return 0;
            }

            try
            {
                var m = progressionValue.GetType().GetMethod(
                    "CalculatedLevel",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(EntityAlive) },
                    null);
                if (m != null)
                {
                    var ret = m.Invoke(progressionValue, new object[] { entity });
                    if (ret is int cl)
                    {
                        return cl;
                    }
                }
            }
            catch
            {
                // fall through
            }

            var level = GameReflection.GetMemberValue<int>(progressionValue, "Level");
            return level != 0 ? level : GameReflection.GetMemberValue<int>(progressionValue, "level");
        }

        /// <summary>
        /// If a stack had N successful consumes but the only touched crafting skill rose by &lt; N, top it up to N.
        /// Uses names from a pre-read QuickList snapshot so we match the live progression id, not a guessed string.
        /// </summary>
        private static void TryReconcileQuickListUnderGranted(
            EntityAlive entity,
            Dictionary<string, int> beforeSnap,
            int successfulConsumes,
            string[] progressionNames,
            string itemName,
            int unitIndex)
        {
            if (entity == null || successfulConsumes <= 1 || beforeSnap == null || beforeSnap.Count == 0)
            {
                return;
            }

            var under = new List<string>();
            var pointsPerRead = GetMagazinePointsPerRead();
            var expectedDelta = successfulConsumes * Math.Max(0, pointsPerRead);
            foreach (var kv in beforeSnap)
            {
                var name = kv.Key;
                var before = kv.Value;
                var after = GetProgressionLevel(entity, name);
                var delta = after - before;
                if (delta > 0 && delta < expectedDelta)
                {
                    under.Add(name);
                }
            }

            if (under.Count == 0)
            {
                return;
            }

            string fix = null;
            if (under.Count == 1)
            {
                fix = under[0];
            }
            else if (progressionNames != null && progressionNames.Length > 0)
            {
                for (var i = 0; i < progressionNames.Length; i++)
                {
                    var want = progressionNames[i]?.Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(want))
                    {
                        continue;
                    }

                    if (under.Contains(want))
                    {
                        fix = want;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(fix) || !beforeSnap.TryGetValue(fix, out var beforeLevel))
            {
                LogHypothesis(
                    $"QuickListReconcile [{unitIndex}] {itemName}: could not pick skill (candidates={under.Count}) on {entity.GetType().Name}");
                return;
            }

            var target = beforeLevel + expectedDelta;
            if (TrySetProgressionLevel(entity, fix, target))
            {
                LogHypothesis(
                    $"QuickListReconcile [{unitIndex}] {itemName}: adjusted {fix} to {target} (was {beforeLevel}, consumed={successfulConsumes}, pointsPerRead={pointsPerRead})");
            }
            else
            {
                LogHypothesis(
                    $"QuickListReconcile [{unitIndex}] {itemName}: failed to set {fix} to {target}");
            }
        }

        private static bool TrySetProgressionLevel(EntityAlive effectEntity, string skill, int level)
        {
            var progressionValue = GetProgressionValue(effectEntity, skill);
            if (progressionValue == null || level <= 0)
            {
                return false;
            }

            var maxLevel = TryGetProgressionMaxLevel(progressionValue, effectEntity);
            var targetLevel = maxLevel > 0 ? Math.Min(level, maxLevel) : level;
            var before = GetProgressionLevel(effectEntity, skill);
            if (before >= targetLevel)
            {
                return true;
            }

            GameReflection.SetMemberValue(progressionValue, "Level", targetLevel);
            GameReflection.SetMemberValue(progressionValue, "level", targetLevel);
            GameReflection.SetMemberValue(progressionValue, "calculatedLevel", (float)targetLevel);
            return GetProgressionLevel(effectEntity, skill) >= targetLevel;
        }

        private static int TryGetProgressionMaxLevel(object progressionValue, EntityAlive effectEntity)
        {
            try
            {
                var max = GameReflection.InvokeMethod(progressionValue, "CalculatedMaxLevel", effectEntity);
                if (max is int calculatedMax && calculatedMax > 0)
                {
                    return calculatedMax;
                }
            }
            catch
            {
                // fall through
            }

            var progressionClass = GameReflection.GetMemberValue<object>(progressionValue, "ProgressionClass")
                                   ?? GameReflection.GetMemberValue<object>(progressionValue, "cachedProgressionClass");
            if (progressionClass != null)
            {
                try
                {
                    var max = GameReflection.InvokeMethod(progressionClass, "GetCalculatedMaxLevel", effectEntity, progressionValue);
                    if (max is int classMax && classMax > 0)
                    {
                        return classMax;
                    }
                }
                catch
                {
                    // fall through
                }

                var declaredMax = GameReflection.GetMemberValue<int>(progressionClass, "MaxLevel");
                if (declaredMax > 0)
                {
                    return declaredMax;
                }
            }

            return 0;
        }

        private static void AttachEventParamsToActionData(
            object actionData,
            ItemClass itemClass,
            ItemValue itemValue,
            ItemInventoryData invData,
            object gameManager,
            EntityAlive effectEntity,
            int itemSlotIndex,
            string itemName,
            int unitIndex)
        {
            if (actionData == null || invData == null)
            {
                return;
            }

            try
            {
                var eventParams = GameReflection.CreateMinEventParams(effectEntity, itemValue, invData);
                if (eventParams == null)
                {
                    LogHypothesis($"AttachEventParams [{unitIndex}] {itemName}: skipped (no MinEventParams)");
                    return;
                }

                GameReflection.SetMemberValue(eventParams, "ItemActionData", actionData);
                GameReflection.SetMemberValue(eventParams, "itemActionData", actionData);
                GameReflection.SetMemberValue(actionData, "EventParms", eventParams);
                GameReflection.SetMemberValue(actionData, "eventParms", eventParams);
                GameReflection.SetMemberValue(actionData, "EventParams", eventParams);
                LogHypothesis($"AttachEventParams [{unitIndex}] {itemName}: wired EventParms for actionData");
            }
            catch (Exception ex)
            {
                var root = UnwrapReflectionException(ex);
                LogHypothesis($"AttachEventParams [{unitIndex}] {itemName}: exception → {root.GetType().Name}: {root.Message}");
            }
        }

        private static Exception UnwrapReflectionException(Exception exception)
        {
            if (exception is TargetInvocationException targetInvocationException && targetInvocationException.InnerException != null)
            {
                return UnwrapReflectionException(targetInvocationException.InnerException);
            }

            return exception;
        }

        private static void SafeLog(string message)
        {
            try
            {
                UnityEngine.Debug.Log($"[AutoReadMod] {message}");
            }
            catch
            {
                // Unity not available (test environment)
            }
        }

        private static void EmitVisualizationEvent(ItemClass itemClass, ItemValue itemValue, int count, AutoReadVisualizationSource source, ReadableItemKind kind)
        {
            if (itemClass == null || count <= 0)
            {
                return;
            }

            string itemClassName = GameReflection.GetItemClassName(itemClass);
            string itemKey = $"{GameReflection.GetItemType(itemValue)}:{itemClassName}".ToLowerInvariant();
            string localizedName = itemClass.GetLocalizedItemName();
            string iconName = GameReflection.GetItemIconName(itemClass);

            if (source == AutoReadVisualizationSource.Bundle)
            {
                AutoReadVisualizationQueue.Enqueue(new AutoReadVisualizationEvent(
                    itemKey, itemClassName, localizedName, iconName, count, source, kind, itemValue,
                    decorateVanillaEntryOnly: true));
                return;
            }

            var evt = new AutoReadVisualizationEvent(itemKey, itemClassName, localizedName, iconName, count, source, kind, itemValue);
            AutoReadVisualizationQueue.Enqueue(evt);
            if (ModConfig.Instance.DebugMode)
            {
                try { UnityEngine.Debug.Log($"[AutoReadMod] Visualization: enqueued event {itemClassName}, Kind={kind}"); } catch { }
            }
        }

        private static void DebugLog(string message)
        {
            if (!ModConfig.Instance.DebugMode)
                return;

            SafeLog($"[DEBUG] {message}");
        }
    }
}
