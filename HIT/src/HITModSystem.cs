using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Common.CommandAbbr;
using IConfig;
using System;

namespace HIT;
public class HITModSystem : ModSystem
{
    public const int TotalSlots = 5;
    public const int ShieldSlotId = 4;

    private const string configFileName = "harpers_immersive_tools.json";
    private const string ChannelName = "harpers_tools_mod";
    private ICoreClientAPI _capi = null!;
    private ICoreServerAPI _sapi = null!;

    private readonly Dictionary<string, ToolRenderer> _rendererByPlayer = new();
    private readonly Dictionary<string, PlayerToolWatcher> _watcherByPlayer = new();
    private static HITConfig ClientConfig;

    internal IClientNetworkChannel ClientChannel = null!;
    internal static IServerNetworkChannel ServerChannel = null!;

    public override void StartPre(ICoreAPI api)
    {
        if (api.Side == EnumAppSide.Client)
            ClientConfig = ModConfig.ReadConfig<HITConfig>(api, configFileName); //initialize the config client-side
    }

    #region Client
    public override void StartClientSide(ICoreClientAPI capi)
    {
        _capi = capi;
        RegisterHITClientCommand(capi); //register commands needs to happen once players join

        _capi.Event.PlayerEntitySpawn += EventOnPlayerEntitySpawn; //runs the method EventOnPlayerEntitySpawn whenever a player joins for that player
        _capi.Event.PlayerEntityDespawn += EventOnPlayerEntityDespawn; //see above and see methods for more info
        ClientChannel = _capi.Network //setting up network for multiplayer
            .RegisterChannel(ChannelName)
            .RegisterMessageType<RequestToolsInfo>()
            .RegisterMessageType<UpdatePlayerTools>()
            .SetMessageHandler<UpdatePlayerTools>(HandleDataFromServer);
    }
    private void EventOnPlayerEntitySpawn(IClientPlayer byplayer) 
    {
        //this loads the config for each player and immediately requests the tool info of the player. If we worked around the event of the player join, 
        //their tools would only render to others / themselves when they UPDATED their inventory.
        ClientConfig = ModConfig.LoadConfig<HITConfig>(_capi, configFileName);
        _rendererByPlayer[byplayer.PlayerUID] = new ToolRenderer(_capi, byplayer);
        ClientChannel.SendPacket(new RequestToolsInfo()
        {
            PlayerUid = byplayer.PlayerUID
        });
    }

    private void EventOnPlayerEntityDespawn(IClientPlayer byplayer)
    {
        //disposing meshes of the player when they leave to prevent memory leaks 
        if (!_rendererByPlayer.TryGetValue(byplayer.PlayerUID, out var renderer)) return;
        renderer.Dispose();

        _rendererByPlayer.Remove(byplayer.PlayerUID);

    }

    private void HandleDataFromServer(UpdatePlayerTools packet)
    {
        //just a handler for updating the tools whenever the server sends out an update to other players
        if (_rendererByPlayer.TryGetValue(packet.PlayerUid, out var renderer))
        {
            renderer.UpdateRenderedTools(packet);
        }
    }

    private void RegisterHITClientCommand(ICoreClientAPI capi)
    {
        //registering the command
        capi.Logger.Notification("[HIT] Registering client-side rendering command.");
        CommandArgumentParsers parsers = capi.ChatCommands.Parsers;
        //the command format is kinda tricky. See the Wiki or Vanilla's commands for worldedit to see the full variety of types
        capi.ChatCommands.Create("hit") 
            .WithDescription("hit parent command") //description is for the command handbook
            .RequiresPrivilege(Privilege.chat) //doesnt work for muted players 
            .RequiresPlayer() //important, unless your commands are run through console
            .BeginSubCommand("disable") //keyword for fetching later, player literally writes this
                .WithDescription("Disables certain tool rendering settings")
                .WithArgs(parsers.OptionalWordRange("setting", new string[4] { "arms", "back", "shields", "favorites" }))
                .HandleWith((args) => { return OnToggleConfigSetting(capi, args, false); }) //this points to a different method that takes whatever the player sent and does whatever it needs to (in this case disables certain sheaths)
            .EndSub()
            .BeginSubCommand("enable")//keyword for fetching later, player literally writes this
                .WithDescription("Enables certain tool rendering settings")
                .WithArgs(parsers.OptionalWordRange("setting", new string[4] { "arms", "back", "shields", "favorites" } ))
                .HandleWith((args) => { return OnToggleConfigSetting(capi, args, true); }) //see above comment on pointing to a method
            .EndSub()
            .BeginSubCommand("set-favorites") //sets up a system that prefers specific slots above others instead of marching through the hotbar in order
                .WithDescription("Sets up to 5 favorited hotbar slots for selective tool rendering [Default 1-5]")
                .WithArgs(parsers.OptionalWord("list without spaces"))
                .HandleWith((args) => { return OnSetFavoriteSlots(capi, args); })
            .EndSub()
            .Validate();
    }

    private TextCommandResult OnToggleConfigSetting(ICoreClientAPI capi, TextCommandCallingArgs args, bool toggled) //necessary for commands, this is the method that handles the arguments of the command
    {
        //this part is just updating the config
        var player = args.Caller.Player;
        if (_watcherByPlayer.TryGetValue(player.PlayerUID, out var watcher))
        {
            var ChangedSetting = (string)args[0];
            switch (ChangedSetting)
            {
                case "arms":
                    watcher.ClientConfig.Forearm_Tools_Enabled = toggled;
                    break;
                case "back":
                    watcher.ClientConfig.Tools_On_Back_Enabled = toggled;
                    break;
                case "shields":
                    watcher.ClientConfig.Shields_Enabled = toggled;
                    break;
                case "favorites":
                    watcher.ClientConfig.Favorited_Slots_Enabled = toggled;
                    break;
                default:
                    return TextCommandResult.Error("");
            }
            ModConfig.SaveConfig<HITConfig>(capi, watcher.ClientConfig, configFileName); //saving config
            //you can't easily send a packet after this to update it, so it's best to just have it change when the player does.
            return TextCommandResult.Success($"Rendering settings for {player.PlayerName} successfully updated. Will take effect on hotbar refresh.");
        }
        else
        {
            return TextCommandResult.Error("No client configs available.");
        }
    }

    private TextCommandResult OnSetFavoriteSlots(ICoreClientAPI capi, TextCommandCallingArgs args) //other handler for config
    {
        var player = args.Caller.Player;
        if (_watcherByPlayer.TryGetValue(player.PlayerUID, out var watcher))
        {
            for (int j = 0; j < args.RawArgs.Length || j < 6; j++)
            {
                watcher.ClientConfig.Favorited_Slots[j] = Int32.Parse(args.RawArgs[j]);
                //casts the index j of the string to an int to be read. An input might be something like, "56812", (even though it's not in order), and it would parse each one into the int array waiting for it in config.
            }
            return TextCommandResult.Success();
        }
        else
        {
            return TextCommandResult.Error("No client configs available.");
        }
    }
    #endregion

    #region Server
    public override void StartServerSide(ICoreServerAPI sapi) //see start client side, same thing.
    {
        _sapi = sapi;
        _sapi.Event.PlayerNowPlaying += EventOnPlayerNowPlaying;
        _sapi.Event.PlayerDisconnect += EventOnPlayerDisconnect;
        ServerChannel = _sapi.Network
            .RegisterChannel(ChannelName)
            .RegisterMessageType<RequestToolsInfo>()
            .RegisterMessageType<UpdatePlayerTools>()
            .SetMessageHandler<RequestToolsInfo>(HandleClientDataRequest);
    }
    //dispose of the updater when the player disconnects
    private void EventOnPlayerDisconnect(IServerPlayer byplayer)
    {
        if (!_watcherByPlayer.TryGetValue(byplayer.PlayerUID, out var watcher)) return;

        watcher.Dispose();
        _watcherByPlayer.Remove(byplayer.PlayerUID);
    }
    //set up dictionary of the updaters to a their playerUID (would get overwritten if they've joined previously)
    private void EventOnPlayerNowPlaying(IServerPlayer byplayer)
    {
        _watcherByPlayer[byplayer.PlayerUID] = new PlayerToolWatcher(byplayer, ClientConfig);
    }
    //handles the request through the network the client makes
    private void HandleClientDataRequest(IServerPlayer fromplayer, RequestToolsInfo packet)
    {
        if (!_watcherByPlayer.TryGetValue(packet.PlayerUid, out var watcher)) return; //if the player does not have a watcher, skip

        var msg = watcher.GenerateUpdateMessage(); //update message just resends all the info again
        ServerChannel.SendPacket(msg, fromplayer); //sends the message
    }
    #endregion
}