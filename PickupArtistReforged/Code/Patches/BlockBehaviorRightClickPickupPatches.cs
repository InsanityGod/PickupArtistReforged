using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace PickupArtistReforged.Code.Patches;

[HarmonyPatch]
static class BlockBehaviorRightClickPickupPatches
{
    [HarmonyPatch(typeof(BlockBehaviorRightClickPickup), nameof(BlockBehaviorRightClickPickup.OnBlockInteractStart))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SetPriotizedSlot(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchStartForward(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.ActiveHotbarSlot)))
        );

        matcher.InsertAfterAndAdvance(
            CodeInstruction.LoadArgument(0), // this
            CodeInstruction.LoadArgument(2), // player
            CodeInstruction.LoadLocal(0), // dropStacks
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BlockBehaviorRightClickPickupPatches), nameof(GetBestSlot)))
        );

        matcher.MatchStartForward(
            CodeMatch.Calls(AccessTools.Method(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.TryGiveItemstack))
        ));

        matcher.RemoveInstruction();

        matcher.Insert(
            new CodeInstruction(OpCodes.Ldnull), // targetSlot (activeSlot)
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PickupUtil), nameof(PickupUtil.TryGiveItemStack)))
        );

        matcher.MatchEndBackwards(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayer), nameof(IPlayer.InventoryManager)))
        );

        matcher.RemoveInstruction();

        return matcher.InstructionEnumeration();
    }

    static ItemSlot GetBestSlot(ItemSlot activeSlot, BlockBehaviorRightClickPickup instance, IPlayer player, ItemStack[] target)
    {
        if(target.Length < 1 || instance.block is BlockPie) return activeSlot;
        return PickupUtil.GetBestSlotForPickup(activeSlot, player, target[0]);
    }
}
