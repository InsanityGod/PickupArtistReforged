using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace PickupArtistReforged.Code.Patches;

//[HarmonyPatch]
static class BlockGroundStoragePatches
{
    //[HarmonyPatch(typeof(BlockGroundStorage), nameof(BlockGroundStorage.CreateStorage))]
    //[HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SetPriotizedSlot_BlockGroundStoragePatches(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchStartForward(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(CollectibleBehaviorGroundStorable), nameof(CollectibleBehaviorGroundStorable.StorageProps)))
        );


        matcher.DeclareLocal(typeof(ItemSlot), out var targetSlot);
        matcher.MatchStartBackwards(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.ActiveHotbarSlot)))
        );

        matcher.InsertAfterAndAdvance(
            CodeInstruction.LoadArgument(1), // world
            CodeInstruction.LoadArgument(2), // selection
            CodeInstruction.LoadArgument(3), // player
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BlockGroundStoragePatches), nameof(GetBestInteractionSlot))),
            new CodeInstruction(OpCodes.Dup),
            CodeInstruction.StoreLocal(targetSlot.LocalIndex)
        );

        matcher.MatchStartForward(
            CodeMatch.IsLdloc(),
            CodeMatch.IsLdloc(),
            CodeMatch.Calls(AccessTools.PropertySetter(typeof(BlockEntityGroundStorage), nameof(BlockEntityGroundStorage.AttachFace)))
        );

        var blockEntityIndex = matcher.Instruction.LocalIndex();

        matcher.Advance(3);
        matcher.Insert(
            CodeInstruction.LoadLocal(blockEntityIndex),
            CodeInstruction.LoadLocal(targetSlot.LocalIndex),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BlockGroundStoragePatches), nameof(FakeSetInitialContent)))
        );

        return matcher.InstructionEnumeration();
    }

    static ItemSlot GetBestInteractionSlot(ItemSlot activeSlot, IWorldAccessor world, BlockSelection selection, IPlayer player)
    {
        if(selection?.Position is null 
            || world.BlockAccessor.GetBlockEntity(selection.Position) is not BlockEntityGroundStorage groundStorage 
            || groundStorage.StorageProps is not { Layout: EnumGroundStorageLayout.Messy12 or EnumGroundStorageLayout.Stacking }) return activeSlot;

        return PickupUtil.GetBestSlotForDropoff(activeSlot, player, groundStorage.Inventory.FirstNonEmptySlot?.Itemstack);
    }

    static void FakeSetInitialContent(BlockEntityGroundStorage groundStorage, ItemSlot target)
    {
        groundStorage.Inventory[0].Itemstack = target.Itemstack?.GetEmptyClone();
    }
}