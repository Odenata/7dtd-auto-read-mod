using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

namespace AutoReadMod
{
    internal static class GameReflection
    {
        private static PropertyInfo _xUiItemStackItemStackProperty;

        /// <summary>Successful <see cref="TryResolveCraftingManager"/> result for the rest of the session (main thread).</summary>
        private static object _cachedCraftingManagerForSession;
        private static readonly object LootDebugFileLock = new object();
        private static string _lootDebugFilePath;

        /// <summary>Hermetic tests: drop cached CraftingManager so a fresh Instance is resolved.</summary>
        internal static void ClearCraftingManagerCacheForTest()
        {
            _cachedCraftingManagerForSession = null;
        }

        private sealed class ReferenceIdentityEqualityComparer : IEqualityComparer<object>
        {
            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        internal static EntityPlayer GetPlayerFromLootWindowGroup(XUiC_LootWindowGroup lootWindowGroup)
        {
            var xui = GetMemberValue<object>(lootWindowGroup, "xui");
            var playerUi = GetMemberValue<object>(xui, "playerUI");
            return GetMemberValue<EntityPlayer>(playerUi, "entityPlayer");
        }

        internal static EntityPlayer GetPlayerFromLootWindow(XUiC_LootWindow lootWindow)
        {
            if (lootWindow == null)
            {
                return null;
            }

            try
            {
                var xui = GetMemberValue<object>(lootWindow, "xui");
                var playerUi = GetMemberValue<object>(xui, "playerUI");
                return GetMemberValue<EntityPlayer>(playerUi, "entityPlayer");
            }
            catch
            {
                return null;
            }
        }

        internal static EntityPlayer GetPlayerFromBagContainer(XUiC_BagContainer bagContainer)
        {
            if (bagContainer == null)
            {
                return null;
            }

            try
            {
                var xui = GetMemberValue<object>(bagContainer, "xui");
                var playerUi = GetMemberValue<object>(xui, "playerUI");
                return GetMemberValue<EntityPlayer>(playerUi, "entityPlayer");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Rebind bag slot controllers from the entity loot <see cref="Bag"/> after auto-read mutates slots.
        /// Uses vanilla <c>SetStacks</c> (same family as <see cref="XUiC_BagContainer.SetBag"/>).
        /// </summary>
        internal static void RefreshBagContainerUiFromBag(XUiC_BagContainer bagContainer, Bag bag)
        {
            if (bagContainer == null || bag == null)
            {
                return;
            }

            try
            {
                var slots = GetBagSlots(bag);
                var setStacks = bagContainer.GetType().GetMethod(
                    "SetStacks",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(ItemStack[]) },
                    null);
                if (setStacks != null)
                {
                    setStacks.Invoke(bagContainer, new object[] { slots });
                }

                SyncLootGridItemControllers(bagContainer, slots);
            }
            catch
            {
                // best-effort UI sync
            }
        }

        internal static XUiC_LootWindowGroup TryResolveLootWindowGroupFromLootWindow(XUiC_LootWindow lootWindow)
        {
            if (lootWindow == null)
            {
                return null;
            }

            try
            {
                for (var current = (object)lootWindow; current != null; current = GetMemberValue<object>(current, "Parent"))
                {
                    if (current is XUiC_LootWindowGroup group)
                    {
                        return group;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }

        /// <summary>
        /// Rebind loot slot controllers from the tile entity after auto-read mutates backing stacks.
        /// Uses vanilla <c>SetSlots</c> (same as <see cref="XUiC_LootWindow.SetTileEntityChest"/>).
        /// </summary>
        internal static void RefreshLootContainerUiFromTileEntity(XUiC_LootWindow lootWindow, TEFeatureStorage te)
        {
            if (lootWindow == null || te == null)
            {
                return;
            }

            try
            {
                var lootContainer = GetMemberValue<object>(lootWindow, "lootContainer");
                if (lootContainer == null)
                {
                    return;
                }

                var grid = te.ItemGrid;
                var items = grid == null ? null : GetMemberValue<ItemStack[]>(grid, "items");
                var setSlots = lootContainer.GetType().GetMethod(
                    "SetSlots",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(TEFeatureStorage) },
                    null);
                if (setSlots != null)
                {
                    setSlots.Invoke(lootContainer, new object[] { te });
                }

                SyncLootGridItemControllers(lootContainer, items);
            }
            catch
            {
                // best-effort UI sync
            }
        }

        private static void SyncLootGridItemControllers(object lootContainer, ItemStack[] items)
        {
            if (lootContainer == null)
            {
                return;
            }

            try
            {
                var controllers = GetMemberValue<object>(lootContainer, "itemControllers") as System.Array;
                if (controllers == null || items == null)
                {
                    return;
                }

                var length = System.Math.Min(controllers.Length, items.Length);
                for (var i = 0; i < length; i++)
                {
                    var controller = controllers.GetValue(i) as XUiC_ItemStack;
                    if (controller == null)
                    {
                        continue;
                    }

                    var displayStack = CloneItemStackForUi(items[i]);
                    var forceSet = typeof(XUiC_ItemStack).GetMethod(
                        "ForceSetItemStack",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(ItemStack) },
                        null);
                    if (forceSet != null)
                    {
                        forceSet.Invoke(controller, new object[] { displayStack });
                        continue;
                    }

                    if (_xUiItemStackItemStackProperty == null)
                    {
                        _xUiItemStackItemStackProperty = typeof(XUiC_ItemStack).GetProperty(
                            "ItemStack",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }

                    _xUiItemStackItemStackProperty?.SetValue(controller, displayStack, null);
                }
            }
            catch
            {
                // per-slot sync is belt-and-suspenders after SetSlots
            }
        }

        private static ItemStack CloneItemStackForUi(ItemStack stack)
        {
            if (stack == null || stack.IsEmpty())
            {
                return ItemStack.Empty;
            }

            try
            {
                var clone = typeof(ItemStack).GetMethod("Clone", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (clone != null)
                {
                    return clone.Invoke(stack, null) as ItemStack;
                }
            }
            catch
            {
                // fall through
            }

            return new ItemStack(stack.itemValue, stack.count);
        }

        internal static void TryUpdateLootContainerSlot(TEFeatureStorage container, int slotIndex, ItemStack updatedStack)
        {
            if (container == null || slotIndex < 0)
            {
                return;
            }

            try
            {
                var stack = updatedStack ?? ItemStack.Empty;
                var updateSlot = container.GetType().GetMethod(
                    "UpdateSlot",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(int), typeof(ItemStack) },
                    null);
                updateSlot?.Invoke(container, new object[] { slotIndex, stack });
            }
            catch
            {
                // array write in TryWriteLootItem is the minimum; UpdateSlot keeps TE + UI listeners aligned
            }
        }

        /// <summary>
        /// Loot UI slot controller for <paramref name="slotIndex"/> — vanilla <c>ItemActionEat.ExecuteInstantAction</c> expects this when reading from an open loot grid (magazines/skills).
        /// </summary>
        internal static XUiC_ItemStack TryGetLootSlotItemStackController(XUiC_LootWindowGroup lootWindowGroup, int slotIndex)
        {
            if (lootWindowGroup == null || slotIndex < 0)
            {
                return null;
            }

            try
            {
                var controllers = TryGetLootItemStackControllersArray(lootWindowGroup);
                if (controllers == null || slotIndex >= controllers.Length)
                {
                    return null;
                }

                return controllers.GetValue(slotIndex) as XUiC_ItemStack;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// When slot index does not line up with UI grids (some containers), match the open loot stack by reference.
        /// </summary>
        internal static XUiC_ItemStack TryGetLootSlotByStackReference(XUiC_LootWindowGroup lootWindowGroup, ItemStack stack)
        {
            if (lootWindowGroup == null || stack == null)
            {
                return null;
            }

            try
            {
                var controllers = TryGetLootItemStackControllersArray(lootWindowGroup);
                if (controllers == null)
                {
                    return null;
                }

                for (var i = 0; i < controllers.Length; i++)
                {
                    var c = controllers.GetValue(i) as XUiC_ItemStack;
                    var uiStack = TryGetItemStackFromLootSlotController(c);
                    if (!ReferenceEquals(uiStack, null) && ReferenceEquals(uiStack, stack))
                    {
                        return c;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        internal static ItemStack TryGetItemStackFromLootSlotController(XUiC_ItemStack controller)
        {
            if (controller == null)
            {
                return null;
            }

            try
            {
                if (_xUiItemStackItemStackProperty == null)
                {
                    _xUiItemStackItemStackProperty = typeof(XUiC_ItemStack).GetProperty(
                        "ItemStack",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                return _xUiItemStackItemStackProperty?.GetValue(controller, null) as ItemStack;
            }
            catch
            {
                return null;
            }
        }

        private static System.Array TryGetLootItemStackControllersArray(XUiC_LootWindowGroup lootWindowGroup)
        {
            object lootWindow = GetMemberValue<object>(lootWindowGroup, "lootWindow");
            if (lootWindow == null)
            {
                return null;
            }

            object lootContainer = GetMemberValue<object>(lootWindow, "lootContainer");
            if (lootContainer == null)
            {
                return null;
            }

            // Prefer backing array (always populated for open loot); GetItemStackControllers() may depend on UI init order.
            var controllers = GetMemberValue<object>(lootContainer, "itemControllers") as System.Array;
            if (controllers == null)
            {
                controllers = InvokeMethod(lootContainer, "GetItemStackControllers") as System.Array;
            }

            return controllers;
        }

        /// <summary>
        /// Bundle outputs land in toolbelt or backpack; vanilla magazine/recipe progression uses the same
        /// <see cref="ItemActionEat.ExecuteInstantAction"/> path as loot when given the slot's <see cref="XUiC_ItemStack"/>.
        /// Resolves that controller from <see cref="LocalPlayerUI.activeItemStackGrids"/>.
        /// Both <see cref="XUiC_Backpack"/> and <see cref="XUiC_Toolbelt"/> are searched: inventory bundle drops
        /// usually map to the backpack grid; searching only one grid leaves <c>stackController</c> null and breaks skill gains.
        /// Slot controllers match by stack reference when possible, then by same item key + count when instances differ.
        /// </summary>
        /// <param name="readFromBag">Legacy hint (still accepted); both grids are always searched.</param>
        internal static XUiC_ItemStack TryGetPlayerItemSlotControllerForBundleRead(
            EntityPlayer player,
            int slotIndex,
            ItemStack liveStack,
            bool readFromBag)
        {
            if (player == null || slotIndex < 0 || liveStack == null)
            {
                return null;
            }

            try
            {
                object lpUi = TryResolveLocalPlayerUIForBundle(player);
                if (lpUi == null)
                {
                    return null;
                }

                _ = readFromBag;

                // Backpack first: bundle-added items typically appear in non-toolbelt inventory slots.
                foreach (var gridTypeName in new[] { "XUiC_Backpack", "XUiC_Toolbelt" })
                {
                    var candidates = new List<object>(4);
                    AddCandidateGridsForBundle(lpUi, gridTypeName, candidates);
                    foreach (var grid in candidates)
                    {
                        var byRef = TryFindItemStackGridControllerByStackReference(grid, liveStack);
                        if (byRef != null)
                        {
                            return byRef;
                        }

                        var byIdx = TryGetItemStackControllerFromGridAtSlot(grid, slotIndex);
                        if (byIdx != null)
                        {
                            var uiStack = TryGetItemStackFromLootSlotController(byIdx);
                            if (uiStack != null && BundleSlotStacksAlign(uiStack, liveStack))
                            {
                                return byIdx;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Non-UI host / stub
            }

            return null;
        }

        /// <summary>
        /// Prefer GameManager local player UI — magazine readers resolve through LocalPlayerUI even when patch context uses another reference.
        /// </summary>
        private static object TryResolveLocalPlayerUIForBundle(EntityPlayer player)
        {
            EntityPlayer local = TryGetLocalEntityPlayer();
            foreach (EntityPlayer ep in new EntityPlayer[] { local, player })
            {
                if (ep == null)
                {
                    continue;
                }

                object ui = GetMemberValue<object>(ep, "PlayerUI");
                if (ui == null)
                {
                    ui = GetMemberValue<object>(ep, "playerUI");
                }

                if (ui != null)
                {
                    return ui;
                }
            }

            return null;
        }

        /// <summary>
        /// <see cref="LocalPlayerUI.activeItemStackGrids"/> often excludes backpack/toolbelt while loot/crafting windows are focused,
        /// leaving no slot controllers and breaking magazine skill grants. When no matching grid is registered, resolve grids from <see cref="XUi"/>.
        /// </summary>
        private static void AddCandidateGridsForBundle(object lpUi, string gridTypeName, List<object> dst)
        {
            var active = GetMemberValue<System.Collections.IList>(lpUi, "activeItemStackGrids");
            if (active != null)
            {
                for (var i = 0; i < active.Count; i++)
                {
                    var grid = active[i];
                    if (grid != null && grid.GetType().Name == gridTypeName)
                    {
                        dst.Add(grid);
                    }
                }
            }

            if (dst.Count > 0)
            {
                return;
            }

            object xui = GetMemberValue<object>(lpUi, "xui");
            if (xui == null)
            {
                xui = GetMemberValue<object>(lpUi, "mXUi");
            }

            if (xui == null)
            {
                return;
            }

            System.Type gridType = typeof(EntityPlayer).Assembly.GetType(gridTypeName);
            if (gridType == null)
            {
                return;
            }

            TryAddXUiChildrenOfType(xui, gridType, dst);
        }

        private static void TryAddXUiChildrenOfType(object xui, System.Type componentType, List<object> dst)
        {
            try
            {
                foreach (var method in xui.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.Name != "GetChildrenByType" || !method.IsGenericMethodDefinition)
                    {
                        continue;
                    }

                    if (method.GetParameters().Length != 0)
                    {
                        continue;
                    }

                    object raw;
                    try
                    {
                        raw = method.MakeGenericMethod(componentType).Invoke(xui, null);
                    }
                    catch
                    {
                        continue;
                    }

                    if (raw is System.Collections.IEnumerable enumerable)
                    {
                        foreach (object item in enumerable)
                        {
                            if (item != null)
                            {
                                dst.Add(item);
                            }
                        }

                        return;
                    }
                }
            }
            catch
            {
                // Stub / older API
            }
        }

        /// <summary>
        /// Player inventory backing arrays and HUD grids often keep different <see cref="ItemStack"/> instances for the same slot.
        /// Reference-only matching leaves <c>XUiC_ItemStack</c> unresolved and breaks vanilla magazine skill paths that expect the UI controller.
        /// </summary>
        private static bool BundleSlotStacksAlign(ItemStack uiStack, ItemStack backingStack)
        {
            if (backingStack == null || backingStack.IsEmpty() || uiStack == null || uiStack.IsEmpty())
            {
                return false;
            }

            if (ReferenceEquals(uiStack, backingStack))
            {
                return true;
            }

            string uiKey = AutoReader.GetItemKey(uiStack);
            string backingKey = AutoReader.GetItemKey(backingStack);
            if (string.IsNullOrEmpty(uiKey) ||
                string.IsNullOrEmpty(backingKey) ||
                !string.Equals(uiKey, backingKey, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return GetItemCount(uiStack) == GetItemCount(backingStack);
        }

        private static XUiC_ItemStack TryFindItemStackGridControllerByStackReference(object grid, ItemStack stack)
        {
            var controllers = TryGetItemStackControllersArrayFromGrid(grid);
            if (controllers == null)
            {
                return null;
            }

            for (var i = 0; i < controllers.Length; i++)
            {
                var c = controllers.GetValue(i) as XUiC_ItemStack;
                var uiStack = TryGetItemStackFromLootSlotController(c);
                if (!ReferenceEquals(uiStack, null) && ReferenceEquals(uiStack, stack))
                {
                    return c;
                }
            }

            for (var i = 0; i < controllers.Length; i++)
            {
                var c = controllers.GetValue(i) as XUiC_ItemStack;
                var uiStack = TryGetItemStackFromLootSlotController(c);
                if (BundleSlotStacksAlign(uiStack, stack))
                {
                    return c;
                }
            }

            return null;
        }

        private static System.Array TryGetItemStackControllersArrayFromGrid(object grid)
        {
            if (grid == null)
            {
                return null;
            }

            var controllers = GetMemberValue<object>(grid, "itemControllers") as System.Array;
            if (controllers == null)
            {
                controllers = InvokeMethod(grid, "GetItemStackControllers") as System.Array;
            }

            return controllers;
        }

        private static XUiC_ItemStack TryGetItemStackControllerFromGridAtSlot(object grid, int slotIndex)
        {
            if (grid == null || slotIndex < 0)
            {
                return null;
            }

            if (grid.GetType().Name == "XUiC_Toolbelt")
            {
                var slot = InvokeMethod(grid, "GetSlotControl", slotIndex);
                if (slot is XUiC_ItemStack xs)
                {
                    return xs;
                }
            }

            var arr = TryGetItemStackControllersArrayFromGrid(grid);
            if (arr != null && slotIndex < arr.Length)
            {
                return arr.GetValue(slotIndex) as XUiC_ItemStack;
            }

            return null;
        }

        /// <summary>
        /// Gets XUiC_CollectedItemList for showing item notifications (same UI as loot pickups).
        /// Tries: (1) main XUi from patched xui's player, (2) GameManager local player XUi, (3) given xui.
        /// </summary>
        internal static object GetCollectedItemList(object xui)
        {
            var mainXui = GetMainXUiFromPatchedXUi(xui);
            var list = GetCollectedItemListFromXUi(mainXui);
            if (list != null) return list;
            var localPlayerXui = GetLocalPlayerXUi();
            list = GetCollectedItemListFromXUi(localPlayerXui);
            if (list != null) return list;
            return GetCollectedItemListFromXUi(xui);
        }

        /// <summary>
        /// Gets the main game XUi from a patched XUi: xui -> playerUI -> entityPlayer -> PlayerUI -> xui.
        /// Works when the patched xui is the loot/HUD root and has playerUI.
        /// </summary>
        private static object GetMainXUiFromPatchedXUi(object xui)
        {
            if (xui == null) return null;
            object playerUI = GetMemberValue<object>(xui, "playerUI");
            if (playerUI == null) playerUI = GetMemberValue<object>(xui, "PlayerUI");
            if (playerUI == null) return null;
            object player = GetMemberValue<object>(playerUI, "entityPlayer");
            if (player == null) player = GetMemberValue<object>(playerUI, "EntityPlayer");
            if (player == null) return null;
            object mainPlayerUI = GetMemberValue<object>(player, "PlayerUI");
            if (mainPlayerUI == null) mainPlayerUI = GetMemberValue<object>(player, "playerUI");
            if (mainPlayerUI == null) return null;
            object mainXui = GetMemberValue<object>(mainPlayerUI, "xui");
            if (mainXui == null) mainXui = GetMemberValue<object>(mainPlayerUI, "mXUi");
            if (mainXui == null) mainXui = GetMemberValue<object>(mainPlayerUI, "Xui");
            return mainXui;
        }

        /// <summary>
        /// Gets XUiC_CollectedItemList from a single XUi instance. Tries multiple member names.
        /// </summary>
        private static object GetCollectedItemListFromXUi(object xui)
        {
            if (xui == null) return null;
            foreach (var name in new[] { "CollectedItemList", "collectedItemList" })
            {
                var list = GetMemberValue<object>(xui, name);
                if (list != null) return list;
            }
            var type = xui.GetType();
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (prop.PropertyType == null) continue;
                if (!prop.PropertyType.Name.Contains("CollectedItemList")) continue;
                if (prop.GetIndexParameters().Length != 0) continue;
                try
                {
                    var list = prop.GetValue(xui, null);
                    if (list != null) return list;
                }
                catch { /* ignore */ }
            }
            return null;
        }

        /// <summary>
        /// Gets the XUi instance for the local player (in-game HUD). GameManager -> local player -> PlayerUI -> xui.
        /// Tries multiple member names for game version compatibility.
        /// </summary>
        internal static object GetLocalPlayerXUi()
        {
            var gm = GetGameManagerInstance();
            if (gm == null) { LogLocalPlayerChain("GameManager null"); return null; }
            object localPlayer = GetMemberValue<object>(gm, "myEntityPlayerLocal");
            if (localPlayer == null) localPlayer = GetMemberValue<object>(gm, "MyEntityPlayerLocal");
            if (localPlayer == null) localPlayer = FindEntityPlayerOnGameManager(gm);
            if (localPlayer == null) { LogLocalPlayerChain("myEntityPlayerLocal null"); return null; }
            object playerUI = GetMemberValue<object>(localPlayer, "PlayerUI");
            if (playerUI == null) playerUI = GetMemberValue<object>(localPlayer, "playerUI");
            if (playerUI == null) { LogLocalPlayerChain("PlayerUI null"); return null; }
            object xui = GetMemberValue<object>(playerUI, "xui");
            if (xui == null) xui = GetMemberValue<object>(playerUI, "mXUi");
            if (xui == null) xui = GetMemberValue<object>(playerUI, "Xui");
            if (xui == null) { LogLocalPlayerChain("xui null"); return null; }
            return xui;
        }

        private static float _lastLocalPlayerChainLogTime = -999f;
        private const float LocalPlayerChainLogIntervalSeconds = 15f;

        private static void LogLocalPlayerChain(string step)
        {
            if (!ModConfig.Instance.DebugMode) return;
            if (UnityEngine.Time.time - _lastLocalPlayerChainLogTime < LocalPlayerChainLogIntervalSeconds) return;
            _lastLocalPlayerChainLogTime = UnityEngine.Time.time;
            try { UnityEngine.Debug.Log($"[AutoReadMod] Visualization: GetLocalPlayerXUi failed at: {step}"); } catch { }
        }

        /// <summary>
        /// Fallback: find any instance field or property on GameManager that holds an EntityPlayer (e.g. different game build naming).
        /// </summary>
        private static object FindEntityPlayerOnGameManager(object gm)
        {
            if (gm == null) return null;
            var type = gm.GetType();
            var entityPlayerType = typeof(EntityPlayer);
            try
            {
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.FieldType == null || !entityPlayerType.IsAssignableFrom(f.FieldType)) continue;
                    var val = f.GetValue(gm);
                    if (val is EntityPlayer) return val;
                }
                foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (p.PropertyType == null || !entityPlayerType.IsAssignableFrom(p.PropertyType)) continue;
                    if (p.GetIndexParameters().Length != 0) continue;
                    try
                    {
                        var val = p.GetValue(gm, null);
                        if (val is EntityPlayer) return val;
                    }
                    catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }
            return null;
        }

        /// <summary>
        /// Resolves an ItemValue by item class name for display (e.g. to build an ItemStack for AddItemStack).
        /// Tries nameToItemCaseInsensitive dictionary, then list + GetItemClass/GetItem (instance methods).
        /// </summary>
        internal static ItemValue GetItemValueByClassName(string itemClassName)
        {
            if (string.IsNullOrWhiteSpace(itemClassName))
            {
                return null;
            }

            var itemClassType = typeof(ItemClass);

            // 1) Try nameToItemCaseInsensitive dictionary (static on ItemClass in game)
            var nameToItemField = itemClassType.GetField("nameToItemCaseInsensitive", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (nameToItemField != null)
            {
                try
                {
                    var dict = nameToItemField.GetValue(null);
                    if (dict != null)
                    {
                        var dictType = dict.GetType();
                        var getItemMethod = dictType.GetMethod("get_Item", new[] { typeof(string) });
                        if (getItemMethod != null)
                        {
                            foreach (var key in new[] { itemClassName, itemClassName.ToLowerInvariant() })
                            {
                                try
                                {
                                    var ic = getItemMethod.Invoke(dict, new object[] { key }) as ItemClass;
                                    if (ic != null)
                                    {
                                        var itemValue = InvokeGetItem(itemClassType, ic, itemClassName);
                                        if (itemValue != null)
                                            return itemValue;
                                    }
                                }
                                catch
                                {
                                    // Key not found, try next
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Fall through
                }
            }

            // 2) Get one ItemClass from list, then call instance GetItemClass(name, true) and GetItem(name, true)
            var listField = itemClassType.GetField("list", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (listField != null)
            {
                var list = listField.GetValue(null) as System.Array;
                if (list != null && list.Length > 0)
                {
                    var firstIc = list.GetValue(0) as ItemClass;
                    if (firstIc != null)
                    {
                        var getItemClassMethod = itemClassType.GetMethod("GetItemClass", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (getItemClassMethod != null)
                        {
                            var parameters = getItemClassMethod.GetParameters();
                            if (parameters.Length == 2)
                            {
                                try
                                {
                                    var itemClass = getItemClassMethod.Invoke(firstIc, new object[] { itemClassName, true }) as ItemClass;
                                    if (itemClass != null)
                                    {
                                        var itemValue = InvokeGetItem(itemClassType, itemClass, itemClassName);
                                        if (itemValue != null)
                                            return itemValue;
                                    }
                                }
                                catch
                                {
                                    // Fall through
                                }
                            }
                        }
                    }

                    // 3) Iterate list and match by name, then GetItem
                    for (int i = 0; i < list.Length; i++)
                    {
                        var ic = list.GetValue(i) as ItemClass;
                        if (ic == null) continue;
                        var name = GetItemClassName(ic);
                        if (string.IsNullOrEmpty(name))
                        {
                            var getNameMethod = itemClassType.GetMethod("GetItemName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                                name = getNameMethod.Invoke(ic, null) as string;
                        }
                        if (string.Equals(name, itemClassName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            var itemValue = InvokeGetItem(itemClassType, ic, itemClassName);
                            if (itemValue != null)
                                return itemValue;
                            break;
                        }
                    }
                }
            }

            return null;
        }

        private static ItemValue InvokeGetItem(System.Type itemClassType, ItemClass itemClass, string itemClassName)
        {
            if (itemClass == null) return null;
            var getItemMethod = itemClassType.GetMethod("GetItem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getItemMethod == null) return null;
            var getItemParams = getItemMethod.GetParameters();
            if (getItemParams.Length != 2) return null;
            try
            {
                return getItemMethod.Invoke(itemClass, new object[] { itemClassName, true }) as ItemValue;
            }
            catch
            {
                return null;
            }
        }

        internal static Inventory GetInventory(EntityAlive entity)
        {
            return GetMemberValue<Inventory>(entity, "inventory");
        }

        /// <summary>
        /// True when <paramref name="candidate"/> is the same object reference as an item stack slot on the player.
        /// Used to refuse loot auto-read if the engine accidentally aliases a chest slot with inventory (would corrupt hotbar).
        /// </summary>
        internal static bool ItemStackAliasesPlayerInventoryOrBag(EntityPlayer player, ItemStack candidate)
        {
            if (player == null || candidate == null)
            {
                return false;
            }

            try
            {
                var inv = GetInventory(player);
                if (inv != null)
                {
                    var slots = GetInventorySlots(inv);
                    if (slots != null)
                    {
                        for (var i = 0; i < slots.Length; i++)
                        {
                            if (ReferenceEquals(candidate, slots[i]))
                            {
                                return true;
                            }
                        }
                    }
                }

                var bagSlots = GetBagSlots(GetBag(player));
                if (bagSlots != null)
                {
                    for (var i = 0; i < bagSlots.Length; i++)
                    {
                        if (ReferenceEquals(candidate, bagSlots[i]))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // stub / reflection variance
            }

            return false;
        }

        /// <summary>
        /// Compact read-only state dump for in-game loot debugging. It avoids game writes and tolerates
        /// runtime API drift by using reflection for optional inventory / UI fields.
        /// </summary>
        internal static string FormatLootReadSnapshot(EntityPlayer player, ItemStack lootBacking, XUiC_ItemStack uiController = null)
        {
            try
            {
                var inv0 = TryGetInventorySlotStack(player, 0);
                var aliases = DescribePlayerStackAliases(player, lootBacking);
                var ui = DescribeItemStackController(uiController);
                return $"loot={DescribeItemStack(lootBacking)} | inv0={DescribeItemStack(inv0)} | aliases={aliases} | ui={ui}";
            }
            catch (Exception ex)
            {
                return $"snapshot-error={ex.GetType().Name}:{ex.Message}";
            }
        }

        private static string DescribePlayerStackAliases(EntityPlayer player, ItemStack candidate)
        {
            if (player == null || candidate == null)
            {
                return "none";
            }

            var hits = new List<string>();
            try
            {
                var inv = GetInventory(player);
                var invSlots = TryGetInventoryLikeSlots(inv);
                if (invSlots != null)
                {
                    for (var i = 0; i < invSlots.Length; i++)
                    {
                        if (ReferenceEquals(candidate, ExtractItemStackFromInventorySlot(invSlots.GetValue(i))))
                        {
                            hits.Add("inv[" + i + "]");
                        }
                    }
                }

                var bagSlots = GetBagSlots(GetBag(player));
                if (bagSlots != null)
                {
                    for (var i = 0; i < bagSlots.Length; i++)
                    {
                        if (ReferenceEquals(candidate, bagSlots[i]))
                        {
                            hits.Add("bag[" + i + "]");
                        }
                    }
                }
            }
            catch
            {
                hits.Add("alias-scan-error");
            }

            return hits.Count == 0 ? "none" : string.Join(",", hits.ToArray());
        }

        private static ItemStack TryGetInventorySlotStack(EntityPlayer player, int slotIndex)
        {
            if (player == null || slotIndex < 0)
            {
                return null;
            }

            try
            {
                var inv = GetInventory(player);
                if (inv == null)
                {
                    return null;
                }

                var direct = InvokeMethod(inv, "GetItemStack", slotIndex) as ItemStack;
                if (direct != null)
                {
                    return direct;
                }

                var slots = TryGetInventoryLikeSlots(inv);
                if (slots == null || slotIndex >= slots.Length)
                {
                    return null;
                }

                return ExtractItemStackFromInventorySlot(slots.GetValue(slotIndex));
            }
            catch
            {
                return null;
            }
        }

        private static System.Array TryGetInventoryLikeSlots(object inventory)
        {
            if (inventory == null)
            {
                return null;
            }

            var grid = GetMemberValue<object>(inventory, "ItemGrid")
                       ?? GetMemberValue<object>(inventory, "itemGrid");
            if (grid != null)
            {
                var items = GetMemberValue<ItemStack[]>(grid, "items");
                if (items != null)
                {
                    return items;
                }
            }

            var slots = InvokeMethod(inventory, "GetSlots") as System.Array;
            if (slots != null)
            {
                return slots;
            }

            return GetMemberValue<object>(inventory, "slots") as System.Array;
        }

        private static ItemStack ExtractItemStackFromInventorySlot(object slot)
        {
            if (slot == null)
            {
                return null;
            }

            if (slot is ItemStack stack)
            {
                return stack;
            }

            return GetMemberValue<ItemStack>(slot, "itemStack")
                ?? GetMemberValue<ItemStack>(slot, "ItemStack");
        }

        private static string DescribeItemStack(ItemStack stack)
        {
            if (stack == null)
            {
                return "null";
            }

            var itemValue = GetItemValue(stack);
            var itemClass = GetItemClass(itemValue);
            var name = GetItemClassName(itemClass);
            var type = GetItemType(itemValue);
            var count = GetItemCount(stack);
            var hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(stack);
            return $"{name}(type={type},count={count},ref={hash})";
        }

        private static string DescribeItemStackController(XUiC_ItemStack controller)
        {
            if (controller == null)
            {
                return "null";
            }

            var slotNumber = GetMemberValue<object>(controller, "SlotNumber") ?? "?";
            var stackLocation = GetMemberValue<object>(controller, "StackLocation") ?? "?";
            var stack = TryGetItemStackFromLootSlotController(controller);
            var hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(controller);
            return $"ctrl(ref={hash},slot={slotNumber},loc={stackLocation},stack={DescribeItemStack(stack)})";
        }

        internal static Bag GetBag(EntityAlive entity)
        {
            return GetMemberValue<Bag>(entity, "bag");
        }

        internal static ItemStack[] GetInventorySlots(Inventory inventory)
        {
            return inventory?.ItemGrid == null
                ? null
                : GetMemberValue<ItemStack[]>(inventory.ItemGrid, "items");
        }

        internal static ItemStack[] GetBagSlots(Bag bag)
        {
            return bag?.ItemGrid == null
                ? null
                : GetMemberValue<ItemStack[]>(bag.ItemGrid, "items");
        }

        internal static ItemStack GetBagItemStack(Bag bag, int slotIndex)
        {
            var slots = GetBagSlots(bag);
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            {
                return GetEmptyItemStack();
            }

            return slots[slotIndex];
        }

        internal static void SetBagItem(Bag bag, int slotIndex, ItemStack itemStack)
        {
            if (bag == null)
            {
                return;
            }

            bag.SetSlot(slotIndex, itemStack ?? GetEmptyItemStack());
        }

        internal static int GetItemCount(ItemStack itemStack)
        {
            if (itemStack == null)
            {
                return 0;
            }

            var itemStackType = itemStack.GetType();

            var field = itemStackType.GetField("count", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var value = field.GetValue(itemStack);
                if (value is int fieldCount)
                {
                    return fieldCount;
                }
            }

            var property = itemStackType.GetProperty("count", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                var value = property.GetValue(itemStack, null);
                if (value is int propertyCount)
                {
                    return propertyCount;
                }
            }

            return 0;
        }

        internal static ItemValue GetItemValue(ItemStack itemStack)
        {
            return GetMemberValue<ItemValue>(itemStack, "itemValue");
        }

        internal static ItemClass GetItemClass(ItemValue itemValue)
        {
            return GetMemberValue<ItemClass>(itemValue, "ItemClass");
        }

        internal static string GetItemClassName(ItemClass itemClass)
        {
            return GetMemberValue<string>(itemClass, "Name") ?? string.Empty;
        }

        /// <summary>
        /// Returns the sprite/icon name for the item (used by XUi atlases). Uses ItemClass.GetIconName() when available.
        /// </summary>
        internal static string GetItemIconName(ItemClass itemClass)
        {
            if (itemClass == null)
            {
                return string.Empty;
            }

            var method = itemClass.GetType().GetMethod("GetIconName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null && method.GetParameters().Length == 0)
            {
                try
                {
                    var result = method.Invoke(itemClass, null);
                    return result?.ToString() ?? string.Empty;
                }
                catch
                {
                    // Fall through to field/property fallback
                }
            }

            var customIcon = GetMemberValue<object>(itemClass, "CustomIcon");
            if (customIcon != null)
            {
                var str = customIcon.ToString();
                if (!string.IsNullOrEmpty(str))
                {
                    return str;
                }
            }

            var itemTypeIcon = GetMemberValue<string>(itemClass, "ItemTypeIcon");
            return itemTypeIcon ?? string.Empty;
        }

        internal static object GetItemTags(ItemClass itemClass)
        {
            return GetMemberValue<object>(itemClass, "ItemTags");
        }

        internal static System.Array GetItemActions(ItemClass itemClass)
        {
            return GetMemberValue<System.Array>(itemClass, "Actions");
        }

        internal static int GetItemType(ItemValue itemValue)
        {
            if (itemValue == null)
            {
                return 0;
            }

            var itemValueType = itemValue.GetType();

            var field = itemValueType.GetField("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var value = field.GetValue(itemValue);
                if (value is int fieldType)
                {
                    return fieldType;
                }
            }

            var property = itemValueType.GetProperty("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                var value = property.GetValue(itemValue, null);
                if (value is int propertyType)
                {
                    return propertyType;
                }
            }

            return 0;
        }

        internal static ItemStack GetEmptyItemStack()
        {
            // Do not reuse the game's static ItemStack.Empty sentinel in persisted containers.
            // Some game paths mutate/serialize the instance they are handed; a shared sentinel can
            // reappear later as a huge stack after save/load. A fresh empty stack is safer.
            return new ItemStack();
        }

        internal static GameManager GetGameManagerInstance()
        {
            var gameManagerType = typeof(GameManager);

            var field = gameManagerType.GetField("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var value = field.GetValue(null);
                if (value is GameManager fieldManager)
                {
                    return fieldManager;
                }
            }

            var property = gameManagerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                var value = property.GetValue(null, null);
                if (value is GameManager propertyManager)
                {
                    return propertyManager;
                }
            }

            return null;
        }

        /// <summary>
        /// Local player from <see cref="GameManager"/> (e.g. myEntityPlayerLocal). Item/perk logic often ties to this instance, not an arbitrary <see cref="EntityPlayer"/> from XUi.
        /// </summary>
        internal static EntityPlayer TryGetLocalEntityPlayer()
        {
            var gm = GetGameManagerInstance();
            if (gm == null)
            {
                return null;
            }

            object raw = GetMemberValue<object>(gm, "myEntityPlayerLocal");
            if (raw == null)
            {
                raw = GetMemberValue<object>(gm, "MyEntityPlayerLocal");
            }

            return raw as EntityPlayer;
        }

        /// <summary>
        /// <see cref="ItemActionEat.ExecuteInstantAction"/> calls <c>LocalPlayerUI.GetUIForPlayer(EntityPlayerLocal)</c>; passing only <see cref="EntityPlayer"/> can skip magazine/recipe UI paths.
        /// Uses reflection so net472 stub builds without a concrete <c>EntityPlayerLocal</c> type.
        /// </summary>
        internal static EntityAlive ResolveEntityPlayerLocalForInstantActions(EntityAlive entity)
        {
            if (entity == null)
            {
                return null;
            }

            var eplType = typeof(EntityPlayer).Assembly.GetType("EntityPlayerLocal");
            if (eplType != null && eplType.IsInstanceOfType(entity))
            {
                return entity;
            }

            var local = TryGetLocalEntityPlayer();
            if (local != null && eplType != null && eplType.IsInstanceOfType(local))
            {
                return local;
            }

            return entity;
        }

        /// <summary>
        /// Prefer <see cref="TryGetLocalEntityPlayer"/> for holding entity / min-events; fall back to the player from the current patch context.
        /// </summary>
        internal static EntityAlive ResolveEntityForItemEffects(EntityPlayer contextPlayer)
        {
            if (contextPlayer == null)
            {
                return null;
            }

            var local = TryGetLocalEntityPlayer();
            if (local != null)
            {
                return local;
            }

            return contextPlayer;
        }

        internal static ItemInventoryData CreateItemInventoryData(ItemClass itemClass, ItemStack itemStack, object gameManager, EntityAlive entity, int slotIdx, ItemValue itemValue)
        {
            var invData = TryInvokeCreateItemInventoryData(itemClass, itemStack, gameManager, entity, slotIdx)
                ?? TryCreateItemInventoryData(itemClass, itemStack, gameManager, entity, slotIdx)
                ?? FormatterServices.GetUninitializedObject(typeof(ItemInventoryData)) as ItemInventoryData;
            if (invData == null)
            {
                return null;
            }

            SetMemberValue(invData, "item", itemClass);
            SetMemberValue(invData, "itemStack", itemStack);
            SetMemberValue(invData, "holdingEntity", entity);
            SetMemberValue(invData, "slotIdx", slotIdx);
            SetMemberValue(invData, "gameManager", gameManager);
            TryPopulateItemInventoryDataWorldAndItemValue(invData, gameManager, entity, itemValue);
            return invData;
        }

        /// <summary>
        /// Vanilla consume/recipe unlock often consults <see cref="ItemInventoryData.world"/> and item value context.
        /// Factory-created data may already set these; reflection fallback often leaves them null.
        /// </summary>
        private static void TryPopulateItemInventoryDataWorldAndItemValue(
            ItemInventoryData invData,
            object gameManager,
            EntityAlive entity,
            ItemValue itemValue)
        {
            if (invData == null)
            {
                return;
            }

            try
            {
                object world = null;
                if (gameManager != null)
                {
                    var gmType = gameManager.GetType();
                    var worldProp = gmType.GetProperty("World", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (worldProp != null && worldProp.GetIndexParameters().Length == 0)
                    {
                        world = worldProp.GetValue(gameManager, null);
                    }

                    if (world == null)
                    {
                        world = GetMemberValue<object>(gameManager, "m_World");
                    }
                }

                if (world == null && entity != null)
                {
                    world = GetMemberValue<object>(entity, "world");
                }

                if (world != null)
                {
                    SetMemberValue(invData, "world", world);
                }

                if (itemValue != null)
                {
                    SetMemberValue(invData, "itemValue", itemValue);
                }
            }
            catch
            {
                // Best-effort only
            }
        }

        private static ItemInventoryData TryInvokeCreateItemInventoryData(ItemClass itemClass, ItemStack itemStack, object gameManager, EntityAlive entity, int slotIdx)
        {
            if (itemClass == null)
            {
                return null;
            }

            foreach (var methodName in new[] { "createItemInventoryData", "CreateInventoryData" })
            {
                var method = itemClass.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 4)
                {
                    continue;
                }

                if (!IsAssignable(parameters[0].ParameterType, itemStack) ||
                    !IsAssignable(parameters[1].ParameterType, gameManager) ||
                    !IsAssignable(parameters[2].ParameterType, entity) ||
                    !IsAssignable(parameters[3].ParameterType, slotIdx))
                {
                    continue;
                }

                try
                {
                    var result = method.Invoke(itemClass, new object[] { itemStack, gameManager, entity, slotIdx });
                    if (result is ItemInventoryData created)
                    {
                        return created;
                    }
                }
                catch
                {
                    // Some game builds throw internally here for container-derived items; fall back to manual creation.
                }
            }

            return null;
        }

        private static ItemInventoryData TryCreateItemInventoryData(ItemClass itemClass, ItemStack itemStack, object gameManager, EntityAlive entity, int slotIdx)
        {
            try
            {
                return System.Activator.CreateInstance(typeof(ItemInventoryData)) as ItemInventoryData;
            }
            catch
            {
                // Fall back to matching constructor shapes used by mocks or game assemblies.
            }

            var constructors = typeof(ItemInventoryData).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length == 5 &&
                    IsAssignable(parameters[0].ParameterType, itemClass) &&
                    IsAssignable(parameters[1].ParameterType, itemStack) &&
                    IsAssignable(parameters[2].ParameterType, gameManager) &&
                    IsAssignable(parameters[3].ParameterType, entity) &&
                    IsAssignable(parameters[4].ParameterType, slotIdx))
                {
                    return constructor.Invoke(new object[] { itemClass, itemStack, gameManager, entity, slotIdx }) as ItemInventoryData;
                }
            }

            return null;
        }

        private static bool IsAssignable(System.Type parameterType, object value)
        {
            if (value == null)
            {
                return !parameterType.IsValueType || System.Nullable.GetUnderlyingType(parameterType) != null;
            }

            return parameterType.IsInstanceOfType(value) ||
                (parameterType.IsPrimitive && value.GetType().IsPrimitive);
        }

        internal static void SetMemberValue(object instance, string memberName, object value)
        {
            if (instance == null || string.IsNullOrEmpty(memberName))
            {
                return;
            }

            var instanceType = instance.GetType();

            var field = instanceType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            var property = instanceType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, value, null);
            }
        }

        /// <summary>Multicast: tests may += Console; AutoReader static ctor += Debug.Log when DebugMode.</summary>
        public static System.Action<string> DiagnosticLine;

        /// <summary>For AutoReader hypothesis logs (same sink as reflection diagnostics).</summary>
        internal static void EmitHypothesisLine(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            AppendLootDebugLine(message);

            try
            {
                DiagnosticLine?.Invoke(message);
            }
            catch
            {
                // Never break read path
            }
        }

        private static void AppendLootDebugLine(string message)
        {
            try
            {
                var path = GetLootDebugFilePath();
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                lock (LootDebugFileLock)
                {
                    File.AppendAllText(
                        path,
                        DateTime.Now.ToString("o") + " " + message + Environment.NewLine);
                }
            }
            catch
            {
                // Diagnostics must never affect gameplay.
            }
        }

        internal static string GetLootDebugFilePath()
        {
            if (!string.IsNullOrEmpty(_lootDebugFilePath))
            {
                return _lootDebugFilePath;
            }

            try
            {
                var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
                if (!string.IsNullOrEmpty(userProfile))
                {
                    var dir = Path.Combine(userProfile, "AppData", "LocalLow", "The Fun Pimps", "7 Days To Die");
                    Directory.CreateDirectory(dir);
                    _lootDebugFilePath = Path.Combine(dir, "AutoReadMod_loot_debug.log");
                    return _lootDebugFilePath;
                }
            }
            catch
            {
                // fall through to temp
            }

            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "The Fun Pimps", "7 Days To Die");
                Directory.CreateDirectory(dir);
                _lootDebugFilePath = Path.Combine(dir, "AutoReadMod_loot_debug.log");
                return _lootDebugFilePath;
            }
            catch
            {
                return null;
            }
        }

        private static void EmitDiagnostic(string message)
        {
            EmitHypothesisLine(message);
        }

        private static string FormatArgTypes(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return "(none)";
            }

            var parts = new string[args.Length];
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == null)
                {
                    parts[i] = "null";
                }
                else
                {
                    parts[i] = args[i].GetType().Name;
                }
            }

            return string.Join(", ", parts);
        }

        internal static MinEventParams CreateMinEventParams(EntityAlive self, ItemValue itemValue, ItemInventoryData itemInventoryData)
        {
            var eventParams = System.Activator.CreateInstance(typeof(MinEventParams)) as MinEventParams;
            if (eventParams == null)
            {
                return null;
            }

            SetMemberValue(eventParams, "Self", self);
            SetMemberValue(eventParams, "Instigator", self);
            SetMemberValue(eventParams, "ItemValue", itemValue);
            SetMemberValue(eventParams, "ItemInventoryData", itemInventoryData);
            // Client-only progression from items.xml (e.g. AddProgressionLevel on onSelfPrimaryActionEnd) often skips when false.
            SetMemberValue(eventParams, "IsLocal", true);
            SetMemberValue(eventParams, "isLocal", true);
            return eventParams;
        }

        /// <summary>
        /// Resolves and invokes an instance method. Walks the inheritance chain with DeclaredOnly so methods declared on
        /// base types (e.g. <c>ItemAction.CreateModifierData</c> on a concrete <c>ItemActionEat</c>) are found. The previous
        /// implementation only used <c>GetMethods()</c> on the run-time type and could miss those methods depending on the runtime.
        /// </summary>
        internal static object InvokeMethod(object instance, string methodName, params object[] args)
        {
            if (instance == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            var method = FindInstanceMethod(instance, methodName, args);
            return method?.Invoke(instance, args);
        }

        /// <summary>True if a matching method was found and invoked. Use this when a void return cannot distinguish "missing" from "ran".</summary>
        internal static bool TryInvokeInstanceMethod(object instance, string methodName, out object returnValue, params object[] args)
        {
            returnValue = null;
            if (instance == null || string.IsNullOrEmpty(methodName))
            {
                return false;
            }

            var method = FindInstanceMethod(instance, methodName, args);
            if (method == null)
            {
                if (ShouldDeepDiagnoseMethod(methodName))
                {
                    EmitDiagnostic(
                        $"TryInvokeInstanceMethod MISS: {instance.GetType().Name}::{methodName} args=[{FormatArgTypes(args)}]");
                }

                return false;
            }

            returnValue = method.Invoke(instance, args);
            return true;
        }

        private static bool ShouldDeepDiagnoseMethod(string methodName)
        {
            return methodName == "CreateModifierData" || methodName == "consume" || methodName == "ExecuteInstantAction" ||
                   methodName == "Completed" || methodName == "ExecuteAction";
        }

        private static System.Reflection.MethodInfo FindInstanceMethod(object instance, string methodName, object[] args)
        {
            if (args == null)
            {
                return null;
            }

            var diagnose = ShouldDeepDiagnoseMethod(methodName);
            var rejectLog = new System.Text.StringBuilder();
            for (var t = instance.GetType(); t != null; t = t.BaseType)
            {
                var methods = t.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (var mi = 0; mi < methods.Length; mi++)
                {
                    var method = methods[mi];
                    if (method.Name != methodName)
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != args.Length)
                    {
                        if (diagnose && rejectLog.Length < 2000)
                        {
                            var sig = string.Join(", ", GetParameterTypeNames(parameters));
                            rejectLog.AppendLine($"  nameMatch arityMismatch: {t.Name}.{method.Name}({sig}) need arity {args.Length}");
                        }

                        continue;
                    }

                    var allAssignable = true;
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (!IsParameterCompatible(parameters[i].ParameterType, args[i]))
                        {
                            if (diagnose && rejectLog.Length < 2000)
                            {
                                rejectLog.AppendLine(
                                    $"  param mismatch: {t.Name}.{method.Name} param[{i}] want {parameters[i].ParameterType.Name} have {(args[i] == null ? "null" : args[i].GetType().Name)}");
                            }

                            allAssignable = false;
                        }
                    }

                    if (allAssignable)
                    {
                        if (diagnose)
                        {
                            var okSig = string.Join(", ", GetParameterTypeNames(parameters));
                            EmitDiagnostic($"FindInstanceMethod OK: {method.DeclaringType?.Name}::{method.Name}({okSig})");
                        }

                        return method;
                    }
                }
            }

            if (diagnose)
            {
                EmitDiagnostic(
                    $"FindInstanceMethod FAIL: {instance.GetType().Name}::{methodName} argTypes=[{FormatArgTypes(args)}] searched inheritance from {instance.GetType().Name}. \n" +
                    (rejectLog.Length > 0 ? "Candidates / rejects:\n" + rejectLog : "No same-name method found with matching arity."));
            }

            return null;
        }

        private static string[] GetParameterTypeNames(System.Reflection.ParameterInfo[] parameters)
        {
            var a = new string[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                a[i] = parameters[i].ParameterType.Name;
            }

            return a;
        }

        /// <summary>Like <see cref="IsAssignable"/> but coerces e.g. boxed <see cref="int"/> to the parameter type.</summary>
        private static bool IsParameterCompatible(System.Type parameterType, object value)
        {
            if (parameterType == null)
            {
                return false;
            }

            if (value == null)
            {
                return !parameterType.IsValueType || System.Nullable.GetUnderlyingType(parameterType) != null;
            }

            if (parameterType.IsInstanceOfType(value))
            {
                return true;
            }

            var valueType = value.GetType();
            if (parameterType.IsEnum && valueType == typeof(int))
            {
                return true;
            }

            if (parameterType.IsPrimitive && valueType.IsPrimitive)
            {
                try
                {
                    System.Convert.ChangeType(value, parameterType, System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    // Fall through
                }
            }

            return IsAssignable(parameterType, value);
        }

        internal static T GetMemberValue<T>(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrEmpty(memberName))
            {
                return default(T);
            }

            var instanceType = instance.GetType();

            var field = instanceType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return (T)field.GetValue(instance);
            }

            var property = instanceType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return (T)property.GetValue(instance, null);
            }

            return default(T);
        }

        /// <summary>
        /// Resolves the runtime <c>CraftingManager</c> across 7DTD builds: named fields differ, and many versions expose a static <c>Instance</c>.
        /// </summary>
        internal static object TryResolveCraftingManager(object gameManager, EntityPlayer player)
        {
            if (_cachedCraftingManagerForSession != null &&
                HasUnlockRecipeStringEntityPlayerMethod(_cachedCraftingManagerForSession))
            {
                return _cachedCraftingManagerForSession;
            }

            var namedGm = TryCraftingManagerFromNamedMemberHosts(gameManager);
            if (namedGm != null)
            {
                return CacheAndReturnCraftingManager(namedGm);
            }

            var world = GetMemberValue<object>(gameManager, "m_World")
                ?? GetMemberValue<object>(gameManager, "World");
            var namedWorld = TryCraftingManagerFromNamedMemberHosts(world);
            if (namedWorld != null)
            {
                return CacheAndReturnCraftingManager(namedWorld);
            }

            var singleton = TryGetCraftingManagerStaticSingleton(gameManager);
            if (singleton != null)
            {
                return CacheAndReturnCraftingManager(singleton);
            }

            var scanGm = TryFindCraftingManagerByMemberTypeScan(gameManager);
            if (scanGm != null)
            {
                return CacheAndReturnCraftingManager(scanGm);
            }

            var scanWorld = TryFindCraftingManagerByMemberTypeScan(world);
            if (scanWorld != null)
            {
                return CacheAndReturnCraftingManager(scanWorld);
            }

            if (player != null)
            {
                var scanPlayer = TryFindCraftingManagerByMemberTypeScan(player);
                if (scanPlayer != null)
                {
                    return CacheAndReturnCraftingManager(scanPlayer);
                }
            }

            EntityPlayer localPlayer = player;
            if (localPlayer == null || localPlayer.GetType().Name != "EntityPlayerLocal")
            {
                localPlayer = GetMemberValue<EntityPlayer>(gameManager, "myEntityPlayerLocal");
            }

            if (localPlayer != null && localPlayer.GetType().Name == "EntityPlayerLocal")
            {
                var playerUi = GetMemberValue<object>(localPlayer, "playerUI") ?? GetMemberValue<object>(localPlayer, "PlayerUI");
                if (playerUi != null)
                {
                    var xui = GetMemberValue<object>(playerUi, "xui")
                        ?? GetMemberValue<object>(playerUi, "mXUi")
                        ?? GetMemberValue<object>(playerUi, "Xui");
                    var scanXui = TryFindCraftingManagerByMemberTypeScan(xui);
                    if (scanXui != null)
                    {
                        return CacheAndReturnCraftingManager(scanXui);
                    }
                }
            }

            var bfs = TryFindCraftingManagerByUnlockRecipeBfs(gameManager, world, player);
            if (bfs != null)
            {
                return CacheAndReturnCraftingManager(bfs);
            }

            return null;
        }

        private static object CacheAndReturnCraftingManager(object resolved)
        {
            if (resolved != null)
            {
                _cachedCraftingManagerForSession = resolved;
            }

            return resolved;
        }

        private static object TryCraftingManagerFromNamedMemberHosts(object host)
        {
            if (host == null)
            {
                return null;
            }

            foreach (var key in new[] { "craftingManager", "CraftingManager", "mCraftingManager" })
            {
                var cm = GetMemberValue<object>(host, key);
                if (IsCraftingManagerLikeObject(cm))
                {
                    return cm;
                }
            }

            return null;
        }

        private static bool IsCraftingManagerInstance(object cm)
        {
            if (cm == null)
            {
                return false;
            }

            for (var t = cm.GetType(); t != null; t = t.BaseType)
            {
                if (t.Name == "CraftingManager")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Some builds rename or wrap the manager; accept any instance that exposes vanilla-style recipe unlock.</summary>
        private static bool IsCraftingManagerLikeObject(object v)
        {
            return IsCraftingManagerInstance(v) || HasUnlockRecipeStringEntityPlayerMethod(v);
        }

        private static bool HasUnlockRecipeStringEntityPlayerMethod(object instance)
        {
            if (instance == null)
            {
                return false;
            }

            try
            {
                foreach (var m in instance.GetType().GetMethods(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.Name != "UnlockRecipe" || m.IsGenericMethodDefinition)
                    {
                        continue;
                    }

                    var ps = m.GetParameters();
                    if (ps.Length != 2 || ps[0].ParameterType != typeof(string))
                    {
                        continue;
                    }

                    var p1 = ps[1].ParameterType;
                    if (p1.IsAssignableFrom(typeof(EntityPlayer)))
                    {
                        return true;
                    }

                    if (p1.Name == "EntityPlayerLocal")
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        private static object TryGetCraftingManagerStaticSingleton(object gameManager)
        {
            try
            {
                var cmType = TryFindCraftingManagerRuntimeType(gameManager);
                if (cmType == null)
                {
                    return null;
                }

                return TryExtractCraftingManagerSingleton(cmType);
            }
            catch
            {
                return null;
            }
        }

        private static System.Type TryFindCraftingManagerRuntimeType(object gameManager)
        {
            System.Type t = null;
            try
            {
                t = typeof(EntityPlayer).Assembly.GetType("CraftingManager", false, false);
            }
            catch
            {
                // ignore
            }

            if (t != null)
            {
                return t;
            }

            if (gameManager != null)
            {
                try
                {
                    t = gameManager.GetType().Assembly.GetType("CraftingManager", false, false);
                }
                catch
                {
                    // ignore
                }
            }

            if (t != null)
            {
                return t;
            }

            return TryFindCraftingManagerTypeInAssemblyCSharp();
        }

        private static System.Type TryFindCraftingManagerTypeInAssemblyCSharp()
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic)
                {
                    continue;
                }

                var asmName = asm.GetName().Name;
                if (!string.Equals(asmName, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var t = asm.GetType("CraftingManager", false, false);
                    if (t != null)
                    {
                        return t;
                    }
                }
                catch
                {
                    // ignore
                }

                System.Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    if (ex.Types == null)
                    {
                        continue;
                    }

                    foreach (var t in ex.Types)
                    {
                        if (t != null && t.Name == "CraftingManager")
                        {
                            return t;
                        }
                    }

                    continue;
                }
                catch
                {
                    continue;
                }

                for (var i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t != null && t.Name == "CraftingManager")
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        private static object TryExtractCraftingManagerSingleton(System.Type cmType)
        {
            if (cmType == null)
            {
                return null;
            }

            try
            {
                const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                foreach (var p in cmType.GetProperties(bf))
                {
                    if (!cmType.IsAssignableFrom(p.PropertyType))
                    {
                        continue;
                    }

                    if (p.Name != "Instance" && p.Name != "instance")
                    {
                        continue;
                    }

                    var v = p.GetValue(null, null);
                    if (v != null)
                    {
                        return v;
                    }
                }

                foreach (var f in cmType.GetFields(bf))
                {
                    if (!cmType.IsAssignableFrom(f.FieldType))
                    {
                        continue;
                    }

                    if (f.Name != "Instance" && f.Name != "instance" && f.Name != "m_Instance" && f.Name != "singleton")
                    {
                        continue;
                    }

                    var v = f.GetValue(null);
                    if (v != null)
                    {
                        return v;
                    }
                }

                foreach (var p in cmType.GetProperties(bf))
                {
                    if (!cmType.IsAssignableFrom(p.PropertyType))
                    {
                        continue;
                    }

                    var v = p.GetValue(null, null);
                    if (v != null)
                    {
                        return v;
                    }
                }

                foreach (var f in cmType.GetFields(bf))
                {
                    if (!cmType.IsAssignableFrom(f.FieldType))
                    {
                        continue;
                    }

                    var v = f.GetValue(null);
                    if (v != null)
                    {
                        return v;
                    }
                }
            }
            catch
            {
                // Reflection failures ignored
            }

            return null;
        }

        private static object TryFindCraftingManagerByUnlockRecipeBfs(object gameManager, object world, EntityPlayer player)
        {
            var seen = new HashSet<object>(new ReferenceIdentityEqualityComparer());
            var queue = new Queue<(object o, int d)>();

            void TryEnqueue(object o, int depth)
            {
                if (o == null || depth > 2)
                {
                    return;
                }

                if (!seen.Add(o))
                {
                    return;
                }

                queue.Enqueue((o, depth));
            }

            TryEnqueue(gameManager, 0);
            TryEnqueue(world, 0);
            TryEnqueue(player, 0);

            var visits = 0;
            while (queue.Count > 0 && visits++ < 400)
            {
                var (o, d) = queue.Dequeue();
                if (HasUnlockRecipeStringEntityPlayerMethod(o))
                {
                    return o;
                }

                if (d >= 2)
                {
                    continue;
                }

                AddReflectionChildrenForCraftingManagerSearch(o, d + 1, seen, queue);
            }

            return null;
        }

        private static void AddReflectionChildrenForCraftingManagerSearch(
            object host,
            int nextDepth,
            HashSet<object> seen,
            Queue<(object o, int d)> queue)
        {
            if (host == null)
            {
                return;
            }

            var hostType = host.GetType();
            if (hostType.IsPrimitive || hostType == typeof(string))
            {
                return;
            }

            if (hostType.FullName != null &&
                hostType.FullName.StartsWith("UnityEngine.", StringComparison.Ordinal))
            {
                return;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var isHeavyWorld = hostType.Name == "World" || hostType.Name == "WorldBase";
            var n = 0;
            foreach (var f in hostType.GetFields(flags))
            {
                if (++n > (isHeavyWorld ? 55 : 130))
                {
                    break;
                }

                var ft = f.FieldType;
                if (ft.IsPrimitive || ft == typeof(string) || ft.IsEnum)
                {
                    continue;
                }

                if (ft.FullName != null && ft.FullName.StartsWith("UnityEngine.", StringComparison.Ordinal))
                {
                    continue;
                }

                object v;
                try
                {
                    v = f.GetValue(host);
                }
                catch
                {
                    continue;
                }

                if (v == null)
                {
                    continue;
                }

                if (ft.IsArray)
                {
                    var el = ft.GetElementType();
                    if (el == null || el.IsPrimitive || el == typeof(string))
                    {
                        continue;
                    }

                    var arr = v as Array;
                    if (arr == null)
                    {
                        continue;
                    }

                    // MD arrays (e.g. World coll caches) throw from GetValue(int); skip — not hosting CraftingManager.
                    if (arr.Rank != 1)
                    {
                        continue;
                    }

                    var limit = System.Math.Min(arr.Length, 6);
                    for (var i = 0; i < limit; i++)
                    {
                        object arrayElement;
                        try
                        {
                            arrayElement = arr.GetValue(i);
                        }
                        catch
                        {
                            continue;
                        }

                        TryEnqueueCraftingSearchChild(arrayElement, nextDepth, seen, queue);
                    }

                    continue;
                }

                if (!ft.IsClass)
                {
                    continue;
                }

                if (isHeavyWorld)
                {
                    var fn = f.Name;
                    var tname = ft.Name;
                    if (fn.IndexOf("craft", StringComparison.OrdinalIgnoreCase) < 0 &&
                        fn.IndexOf("Recipe", StringComparison.OrdinalIgnoreCase) < 0 &&
                        tname.IndexOf("Craft", StringComparison.OrdinalIgnoreCase) < 0 &&
                        tname.IndexOf("Recipe", StringComparison.OrdinalIgnoreCase) < 0 &&
                        !tname.EndsWith("Manager", StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                TryEnqueueCraftingSearchChild(v, nextDepth, seen, queue);
            }
        }

        private static void TryEnqueueCraftingSearchChild(
            object child,
            int nextDepth,
            HashSet<object> seen,
            Queue<(object o, int d)> queue)
        {
            if (child == null)
            {
                return;
            }

            var t = child.GetType();
            if (t.IsPrimitive || t == typeof(string))
            {
                return;
            }

            if (t.FullName != null && t.FullName.StartsWith("UnityEngine.", StringComparison.Ordinal))
            {
                return;
            }

            if (!seen.Add(child))
            {
                return;
            }

            queue.Enqueue((child, nextDepth));
        }

        private static object TryFindCraftingManagerByMemberTypeScan(object host)
        {
            if (host == null)
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            try
            {
                for (var t = host.GetType(); t != null; t = t.BaseType)
                {
                    foreach (var f in t.GetFields(flags))
                    {
                        if (!LooksLikeCraftingManagerReferenceType(f.FieldType))
                        {
                            continue;
                        }

                        object v;
                        try
                        {
                            v = f.GetValue(host);
                        }
                        catch
                        {
                            continue;
                        }

                        if (IsCraftingManagerLikeObject(v))
                        {
                            return v;
                        }
                    }

                    foreach (var p in t.GetProperties(flags))
                    {
                        if (!p.CanRead || p.GetIndexParameters().Length != 0)
                        {
                            continue;
                        }

                        if (!LooksLikeCraftingManagerReferenceType(p.PropertyType))
                        {
                            continue;
                        }

                        object v;
                        try
                        {
                            v = p.GetValue(host, null);
                        }
                        catch
                        {
                            continue;
                        }

                        if (IsCraftingManagerLikeObject(v))
                        {
                            return v;
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static bool LooksLikeCraftingManagerReferenceType(System.Type memberType)
        {
            if (memberType == null || !memberType.IsClass)
            {
                return false;
            }

            var n = memberType.Name;
            return n == "CraftingManager" || n.IndexOf("CraftingManager", StringComparison.Ordinal) >= 0;
        }
    }
}
