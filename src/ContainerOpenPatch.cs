using HarmonyLib;

namespace AutoReadMod
{
    [HarmonyPatch(typeof(XUiC_LootWindowGroup))]
    [HarmonyPatch("SetTileEntityChest")]
    public class ContainerOpenPatch
    {
        static void Postfix(XUiC_LootWindowGroup __instance, ITileEntityLootable _te)
        {
            SafeLog("[DEBUG] ContainerOpenPatch.Postfix triggered");
            
            if (__instance?.xui?.playerUI?.entityPlayer == null)
            {
                SafeLog("[DEBUG] Player is null, skipping");
                return;
            }

            if (_te == null)
            {
                SafeLog("[DEBUG] Tile entity is null, skipping");
                return;
            }

            var lootContainer = _te as TileEntityLootContainer;
            if (lootContainer == null)
            {
                SafeLog("[DEBUG] Not a loot container, skipping");
                return;
            }

            SafeLog($"[DEBUG] Processing container with {lootContainer.items?.Length ?? 0} slots");
            var player = __instance.xui.playerUI.entityPlayer;
            AutoReader.ProcessContainer(player, lootContainer);
        }

        private static void SafeLog(string message)
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
