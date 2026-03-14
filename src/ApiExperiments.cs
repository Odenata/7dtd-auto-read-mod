using System;
using System.Collections.Generic;

namespace AutoReadMod
{
    /// <summary>
    /// Experimental class for testing alternative APIs to ItemActionEat.consume()
    /// This class is used for investigation purposes only.
    /// Enable DebugMode in config to see detailed logs.
    /// </summary>
    public static class ApiExperiments
    {
        /// <summary>
        /// Test Alternative 1: ItemClass.ExecuteAction()
        /// </summary>
        public static bool TestExecuteAction(EntityPlayer player, TileEntityLootContainer container, int slotIndex)
        {
            var itemStack = container.items[slotIndex];
            if (itemStack?.itemValue?.ItemClass == null)
                return false;

            var itemClass = itemStack.itemValue.ItemClass;
            var itemName = itemClass.GetLocalizedItemName();

            Log($"[API Test 1] Testing ItemClass.ExecuteAction() on {itemName}");

            try
            {
                // Find the action index for ItemActionEat
                int actionIndex = -1;
                for (int i = 0; i < itemClass.Actions.Length; i++)
                {
                    if (itemClass.Actions[i] is ItemActionEat)
                    {
                        actionIndex = i;
                        break;
                    }
                }

                if (actionIndex == -1)
                {
                    Log($"[API Test 1] No ItemActionEat found");
                    return false;
                }

                // Create ItemInventoryData
                var gameManager = GameManager.Instance;
                var inventoryData = new ItemInventoryData(itemClass, itemStack, gameManager, player, -1);

                // Get PlayerActionsLocal - this might be the tricky part
                // Try to get it from the player's entity
                var playerActions = player.playerInput?.PrimaryPlayer;
                
                if (playerActions == null)
                {
                    Log($"[API Test 1] Could not get PlayerActionsLocal - player.playerInput.PrimaryPlayer is null");
                    return false;
                }

                Log($"[API Test 1] Calling ExecuteAction(actionIdx={actionIndex}, bReleased=false)");
                
                // Call ExecuteAction
                itemClass.ExecuteAction(actionIndex, inventoryData, false, playerActions);
                
                Log($"[API Test 1] ExecuteAction completed without exception");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[API Test 1] Exception: {ex.Message}");
                Log($"[API Test 1] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Test Alternative 2: ItemClass.Interact()
        /// </summary>
        public static bool TestInteract(EntityPlayer player, TileEntityLootContainer container, int slotIndex)
        {
            var itemStack = container.items[slotIndex];
            if (itemStack?.itemValue?.ItemClass == null)
                return false;

            var itemClass = itemStack.itemValue.ItemClass;
            var itemName = itemClass.GetLocalizedItemName();

            Log($"[API Test 2] Testing ItemClass.Interact() on {itemName}");

            try
            {
                // Create ItemInventoryData
                var gameManager = GameManager.Instance;
                var inventoryData = new ItemInventoryData(itemClass, itemStack, gameManager, player, -1);

                Log($"[API Test 2] Calling Interact()");
                
                // Call Interact
                itemClass.Interact(inventoryData);
                
                Log($"[API Test 2] Interact completed without exception");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[API Test 2] Exception: {ex.Message}");
                Log($"[API Test 2] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Test Alternative 3: ItemActionEat.ExecuteAction() with _bReleased parameter
        /// </summary>
        public static bool TestItemActionExecuteAction(EntityPlayer player, TileEntityLootContainer container, int slotIndex, bool bReleased)
        {
            var itemStack = container.items[slotIndex];
            if (itemStack?.itemValue?.ItemClass == null)
                return false;

            var itemClass = itemStack.itemValue.ItemClass;
            var itemName = itemClass.GetLocalizedItemName();

            Log($"[API Test 3] Testing ItemActionEat.ExecuteAction(bReleased={bReleased}) on {itemName}");

            try
            {
                // Find ItemActionEat
                ItemActionEat eatAction = null;
                foreach (var action in itemClass.Actions)
                {
                    if (action is ItemActionEat)
                    {
                        eatAction = action as ItemActionEat;
                        break;
                    }
                }

                if (eatAction == null)
                {
                    Log($"[API Test 3] No ItemActionEat found");
                    return false;
                }

                // Create ItemInventoryData and ItemActionData
                var gameManager = GameManager.Instance;
                var inventoryData = new ItemInventoryData(itemClass, itemStack, gameManager, player, -1);
                var actionData = new ItemActionData(inventoryData, 0);

                Log($"[API Test 3] Calling ExecuteAction(bReleased={bReleased})");
                
                // Call ExecuteAction
                eatAction.ExecuteAction(actionData, bReleased);
                
                Log($"[API Test 3] ExecuteAction completed without exception");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[API Test 3] Exception: {ex.Message}");
                Log($"[API Test 3] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Test Alternative 4: ItemActionEat.Completed()
        /// </summary>
        public static bool TestCompleted(EntityPlayer player, TileEntityLootContainer container, int slotIndex)
        {
            var itemStack = container.items[slotIndex];
            if (itemStack?.itemValue?.ItemClass == null)
                return false;

            var itemClass = itemStack.itemValue.ItemClass;
            var itemName = itemClass.GetLocalizedItemName();

            Log($"[API Test 4] Testing ItemActionEat.Completed() on {itemName}");

            try
            {
                // Find ItemActionEat
                ItemActionEat eatAction = null;
                foreach (var action in itemClass.Actions)
                {
                    if (action is ItemActionEat)
                    {
                        eatAction = action as ItemActionEat;
                        break;
                    }
                }

                if (eatAction == null)
                {
                    Log($"[API Test 4] No ItemActionEat found");
                    return false;
                }

                // Create ItemInventoryData and ItemActionData
                var gameManager = GameManager.Instance;
                var inventoryData = new ItemInventoryData(itemClass, itemStack, gameManager, player, -1);
                var actionData = new ItemActionData(inventoryData, 0);

                Log($"[API Test 4] Calling Completed()");
                
                // Call Completed
                eatAction.Completed(actionData);
                
                Log($"[API Test 4] Completed() finished without exception");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[API Test 4] Exception: {ex.Message}");
                Log($"[API Test 4] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Test Alternative 5: Direct buff application
        /// </summary>
        public static bool TestDirectBuffApplication(EntityPlayer player, TileEntityLootContainer container, int slotIndex)
        {
            var itemStack = container.items[slotIndex];
            if (itemStack?.itemValue?.ItemClass == null)
                return false;

            var itemClass = itemStack.itemValue.ItemClass;
            var itemName = itemClass.GetLocalizedItemName();

            Log($"[API Test 5] Testing direct buff application on {itemName}");

            try
            {
                // Find ItemActionEat
                ItemActionEat eatAction = null;
                foreach (var action in itemClass.Actions)
                {
                    if (action is ItemActionEat)
                    {
                        eatAction = action as ItemActionEat;
                        break;
                    }
                }

                if (eatAction == null)
                {
                    Log($"[API Test 5] No ItemActionEat found");
                    return false;
                }

                // Create ItemInventoryData and ItemActionData
                var gameManager = GameManager.Instance;
                var inventoryData = new ItemInventoryData(itemClass, itemStack, gameManager, player, -1);
                var actionData = new ItemActionData(inventoryData, 0);

                // Get buff actions
                Log($"[API Test 5] Getting buff actions");
                var buffActions = eatAction.getBuffActions(actionData);
                
                if (buffActions == null || buffActions.Count == 0)
                {
                    Log($"[API Test 5] No buff actions found");
                    return false;
                }

                Log($"[API Test 5] Found {buffActions.Count} buff actions");

                // Apply each buff to the player
                foreach (var buffClass in buffActions)
                {
                    Log($"[API Test 5] Applying buff: {buffClass.Name}");
                    
                    // Try to add buff to player
                    // Need to investigate the correct method - might be AddBuff, Buffs.AddBuff, etc.
                    // This is a placeholder - actual implementation depends on EntityPlayer API
                    player.Buffs.AddBuff(buffClass.Name);
                }

                Log($"[API Test 5] Direct buff application completed");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[API Test 5] Exception: {ex.Message}");
                Log($"[API Test 5] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Run all API tests on a single item
        /// </summary>
        public static void RunAllTests(EntityPlayer player, TileEntityLootContainer container, int slotIndex)
        {
            Log("========================================");
            Log("Starting API Investigation Tests");
            Log("========================================");

            var itemStack = container.items[slotIndex];
            if (itemStack?.itemValue?.ItemClass == null)
            {
                Log("Invalid item stack");
                return;
            }

            var itemName = itemStack.itemValue.ItemClass.GetLocalizedItemName();
            Log($"Testing on item: {itemName}");
            Log("");

            // Test 1: ItemClass.ExecuteAction()
            Log("--- Test 1: ItemClass.ExecuteAction() ---");
            bool test1Result = TestExecuteAction(player, container, slotIndex);
            Log($"Result: {(test1Result ? "SUCCESS" : "FAILED")}");
            Log("");

            // Test 2: ItemClass.Interact()
            Log("--- Test 2: ItemClass.Interact() ---");
            bool test2Result = TestInteract(player, container, slotIndex);
            Log($"Result: {(test2Result ? "SUCCESS" : "FAILED")}");
            Log("");

            // Test 3a: ItemActionEat.ExecuteAction(bReleased=false)
            Log("--- Test 3a: ItemActionEat.ExecuteAction(bReleased=false) ---");
            bool test3aResult = TestItemActionExecuteAction(player, container, slotIndex, false);
            Log($"Result: {(test3aResult ? "SUCCESS" : "FAILED")}");
            Log("");

            // Test 3b: ItemActionEat.ExecuteAction(bReleased=true)
            Log("--- Test 3b: ItemActionEat.ExecuteAction(bReleased=true) ---");
            bool test3bResult = TestItemActionExecuteAction(player, container, slotIndex, true);
            Log($"Result: {(test3bResult ? "SUCCESS" : "FAILED")}");
            Log("");

            // Test 4: ItemActionEat.Completed()
            Log("--- Test 4: ItemActionEat.Completed() ---");
            bool test4Result = TestCompleted(player, container, slotIndex);
            Log($"Result: {(test4Result ? "SUCCESS" : "FAILED")}");
            Log("");

            // Test 5: Direct buff application
            Log("--- Test 5: Direct Buff Application ---");
            bool test5Result = TestDirectBuffApplication(player, container, slotIndex);
            Log($"Result: {(test5Result ? "SUCCESS" : "FAILED")}");
            Log("");

            Log("========================================");
            Log("API Investigation Tests Complete");
            Log("========================================");
        }

        private static void Log(string message)
        {
            try
            {
                UnityEngine.Debug.Log($"[AutoReadMod][APITest] {message}");
            }
            catch
            {
                // Unity not available
            }
        }
    }
}
