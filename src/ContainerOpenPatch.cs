using System;
using System.Reflection;
using HarmonyLib;

namespace AutoReadMod
{
    /// <summary>
    /// Runs <see cref="ContainerOpenPatchShared.ProcessDeferredContainer"/> at most once per Unity frame so multiple hooks
    /// (XUi Update + GameManager Update) do not double-tick pending loot.
    /// </summary>
    internal static class DeferredContainerTick
    {
        private static int _lastUnityFrame = int.MinValue;

        internal static void TickIfNewUnityFrame()
        {
            try
            {
                var fp = typeof(UnityEngine.Time).GetProperty(
                    "frameCount",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (fp != null)
                {
                    var f = System.Convert.ToInt32(fp.GetValue(null, null));
                    if (f == _lastUnityFrame)
                    {
                        return;
                    }

                    _lastUnityFrame = f;
                }
            }
            catch
            {
                // Hermetic stubs may omit Time.frameCount — process every call (may tick twice if multiple hooks run).
            }

            ContainerOpenPatchShared.ProcessDeferredContainer();
        }
    }

    /// <summary>Backup driver when XUi Update postfix is missing or runs late.</summary>
    internal static class GameManagerDeferredTickHook
    {
        internal static void GameManagerUpdatePostfix()
        {
            DeferredContainerTick.TickIfNewUnityFrame();
        }
    }

    /// <summary>
    /// v3.0: hook <see cref="XUiC_LootWindow.SetTileEntityChest"/> — runs after <c>lootContainer.SetSlots</c> binds
    /// itemControllers, so auto-read can run immediately (no frame delay). Do not also patch <c>OpenLooting</c> (double read).
    /// </summary>
    [HarmonyPatch(typeof(XUiC_LootWindow), "SetTileEntityChest", new System.Type[] { typeof(string), typeof(ITileEntityLootable), typeof(bool) })]
    public class ContainerOpenPatch
    {
        static void Postfix(XUiC_LootWindow __instance, string _lootContainerName, ITileEntityLootable _te, bool _playOpenSound)
        {
            if (_te == null)
                return;

            var lootWindowGroup = GameReflection.TryResolveLootWindowGroupFromLootWindow(__instance);
            var items = ContainerOpenPatchShared.TryGetLootItems(_te);
            int itemsLength = items?.Length ?? -1;
            int nonEmptyCount = 0;
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    var s = items[i];
                    if (s != null && !s.IsEmpty()) nonEmptyCount++;
                }
            }

            var player = GameReflection.GetPlayerFromLootWindow(__instance)
                         ?? (lootWindowGroup != null ? GameReflection.GetPlayerFromLootWindowGroup(lootWindowGroup) : null);

            if (player != null && nonEmptyCount == 0 && itemsLength >= 0)
            {
                ContainerOpenPatchShared.SetDeferred(player, _te, lootWindowGroup, __instance);
                return;
            }

            ContainerOpenPatchShared.ProcessLootContainer(player, _te, lootWindowGroup, __instance);
        }
    }

    /// <summary>Re-sync loot slot controllers after the window is shown (SetTileEntityChest runs before Open).</summary>
    [HarmonyPatch(typeof(XUiC_LootWindow), "OnOpen")]
    public class LootWindowOnOpenPatch
    {
        static void Postfix(XUiC_LootWindow __instance)
        {
            ContainerOpenPatchShared.TryApplyPendingLootUiRefresh(__instance);
        }
    }

    internal static class ContainerOpenPatchShared
    {
        private static EntityPlayer _deferredPlayer;
        private static ITileEntityLootable _deferredContainer;
        private static XUiC_LootWindowGroup _deferredLootWindowGroup;
        private static XUiC_LootWindow _deferredLootWindow;
        private static int _deferredFramesLeft;

        private static XUiC_LootWindow _pendingRefreshLootWindow;
        private static ITileEntityLootable _pendingRefreshTileEntity;

        /// <summary>Frames to wait for loot to populate after opening an empty container (~3s at 60fps).</summary>
        internal const int MaxDeferredFrames = 180;

        internal static bool HasNonEmptyLootItems(ITileEntityLootable container)
        {
            var items = TryGetLootItems(container);
            if (items == null)
            {
                return false;
            }

            for (int i = 0; i < items.Length; i++)
            {
                var s = items[i];
                if (s != null && !s.IsEmpty())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Called once per frame from XUi Update. When loot was empty at open, waits until items appear then auto-reads once.</summary>
        internal static void ProcessDeferredContainer()
        {
            if (_deferredPlayer == null || _deferredContainer == null)
            {
                return;
            }

            if (HasNonEmptyLootItems(_deferredContainer))
            {
                ProcessLootContainer(_deferredPlayer, _deferredContainer, _deferredLootWindowGroup, _deferredLootWindow);
                _deferredPlayer = null;
                _deferredContainer = null;
                _deferredLootWindowGroup = null;
                _deferredLootWindow = null;
                return;
            }

            _deferredFramesLeft--;
            if (_deferredFramesLeft <= 0)
            {
                _deferredPlayer = null;
                _deferredContainer = null;
                _deferredLootWindowGroup = null;
                _deferredLootWindow = null;
            }
        }

        internal static void SetDeferred(EntityPlayer player, ITileEntityLootable container, XUiC_LootWindowGroup lootWindowGroup, XUiC_LootWindow lootWindow)
        {
            _deferredPlayer = player;
            _deferredContainer = container;
            _deferredLootWindowGroup = lootWindowGroup;
            _deferredLootWindow = lootWindow;
            _deferredFramesLeft = MaxDeferredFrames;
        }

        internal static void ProcessLootContainer(EntityPlayer player, ITileEntityLootable te, XUiC_LootWindowGroup lootWindowGroup = null, XUiC_LootWindow lootWindow = null)
        {
            if (player == null || te == null)
                return;

            AutoReader.ProcessContainer(player, te, lootWindowGroup, lootWindow);
            MarkLootUiRefreshNeeded(lootWindow, te);
            GameReflection.RefreshLootContainerUiFromTileEntity(lootWindow, te);
        }

        internal static void MarkLootUiRefreshNeeded(XUiC_LootWindow lootWindow, ITileEntityLootable te)
        {
            if (lootWindow == null || te == null)
                return;
            _pendingRefreshLootWindow = lootWindow;
            _pendingRefreshTileEntity = te;
        }

        internal static void TryApplyPendingLootUiRefresh(XUiC_LootWindow lootWindow)
        {
            if (lootWindow == null || _pendingRefreshLootWindow != lootWindow || _pendingRefreshTileEntity == null)
                return;

            var te = _pendingRefreshTileEntity;
            _pendingRefreshLootWindow = null;
            _pendingRefreshTileEntity = null;
            GameReflection.RefreshLootContainerUiFromTileEntity(lootWindow, te);
        }

        internal static ItemStack[] TryGetLootItems(ITileEntityLootable container)
        {
            if (container == null)
            {
                return null;
            }

            return GameReflection.GetMemberValue<ItemStack[]>(container, "items");
        }

        internal static bool TryWriteLootItem(ITileEntityLootable container, ItemStack[] items, int slotIndex, ItemStack updatedStack)
        {
            if (items == null || slotIndex < 0 || slotIndex >= items.Length)
            {
                return false;
            }

            items[slotIndex] = updatedStack;
            GameReflection.TryUpdateLootContainerSlot(container, slotIndex, updatedStack);
            TryMarkLootContainerTouched(container);
            return true;
        }

        private static void TryMarkLootContainerTouched(ITileEntityLootable container)
        {
            if (container == null)
            {
                return;
            }

            // v3 loot containers are interface-based; touch flags nudge persistence/replication without concrete type refs.
            GameReflection.SetMemberValue(container, "bTouched", true);
            GameReflection.SetMemberValue(container, "bWasTouched", true);
        }

        internal static void SafeLog(string message)
        {
            try
            {
                UnityEngine.Debug.Log($"[AutoReadMod] {message}");
            }
            catch
            {
                // Unity not available
            }
        }
    }
}
