using System;
using System.Reflection;
using HarmonyLib;

namespace AutoReadMod
{
    /// <summary>
    /// v3 entity loot (death bags, zombie bags, <see cref="EntityLootContainer"/>) opens via
    /// <c>XUiC_BagStorageWindowGroup</c> → <see cref="XUiC_BagContainer.SetBag"/>, not
    /// <see cref="XUiC_LootWindow.SetTileEntityChest"/>. Patch the bag UI bind site only (do not also
    /// patch <c>Open</c> / <c>OpenStorageWindow</c> — double-read risk).
    /// </summary>
    [HarmonyPatch(typeof(XUiC_BagContainer), "SetBag", new Type[] { typeof(Bag), typeof(LootContainer), typeof(string), typeof(Action) })]
    [HarmonyWrapSafe]
    public class BagOpenPatch
    {
        static void Postfix(XUiC_BagContainer __instance, Bag _bag, LootContainer _lootContainer, string _containerName, Action _onModified)
        {
            if (_bag == null)
                return;

            var player = GameReflection.GetPlayerFromBagContainer(__instance);
            var slots = GameReflection.GetBagSlots(_bag);
            int nonEmptyCount = 0;
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var s = slots[i];
                    if (s != null && !s.IsEmpty())
                        nonEmptyCount++;
                }
            }

            if (player != null && nonEmptyCount == 0 && slots != null)
            {
                BagOpenPatchShared.SetDeferred(player, _bag, __instance);
                return;
            }

            BagOpenPatchShared.ProcessEntityBag(player, _bag, __instance);
        }
    }

    /// <summary>Re-sync bag slot controllers after the window is shown (SetBag may run before Open).</summary>
    [HarmonyPatch(typeof(XUiC_BagContainer), "OnOpen")]
    [HarmonyWrapSafe]
    public class BagContainerOnOpenPatch
    {
        static void Postfix(XUiC_BagContainer __instance)
        {
            BagOpenPatchShared.TryApplyPendingBagUiRefresh(__instance);
        }
    }

    internal static class BagOpenPatchShared
    {
        private static EntityPlayer _deferredPlayer;
        private static Bag _deferredBag;
        private static XUiC_BagContainer _deferredBagContainer;
        private static int _deferredFramesLeft;

        private static XUiC_BagContainer _pendingRefreshBagContainer;
        private static Bag _pendingRefreshBag;

        internal const int MaxDeferredFrames = 180;

        internal static bool HasNonEmptyBagSlots(Bag bag)
        {
            var slots = GameReflection.GetBagSlots(bag);
            if (slots == null)
                return false;

            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s != null && !s.IsEmpty())
                    return true;
            }

            return false;
        }

        /// <summary>Called once per frame from the shared deferred tick. When bag was empty at open, waits until items appear then auto-reads once.</summary>
        internal static void ProcessDeferredBag()
        {
            if (_deferredPlayer == null || _deferredBag == null)
                return;

            if (HasNonEmptyBagSlots(_deferredBag))
            {
                ProcessEntityBag(_deferredPlayer, _deferredBag, _deferredBagContainer);
                ClearDeferred();
                return;
            }

            _deferredFramesLeft--;
            if (_deferredFramesLeft <= 0)
                ClearDeferred();
        }

        internal static void SetDeferred(EntityPlayer player, Bag bag, XUiC_BagContainer bagContainer)
        {
            _deferredPlayer = player;
            _deferredBag = bag;
            _deferredBagContainer = bagContainer;
            _deferredFramesLeft = MaxDeferredFrames;
        }

        private static void ClearDeferred()
        {
            _deferredPlayer = null;
            _deferredBag = null;
            _deferredBagContainer = null;
        }

        internal static void ProcessEntityBag(EntityPlayer player, Bag bag, XUiC_BagContainer bagContainer = null)
        {
            if (player == null || bag == null)
                return;

            AutoReader.ProcessEntityLootBag(player, bag, bagContainer);
            MarkBagUiRefreshNeeded(bagContainer, bag);
            GameReflection.RefreshBagContainerUiFromBag(bagContainer, bag);
        }

        internal static void MarkBagUiRefreshNeeded(XUiC_BagContainer bagContainer, Bag bag)
        {
            if (bagContainer == null || bag == null)
                return;
            _pendingRefreshBagContainer = bagContainer;
            _pendingRefreshBag = bag;
        }

        internal static void TryApplyPendingBagUiRefresh(XUiC_BagContainer bagContainer)
        {
            if (bagContainer == null || _pendingRefreshBagContainer != bagContainer || _pendingRefreshBag == null)
                return;

            var bag = _pendingRefreshBag;
            _pendingRefreshBagContainer = null;
            _pendingRefreshBag = null;
            GameReflection.RefreshBagContainerUiFromBag(bagContainer, bag);
        }
    }
}
