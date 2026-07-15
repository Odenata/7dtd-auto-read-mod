using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace AutoReadMod
{
    /// <summary>
    /// When World <c>ReadOnInventoryAdd</c> is on, auto-read readables as they enter the local
    /// player's toolbelt/backpack (loot take, pickup, quest/trader, bundles).
    /// </summary>
    internal static class InventoryAddPatchShared
    {
        private static int _ingressDepth;
        private static int _debounceFrame = int.MinValue;
        private static readonly HashSet<string> _debouncedKeys = new HashSet<string>(StringComparer.Ordinal);

        internal static bool IsInventoryAddModeActive()
        {
            GearsModApi.SyncWorldSettingsIfLoaded();
            return ModConfig.Instance.Enabled && ModConfig.Instance.ReadOnInventoryAdd;
        }

        internal static bool TryEnterIngress()
        {
            if (Interlocked.Increment(ref _ingressDepth) == 1)
            {
                return true;
            }

            Interlocked.Decrement(ref _ingressDepth);
            return false;
        }

        internal static void ExitIngress()
        {
            Interlocked.Decrement(ref _ingressDepth);
        }

        /// <summary>Same-frame debounce across XUiM and Bag/Inventory dual hooks.</summary>
        internal static bool TryMarkDebounced(string itemClassName, int slot, bool fromBag, int count)
        {
            int frame = TryGetFrameCount();
            if (frame != _debounceFrame)
            {
                _debounceFrame = frame;
                _debouncedKeys.Clear();
            }

            var key = $"{itemClassName}|{(fromBag ? "b" : "i")}|{slot}|{count}";
            return _debouncedKeys.Add(key);
        }

        /// <summary>Test seam: clear debounce between cases.</summary>
        internal static void ResetDebounceForTest()
        {
            _debounceFrame = int.MinValue;
            _debouncedKeys.Clear();
            Interlocked.Exchange(ref _ingressDepth, 0);
        }

        private static int TryGetFrameCount()
        {
            try
            {
                return Time.frameCount;
            }
            catch
            {
                return Environment.TickCount;
            }
        }

        internal static bool IsLocalPlayer(EntityPlayer player)
        {
            if (player == null)
            {
                return false;
            }

            var local = GameReflection.TryGetLocalEntityPlayer();
            if (local != null)
            {
                return ReferenceEquals(local, player);
            }

            // Hermetic / early boot: GameManager local not resolved yet — allow.
            return true;
        }

        internal static bool IsLocalPlayerBag(Bag bag)
        {
            if (bag == null)
            {
                return false;
            }

            var local = GameReflection.TryGetLocalEntityPlayer();
            if (local == null)
            {
                return true;
            }

            return ReferenceEquals(GameReflection.GetBag(local), bag);
        }

        internal static bool IsLocalPlayerInventory(Inventory inventory)
        {
            if (inventory == null)
            {
                return false;
            }

            var local = GameReflection.TryGetLocalEntityPlayer();
            if (local == null)
            {
                return true;
            }

            return ReferenceEquals(GameReflection.GetInventory(local), inventory);
        }

        /// <summary>
        /// Resolve player from <c>XUiM_PlayerInventory</c> without a compile-time dependency on that type
        /// (hermetic mocks may omit it; game DLL has it).
        /// </summary>
        internal static EntityPlayer ResolvePlayerFromXuiM(object playerInventory)
        {
            if (playerInventory == null)
            {
                return null;
            }

            try
            {
                var localProp = playerInventory.GetType().GetProperty(
                    "localPlayer",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (localProp != null)
                {
                    var local = localProp.GetValue(playerInventory, null) as EntityPlayer;
                    if (local != null)
                    {
                        return local;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return GameReflection.TryGetLocalEntityPlayer();
        }

        internal static int ComputeAcceptedCount(ItemStack stack, int countBefore)
        {
            if (stack == null)
            {
                return Math.Max(0, countBefore);
            }

            int remaining = GameReflection.GetItemCount(stack);
            int accepted = countBefore - remaining;
            if (accepted > 0)
            {
                return accepted;
            }

            // Fully transferred without mutating the argument, or count unchanged after success.
            return countBefore > 0 ? countBefore : remaining;
        }

        internal static void TryProcessSuccessfulAdd(
            EntityPlayer player,
            ItemStack stack,
            int countBefore,
            int preferredSlot = -1,
            bool preferBag = false)
        {
            if (!IsInventoryAddModeActive() || !IsLocalPlayer(player) || stack == null)
            {
                return;
            }

            try
            {
                int accepted = ComputeAcceptedCount(stack, countBefore);
                if (accepted <= 0 && (stack.IsEmpty() || GameReflection.GetItemCount(stack) <= 0))
                {
                    // Fully moved; hint emptied — still try preferred slot / class from emptied stack's ItemValue
                    accepted = countBefore > 0 ? countBefore : 1;
                }

                if (accepted <= 0)
                {
                    return;
                }

                AutoReader.TryAutoReadInventoryIngress(player, stack, preferredSlot, accepted, preferBag);
            }
            catch (Exception ex)
            {
                try
                {
                    UnityEngine.Debug.LogWarning("[AutoReadMod] Inventory ingress failed: " + ex.Message);
                }
                catch
                {
                    // ignore
                }
            }
        }

        internal static Type TryGetXUiMPlayerInventoryType()
        {
            return AccessTools.TypeByName("XUiM_PlayerInventory")
                   ?? typeof(EntityPlayer).Assembly.GetType("XUiM_PlayerInventory");
        }

        /// <summary>True when the slot UI is the local player's backpack or toolbelt (not loot/workstation).</summary>
        internal static bool TryGetLocalPlayerSlotContext(
            XUiC_ItemStack slotController,
            out EntityPlayer player,
            out int slotNumber,
            out bool preferBag)
        {
            player = null;
            slotNumber = -1;
            preferBag = false;
            if (slotController == null)
            {
                return false;
            }

            try
            {
                var locationObj = GameReflection.GetMemberValue<object>(slotController, "StackLocation");
                var locationName = locationObj != null ? locationObj.ToString() : string.Empty;
                if (string.Equals(locationName, "Backpack", StringComparison.OrdinalIgnoreCase))
                {
                    preferBag = true;
                }
                else if (string.Equals(locationName, "ToolBelt", StringComparison.OrdinalIgnoreCase))
                {
                    preferBag = false;
                }
                else
                {
                    return false;
                }

                slotNumber = GameReflection.GetMemberValue<int>(slotController, "SlotNumber");
                player = GameReflection.TryGetLocalEntityPlayer();
                return player != null && IsLocalPlayer(player);
            }
            catch
            {
                return false;
            }
        }

        internal static void CaptureSlotStackState(XUiC_ItemStack slotController, out string itemKey, out int count)
        {
            itemKey = string.Empty;
            count = 0;
            if (slotController == null)
            {
                return;
            }

            try
            {
                var stack = GameReflection.GetMemberValue<ItemStack>(slotController, "ItemStack")
                            ?? GameReflection.TryGetItemStackFromLootSlotController(slotController);
                if (stack == null || stack.IsEmpty())
                {
                    return;
                }

                itemKey = AutoReader.GetItemKey(stack) ?? string.Empty;
                count = GameReflection.GetItemCount(stack);
            }
            catch
            {
                // ignore
            }
        }

        internal static void TryProcessPlayerSlotAfterMove(
            XUiC_ItemStack slotController,
            string beforeKey,
            int beforeCount)
        {
            if (!IsInventoryAddModeActive() || slotController == null)
            {
                return;
            }

            if (!TryGetLocalPlayerSlotContext(slotController, out var player, out var slotNumber, out var preferBag))
            {
                return;
            }

            ItemStack stack;
            try
            {
                stack = GameReflection.GetMemberValue<ItemStack>(slotController, "ItemStack")
                        ?? GameReflection.TryGetItemStackFromLootSlotController(slotController);
            }
            catch
            {
                return;
            }

            if (stack == null || stack.IsEmpty() || !AutoReader.ShouldAutoReadItem(stack))
            {
                return;
            }

            var afterKey = AutoReader.GetItemKey(stack) ?? string.Empty;
            int afterCount = GameReflection.GetItemCount(stack);
            int accepted;
            if (!string.IsNullOrEmpty(beforeKey) &&
                string.Equals(beforeKey, afterKey, StringComparison.OrdinalIgnoreCase))
            {
                accepted = afterCount - beforeCount;
            }
            else
            {
                // New readable replaced whatever was there (or empty).
                accepted = afterCount;
            }

            if (accepted <= 0)
            {
                return;
            }

            try
            {
                AutoReader.TryAutoReadInventoryIngress(player, stack, slotNumber, accepted, preferBag);
            }
            catch (Exception ex)
            {
                try
                {
                    UnityEngine.Debug.LogWarning("[AutoReadMod] Slot-move inventory ingress failed: " + ex.Message);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }

    /// <summary>Drag-drop onto backpack/toolbelt does not call AddItem — hook stack swap instead.</summary>
    [HarmonyPatch]
    [HarmonyWrapSafe]
    public class ItemStackHandleStackSwapIngressPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(XUiC_ItemStack), "HandleStackSwap");
        }

        static void Prefix(XUiC_ItemStack __instance, ref string __state)
        {
            __state = null;
            if (!InventoryAddPatchShared.IsInventoryAddModeActive())
            {
                return;
            }

            InventoryAddPatchShared.CaptureSlotStackState(__instance, out var key, out var count);
            __state = key + "\n" + count.ToString();
        }

        static void Postfix(XUiC_ItemStack __instance, string __state)
        {
            if (string.IsNullOrEmpty(__state))
            {
                return;
            }

            var parts = __state.Split('\n');
            var beforeKey = parts.Length > 0 ? parts[0] : string.Empty;
            int beforeCount = 0;
            if (parts.Length > 1)
            {
                int.TryParse(parts[1], out beforeCount);
            }

            InventoryAddPatchShared.TryProcessPlayerSlotAfterMove(__instance, beforeKey, beforeCount);
        }
    }

    [HarmonyPatch]
    [HarmonyWrapSafe]
    public class ItemStackSwapItemIngressPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(XUiC_ItemStack), "SwapItem");
        }

        static void Prefix(XUiC_ItemStack __instance, ref string __state)
        {
            __state = null;
            if (!InventoryAddPatchShared.IsInventoryAddModeActive())
            {
                return;
            }

            InventoryAddPatchShared.CaptureSlotStackState(__instance, out var key, out var count);
            __state = key + "\n" + count.ToString();
        }

        static void Postfix(XUiC_ItemStack __instance, string __state)
        {
            if (string.IsNullOrEmpty(__state))
            {
                return;
            }

            var parts = __state.Split('\n');
            var beforeKey = parts.Length > 0 ? parts[0] : string.Empty;
            int beforeCount = 0;
            if (parts.Length > 1)
            {
                int.TryParse(parts[1], out beforeCount);
            }

            InventoryAddPatchShared.TryProcessPlayerSlotAfterMove(__instance, beforeKey, beforeCount);
        }
    }

    /// <summary>Shared TargetMethod helpers for optional <c>XUiM_PlayerInventory</c> (absent from some hermetic mocks).</summary>
    internal static class XUiMPlayerInventoryPatchTargets
    {
        internal static MethodBase AddItem()
        {
            var t = InventoryAddPatchShared.TryGetXUiMPlayerInventoryType();
            return t == null ? null : AccessTools.Method(t, "AddItem", new[] { typeof(ItemStack) });
        }

        internal static MethodBase AddItemWithSound()
        {
            var t = InventoryAddPatchShared.TryGetXUiMPlayerInventoryType();
            return t == null ? null : AccessTools.Method(t, "AddItem", new[] { typeof(ItemStack), typeof(bool) });
        }

        internal static MethodBase AddItems()
        {
            var t = InventoryAddPatchShared.TryGetXUiMPlayerInventoryType();
            return t == null ? null : AccessTools.Method(t, "AddItems", new[] { typeof(ItemStack[]) });
        }

        internal static MethodBase AddItemToBackpack()
        {
            var t = InventoryAddPatchShared.TryGetXUiMPlayerInventoryType();
            return t == null ? null : AccessTools.Method(t, "AddItemToBackpack", new[] { typeof(ItemStack) });
        }

        internal static MethodBase AddItemToToolbelt()
        {
            var t = InventoryAddPatchShared.TryGetXUiMPlayerInventoryType();
            return t == null ? null : AccessTools.Method(t, "AddItemToToolbelt", new[] { typeof(ItemStack) });
        }

        internal static MethodBase AddItemNoPartial()
        {
            var t = InventoryAddPatchShared.TryGetXUiMPlayerInventoryType();
            return t == null ? null : AccessTools.Method(t, "AddItemNoPartial", new[] { typeof(ItemStack), typeof(bool) });
        }
    }

    [HarmonyPatch]
    [HarmonyWrapSafe]
    public class XUiMPlayerInventoryAddItemPatch
    {
        static MethodBase TargetMethod() => XUiMPlayerInventoryPatchTargets.AddItem();

        static void Prefix(ItemStack _itemStack, ref int __state)
        {
            __state = _itemStack != null ? GameReflection.GetItemCount(_itemStack) : 0;
        }

        static void Postfix(object __instance, ItemStack _itemStack, bool __result, int __state)
        {
            if (!__result || !InventoryAddPatchShared.IsInventoryAddModeActive())
            {
                return;
            }

            var player = InventoryAddPatchShared.ResolvePlayerFromXuiM(__instance);
            InventoryAddPatchShared.TryProcessSuccessfulAdd(player, _itemStack, __state);
        }
    }

    [HarmonyPatch]
    [HarmonyWrapSafe]
    public class XUiMPlayerInventoryAddItemWithSoundPatch
    {
        static MethodBase TargetMethod() => XUiMPlayerInventoryPatchTargets.AddItemWithSound();

        static void Prefix(ItemStack _itemStack, ref int __state)
        {
            __state = _itemStack != null ? GameReflection.GetItemCount(_itemStack) : 0;
        }

        static void Postfix(object __instance, ItemStack _itemStack, bool __result, int __state)
        {
            if (!__result || !InventoryAddPatchShared.IsInventoryAddModeActive())
            {
                return;
            }

            var player = InventoryAddPatchShared.ResolvePlayerFromXuiM(__instance);
            InventoryAddPatchShared.TryProcessSuccessfulAdd(player, _itemStack, __state);
        }
    }

    [HarmonyPatch]
    [HarmonyWrapSafe]
    public class XUiMPlayerInventoryAddItemsPatch
    {
        static MethodBase TargetMethod() => XUiMPlayerInventoryPatchTargets.AddItems();

        static void Prefix(ItemStack[] _itemStacks, ref int[] __state)
        {
            if (_itemStacks == null)
            {
                __state = null;
                return;
            }

            __state = new int[_itemStacks.Length];
            for (int i = 0; i < _itemStacks.Length; i++)
            {
                __state[i] = _itemStacks[i] != null ? GameReflection.GetItemCount(_itemStacks[i]) : 0;
            }
        }

        static void Postfix(object __instance, ItemStack[] _itemStacks, bool __result, int[] __state)
        {
            if (!__result || !InventoryAddPatchShared.IsInventoryAddModeActive() || _itemStacks == null)
            {
                return;
            }

            var player = InventoryAddPatchShared.ResolvePlayerFromXuiM(__instance);
            for (int i = 0; i < _itemStacks.Length; i++)
            {
                int before = __state != null && i < __state.Length ? __state[i] : 0;
                InventoryAddPatchShared.TryProcessSuccessfulAdd(player, _itemStacks[i], before);
            }
        }
    }

    [HarmonyPatch]
    [HarmonyWrapSafe]
    public class XUiMPlayerInventoryAddItemToBackpackPatch
    {
        static MethodBase TargetMethod() => XUiMPlayerInventoryPatchTargets.AddItemToBackpack();

        static void Prefix(ItemStack _itemStack, ref int __state)
        {
            __state = _itemStack != null ? GameReflection.GetItemCount(_itemStack) : 0;
        }

        static void Postfix(object __instance, ItemStack _itemStack, bool __result, int __state)
        {
            if (!__result || !InventoryAddPatchShared.IsInventoryAddModeActive())
            {
                return;
            }

            var player = InventoryAddPatchShared.ResolvePlayerFromXuiM(__instance);
            InventoryAddPatchShared.TryProcessSuccessfulAdd(player, _itemStack, __state, preferBag: true);
        }
    }

    [HarmonyPatch]
    [HarmonyWrapSafe]
    public class XUiMPlayerInventoryAddItemToToolbeltPatch
    {
        static MethodBase TargetMethod() => XUiMPlayerInventoryPatchTargets.AddItemToToolbelt();

        static void Prefix(ItemStack _itemStack, ref int __state)
        {
            __state = _itemStack != null ? GameReflection.GetItemCount(_itemStack) : 0;
        }

        static void Postfix(object __instance, ItemStack _itemStack, bool __result, int __state)
        {
            if (!__result || !InventoryAddPatchShared.IsInventoryAddModeActive())
            {
                return;
            }

            var player = InventoryAddPatchShared.ResolvePlayerFromXuiM(__instance);
            InventoryAddPatchShared.TryProcessSuccessfulAdd(player, _itemStack, __state, preferBag: false);
        }
    }

    [HarmonyPatch]
    [HarmonyWrapSafe]
    public class XUiMPlayerInventoryAddItemNoPartialPatch
    {
        static MethodBase TargetMethod() => XUiMPlayerInventoryPatchTargets.AddItemNoPartial();

        static void Prefix(ItemStack _itemStack, ref int __state)
        {
            __state = _itemStack != null ? GameReflection.GetItemCount(_itemStack) : 0;
        }

        static void Postfix(object __instance, ItemStack _itemStack, bool __result, int __state)
        {
            if (!__result || !InventoryAddPatchShared.IsInventoryAddModeActive())
            {
                return;
            }

            var player = InventoryAddPatchShared.ResolvePlayerFromXuiM(__instance);
            InventoryAddPatchShared.TryProcessSuccessfulAdd(player, _itemStack, __state);
        }
    }

    [HarmonyPatch]
    [HarmonyWrapSafe]
    public class BagAddItemPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Bag), "AddItem", new[] { typeof(ItemStack) });
        }

        static void Prefix(ItemStack _itemStack, ref int __state)
        {
            __state = _itemStack != null ? GameReflection.GetItemCount(_itemStack) : 0;
        }

        static void Postfix(Bag __instance, ItemStack _itemStack, bool __result, int __state)
        {
            if (!__result || !InventoryAddPatchShared.IsInventoryAddModeActive())
            {
                return;
            }

            if (!InventoryAddPatchShared.IsLocalPlayerBag(__instance))
            {
                return;
            }

            var player = GameReflection.TryGetLocalEntityPlayer();
            InventoryAddPatchShared.TryProcessSuccessfulAdd(player, _itemStack, __state, preferBag: true);
        }
    }

    [HarmonyPatch]
    [HarmonyWrapSafe]
    public class InventoryAddItemPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Inventory), "AddItem", new[] { typeof(ItemStack) });
        }

        static void Prefix(ItemStack _itemStack, ref int __state)
        {
            __state = _itemStack != null ? GameReflection.GetItemCount(_itemStack) : 0;
        }

        static void Postfix(Inventory __instance, ItemStack _itemStack, bool __result, int __state)
        {
            if (!__result || !InventoryAddPatchShared.IsInventoryAddModeActive())
            {
                return;
            }

            if (!InventoryAddPatchShared.IsLocalPlayerInventory(__instance))
            {
                return;
            }

            var player = GameReflection.TryGetLocalEntityPlayer();
            InventoryAddPatchShared.TryProcessSuccessfulAdd(player, _itemStack, __state, preferBag: false);
        }
    }

    [HarmonyPatch]
    [HarmonyWrapSafe]
    public class InventoryAddItemWithSlotPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(Inventory),
                "AddItem",
                new[] { typeof(ItemStack), typeof(int).MakeByRefType() });
        }

        static void Prefix(ItemStack _itemStack, ref int __state)
        {
            __state = _itemStack != null ? GameReflection.GetItemCount(_itemStack) : 0;
        }

        static void Postfix(Inventory __instance, ItemStack _itemStack, int _slot, bool __result, int __state)
        {
            if (!__result || !InventoryAddPatchShared.IsInventoryAddModeActive())
            {
                return;
            }

            if (!InventoryAddPatchShared.IsLocalPlayerInventory(__instance))
            {
                return;
            }

            var player = GameReflection.TryGetLocalEntityPlayer();
            InventoryAddPatchShared.TryProcessSuccessfulAdd(player, _itemStack, __state, preferredSlot: _slot, preferBag: false);
        }
    }
}
