using static Elephant.HIT.ModConstants;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Common.CommandAbbr;
using Newtonsoft.Json;
using Elephant.Configuration;
using System.Linq;
using Elephant.Extensions;
using Vintagestory.API.Util;

namespace Elephant.HIT;
public class ModMain : ModSystem
{
    public const int TotalSlots = 5;
    public const int ShieldSlotId = 4;

    private static ICoreClientAPI _capi = null!;
    private static ICoreServerAPI _sapi = null!;

    private readonly Dictionary<string, ToolRenderer> _rendererByPlayer = new();
    private readonly Dictionary<string, PlayerToolWatcher> _watcherByPlayer = new();

    internal static IClientNetworkChannel ClientChannel = null!;
    internal static IServerNetworkChannel ServerChannel = null!;

    public override double ExecuteOrder() => 0.01;
    public override void StartPre(ICoreAPI api)
    {
        InitHelpers(this);

        base.StartPre(api);
    }

    //Handles registration of all client-side events and message handlers
    public override void StartClientSide(ICoreClientAPI capi)
    {
        _capi = capi;

        _capi.Event.PlayerEntitySpawn += EventOnPlayerEntitySpawn;
        _capi.Event.PlayerEntityDespawn += EventOnPlayerEntityDespawn;
        ClientChannel = _capi.Network
            .RegisterChannel(NETWORK_CHANNEL_MAIN)
            .RegisterMessageType<RequestToolsInfo>()
            .RegisterMessageType<UpdatePlayerTools>()
            .SetMessageHandler<UpdatePlayerTools>(HandleDataFromServer);
    }

    //Happens client-side when a player enters any world
    private void EventOnPlayerEntitySpawn(IClientPlayer byplayer)
    {
        _rendererByPlayer[byplayer.PlayerUID] = new ToolRenderer(_capi, byplayer); //first initializes a new ToolRenderer for the player
        _capi.Event.PushEvent(EventIDs.Client_Send_Config);
    }

    //Happens client-side when a player leaves any world
    private void EventOnPlayerEntityDespawn(IClientPlayer byplayer)
    {
        if (!_rendererByPlayer.TryGetValue(byplayer.PlayerUID, out var renderer)) return;

        renderer.Dispose(); //if the player has a ToolRenderer, we dispose of it upon log-out
        _rendererByPlayer.Remove(byplayer.PlayerUID); //and remove it from the renderer array
    }

    //Handles network messages sent to the client from the server
    private void HandleDataFromServer(UpdatePlayerTools packet)
    {
        if (_rendererByPlayer.TryGetValue(packet.PlayerUid, out var renderer))
        {
            renderer.UpdateRenderedTools(packet); //every time the server sends an updated tool packet, we update the rendered tools on the client
        }
    }

    //Saves the local config and sends a new packet to the server
    //Returns a 'success' string for use in commands
    /*public static string PlayerConfigsUpdated(IPlayer byplayer)
    {
        var configInfo = ConfigManager.ConfigInfo[0];
        ConfigHelper.WriteConfig<ClientConfig>(_capi, (ClientConfig)ConfigManager.ConfigsByName.FirstOrDefault); //updates the config file client-side
        ConfigManager.SendClientConfig(byplayer); //updates it server-side via packet
        return $"Config settings for {byplayer.PlayerName} successfully updated.";
    }*/

    //Handles registration of all server-side events and message handlers
    public override void StartServerSide(ICoreServerAPI sapi) 
    {
        _sapi = sapi;

        _sapi.Event.PlayerNowPlaying += EventOnPlayerNowPlaying;
        _sapi.Event.PlayerDisconnect += EventOnPlayerDisconnect;
        ServerChannel = _sapi.Network
            .RegisterChannel(NETWORK_CHANNEL_MAIN)
            .RegisterMessageType<RequestToolsInfo>()
            .RegisterMessageType<UpdatePlayerTools>()
            .SetMessageHandler<RequestToolsInfo>(HandleClientDataRequest);
    }

    //Creates a new PlayerToolWatcher object for each player upon joining a server.
    //The PlayerToolWatcher receives data from each client and tracks their tool rendering for all other players
    private void EventOnPlayerNowPlaying(IServerPlayer byplayer)
    {
        _watcherByPlayer[byplayer.PlayerUID] = new PlayerToolWatcher(byplayer);
    }

    //Disposes of the player's ToolWatcher on disconnect
    private void EventOnPlayerDisconnect(IServerPlayer byplayer)
    {
        if (!_watcherByPlayer.TryGetValue(byplayer.PlayerUID, out var watcher)) return;

        watcher.Dispose();
        _watcherByPlayer.Remove(byplayer.PlayerUID);
    }

    //Handles network messages sent to the server from the client
    private void HandleClientDataRequest(IServerPlayer fromplayer, RequestToolsInfo packet)
    {
        if (!_watcherByPlayer.TryGetValue(packet.PlayerUid, out var watcher)) return; //if the player does not have a watcher, skip

        ConfigManager.ClientConfig = JsonConvert.DeserializeObject<ClientConfig>(packet.ConfigData);
        watcher.ClientConfig = ConfigManager.ClientConfig;
        watcher.UpdateInventories(0);
        _sapi.Log("Client config updates registered, passing them on to server...");

        var msg = watcher.GenerateUpdateMessage(); //generates a new message containing all of the player's updated tool rendering data
        ServerChannel.SendPacket(msg, fromplayer); //and sends it back to the client
    }

    internal static void InitHelpers(ModSystem modMain)
    {
        ModConstants.Init(modMain.Mod.Info);
    }
}