using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace AutoReadMod
{
    [HarmonyPatch(typeof(ItemActionOpenBundle), "ExecuteInstantAction", new System.Type[] { typeof(EntityAlive), typeof(ItemStack), typeof(bool), typeof(XUiC_ItemStack) })]
    [HarmonyWrapSafe]
    public class OpenBundlePatch
    {
        static void Prefix(EntityAlive ent, ref InventorySnapshot __state)
        {
            __state = BundleOpenPatchShared.CaptureInventorySnapshot(ent);
        }

        static void Postfix(EntityAlive ent, bool __result, InventorySnapshot __state)
        {
            BundleOpenPatchShared.ProcessOpenedBundle(ent, __state, "ItemActionOpenBundle");
        }
    }

    [HarmonyPatch(typeof(ItemActionOpenLootBundle), "ExecuteInstantAction", new System.Type[] { typeof(EntityAlive), typeof(ItemStack), typeof(bool), typeof(XUiC_ItemStack) })]
    [HarmonyWrapSafe]
    public class OpenLootBundlePatch
    {
        static void Prefix(EntityAlive ent, ref InventorySnapshot __state)
        {
            __state = BundleOpenPatchShared.CaptureInventorySnapshot(ent);
        }

        static void Postfix(EntityAlive ent, bool __result, InventorySnapshot __state)
        {
            BundleOpenPatchShared.ProcessOpenedBundle(ent, __state, "ItemActionOpenLootBundle");
        }
    }

    internal static class BundleOpenPatchShared
    {
        /// <summary>
        /// Opening a bundle mutates inventory; callbacks can synchronously re-enter bundle/item paths.
        /// Processing nested runs can mis-diff snapshots and spam exceptions — skip until the outer run completes.
        /// </summary>
        private static int _bundleAutoReadDepth;

        private static float _lastBundleFailureLogTime = -999f;
        private static float _lastBundleReentrantLogTime = -999f;
        private const float BundleFailureLogIntervalSeconds = 5f;

        internal static InventorySnapshot CaptureInventorySnapshot(EntityAlive entity)
        {
            if (!ModConfig.Instance.Enabled || ModConfig.Instance.ReadOnInventoryAdd)
            {
                return null;
            }

            var player = entity as EntityPlayer;
            var inventory = GameReflection.GetInventory(player);
            var bag = GameReflection.GetBag(player);
            if (inventory == null && bag == null)
            {
                return null;
            }

            return new InventorySnapshot(
                InventorySlotSnapshotCollection.Capture(inventory?.GetSlots()),
                InventorySlotSnapshotCollection.Capture(GameReflection.GetBagSlots(bag)));
        }

        /// <summary>Harmony postfix — runs right after bundle contents land in inventory/bag.</summary>
        internal static void ProcessOpenedBundle(EntityAlive entity, InventorySnapshot beforeSnapshot, string bundlePatchSource = null)
        {
            // Do not gate on ExecuteInstantAction's bool return: some builds report false even when
            // bundle loot was applied; diff-based detection is harmless when nothing was added.
            if (beforeSnapshot == null)
            {
                return;
            }

            var player = entity as EntityPlayer;
            var inventory = GameReflection.GetInventory(player);
            var bag = GameReflection.GetBag(player);
            if (player == null || (inventory == null && bag == null))
            {
                return;
            }

            if (Interlocked.Increment(ref _bundleAutoReadDepth) > 1)
            {
                Interlocked.Decrement(ref _bundleAutoReadDepth);
                try
                {
                    if (Time.time - _lastBundleReentrantLogTime >= BundleFailureLogIntervalSeconds)
                    {
                        _lastBundleReentrantLogTime = Time.time;
                        Debug.LogWarning("[AutoReadMod] Bundle auto-read skipped (re-entrant/nested inventory callback)");
                    }
                }
                catch
                {
                    // ignore
                }

                return;
            }

            try
            {
                ApplyOpenedBundleEffectsImmediately(player, beforeSnapshot);
            }
            finally
            {
                Interlocked.Decrement(ref _bundleAutoReadDepth);
            }
        }

        /// <summary>Runs bundle diff + auto-read (shared by <see cref="ProcessOpenedBundle"/> and tests).</summary>
        internal static void ApplyOpenedBundleEffectsImmediately(EntityPlayer player, InventorySnapshot beforeSnapshot)
        {
            if (beforeSnapshot == null || player == null)
            {
                return;
            }

            if (ModConfig.Instance.ReadOnInventoryAdd)
            {
                return;
            }

            try
            {
                var inventory = GameReflection.GetInventory(player);
                var bag = GameReflection.GetBag(player);
                if (inventory == null && bag == null)
                {
                    return;
                }

                var addedInventoryReadableCounts = FindAddedReadableSlotCounts(beforeSnapshot.Inventory, inventory?.GetSlots());
                var addedBagReadableCounts = FindAddedReadableSlotCounts(beforeSnapshot.Bag, GameReflection.GetBagSlots(bag));
                if (addedInventoryReadableCounts.Count == 0 && addedBagReadableCounts.Count == 0)
                {
                    return;
                }

                if (addedInventoryReadableCounts.Count > 0)
                {
                    AutoReader.ProcessInventorySlots(player, inventory, addedInventoryReadableCounts);
                }

                if (addedBagReadableCounts.Count > 0)
                {
                    AutoReader.ProcessBagSlots(player, bag, addedBagReadableCounts);
                }
            }
            catch (System.Exception ex)
            {
                try
                {
                    if (Time.time - _lastBundleFailureLogTime >= BundleFailureLogIntervalSeconds)
                    {
                        _lastBundleFailureLogTime = Time.time;
                        UnityEngine.Debug.LogWarning("[AutoReadMod] Bundle auto-read failed (inventory unchanged): " + ex.Message + "\n" + ex.StackTrace);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        internal static Dictionary<int, int> FindAddedReadableSlotCounts(InventorySlotSnapshotCollection beforeSnapshot, ItemStack[] afterSlots)
        {
            var results = new Dictionary<int, int>();
            if (beforeSnapshot == null || afterSlots == null)
            {
                return results;
            }

            var remainingAddedByKey = BuildAddedItemCounts(beforeSnapshot, afterSlots);

            for (int slotIndex = 0; slotIndex < afterSlots.Length; slotIndex++)
            {
                var currentStack = afterSlots[slotIndex];
                if (!AutoReader.ShouldAutoReadItem(currentStack))
                {
                    continue;
                }

                var itemKey = AutoReader.GetItemKey(currentStack);
                if (!remainingAddedByKey.TryGetValue(itemKey, out int remainingAdded) || remainingAdded <= 0)
                {
                    continue;
                }

                var beforeSlot = beforeSnapshot.GetSlot(slotIndex);
                if (!beforeSlot.Matches(currentStack))
                {
                    continue;
                }

                var directIncrease = GameReflection.GetItemCount(currentStack) - beforeSlot.Count;
                if (directIncrease <= 0)
                {
                    continue;
                }

                int assigned = System.Math.Min(directIncrease, remainingAdded);
                results[slotIndex] = assigned;
                remainingAddedByKey[itemKey] = remainingAdded - assigned;
            }

            for (int slotIndex = 0; slotIndex < afterSlots.Length; slotIndex++)
            {
                var currentStack = afterSlots[slotIndex];
                if (!AutoReader.ShouldAutoReadItem(currentStack))
                {
                    continue;
                }

                var itemKey = AutoReader.GetItemKey(currentStack);
                if (!remainingAddedByKey.TryGetValue(itemKey, out int remainingAdded) || remainingAdded <= 0)
                {
                    continue;
                }

                var beforeSlot = beforeSnapshot.GetSlot(slotIndex);
                if (beforeSlot.Matches(currentStack))
                {
                    continue;
                }

                int assigned = System.Math.Min(GameReflection.GetItemCount(currentStack), remainingAdded);
                if (assigned <= 0)
                {
                    continue;
                }

                results[slotIndex] = results.TryGetValue(slotIndex, out int existingAssigned)
                    ? existingAssigned + assigned
                    : assigned;
                remainingAddedByKey[itemKey] = remainingAdded - assigned;
            }

            return results;
        }

        private static Dictionary<string, int> BuildAddedItemCounts(InventorySlotSnapshotCollection beforeSnapshot, ItemStack[] afterSlots)
        {
            var beforeCounts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var slot in beforeSnapshot.Slots)
            {
                if (slot.IsEmpty)
                {
                    continue;
                }

                beforeCounts[slot.ItemKey] = beforeCounts.TryGetValue(slot.ItemKey, out int existingCount)
                    ? existingCount + slot.Count
                    : slot.Count;
            }

            var afterCounts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < afterSlots.Length; i++)
            {
                var stack = afterSlots[i];
                if (stack == null || stack.IsEmpty())
                {
                    continue;
                }

                var itemKey = AutoReader.GetItemKey(stack);
                if (string.IsNullOrEmpty(itemKey))
                {
                    continue;
                }

                afterCounts[itemKey] = afterCounts.TryGetValue(itemKey, out int existingCount)
                    ? existingCount + GameReflection.GetItemCount(stack)
                    : GameReflection.GetItemCount(stack);
            }

            var addedCounts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var afterCount in afterCounts)
            {
                int beforeCount = beforeCounts.TryGetValue(afterCount.Key, out int existingCount)
                    ? existingCount
                    : 0;
                int addedCount = afterCount.Value - beforeCount;
                if (addedCount > 0)
                {
                    addedCounts[afterCount.Key] = addedCount;
                }
            }

            return addedCounts;
        }
    }

    internal sealed class InventorySnapshot
    {
        internal InventorySnapshot(InventorySlotSnapshotCollection inventory, InventorySlotSnapshotCollection bag)
        {
            Inventory = inventory ?? InventorySlotSnapshotCollection.Empty;
            Bag = bag ?? InventorySlotSnapshotCollection.Empty;
        }

        internal InventorySlotSnapshotCollection Inventory { get; }
        internal InventorySlotSnapshotCollection Bag { get; }
    }

    internal sealed class InventorySlotSnapshotCollection
    {
        internal static readonly InventorySlotSnapshotCollection Empty =
            new InventorySlotSnapshotCollection(System.Array.Empty<InventorySlotSnapshot>());

        internal InventorySlotSnapshotCollection(InventorySlotSnapshot[] slots)
        {
            Slots = slots ?? System.Array.Empty<InventorySlotSnapshot>();
        }

        internal InventorySlotSnapshot[] Slots { get; }

        internal static InventorySlotSnapshotCollection Capture(ItemStack[] slots)
        {
            if (slots == null)
            {
                return Empty;
            }

            var snapshot = new InventorySlotSnapshot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                snapshot[i] = InventorySlotSnapshot.FromItemStack(slots[i]);
            }

            return new InventorySlotSnapshotCollection(snapshot);
        }

        internal InventorySlotSnapshot GetSlot(int index)
        {
            if (index < 0 || Slots == null || index >= Slots.Length)
            {
                return InventorySlotSnapshot.Empty;
            }

            return Slots[index];
        }
    }

    internal readonly struct InventorySlotSnapshot
    {
        internal static readonly InventorySlotSnapshot Empty = new InventorySlotSnapshot(string.Empty, 0);

        internal InventorySlotSnapshot(string itemKey, int count)
        {
            ItemKey = itemKey ?? string.Empty;
            Count = count;
        }

        internal string ItemKey { get; }
        internal int Count { get; }
        internal bool IsEmpty => Count <= 0 || string.IsNullOrEmpty(ItemKey);

        internal bool Matches(ItemStack itemStack)
        {
            if (itemStack == null || itemStack.IsEmpty())
            {
                return IsEmpty;
            }

            return ItemKey == AutoReader.GetItemKey(itemStack);
        }

        internal static InventorySlotSnapshot FromItemStack(ItemStack itemStack)
        {
            if (itemStack == null || itemStack.IsEmpty())
            {
                return Empty;
            }

            return new InventorySlotSnapshot(AutoReader.GetItemKey(itemStack), GameReflection.GetItemCount(itemStack));
        }
    }
}
