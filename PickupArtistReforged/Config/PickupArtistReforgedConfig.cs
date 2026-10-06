using InsanityLib.Generators.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ProtoBuf;
using System.Collections.Generic;

namespace PickupArtistReforged.Config;

//TODO ArtOfGrowing compat?
//TODO maybe rework client -> server configs into a standard approach built into InsanityLib
[ProtoContract] public sealed class ConfigRequest;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class PickupArtistReforgedConfig
{
    [AutoConfig("PickupArtistReforged.json", ServerSync = false)]
    public static PickupArtistReforgedConfig? LocalInstance { get; internal set; }

    [AutoClear]
    public static Dictionary<string, PickupArtistReforgedConfig> KnownRemoteConfigs { get; set; } = [];

    public PickupSlotPriority[] PickupPriorities { get; set; } = [
        PickupSlotPriority.HotbarSlotClosestToBeingFilled,
        PickupSlotPriority.ActiveHotbarSlotIfEmpty,
        PickupSlotPriority.BackpackSlotClosestToBeingFilled,
        PickupSlotPriority.BestSuitedSlot,
        PickupSlotPriority.EmptyBackpackSlot,
        PickupSlotPriority.EmptyHotbarSlot,
    ];

    public DropoffSlotPriority[] DropoffPriorities { get; set; } = [
        DropoffSlotPriority.ActiveHotbarSlotIfMatching,
        DropoffSlotPriority.BackpackslotClosestToEmpty,
        DropoffSlotPriority.HotbarslotClosestToEmpty
    ];
}