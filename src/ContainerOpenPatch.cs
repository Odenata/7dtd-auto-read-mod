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
            ContainerOpenPatchShared.ProcessLootContainer(GameReflection.GetPlayerFromLootWindowGroup(__instance), _te);
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
