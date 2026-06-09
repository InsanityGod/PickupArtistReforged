using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace PickupArtistReforged.Code.Patches;

[HarmonyPatch]
static class BlockEntityCratePatches
{
    [HarmonyPatch(typeof(BlockEntityCrate), nameof(BlockEntityCrate.OnBlockInteractStart))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SetPriotizedSlot(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchEndForward(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.ActiveHotbarSlot))),
            CodeMatch.IsStloc()
        );
        var slotIndex = matcher.Instruction.LocalIndex();

        matcher.InsertAndAdvance(
            CodeInstruction.LoadArgument(1), // player
            CodeInstruction.LoadArgument(0), // crate (this)
            CodeInstruction.LoadLocal(1), // take
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BlockEntityCratePatches), nameof(GetBestSlot)))
        );

        matcher.MatchStartForward(
            CodeMatch.Calls(AccessTools.Method(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.TryGiveItemstack))
        ));

        matcher.RemoveInstruction();

        matcher.Insert(
            CodeInstruction.LoadLocal(slotIndex), // targetSlot (activeSlot)
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PickupUtil), nameof(PickupUtil.TryGiveItemStack)))
        );

        matcher.MatchEndBackwards(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayer), nameof(IPlayer.InventoryManager)))
        );

        matcher.RemoveInstruction();

        return matcher.InstructionEnumeration();
    }

    static ItemSlot GetBestSlot(ItemSlot activeSlot, IPlayer player, BlockEntityCrate crate, bool take)
    {
        var target = crate.Inventory.FirstNonEmptySlot?.Itemstack;
        if (take)
        {
            return PickupUtil.GetBestSlotForPickup(activeSlot, player, target);
        }
        else return PickupUtil.GetBestSlotForDropoff(activeSlot, player, target);
    }
}
