﻿using static Ele.HIT.ModConstants;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Common.CommandAbbr;
using Newtonsoft.Json;
using Ele.Configuration;


namespace Ele.HIT;
public class ModMain : ModSystem
{
    public const int TotalSlots = 5;
    public const int ShieldSlotId = 4;

    private ICoreClientAPI _capi = null!;
    private ICoreServerAPI _sapi = null!;

    private readonly Dictionary<string, ToolRenderer> _rendererByPlayer = new();
    private readonly Dictionary<string, PlayerToolWatcher> _watcherByPlayer = new();
    public static ModConfig ClientConfig;

    internal IClientNetworkChannel ClientChannel = null!;
    internal static IServerNetworkChannel ServerChannel = null!;

    public readonly Dictionary<int, int> HotbarMap = new Dictionary<int, int>(){
        {1, 0},
        {2, 1},
        {3, 2},
        {4, 3},
        {5, 4},
        {6, 5},
        {7, 6},
        {8, 7},
        {9, 8},
        {0, 9}
    };

    #region Client
    //Handles registration of all client-side events and message handlers
    public override void StartClientSide(ICoreClientAPI capi)
    {
        _capi = capi;
        ClientConfig = ConfigHelper.ReadConfig<ModConfig>(capi); //initialize the config client-side
        if (capi.ModLoader.IsModEnabled("configlib"))
        {
            capi.Logger.Notification("[HIT] Initializing configlib support...");
            _ = new ConfigLibCompat(capi);
        }
        RegisterHITClientCommand(capi); //registers the core client-side command

        _capi.Event.PlayerEntitySpawn += EventOnPlayerEntitySpawn;
        _capi.Event.PlayerEntityDespawn += EventOnPlayerEntityDespawn;
        ClientChannel = _capi.Network
            .RegisterChannel(mainChannel)
            .RegisterMessageType<RequestToolsInfo>()
            .RegisterMessageType<UpdatePlayerTools>()
            .SetMessageHandler<UpdatePlayerTools>(HandleDataFromServer);
    }

    //Happens client-side when a player enters any world
    private void EventOnPlayerEntitySpawn(IClientPlayer byplayer)
    {
        _rendererByPlayer[byplayer.PlayerUID] = new ToolRenderer(_capi, byplayer); //first initializes a new ToolRenderer for the player
        ClientConfig = ConfigHelper.LoadConfig<ModConfig>(_capi); //then reads the client config and sends it in a packet to the server
        SendClientPacket(byplayer, ClientConfig);
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

    //Sends player data and client-side configs to the server as a serialized packet
    private void SendClientPacket(IPlayer byplayer, ModConfig clientConfig)
    {
        ClientChannel.SendPacket(new RequestToolsInfo()
        {
            PlayerUid = byplayer.PlayerUID,
            ConfigData = JsonConvert.SerializeObject(clientConfig)
        });
    }

    //Saves the local config and sends a new packet to the server
    //Returns a 'success' string for use in commands
    private string PlayerConfigsUpdated(IPlayer byplayer)
    {
        ConfigHelper.WriteConfig<ModConfig>(_capi, ClientConfig); //saves the config file client-side
        SendClientPacket(byplayer, ClientConfig); //updates it server-side via packet
        return $"Config settings for {byplayer.PlayerName} successfully updated.";
    }
    #endregion

    #region Commands
    //Registers the '.hit' client-side command and all of its sub-commands
    private void RegisterHITClientCommand(ICoreClientAPI capi)
    {
        capi.Logger.Notification("[HIT] Registering client-side rendering command.");
        CommandArgumentParsers parsers = capi.ChatCommands.Parsers;
        //the command format is kinda tricky. See the Wiki or Vanilla's commands for worldedit to see the full variety of types
        capi.ChatCommands.Create("hit")
            .WithDescription("hit parent command") //description is for the command handbook
            .RequiresPrivilege(Privilege.chat) //doesnt work for muted players 
            .RequiresPlayer() //important, unless your commands are run through console
            .BeginSubCommand("disable")
                .WithDescription("Disables certain tool rendering settings")
                .WithArgs(parsers.OptionalWordRange("setting", new string[4] { "arms", "back", "shields", "favorites" }))
                .HandleWith((args) => { return OnToggleConfigSetting(args, false); }) //this sets the command to be handled by the 'OnToggleConfigSetting' method below
            .EndSub()
            .BeginSubCommand("enable")
                .WithDescription("Enables certain tool rendering settings")
                .WithArgs(parsers.OptionalWordRange("setting", new string[4] { "arms", "back", "shields", "favorites" }))
                .HandleWith((args) => { return OnToggleConfigSetting(args, true); }) //both sub-commands are handled by the same handler method
            .EndSub()
            .BeginSubCommand("set-favorites") //sets up a system that prefers specific slots above others instead of marching through the hotbar in order
                .WithDescription("Sets up to 5 (optional) favorited hotbar slots for selective tool rendering")
                .WithArgs(
                    parsers.OptionalIntRange("slot 1", 0, 9, -1),
                    parsers.OptionalIntRange("slot 2", 0, 9, -1),
                    parsers.OptionalIntRange("slot 3", 0, 9, -1),
                    parsers.OptionalIntRange("slot 4", 0, 9, -1),
                    parsers.OptionalIntRange("slot 5", 0, 9, -1))
                .HandleWith(new OnCommandDelegate(OnSetFavoriteSlots)) //standalone method, args are automatically passed as the only parameter
            .EndSub()
            .BeginSubCommand("reset")
                .WithDescription("Resets all rendering settings to their default values")
                .HandleWith(new OnCommandDelegate(OnConfigReset)) //standalone method, args are automatically passed as the only parameter
            .EndSub()
            .Validate();
    }

    //Handles the 'enable' and 'disable' sub-commands
    private TextCommandResult OnToggleConfigSetting(TextCommandCallingArgs args, bool toggled)
    {
        var player = args.Caller.Player;
        if (ClientConfig != null) //if the player hasn't registered their client-side config, throw an error
        {
            var ChangedSetting = (string)args[0];
            switch (ChangedSetting) //switches through all valid sub-command arguments and sets the appropriate config value
            {
                case "arms":
                    ClientConfig.Forearm_Tools_Enabled = toggled;
                    break;
                case "back":
                    ClientConfig.Tools_On_Back_Enabled = toggled;
                    break;
                case "shields":
                    ClientConfig.Shields_Enabled = toggled;
                    break;
                case "favorites":
                    ClientConfig.Favorited_Slots_Enabled = toggled;
                    break;
                default:
                    return TextCommandResult.Error("");
            }
            return TextCommandResult.Success(PlayerConfigsUpdated(player));
        }
        else
        {
            return TextCommandResult.Error("Client configs not initialized yet.");
        }
    }

    //Handles the 'set-favorites' sub-command
    private TextCommandResult OnSetFavoriteSlots(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player;
        if (ClientConfig != null)
        {
            for (int j = 0; j < args.ArgCount; j++) //iterates through each arg, the total of which will always be 5 (will return -1 as a default value if none provided)
            {
                if ((int)args[j] != -1) //if an int was provided, update the index in the Favorited_Slots config array using the HotbarMap
                {
                    ClientConfig.Favorited_Slots[j] = HotbarMap[(int)args[j]];
                } 
                else //otherwise, updates the Favorited_Slots index to -1 to disable it
                {
                    ClientConfig.Favorited_Slots[j] = -1;
                }
            }
            return TextCommandResult.Success(PlayerConfigsUpdated(player));
        }
        else
        {
            return TextCommandResult.Error("No client configs available.");
        }
    }

    //A simple handler method that resets all config values by regenerating a new config
    private TextCommandResult OnConfigReset(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player;
        if (ClientConfig != null)
        {
            ClientConfig = new ModConfig(_capi);
            PlayerConfigsUpdated(player);
            return TextCommandResult.Success($"Config settings reset to default.");
        }
        else
        {
            return TextCommandResult.Error("No client configs available.");
        }
    }
    #endregion

    #region Server
    //Handles registration of all server-side events and message handlers
    public override void StartServerSide(ICoreServerAPI sapi) 
    {
        _sapi = sapi;
        _sapi.Event.PlayerNowPlaying += EventOnPlayerNowPlaying;
        _sapi.Event.PlayerDisconnect += EventOnPlayerDisconnect;
        ServerChannel = _sapi.Network
            .RegisterChannel(mainChannel)
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

        ModConfig configPacket = JsonConvert.DeserializeObject<ModConfig>(packet.ConfigData); //deserialize the config object from the client's data packet
        if (watcher.ClientConfig != configPacket) //this check probably isn't necessary, but oh well, it isn't hurting anything
        {
            watcher.ClientConfig = configPacket;
            watcher.UpdateInventories(0);
            _sapi.Logger.Notification("[HIT] Client config updates registered, passing them on to server...");
        }

        var msg = watcher.GenerateUpdateMessage(); //generates a new message containing all of the player's updated tool rendering data
        ServerChannel.SendPacket(msg, fromplayer); //and sends it back to the client
    }
    #endregion
}