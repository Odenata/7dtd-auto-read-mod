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
    /// Patches XUiC_LootWindowGroup.SetTileEntityChest - called when loot UI is shown.
    /// Parameter names must match exactly: (string _lootContainerName, ITileEntityLootable _te).
    /// </summary>
    [HarmonyPatch(typeof(XUiC_LootWindowGroup), "SetTileEntityChest", new System.Type[] { typeof(string), typeof(ITileEntityLootable) })]
    public class ContainerOpenPatch
    {
        static void Postfix(XUiC_LootWindowGroup __instance, string _lootContainerName, ITileEntityLootable _te)
        {
            var lootContainer = _te as TileEntityLootContainer;
            int itemsLength = lootContainer?.items?.Length ?? -1;
            int nonEmptyCount = 0;
            if (lootContainer?.items != null)
            {
                for (int i = 0; i < lootContainer.items.Length; i++)
                {
                    var s = lootContainer.items[i];
                    if (s != null && !s.IsEmpty()) nonEmptyCount++;
                }
            }
            var player = GameReflection.GetPlayerFromLootWindowGroup(__instance);
            if (lootContainer != null && player != null && nonEmptyCount == 0 && itemsLength >= 0)
            {
                ContainerOpenPatchShared.SetDeferred(player, lootContainer, __instance);
                return;
            }

            // Non-empty loot: wait for XUiC_LootContainer.itemControllers to bind (otherwise TryGetLootSlotItemStackController stays null).
            if (lootContainer != null && player != null && nonEmptyCount > 0)
            {
                ContainerOpenPatchShared.ScheduleLootReadAfterUiBind(player, _te, __instance);
                return;
            }

            ContainerOpenPatchShared.ProcessLootContainer(player, _te, __instance);
        }
    }

    /// <summary>
    /// Only patch XUiC_LootWindowGroup - XUiC_LootWindow.SetTileEntityChest is called from it,
    /// so patching both caused double processing (same container read twice).
    /// </summary>

    internal static class ContainerOpenPatchShared
    {
        private static EntityPlayer _deferredPlayer;
        private static TileEntityLootContainer _deferredContainer;
        private static XUiC_LootWindowGroup _deferredLootWindowGroup;
        private static int _deferredFramesLeft;

        private static EntityPlayer _pendingLootPlayer;
        private static ITileEntityLootable _pendingLootTile;
        private static XUiC_LootWindowGroup _pendingLootWindowGroup;
        private static int _pendingLootFramesLeft;

        /// <summary>Frames to wait for loot to populate after opening an empty container (~3s at 60fps).</summary>
        internal const int MaxDeferredFrames = 180;

        /// <summary>Frames before auto-read when loot already has items — lets loot slot XUi controllers attach.</summary>
        internal const int LootUiBindDelayFrames = 10;

        internal static bool HasNonEmptyLootItems(TileEntityLootContainer container)
        {
            if (container?.items == null)
            {
                return false;
            }

            for (int i = 0; i < container.items.Length; i++)
            {
                var s = container.items[i];
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
            TickPendingLootReadFrame();

            if (_deferredPlayer == null || _deferredContainer == null)
            {
                return;
            }

            if (HasNonEmptyLootItems(_deferredContainer))
            {
                ProcessLootContainer(_deferredPlayer, _deferredContainer, _deferredLootWindowGroup);
                _deferredPlayer = null;
                _deferredContainer = null;
                _deferredLootWindowGroup = null;
                return;
            }

            _deferredFramesLeft--;
            if (_deferredFramesLeft <= 0)
            {
                _deferredPlayer = null;
                _deferredContainer = null;
                _deferredLootWindowGroup = null;
            }
        }

        internal static void SetDeferred(EntityPlayer player, TileEntityLootContainer container, XUiC_LootWindowGroup lootWindowGroup)
        {
            _deferredPlayer = player;
            _deferredContainer = container;
            _deferredLootWindowGroup = lootWindowGroup;
            _deferredFramesLeft = MaxDeferredFrames;
        }

        internal static void ScheduleLootReadAfterUiBind(EntityPlayer player, ITileEntityLootable tileEntity, XUiC_LootWindowGroup lootWindowGroup)
        {
            _pendingLootPlayer = player;
            _pendingLootTile = tileEntity;
            _pendingLootWindowGroup = lootWindowGroup;
            _pendingLootFramesLeft = LootUiBindDelayFrames;
        }

        private static void TickPendingLootReadFrame()
        {
            if (_pendingLootFramesLeft <= 0 || _pendingLootTile == null || _pendingLootPlayer == null)
            {
                return;
            }

            _pendingLootFramesLeft--;
            if (_pendingLootFramesLeft > 0)
            {
                return;
            }

            var player = _pendingLootPlayer;
            var tile = _pendingLootTile;
            var group = _pendingLootWindowGroup;
            _pendingLootPlayer = null;
            _pendingLootTile = null;
            _pendingLootWindowGroup = null;
            ProcessLootContainer(player, tile, group);
        }

        internal static void ProcessLootContainer(EntityPlayer player, ITileEntityLootable _te, XUiC_LootWindowGroup lootWindowGroup = null)
        {
            if (player == null || _te == null)
                return;

            var lootContainer = _te as TileEntityLootContainer;
            if (lootContainer == null)
                return;

            AutoReader.ProcessContainer(player, lootContainer, lootWindowGroup);
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
