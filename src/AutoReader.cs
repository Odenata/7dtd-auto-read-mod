using System;
using System.Collections.Generic;
using System.Reflection;

namespace AutoReadMod
{
    public static class AutoReader
    {
        public enum ReadableItemKind
        {
            None,
            Book,
            Magazine,
            Schematic,
            Recipe,
        }

        public static void ProcessContainer(EntityPlayer player, TileEntityLootContainer container)
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
                ReadStack(player, container.items[slotIndex], GameReflection.GetItemCount(container.items[slotIndex]), AutoReadVisualizationSource.Container, updatedStack =>
                {
                    container.UpdateSlot(slotIndex, updatedStack);
                    container.SetModified();
                });
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

                ReadStack(player, itemStack, consumeCount, AutoReadVisualizationSource.Bundle, updatedStack =>
                {
                    inventory.SetItem(slotIndex, updatedStack);
                    inventory.Changed();
                });
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

                ReadStack(player, itemStack, consumeCount, AutoReadVisualizationSource.Bundle, updatedStack =>
                {
                    GameReflection.SetBagItem(bag, slotIndex, updatedStack);
                });
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

        private static void ReadStack(EntityPlayer player, ItemStack itemStack, int consumeCount, AutoReadVisualizationSource source, Action<ItemStack> updateStack)
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
                var successfulConsumes = ApplyReadableEffects(player, itemStack, targetCount);
                DebugLog($"Consumed {successfulConsumes}/{targetCount}x {itemName}, now updating slot");

                if (successfulConsumes <= 0)
                {
                    return;
                }

                // ItemStack.count was decremented in place by ItemActionEat.ExecuteInstantAction(isHeldItem:false).
                if (itemStack.IsEmpty())
                {
                    updateStack(GameReflection.GetEmptyItemStack());
                }
                else
                {
                    updateStack(itemStack);
                    DebugLog($"Reduced stack to {GameReflection.GetItemCount(itemStack)}");
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
        internal static int ApplyReadableEffectsForTest(EntityPlayer player, ItemStack itemStack, int consumeCount)
        {
            return ApplyReadableEffects(player, itemStack, consumeCount);
        }

        private static int ApplyReadableEffects(EntityPlayer player, ItemStack itemStack, int consumeCount)
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

            object eatAction = null;
            foreach (var action in itemActions)
            {
                if (action != null && action.GetType().Name == "ItemActionEat")
                {
                    eatAction = action;
                    break;
                }
            }

            if (eatAction == null)
            {
                SafeLog($"No eat action found for {itemName}");
                return 0;
            }

            var executeMethod = eatAction.GetType().GetMethod(
                "ExecuteInstantAction",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(EntityAlive), typeof(ItemStack), typeof(bool), typeof(XUiC_ItemStack) },
                null);
            if (executeMethod == null || executeMethod.ReturnType != typeof(bool))
            {
                SafeLog($"ItemActionEat.ExecuteInstantAction(bool) not found for {itemName}");
                return 0;
            }

            DebugLog($"Found ItemActionEat.ExecuteInstantAction, will consume up to {consumeCount} from stack");

            int successfulConsumes = 0;
            var gameManager = (object)GameReflection.GetGameManagerInstance();

            for (int i = 0; i < consumeCount; i++)
            {
                if (itemStack == null || itemStack.IsEmpty())
                {
                    break;
                }

                DebugLog($"Consuming item {i + 1}/{consumeCount}");
                try
                {
                    var ret = executeMethod.Invoke(eatAction, new object[] { player, itemStack, false, null });
                    if (ret is not bool ok || !ok)
                    {
                        break;
                    }

                    // Vanilla ItemActionEat skips onSelfPrimaryActionEnd when stackController is null; books need it for perk XML.
                    var invData = GameReflection.CreateItemInventoryData(itemClass, itemStack, gameManager, player, -1, GameReflection.GetItemValue(itemStack));
                    if (invData != null)
                    {
                        var evtParams = GameReflection.CreateMinEventParams(player, GameReflection.GetItemValue(itemStack), invData);
                        if (evtParams != null)
                        {
                            itemClass.FireEvent(MinEventTypes.onSelfPrimaryActionEnd, evtParams);
                        }
                    }

                    successfulConsumes++;
                    DebugLog($"Successfully applied effect for item {successfulConsumes}/{consumeCount}");
                }
                catch (Exception consumeEx)
                {
                    var rootException = UnwrapReflectionException(consumeEx);
                    SafeLog($"Error consuming {itemName} (item {i + 1}/{consumeCount}): {rootException.Message}");
                    SafeLog(rootException.StackTrace ?? consumeEx.StackTrace ?? string.Empty);
                }
            }

            return successfulConsumes;
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
