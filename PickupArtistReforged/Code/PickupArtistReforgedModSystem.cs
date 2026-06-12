using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace PickupArtistReforged.Code;

public partial class PickupArtistReforgedModSystem : ModSystem
{
    public EnumAppSide PresentOn { get; private set; }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "ServerMods")]
    static extern ref List<ModId> GetServerMods(ClientMain clientMain);

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);

        if (api.World is ClientMain client)
        {
            var serverMods = GetServerMods(client);
            PresentOn = serverMods.Any(mod => mod.Id == Mod.Info.ModID) ? EnumAppSide.Universal : EnumAppSide.Client;
        }
        else PresentOn = EnumAppSide.Universal;

        AutoSetup(api);
    }
    
    public override void StartClientSide(ICoreClientAPI api) 
    {
        base.StartClientSide(api);

        api.Input.RegisterHotKey("nohands-clear-active", Lang.Get("pickupartistreforged:hotkey-clear-active"), GlKeys.R, HotkeyType.InventoryHotkeys);
        
        api.Input.SetHotKeyHandler("nohands-clear-active", keyCombination => ClearSlot(api, api.World.Player.InventoryManager.ActiveHotbarSlot));

        api.Input.RegisterHotKey("nohands-clear-both", Lang.Get("pickupartistreforged:hotkey-clear-both"), GlKeys.R, HotkeyType.InventoryHotkeys, false, false, true);
        api.Input.SetHotKeyHandler("nohands-clear-both", keyCombination =>
        {
            var invManager = api.World.Player.InventoryManager;

            return ClearSlot(api, invManager.ActiveHotbarSlot) || ClearSlot(api, invManager.OffhandHotbarSlot);
        });
    }
    
    private static bool ClearSlot(ICoreClientAPI api, ItemSlot slot) 
    {
        if (slot is not { Empty: false }) return false;
        
        var operation = new ItemStackMoveOperation(
          api.World,
          EnumMouseButton.None,
          EnumModifierKey.SHIFT,
          EnumMergePriority.AutoMerge
        );

        var packets = api.World.Player.InventoryManager.TryTransferAway(slot, ref operation, true, true);
        if (packets is null) return false;
        
        foreach (object packet in packets) api.Network.SendPacketClient(packet);

        return true;
    }

    partial void ManualPatches(Harmony harmony, ICoreAPI api)
    {
        if((PresentOn & EnumAppSide.Server) != 0)
        {
            harmony.PatchCategory("ModOnServer");
        }
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);
        AutoAssetsLoaded(api);
    }

    public override void Dispose()
    {
        base.Dispose();
        AutoDispose();
    }

}
