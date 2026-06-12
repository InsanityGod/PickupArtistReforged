using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace PickupArtistReforged.Code.Patches;

[HarmonyPatch]
static class BlockEntityToolRackPatches
{
    [HarmonyPatch(typeof(BlockEntityToolrack), "TakeFromSlot")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SetPriotizedSlot_TakeFromSlot(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchStartForward(
            CodeMatch.Calls(AccessTools.Method(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.TryGiveItemstack))
        ));

        matcher.RemoveInstruction();

        matcher.Insert(
            new CodeInstruction(OpCodes.Ldnull),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PickupUtil), nameof(PickupUtil.TryGiveItemStack)))
        );

        matcher.MatchEndBackwards(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayer), nameof(IPlayer.InventoryManager)))
        );

        matcher.RemoveInstruction();

        return matcher.InstructionEnumeration();
    }
}
