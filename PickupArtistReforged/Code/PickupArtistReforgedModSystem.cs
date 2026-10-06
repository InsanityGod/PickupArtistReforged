using InsanityLib.Generators.Attributes;
using Newtonsoft.Json;
using PickupArtistReforged.Config;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PickupArtistReforged.Code;
public partial class PickupArtistReforgedModSystem : ModSystem
{
    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        AutoSetup(api);
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);
        AutoAssetsLoaded(api);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        api.Event.PlayerDisconnect += OnPlayerDisconect;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        api.ChatCommands.Create("pickupartistreforged")
            .RequiresPrivilege(Privilege.chat)
            .BeginSubCommand("reloadconfig")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ReloadConfig)
            .EndSubCommand();
    }

    private TextCommandResult ReloadConfig(TextCommandCallingArgs? args = null)
    {
        try
        {
            var config = _api!.LoadModConfig("PickupArtistReforged.json");
            if(config?.Token is null) return TextCommandResult.Error("Config file not found");

            using var reader = config.Token.CreateReader();
            JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace,
            }).Populate(reader, PickupArtistReforgedConfig.LocalInstance!);
            SendConfigToServer();
        }
        catch(Exception ex)
        {
            return TextCommandResult.Error($"An unnexpected error occured: {ex}");
        }

        return TextCommandResult.Success("Config has been reloaded");
    }

    private static void OnPlayerDisconect(IServerPlayer byPlayer) => PickupArtistReforgedConfig.KnownRemoteConfigs.Remove(byPlayer.PlayerUID);

    [AutoNetworkMessage]
    private void SendConfigToServer(ConfigRequest? packet = null)
    {
        if(_api is not ICoreClientAPI capi) return;
        capi.Network.GetChannel("pickupartistreforged").SendPacket(PickupArtistReforgedConfig.LocalInstance);
    }

    [AutoNetworkMessage]
    private static void OnConfigReceived(IServerPlayer player, PickupArtistReforgedConfig config) => PickupArtistReforgedConfig.KnownRemoteConfigs[player.PlayerUID] = config;

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        if(api is ICoreClientAPI capi) capi.Network.GetChannel("pickupartistreforged").SendPacket(PickupArtistReforgedConfig.LocalInstance);
    }

    public override void Dispose()
    {
        base.Dispose();
        AutoDispose();
        PickupUtil.ClearToolTagCache();

        if(_api is ICoreServerAPI sapi)
        {
            sapi.Event.PlayerDisconnect -= OnPlayerDisconect;
        }
    }

}
