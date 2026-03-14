using System.Collections.Generic;

namespace AutoReadMod
{
    public static class AutoReader
    {
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

            DebugLog($"Scanning {container.items.Length} slots for readable items");
            var itemsToRead = new List<int>();

            // Find all readable items
            for (int i = 0; i < container.items.Length; i++)
            {
                var itemStack = container.items[i];
                if (itemStack.IsEmpty())
                    continue;

                DebugLog($"Slot {i}: {itemStack.itemValue?.ItemClass?.GetLocalizedItemName() ?? "unknown"}");
                
                if (IsReadableItem(itemStack))
                {
                    DebugLog($"Slot {i} is readable, adding to list");
                    itemsToRead.Add(i);
                }
            }

            DebugLog($"Found {itemsToRead.Count} readable items");
            
            // If experimental API testing is enabled, run tests on first item only
            if (ModConfig.Instance.ExperimentalApiTesting && itemsToRead.Count > 0)
            {
                SafeLog("[EXPERIMENTAL] Running API tests on first readable item");
                ApiExperiments.RunAllTests(player, container, itemsToRead[0]);
                SafeLog("[EXPERIMENTAL] API tests complete - check logs for results");
                return; // Don't process items normally when testing
            }
            
            // Read items (in reverse to avoid index issues when removing)
            for (int i = itemsToRead.Count - 1; i >= 0; i--)
            {
                int slotIndex = itemsToRead[i];
                ReadItem(player, container, slotIndex);
            }
        }

        private static bool IsReadableItem(ItemStack itemStack)
        {
            if (itemStack?.itemValue?.ItemClass == null)
            {
                DebugLog("IsReadableItem: null item");
                return false;
            }

            var itemClass = itemStack.itemValue.ItemClass;
            var config = ModConfig.Instance;

            // Debug: Check what properties the item has
            DebugLog($"Item: {itemClass.GetLocalizedItemName()}");
            DebugLog($"  ItemTags: {itemClass.ItemTags}");
            DebugLog($"  Name: {itemClass.Name}");
            
            // Try checking the item name directly for known readable item patterns
            string itemName = itemClass.Name?.ToLower() ?? "";
            
            // Books and magazines typically start with "book" or contain "magazine"
            // BUT exclude "bundle" items which are containers, not readable items
            bool isBook = itemName.StartsWith("book") && !itemName.Contains("bundle");
            bool isMagazine = itemName.Contains("magazine") && !itemName.Contains("bundle");
            
            // Schematics typically end with "schematic"
            bool isSchematic = itemName.EndsWith("schematic");
            bool isRecipe = itemName.EndsWith("recipe");
            
            DebugLog($"  isBook={isBook}, isMagazine={isMagazine}, isSchematic={isSchematic}, isRecipe={isRecipe}");
            
            // Check config settings
            if (isBook || isMagazine)
            {
                return config.ReadBooks || config.ReadMagazines;
            }
            
            if (isSchematic || isRecipe)
            {
                return config.ReadSchematics;
            }

            return false;
        }

        private static void ReadItem(EntityPlayer player, TileEntityLootContainer container, int slotIndex)
        {
            var itemStack = container.items[slotIndex];
            if (itemStack?.itemValue?.ItemClass == null)
                return;

            var itemClass = itemStack.itemValue.ItemClass;
            var itemName = itemClass.GetLocalizedItemName();
            int stackCount = itemStack.count;

            SafeLog($"AutoReadMod: {player.EntityName} reading {stackCount}x {itemName}");

            try
            {
                // Get the ItemAction for this item
                var itemActions = itemClass.Actions;
                if (itemActions == null || itemActions.Length == 0)
                {
                    SafeLog($"No actions found for {itemName}");
                    return;
                }

                // Find ItemActionEat
                ItemActionEat eatAction = null;
                foreach (var action in itemActions)
                {
                    if (action is ItemActionEat)
                    {
                        eatAction = action as ItemActionEat;
                        break;
                    }
                }

                if (eatAction == null)
                {
                    SafeLog($"No eat action found for {itemName}");
                    return;
                }

                DebugLog($"Found ItemActionEat, will consume {stackCount} items");
                
                // Fire the item's onSelfPrimaryActionEnd event - this runs effect_groups (AddProgressionLevel, GiveExp, etc)
                // without needing the item in player inventory. Same path the game uses when reading from container.
                int successfulConsumes = 0;
                var gameManager = GameManager.Instance;

                for (int i = 0; i < stackCount; i++)
                {
                    DebugLog($"Consuming item {i + 1}/{stackCount}");
                    try
                    {
                        var singleStack = new ItemStack(itemStack.itemValue, 1);
                        var invData = new ItemInventoryData(itemClass, singleStack, gameManager, player, -1);
                        var actionData = new ItemActionData(invData, 0);
                        var evtParams = new MinEventParams { Self = player, ItemValue = itemStack.itemValue, ItemInventoryData = invData };
                        itemClass.FireEvent(MinEventTypes.onSelfPrimaryActionEnd, evtParams);
                        successfulConsumes++;
                        DebugLog($"Successfully applied effect for item {i + 1}/{stackCount}");
                    }
                    catch (System.Exception consumeEx)
                    {
                        SafeLog($"Error consuming {itemName} (item {i + 1}/{stackCount}): {consumeEx.Message}");
                        DebugLog($"Stack trace: {consumeEx.StackTrace}");
                    }
                }
                
                DebugLog($"Consumed {successfulConsumes}/{stackCount}x {itemName}, now updating container");
                
                // Remove only successfully consumed items
                if (successfulConsumes > 0)
                {
                    if (successfulConsumes >= stackCount)
                    {
                        container.UpdateSlot(slotIndex, ItemStack.Empty);
                        DebugLog($"Stack removed from container slot {slotIndex}");
                    }
                    else
                    {
                        var remaining = stackCount - successfulConsumes;
                        container.UpdateSlot(slotIndex, new ItemStack(itemStack.itemValue, remaining));
                        DebugLog($"Reduced stack to {remaining} in slot {slotIndex}");
                    }
                    container.SetModified();
                }
            }
            catch (System.Exception ex)
            {
                SafeLog($"Error reading {itemName}: {ex.Message}");
                DebugLog($"Stack trace: {ex.StackTrace}");
            }
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

        private static void DebugLog(string message)
        {
            if (!ModConfig.Instance.DebugMode)
                return;
                
            SafeLog($"[DEBUG] {message}");
        }
    }
}
