using System;
using System.Collections.Generic;
using System.Reflection;
namespace AutoReadMod
{
    public static class AutoReader
    {
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

        public enum ReadableItemKind
        {
            None,
            Book,
            Magazine,
            Schematic,
            Recipe,
        }

        public static void ProcessContainer(EntityPlayer player, TileEntityLootContainer container, XUiC_LootWindowGroup lootWindowGroup = null)
        {
            DebugLog($"ProcessContainer called, Enabled={ModConfig.Instance.Enabled}");

            if (!ModConfig.Instance.Enabled)
            {
                DebugLog("Mod is disabled in config");
                return;
            }

            if (player == null || container?.items == null)
            {
                DebugLog($"Null check failed: player={player != null}, items={container?.items != null}");
                return;
            }

            var itemsToRead = FindReadableSlotIndices(container.items);

            DebugLog($"Found {itemsToRead.Count} readable items");

            // Read items (in reverse to avoid index issues when removing)
            for (int i = itemsToRead.Count - 1; i >= 0; i--)
            {
                int slotIndex = itemsToRead[i];
                ReadStack(
                    player,
                    container.items[slotIndex],
                    GameReflection.GetItemCount(container.items[slotIndex]),
                    AutoReadVisualizationSource.Container,
                    updatedStack =>
                {
                    container.UpdateSlot(slotIndex, updatedStack);
                    container.SetModified();
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

        internal static bool ShouldAutoReadItem(ItemStack itemStack)
        {
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
            bool bundleReadFromBag = false)
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

            SafeLog($"AutoReadMod: {player?.EntityName ?? "player"} reading {targetCount}x {itemName}");

            try
            {
                LogHypothesis(
                    $"ReadStack: item={itemName} currentCount={currentCount} targetCount={targetCount} slot={itemSlotIndex} source={source} playerType={player?.GetType().Name ?? "null"}");
                XUiC_ItemStack lootSlotController = null;
                if (lootWindowGroup != null)
                {
                    if (itemSlotIndex >= 0)
                    {
                        lootSlotController = GameReflection.TryGetLootSlotItemStackController(lootWindowGroup, itemSlotIndex);
                    }

                    if (lootSlotController == null)
                    {
                        lootSlotController = GameReflection.TryGetLootSlotByStackReference(lootWindowGroup, itemStack);
                    }

                    LogHypothesis(
                        $"ReadStack: lootSlotController={(lootSlotController == null ? "null" : lootSlotController.GetType().Name)} slot={itemSlotIndex}");
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

                var successfulConsumes = ApplyReadableEffects(player, itemStack, targetCount, itemSlotIndex, lootSlotController);
                DebugLog($"Consumed {successfulConsumes}/{targetCount}x {itemName}, now updating slot");

                if (successfulConsumes <= 0)
                {
                    LogHypothesis("ReadStack: no successful consumptions; slot not updated");
                    return;
                }

                // Prefer post-read counts from the UI slot when present — backing inventory may still hold a stale ItemStack reference on new slots until sync.
                int remaining;
                if (lootSlotController != null)
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
                if (remaining == 0)
                {
                    updateStack(GameReflection.GetEmptyItemStack());
                }
                else
                {
                    updateStack(new ItemStack(itemValue, remaining));
                    DebugLog($"Reduced stack to {remaining}");
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
            XUiC_ItemStack lootSlotController = null)
        {
            return ApplyReadableEffects(player, itemStack, consumeCount, itemSlotIndex, lootSlotController);
        }

        private static int ApplyReadableEffects(
            EntityPlayer player,
            ItemStack itemStack,
            int consumeCount,
            int itemSlotIndex = -1,
            XUiC_ItemStack lootSlotController = null)
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

            // Match concrete vanilla eat action only (see 09b86f5). ItemActionGainSkill is separate — magazines often use Eat + GainSkill.
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

            if (eatAction == null && gainSkillAction == null)
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

                SafeLog($"No ItemActionEat or ItemActionGainSkill for {itemName}. Actions[]={actionDiag}");
                return 0;
            }

            var effectiveCount = Math.Min(consumeCount, GameReflection.GetItemCount(itemStack));
            if (effectiveCount <= 0)
            {
                return 0;
            }

            LogHypothesis(
                $"ApplyReadableEffects: {effectiveCount}/{consumeCount} x {itemName} | eatAction={(eatAction == null ? "null" : eatAction.GetType().FullName)} | gainSkillAction={(gainSkillAction == null ? "null" : gainSkillAction.GetType().FullName)} | slotIdx={itemSlotIndex} eatIdx={eatActionIndex} gainSkillIdx={gainSkillActionIndex}");
            if (itemSlotIndex < 0)
            {
                LogHypothesis(
                    "ApplyReadableEffects: WARNING slotIdx=-1 — many builds skip perk/recipe unlock logic when ItemInventoryData.slotIdx is not a real inventory/container slot.");
            }

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

            DebugLog(
                $"ApplyReadableEffects: {effectiveCount}/{consumeCount} x {itemName} (slotIdx={itemSlotIndex}), lootUi={(lootSlotController == null ? "no" : "yes")}, Eat/GainSkill/FireEvent paths");

            var progressionNames = ResolveGainSkillProgressionNames(gainSkillAction, itemClass);
            if (gainSkillAction != null)
            {
                LogHypothesis(
                    $"ApplyReadableEffects: resolved gain-skill progressions for {itemName}: {(progressionNames.Length == 0 ? "(none)" : string.Join(",", progressionNames))}");
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

            for (int i = 0; i < effectiveCount; i++)
            {
                if (successfulConsumes >= effectiveCount)
                {
                    break;
                }

                DebugLog($"Consuming item {i + 1}/{effectiveCount}");
                try
                {
                    // Keep the live slot stack as the source of truth. Vanilla read actions mutate
                    // that stack; synthetic clones can apply effects without consuming inventory.
                    ItemStack liveStack = itemStack;
                    if (lootSlotController != null)
                    {
                        var fromUi = GameReflection.TryGetItemStackFromLootSlotController(lootSlotController);
                        if (fromUi != null && !fromUi.IsEmpty())
                        {
                            liveStack = fromUi;
                        }
                    }

                    if (liveStack == null || liveStack.IsEmpty())
                    {
                        LogHypothesis($"ApplyReadableEffects: stop at unit {i + 1}/{effectiveCount} — stack depleted");
                        break;
                    }

                    int countStart = GameReflection.GetItemCount(liveStack);
                    var effectStack = new ItemStack(itemValue, 1);
                    var effectSkillLevelsBefore = CaptureGainSkillLevels(progressionNames, effectEntity);
                    var playerSkillLevelsBefore = ReferenceEquals(effectEntity, player)
                        ? null
                        : CaptureGainSkillLevels(progressionNames, player);

                    TryFireOnSelfPrimaryActionStart(
                        itemClass,
                        itemValue,
                        gameManager,
                        effectEntity,
                        itemSlotIndex,
                        itemName,
                        i + 1,
                        effectStack);

                    int consumedThisUnit = 0;
                    if (eatAction != null)
                    {
                        // Manual reads use ItemActionEat.ExecuteInstantAction(entity, stack, false, controller).
                        // Run that first for both loot and bundle outputs; some builds return false
                        // even after mutating the stack, so success is measured from the stack delta.
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

                        var afterInstantStack = lootSlotController != null
                            ? (GameReflection.TryGetItemStackFromLootSlotController(lootSlotController) ?? stackForInstant)
                            : stackForInstant;
                        consumedThisUnit = Math.Max(0, countStart - GameReflection.GetItemCount(afterInstantStack));
                        consumedThisUnit = Math.Min(consumedThisUnit, effectiveCount - successfulConsumes);

                        if (consumedThisUnit <= 0)
                        {
                            if (TryConsumeEatActionReflection(
                                    eatAction,
                                    itemClass,
                                    liveStack,
                                    gameManager,
                                    effectEntity,
                                    itemValue,
                                    itemSlotIndex,
                                    eatActionIndex,
                                    itemName,
                                    i + 1,
                                    effectiveCount))
                            {
                                var afterConsumeStack = lootSlotController != null
                                    ? (GameReflection.TryGetItemStackFromLootSlotController(lootSlotController) ?? liveStack)
                                    : liveStack;
                                consumedThisUnit = Math.Max(0, countStart - GameReflection.GetItemCount(afterConsumeStack));
                                consumedThisUnit = Math.Min(consumedThisUnit, effectiveCount - successfulConsumes);
                            }
                        }

                        if (consumedThisUnit > 0)
                        {
                            LogHypothesis(
                                $"ApplyReadableEffects: item {i + 1}: consumed {consumedThisUnit} via vanilla read path for {itemName}");
                            TryFireOnSelfPrimaryActionEnd(
                                itemClass,
                                itemValue,
                                gameManager,
                                effectEntity,
                                itemSlotIndex,
                                itemName,
                                i + 1,
                                "after vanilla read path",
                                effectStack);
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

                    if (consumedThisUnit > 0 && gainSkillAction != null)
                    {
                        var skillOk = TryGainSkillExecuteActionReflection(
                            gainSkillAction,
                            itemClass,
                            effectStack,
                            gameManager,
                            effectEntity,
                            itemValue,
                            itemSlotIndex,
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

                    if (consumedThisUnit > 0)
                    {
                        successfulConsumes += consumedThisUnit;
                        DebugLog($"Applied effect for item {i + 1}/{effectiveCount} (stack -{consumedThisUnit})");
                    }
                }
                catch (Exception consumeEx)
                {
                    var rootException = UnwrapReflectionException(consumeEx);
                    SafeLog($"Error consuming {itemName} (item {i + 1}/{effectiveCount}): {rootException.Message}");
                    SafeLog(rootException.StackTrace ?? consumeEx.StackTrace ?? string.Empty);
                }
            }

            if (successfulConsumes > 0 && progressionNames.Length > 0)
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

            if (successfulConsumes > 1)
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
                    LogHypothesis($"TryFireOnSelfPrimaryActionEnd [{unitIndex}] {itemName}: skipped (no ItemInventoryData) reason={reason}");
                    return;
                }

                var evtParams = GameReflection.CreateMinEventParams(effectEntity, itemValue, invData);
                if (evtParams == null)
                {
                    LogHypothesis($"TryFireOnSelfPrimaryActionEnd [{unitIndex}] {itemName}: skipped (no MinEventParams) reason={reason}");
                    return;
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
            int effectiveCount)
        {
            try
            {
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

                LogHypothesis(
                    $"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: invDataType={invData.GetType().Name} slotIdx={itemSlotIndex} will try CreateModifierData then ExecuteAction(true), else consume");

                object actionData = null;
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

                // Real game: ItemActionEat overrides ExecuteAction (books/schematics); consume() is a separate hook and is not always the path that applies progression.
                if (GameReflection.TryInvokeInstanceMethod(eatAction, "ExecuteAction", out var _, actionData, true))
                {
                    LogHypothesis(
                        $"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: ExecuteAction(true) ran (ItemAction entry) actionDataType={actionData?.GetType().Name ?? "null"}");
                }
                else if (GameReflection.TryInvokeInstanceMethod(eatAction, "consume", out var _, actionData))
                {
                    LogHypothesis(
                        $"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: consume() ran (ExecuteAction missing) actionDataType={actionData?.GetType().Name ?? "null"}");
                }
                else
                {
                    LogHypothesis(
                        $"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: neither ExecuteAction(true) nor consume invoked actionDataType={actionData?.GetType().Name ?? "null"}");
                    return false;
                }

                if (GameReflection.TryInvokeInstanceMethod(eatAction, "Completed", out var _, actionData))
                {
                    LogHypothesis($"TryConsume [{unitIndex}/{effectiveCount}] {itemName}: Completed() ran after eat action");
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
                var targetLevel = beforeLevel + consumedUnits;
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

        private static string[] ResolveGainSkillProgressionNames(object gainSkillAction, ItemClass itemClass)
        {
            var names = new List<string>();
            AddProgressionNames(names, GetSkillsToGain(gainSkillAction));

            var craftingSkillGroup = GameReflection.GetMemberValue<string>(itemClass, "CraftingSkillGroup");
            if (!string.IsNullOrWhiteSpace(craftingSkillGroup))
            {
                AddProgressionName(names, MapCraftingSkillGroupToProgressionName(craftingSkillGroup));
            }

            return names.ToArray();
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
            foreach (var kv in beforeSnap)
            {
                var name = kv.Key;
                var before = kv.Value;
                var after = GetProgressionLevel(entity, name);
                var delta = after - before;
                if (delta > 0 && delta < successfulConsumes)
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

            var target = beforeLevel + successfulConsumes;
            if (TrySetProgressionLevel(entity, fix, target))
            {
                LogHypothesis(
                    $"QuickListReconcile [{unitIndex}] {itemName}: adjusted {fix} to {target} (was {beforeLevel}, consumed={successfulConsumes})");
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
