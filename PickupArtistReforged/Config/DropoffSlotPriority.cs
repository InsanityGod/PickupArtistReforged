using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PickupArtistReforged.Config;

[JsonConverter(typeof(StringEnumConverter))]
public enum DropoffSlotPriority
{
    /// <summary>
    /// Active hotbar slot if it has matching item
    /// </summary>
    ActiveHotbarSlotIfMatching,

    /// <summary>
    /// Backpack slot with existing stack that is closest to being emptied
    /// </summary>
    BackpackslotClosestToEmpty,
    
    /// <summary>
    /// Hotbar slot with existing stack that is closest to being emptied
    /// </summary>
    HotbarslotClosestToEmpty
}
