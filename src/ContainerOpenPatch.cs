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
            ContainerOpenPatchShared.ProcessLootContainer(__instance?.xui?.playerUI?.entityPlayer, _te);
        }
    }

    /// <summary>
    /// Only patch XUiC_LootWindowGroup - XUiC_LootWindow.SetTileEntityChest is called from it,
    /// so patching both caused double processing (same container read twice).
    /// </summary>

    internal static class ContainerOpenPatchShared
    {
        internal static void ProcessLootContainer(EntityPlayer player, ITileEntityLootable _te)
        {
            SafeLog("ContainerOpenPatch triggered");
            
            if (player == null)
            {
                SafeLog("Player is null, skipping");
                return;
            }

            if (_te == null)
            {
                SafeLog("Tile entity is null, skipping");
                return;
            }

            var lootContainer = _te as TileEntityLootContainer;
            if (lootContainer == null)
            {
                SafeLog("Not a loot container, skipping");
                return;
            }

            SafeLog($"Processing container with {lootContainer.items?.Length ?? 0} slots");
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
