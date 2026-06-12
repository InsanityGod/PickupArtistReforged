using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace PickupArtistReforged.Code.Patches;

[HarmonyPatch]
[HarmonyPatchCategory("ModOnServer")]
static class BlockTorchHolderRackPatches
{
    [HarmonyPatch(typeof(BlockTorchHolder), nameof(BlockTorchHolder.OnBlockInteractStart))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SetPriotizedSlot_OnBlockInteractStart(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchStartForward(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.ActiveHotbarSlot))),
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(ItemSlot), nameof(ItemSlot.Itemstack)))
        );
        matcher.DeclareLocal(typeof(ItemSlot), out var targetSlot);

        matcher.InsertAfter(
            CodeInstruction.LoadArgument(2), // player
            CodeInstruction.LoadArgument(1), // world
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BlockTorchHolderRackPatches), nameof(GetTorchSlot))),
            new CodeInstruction(OpCodes.Dup),
            CodeInstruction.StoreLocal(targetSlot.LocalIndex)
        );

        CodeMatch[] match = [
            new CodeMatch(OpCodes.Ldarg_2),
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayer), nameof(IPlayer.InventoryManager))),
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.ActiveHotbarSlot)))
        ];

        for(var i = 0; i < 2; i++)
        {
            matcher.MatchStartForward(match);

            matcher.Opcode = OpCodes.Ldloc_S;
            matcher.Operand = targetSlot.LocalIndex;
            matcher.Advance();
            matcher.RemoveInstructions(2);
        }

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

    static ItemSlot GetTorchSlot(ItemSlot activeSlot, IPlayer player, IWorldAccessor world)
    {
        var itemStack = new ItemStack(world.GetBlock(new AssetLocation("game", "torch-basic-lit-up")));

        return PickupUtil.GetBestSlotForDropoff(activeSlot, player, itemStack);
    }
}
