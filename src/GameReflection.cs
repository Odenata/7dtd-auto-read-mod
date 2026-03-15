using System.Reflection;
using System.Runtime.Serialization;

namespace AutoReadMod
{
    internal static class GameReflection
    {
        internal static EntityPlayer GetPlayerFromLootWindowGroup(XUiC_LootWindowGroup lootWindowGroup)
        {
            var xui = GetMemberValue<object>(lootWindowGroup, "xui");
            var playerUi = GetMemberValue<object>(xui, "playerUI");
            return GetMemberValue<EntityPlayer>(playerUi, "entityPlayer");
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
            var itemStackType = typeof(ItemStack);

            var field = itemStackType.GetField("Empty", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var value = field.GetValue(null);
                if (value is ItemStack fieldStack)
                {
                    return fieldStack;
                }
            }

            var property = itemStackType.GetProperty("Empty", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                var value = property.GetValue(null, null);
                if (value is ItemStack propertyStack)
                {
                    return propertyStack;
                }
            }

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
            return invData;
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
            return eventParams;
        }

        internal static object InvokeMethod(object instance, string methodName, params object[] args)
        {
            if (instance == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            var instanceType = instance.GetType();
            var methods = instanceType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var method in methods)
            {
                if (method.Name != methodName)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                bool allAssignable = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (!IsAssignable(parameters[i].ParameterType, args[i]))
                    {
                        allAssignable = false;
                        break;
                    }
                }

                if (!allAssignable)
                {
                    continue;
                }

                return method.Invoke(instance, args);
            }

            return null;
        }

        private static T GetMemberValue<T>(object instance, string memberName)
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
