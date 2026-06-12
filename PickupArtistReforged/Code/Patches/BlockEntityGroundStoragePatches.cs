using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace PickupArtistReforged.Code.Patches;

[HarmonyPatch]
[HarmonyPatchCategory("ModOnServer")]
static class BlockEntityGroundStoragePatches
{
    [HarmonyPatch(typeof(BlockEntityGroundStorage), nameof(BlockEntityGroundStorage.OnPlayerInteractStart))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SetPriotizedSlot_OnPlayerInteractStart(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchEndForward(
            CodeMatch.Calls(AccessTools.Method(typeof(BlockEntityGroundStorage), nameof(BlockEntityGroundStorage.GetSlotAt))),
            CodeMatch.IsStloc()
        );
        var targetSlotIndex = matcher.Instruction.LocalIndex();

        //if sneak then droppoff else pickup

        matcher.MatchStartForward(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.ActiveHotbarSlot)))
        );

        matcher.InsertAfterAndAdvance(
            CodeInstruction.LoadArgument(1), // player
            CodeInstruction.LoadLocal(targetSlotIndex), // target)
            CodeInstruction.LoadArgument(0), // groundStorage
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BlockEntityGroundStoragePatches), nameof(GetBestInteractionSlot)))
        );

        return matcher.InstructionEnumeration();
    }

    static ItemSlot GetBestInteractionSlot(ItemSlot activeSlot, IPlayer player, ItemSlot target, BlockEntityGroundStorage groundStorage)
    {
        if(groundStorage is not { StorageProps.Layout: EnumGroundStorageLayout.Stacking or EnumGroundStorageLayout.Messy12 } || groundStorage.Inventory.Empty) return activeSlot;

        if(groundStorage.StorageProps.CtrlKey && !player.Entity.Controls.CtrlKey) return new DummySlot();

        if (player.Entity.Controls.ShiftKey)
        {
            if (activeSlot.Itemstack?.Collectible is ItemDryGrass)
            {
                var itemstack = groundStorage.Inventory[0].Itemstack;
                if(itemstack?.Collectible?.GetCombustibleProperties(player.Entity.World, itemstack, groundStorage.Pos) is { SmeltingType: EnumSmeltType.Fire })
                {
                    return activeSlot;
                }
            }

            return PickupUtil.GetBestSlotForDropoff(activeSlot, player, target.Itemstack);
        }
        else
        {
            return PickupUtil.GetBestSlotForPickup(activeSlot, player, target.Itemstack);
        }
    }

    [HarmonyPatch(typeof(BlockEntityGroundStorage), nameof(BlockEntityGroundStorage.TryPutItem))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SetPriotizedSlot_TryPutItem(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchStartForward(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.ActiveHotbarSlot)))
        );

        matcher.InsertAfterAndAdvance(
            CodeInstruction.LoadArgument(1), // player
            CodeInstruction.LoadArgument(0), // groundStorage
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BlockEntityGroundStoragePatches), nameof(GetBestDropoffSlot)))
        );

        return matcher.InstructionEnumeration();
    }

    static ItemSlot GetBestDropoffSlot(ItemSlot activeSlot, IPlayer player, BlockEntityGroundStorage groundStorage) => PickupUtil.GetBestSlotForDropoff(activeSlot, player, groundStorage.Inventory[0].Itemstack);

    [HarmonyPatch(typeof(BlockEntityGroundStorage), nameof(BlockEntityGroundStorage.TryTakeItem))]
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

    [HarmonyPatch(typeof(BlockEntityGroundStorage), "putOrGetItemStacking")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SetPriotizedSlot_putOrGetItemStacking(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchStartForward(
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.ActiveHotbarSlot)))
        );

        matcher.InsertAfterAndAdvance(
            CodeInstruction.LoadArgument(1), // player
            CodeInstruction.LoadArgument(0), // groundStorage
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BlockEntityGroundStoragePatches), nameof(GetBestInteractionSlot_putOrGetItemStacking)))
        );
        matcher.Advance(1);
        var targetStackIndex = matcher.Instruction.LocalIndex();

        matcher.MatchStartForward(
            CodeMatch.Calls(AccessTools.Method(typeof(BlockGroundStorage), nameof(BlockGroundStorage.CreateStorage)))
        );
        matcher.Instruction.opcode = OpCodes.Call;
        matcher.Instruction.operand = AccessTools.Method(typeof(BlockEntityGroundStoragePatches), nameof(CreateStorage));
        matcher.InsertAndAdvance(
            CodeInstruction.LoadLocal(targetStackIndex)
        );

        return matcher.InstructionEnumeration();
    }

    static ItemSlot GetBestInteractionSlot_putOrGetItemStacking(ItemSlot activeSlot, IPlayer player, BlockEntityGroundStorage groundStorage) => player.Entity.Controls.ShiftKey
            ? PickupUtil.GetBestSlotForDropoff(activeSlot, player, groundStorage.Inventory[0].Itemstack)
            : PickupUtil.GetBestSlotForPickup(activeSlot, player, groundStorage.Inventory[0].Itemstack);

    static bool CreateStorage(BlockGroundStorage groundStorage, IWorldAccessor world, BlockSelection blockSel, IPlayer player, ItemSlot targetSlot)
    {
        if (!world.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak) || targetSlot.Empty)
		{
			targetSlot.MarkDirty();
			return false;
		}
		BlockPos pos = blockSel.Position.AddCopy(blockSel.Face);

		if (pos.Y >= world.BlockAccessor.MapSizeY) return false;
		BlockPos posBelow = pos.DownCopy(1);
		
        if (!world.BlockAccessor.GetBlock(posBelow).CanAttachBlockAt(world.BlockAccessor, groundStorage, posBelow, BlockFacing.UP, null)) return false;

		CollectibleBehaviorGroundStorable behavior = targetSlot.Itemstack.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>();
		if (behavior?.StorageProps is not { Layout: EnumGroundStorageLayout.Messy12 or EnumGroundStorageLayout.Stacking } storageProps || (storageProps.CtrlKey && !player.Entity.Controls.CtrlKey))
		{
			return false;
		}

		BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
		double y = player.Entity.Pos.X - ((double)targetPos.X + blockSel.HitPosition.X);
		double dz = (double)((float)player.Entity.Pos.Z) - ((double)targetPos.Z + blockSel.HitPosition.Z);
		double num = (double)((float)Math.Atan2(y, dz));
		float deg90 = 1.5707964f;
		float roundRad = (float)((int)Math.Round(num / (double)deg90)) * deg90;
		BlockFacing? attachFace = null;

		world.BlockAccessor.SetBlock(groundStorage.BlockId, pos);
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityGroundStorage beg)
        {
            beg.MeshAngle = roundRad;
            beg.AttachFace = attachFace;
            beg.clientsideFirstPlacement = (world.Side == EnumAppSide.Client);
            var targetCode = targetSlot.Itemstack.Collectible.Code;

            ItemSlot invSlot = beg.Inventory[0];
            bool putBulk = player.Entity.Controls.CtrlKey;
            beg.DetermineStorageProperties(targetSlot);
			if (targetSlot.TryPutInto(world, invSlot, putBulk ? beg.BulkTransferQuantity : beg.TransferQuantity) > 0)
			{
				world.PlaySoundAt(behavior.StorageProps.PlaceRemoveSound.WithPathPrefixOnce("sounds/"), (double)pos.X + 0.5, (double)pos.InternalY, (double)pos.Z + 0.5, null, 0.88f + (float)world.Rand.NextDouble() * 0.24f, 16f, 1f);
				beg.LightUpdate(invSlot.Itemstack);
			}
            else
            {
                world.Logger.Error("[pickupartistreforged] failed to put items in newly created ground storage, removing block again.");
                world.BlockAccessor.SetBlock(0, pos);
                return false;
            }
			world.Logger.Audit("{0} Put {1}x{2} into new Ground storage at {3}.",
            [
                player.PlayerName,
				beg.TransferQuantity,
				targetCode,
				pos
			]);
			world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
        }
        
        if (CollisionTester.AabbIntersect(groundStorage.GetCollisionBoxes(world.BlockAccessor, pos)[0], (double)pos.X, (double)pos.Y, (double)pos.Z, player.Entity.SelectionBox, player.Entity.Pos.XYZ))
		{
			player.Entity.Pos.Y += (double)groundStorage.GetCollisionBoxes(world.BlockAccessor, pos)[0].Y2;
		}

        if(player is IClientPlayer clientPlayer)
        {
            clientPlayer.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
        }
		
		return true;
    }
}