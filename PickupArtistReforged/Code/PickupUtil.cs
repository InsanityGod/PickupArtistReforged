using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace PickupArtistReforged.Code;
public static class PickupUtil
{

    public static ItemSlot GetBestSlotForPickup(ItemSlot activeSlot, IPlayer player, ItemStack? target)
    {
        if (target is null) return activeSlot;
        // 1. Hotbar slot with existing stack closest to being full
        var hotbar = player.InventoryManager.GetHotbarInventory();
        var bestSlot = GetClosestToFullItemSlot(hotbar, target);
        if (bestSlot is not null) return bestSlot;

        var dummySlot = new DummySlot(target);

        // 2. Active hotbar slot if it has space
        if (activeSlot is { Empty: true} && activeSlot.CanHold(dummySlot)) return activeSlot;

        // 3. Backpack slot with existing stack closest to being full
        var backpack = player.InventoryManager.GetOwnInventory("backpack");
        bestSlot = GetClosestToFullItemSlot(backpack, target);
        if (bestSlot is not null) return bestSlot;

        // 4. Best Suited backpack slot
        var bestWeightedSlot = backpack.GetBestSuitedSlot(dummySlot);
        if (bestWeightedSlot?.slot is not null) return bestWeightedSlot.slot;

        // 5. Empty backpack slot
        bestSlot = GetFirstEmptySlot(backpack, dummySlot);
        if (bestSlot is not null) return bestSlot;

        // 6. Empty hotbar slot
        bestSlot = GetFirstEmptySlot(hotbar, dummySlot);
        if (bestSlot is not null) return bestSlot;

        return activeSlot;
    }

    public static ItemSlot GetBestSlotForDropoff(ItemSlot activeSlot, IPlayer player, ItemStack? target)
    {
        if (target is null) return activeSlot;

        // 1. Active hotbar slot if it has matching item
        if(!activeSlot.Empty && target.Collectible.Equals(activeSlot.Itemstack, target, GlobalConstants.IgnoredStackAttributes))
        {
            return activeSlot;
        }

        // 2. Backpack slot with smallest partial stack
        var backpack = player.InventoryManager.GetOwnInventory("backpack");
        var bestSlot = GetSlotWithLeastItems(backpack, target);
        if (bestSlot is not null) return bestSlot;

        // 3. Hotbar slot with smallest partial stack
        var hotbar = player.InventoryManager.GetHotbarInventory();
        bestSlot = GetSlotWithLeastItems(hotbar, target);
        if (bestSlot is not null) return bestSlot;

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
