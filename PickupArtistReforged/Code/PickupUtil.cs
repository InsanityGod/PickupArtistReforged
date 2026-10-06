using PickupArtistReforged.Config;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace PickupArtistReforged.Code;
public static class PickupUtil
{
    private static TagSet? ToolTag;
    internal static void ClearToolTagCache() => ToolTag = default;
    public static ItemSlot GetBestSlotForPickup(ItemSlot activeSlot, IPlayer player, ItemStack? target)
    {
        if (target is null) return activeSlot;

        if(!PickupArtistReforgedConfig.KnownRemoteConfigs.TryGetValue(player.PlayerUID, out var config))
        {
            player.Entity.Api.Logger.Warning("[pickupartistreforged] client config of player '{0}' is not known by the server, using server defaults", player.PlayerName);
            config = PickupArtistReforgedConfig.LocalInstance!;
        }
        ToolTag ??= player.Entity.Api.CollectibleTagRegistry.CreateTagSet("tool");

        var hotbar = player.InventoryManager.GetHotbarInventory();
        var backpack = player.InventoryManager.GetOwnInventory("backpack");
        var dummySlot = new DummySlot(target);
        ItemSlot? bestSlot;

        foreach(var pickupPriority in config.PickupPriorities)
        {
            switch (pickupPriority)
            {
                case PickupSlotPriority.HotbarSlotClosestToBeingFilled:
                    bestSlot = GetClosestToFullItemSlot(hotbar, target);
                    if (bestSlot is not null) return bestSlot;
                    break;

                case PickupSlotPriority.ActiveHotbarSlotIfEmpty:
                    if (activeSlot is { Empty: true } && activeSlot.CanHold(dummySlot)) return activeSlot;
                    break;

                case PickupSlotPriority.BackpackSlotClosestToBeingFilled:
                    bestSlot = GetClosestToFullItemSlot(backpack, target);
                    if (bestSlot is not null) return bestSlot;
                    break;

                case PickupSlotPriority.BestSuitedSlot:
                    var bestWeightedSlot = backpack.GetBestSuitedSlot(dummySlot);
                    if (bestWeightedSlot?.slot is not null) return bestWeightedSlot.slot;
                    break;

                case PickupSlotPriority.EmptyBackpackSlot:
                    bestSlot = GetFirstEmptySlot(backpack, dummySlot);
                    if (bestSlot is not null) return bestSlot;
                    break;

                case PickupSlotPriority.EmptyHotbarSlot:
                    bestSlot = GetFirstEmptySlot(hotbar, dummySlot);
                    if (bestSlot is not null) return bestSlot;
                    break;

                case PickupSlotPriority.HotBarSlotIfTool:
                    if(target.Collectible.Tool is null && !target.Collectible.GetTags(target).Overlaps(ToolTag.Value)) break;

                    bestSlot = GetFirstEmptySlot(hotbar, dummySlot);
                    if (bestSlot is not null) return bestSlot;
                    break;
            }
        }

        return activeSlot;
    }

    //TODO test
    public static ItemSlot GetBestSlotForDropoff(ItemSlot activeSlot, IPlayer player, ItemStack? target)
    {
        if (target is null) return activeSlot;

        if(!PickupArtistReforgedConfig.KnownRemoteConfigs.TryGetValue(player.PlayerUID, out var config))
        {
            player.Entity.Api.Logger.Warning("[pickupartistreforged] client config of player '{0}' is not known by the server, using server defaults", player.PlayerName);
            config = PickupArtistReforgedConfig.LocalInstance!;
        }
        
        var hotbar = player.InventoryManager.GetHotbarInventory();
        var backpack = player.InventoryManager.GetOwnInventory("backpack");
        ItemSlot? bestSlot;

        foreach(var pickupPriority in config.DropoffPriorities)
        {
            switch (pickupPriority)
            {
                case DropoffSlotPriority.ActiveHotbarSlotIfMatching:
                    if (!activeSlot.Empty && target.Collectible.Equals(activeSlot.Itemstack, target, GlobalConstants.IgnoredStackAttributes))
                    {
                        return activeSlot;
                    }
                    break;

                case DropoffSlotPriority.BackpackslotClosestToEmpty:
                    bestSlot = GetSlotWithLeastItems(backpack, target);
                    if (bestSlot is not null) return bestSlot;
                    break;

                case DropoffSlotPriority.HotbarslotClosestToEmpty:
                    bestSlot = GetSlotWithLeastItems(hotbar, target);
                    if (bestSlot is not null) return bestSlot;
                    break;
            }
        }

        return activeSlot;
    }

    public static bool TryGiveItemStack(IPlayer player, ItemStack stack, bool slotNotifyEffect, ItemSlot? targetSlot)
    {
        var dummySlot = new DummySlot(stack);
        ItemSlot? previousTargetSlot = null;
        do
        {
            if(targetSlot is null)
            {
                targetSlot = GetBestSlotForPickup(previousTargetSlot ?? player.InventoryManager.ActiveHotbarSlot, player, stack);
                if(targetSlot is null || targetSlot == previousTargetSlot)
                {
                    player.InventoryManager.TryGiveItemstack(stack, slotNotifyEffect);
                    break;
                }
            }
             // do we need the onitemgrabbed event?
            if(dummySlot.TryPutInto(player.Entity.World, targetSlot, stack.StackSize) > 0 && slotNotifyEffect)
            {
                targetSlot.MarkDirty();
                player.InventoryManager.NotifySlot(player, targetSlot);
                if(targetSlot == player.InventoryManager.ActiveHotbarSlot) player.InventoryManager.BroadcastHotbarSlot();
            }

            previousTargetSlot = targetSlot;
            targetSlot = null;
        }
        while(stack.StackSize > 0);

        return stack.StackSize <= 0;
    }

    private static ItemSlot? GetFirstEmptySlot(IEnumerable<ItemSlot> slots, DummySlot target)
    {
        foreach (var slot in slots)
        {
            if (slot is { Empty: true } && slot.CanHold(target)) return slot;
        }

        return null;
    }

    private static ItemSlot? GetSlotWithLeastItems(IEnumerable<ItemSlot> slots, ItemStack target)
    {
        ItemSlot? bestSlot = null;
        int lowestStackCount = int.MaxValue;
        foreach (var slot in slots)
        {
            if (slot is null || slot.Empty || !target.Collectible.Equals(slot.Itemstack, target, GlobalConstants.IgnoredStackAttributes)) continue;

            if (slot.StackSize < lowestStackCount)
            {
                lowestStackCount = slot.StackSize;
                bestSlot = slot;
            }
        }

        return bestSlot;
    }

    private static ItemSlot? GetClosestToFullItemSlot(IEnumerable<ItemSlot> slots, ItemStack target)
    {
        ItemSlot? bestSlot = null;
        int lowestMissingForFullSlot = int.MaxValue;
        foreach (var slot in slots)
        {
            if (slot is not { Empty: false } || !target.Collectible.Equals(slot.Itemstack, target, GlobalConstants.IgnoredStackAttributes)) continue;

            var missingForFullSlot = MissingForFullSlot(slot, target);
            if (missingForFullSlot > 0 && missingForFullSlot < lowestMissingForFullSlot)
            {
                lowestMissingForFullSlot = missingForFullSlot;
                bestSlot = slot;
            }
        }

        return bestSlot;
    }

    public static int MissingForFullSlot(ItemSlot sinkSlot, ItemStack target) => Math.Min(sinkSlot.GetRemainingSlotSpace(target), target.Collectible.MaxStackSize - sinkSlot.StackSize);
}
