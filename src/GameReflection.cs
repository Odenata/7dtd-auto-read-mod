using System.Reflection;
using System.Runtime.Serialization;

namespace AutoReadMod
{
    internal static class GameReflection
    {
        private static PropertyInfo _xUiItemStackItemStackProperty;

        internal static EntityPlayer GetPlayerFromLootWindowGroup(XUiC_LootWindowGroup lootWindowGroup)
        {
            var xui = GetMemberValue<object>(lootWindowGroup, "xui");
            var playerUi = GetMemberValue<object>(xui, "playerUI");
            return GetMemberValue<EntityPlayer>(playerUi, "entityPlayer");
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
                object lpUi = GetMemberValue<object>(player, "PlayerUI");
                if (lpUi == null)
                {
                    lpUi = GetMemberValue<object>(player, "playerUI");
                }

                if (lpUi == null)
                {
                    return null;
                }

                var grids = GetMemberValue<System.Collections.IList>(lpUi, "activeItemStackGrids");
                if (grids == null)
                {
                    return null;
                }

                _ = readFromBag;

                // Backpack first: bundle-added items typically appear in non-toolbelt inventory slots.
                foreach (var gridTypeName in new[] { "XUiC_Backpack", "XUiC_Toolbelt" })
                {
                    for (var gi = 0; gi < grids.Count; gi++)
                    {
                        var grid = grids[gi];
                        if (grid == null || grid.GetType().Name != gridTypeName)
                        {
                            continue;
                        }

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

        internal static Bag GetBag(EntityAlive entity)
        {
            return GetMemberValue<Bag>(entity, "bag");
        }

        internal static ItemStack[] GetBagSlots(Bag bag)
        {
            return bag == null
                ? null
                : InvokeMethod(bag, "GetSlots") as ItemStack[];
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

            InvokeMethod(bag, "SetSlot", slotIndex, itemStack, true);
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

            try
            {
                DiagnosticLine?.Invoke(message);
            }
            catch
            {
                // Never break read path
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
    }
}
