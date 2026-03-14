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
