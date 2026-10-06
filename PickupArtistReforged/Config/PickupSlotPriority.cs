using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PickupArtistReforged.Config;

[JsonConverter(typeof(StringEnumConverter))]
public enum PickupSlotPriority
{
    /// <summary>
    /// Hotbar slot with existing stack closest to being full
    /// </summary>
    HotbarSlotClosestToBeingFilled,

    /// <summary>
    /// Currently selected hotbar slot if empty (and capable of holding the item)
    /// </summary>
    ActiveHotbarSlotIfEmpty,

    /// <summary>
    /// Backpack slot with existing stack closest to being full
    /// </summary>
    BackpackSlotClosestToBeingFilled,

    /// <summary>
    /// The best suited slot, this for instance can make stones prefer going into mining backpack slots.
    /// (it uses the base game slot weight system, that other mods may also effect)
    /// </summary>
    BestSuitedSlot,

    /// <summary>
    /// First empty backpack slot
    /// </summary>
    EmptyBackpackSlot,

    /// <summary>
    /// First empty hotbar slot
    /// </summary>
    EmptyHotbarSlot,

    /// <summary>
    /// First available hotbar slot if tool
    /// </summary>
    HotBarSlotIfTool,
}
