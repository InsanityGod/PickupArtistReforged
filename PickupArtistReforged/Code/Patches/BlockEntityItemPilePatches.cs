using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace PickupArtistReforged.Code.Patches;

[HarmonyPatch]
static class BlockEntityItemPilePatches
{
    [HarmonyPatch(typeof(BlockEntityItemPile), nameof(BlockEntityItemPile.OnPlayerInteract))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SetPriotizedSlot_OnPlayerInteract(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchEndForward(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.ActiveHotbarSlot))),
            CodeMatch.IsStloc()
        );

        matcher.InsertAndAdvance(
            CodeInstruction.LoadArgument(1), // player
            CodeInstruction.LoadArgument(0), // pile (this)
            CodeInstruction.LoadLocal(2), // sneaking (dropoff)
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BlockEntityItemPilePatches), nameof(GetBestSlot)))
        );

        matcher.MatchStartForward(
            new CodeMatch( instruction => instruction.operand is MethodBase method && method.Name == nameof(IBlockAccessor.SetBlock))
        );

        matcher.MatchStartForward(
            CodeMatch.Calls(AccessTools.Method(typeof(BlockEntityItemPile), nameof(BlockEntityItemPile.TryPutItem)))
        );

        matcher.Instruction.opcode = OpCodes.Call;
        matcher.Instruction.operand = AccessTools.Method(typeof(BlockEntityItemPilePatches), nameof(TryPutItem));
        matcher.Insert(
            CodeInstruction.LoadArgument(0) // this
        );

        return matcher.InstructionEnumeration();
    }

    static bool TryPutItem(BlockEntityItemPile newPile, IPlayer byPlayer, BlockEntityItemPile currentPile)
    {
        newPile.inventory[0].Itemstack = currentPile.inventory[0].Itemstack!.GetEmptyClone();
        return newPile.TryPutItem(byPlayer);
    }

    [HarmonyPatch(typeof(BlockEntityItemPile), nameof(BlockEntityItemPile.TryPutItem))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SetPriotizedSlot_TryPutItem(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchEndForward(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.ActiveHotbarSlot))),
            CodeMatch.IsStloc()
        );

        matcher.InsertAndAdvance(
            CodeInstruction.LoadArgument(1), // player
            CodeInstruction.LoadArgument(0), // pile (this)
            new CodeInstruction(OpCodes.Ldc_I4_1),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BlockEntityItemPilePatches), nameof(GetBestSlot)))
        );

        return matcher.InstructionEnumeration();
    }

    static ItemSlot GetBestSlot(ItemSlot activeSlot, IPlayer player, BlockEntityItemPile pile, bool dropoff)
    {
        var target = pile.inventory[0].Itemstack;
        if (dropoff)
        {
            return PickupUtil.GetBestSlotForDropoff(activeSlot, player, target);
        }
        else return PickupUtil.GetBestSlotForPickup(activeSlot, player, target);
    }

    [HarmonyPatch(typeof(BlockEntityItemPile), nameof(BlockEntityItemPile.TryTakeItem))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SetPriotizedSlot_TryTakeItem(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
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
