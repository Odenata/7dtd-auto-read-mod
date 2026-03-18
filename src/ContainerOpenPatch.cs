using System;
using HarmonyLib;

namespace AutoReadMod
{
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
                ContainerOpenPatchShared.SetDeferred(player, lootContainer);
                return;
            }
            ContainerOpenPatchShared.ProcessLootContainer(player, _te);
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
        private static int _deferredFramesLeft;

        /// <summary>Frames to wait for loot to populate after opening an empty container (~3s at 60fps).</summary>
        internal const int MaxDeferredFrames = 180;

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
            if (_deferredPlayer == null || _deferredContainer == null)
            {
                return;
            }

            if (HasNonEmptyLootItems(_deferredContainer))
            {
                ProcessLootContainer(_deferredPlayer, _deferredContainer);
                _deferredPlayer = null;
                _deferredContainer = null;
                return;
            }

            _deferredFramesLeft--;
            if (_deferredFramesLeft <= 0)
            {
                _deferredPlayer = null;
                _deferredContainer = null;
            }
        }

        internal static void SetDeferred(EntityPlayer player, TileEntityLootContainer container)
        {
            _deferredPlayer = player;
            _deferredContainer = container;
            _deferredFramesLeft = MaxDeferredFrames;
        }

        internal static void ProcessLootContainer(EntityPlayer player, ITileEntityLootable _te)
        {
            if (player == null || _te == null)
                return;

            var lootContainer = _te as TileEntityLootContainer;
            if (lootContainer == null)
                return;

            AutoReader.ProcessContainer(player, lootContainer);
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
