using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Common.CommandAbbr;
using Newtonsoft.Json;
using IConfig;


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
        RegisterHITClientCommand(capi); //registers the core client-side command

        _capi.Event.PlayerEntitySpawn += EventOnPlayerEntitySpawn; //runs the method EventOnPlayerEntitySpawn whenever a player joins for that player
        _capi.Event.PlayerEntityDespawn += EventOnPlayerEntityDespawn; //see above and see methods for more info
        ClientChannel = _capi.Network //setting up network for multiplayer
            .RegisterChannel(ChannelName)
            .RegisterMessageType<RequestToolsInfo>()
            .RegisterMessageType<UpdatePlayerTools>()
            .SetMessageHandler<UpdatePlayerTools>(HandleDataFromServer);
    }

    //Happens client-side when a player enters any world
    private void EventOnPlayerEntitySpawn(IClientPlayer byplayer)
    {
        _rendererByPlayer[byplayer.PlayerUID] = new ToolRenderer(_capi, byplayer); //first initializes a new ToolRenderer for the player
        ClientConfig = ModConfig.LoadConfig<HITConfig>(_capi, configFileName); //then reads the client config and sends it in a packet to the server
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
    private void SendClientPacket(IPlayer byplayer, HITConfig clientConfig)
    {
        ClientChannel.SendPacket(new RequestToolsInfo()
        {
            PlayerUid = byplayer.PlayerUID,
            ConfigData = JsonConvert.SerializeObject(clientConfig)
        });
    }

    //Registers the '.hit' client-side command, and all of its sub-commands
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
                .HandleWith((args) => { return OnToggleConfigSetting(capi, args, false); }) //this sets the command to be handled by the 'OnToggleConfigSetting' method below
            .EndSub()
            .BeginSubCommand("enable")
                .WithDescription("Enables certain tool rendering settings")
                .WithArgs(parsers.OptionalWordRange("setting", new string[4] { "arms", "back", "shields", "favorites" } ))
                .HandleWith((args) => { return OnToggleConfigSetting(capi, args, true); }) //both sub-commands are handled by the same handler method
            .EndSub()
            .BeginSubCommand("set-favorites") //sets up a system that prefers specific slots above others instead of marching through the hotbar in order
                .WithDescription("Sets up to 5 favorited hotbar slots for selective tool rendering [Default 1-5]")
                .WithArgs(parsers.OptionalWord("list without spaces"))
                .HandleWith((args) => { return OnSetFavoriteSlots(capi, args); }) //this one has its own handler method
            .EndSub()
            .Validate();
    }

    //Handles the 'enable' and 'disable' sub-commands
    private TextCommandResult OnToggleConfigSetting(ICoreClientAPI capi, TextCommandCallingArgs args, bool toggled)
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
            ModConfig.SaveConfig<HITConfig>(capi, ClientConfig, configFileName); //saving config client-side
            SendClientPacket(player, ClientConfig); //and updating it server-side via packet
            return TextCommandResult.Success($"Rendering settings for {player.PlayerName} successfully updated.");
        }
        else
        {
            return TextCommandResult.Error("Client configs not initialized yet.");
        }
    }

    //Handles the 'set-favorites' sub-commands
    private TextCommandResult OnSetFavoriteSlots(ICoreClientAPI capi, TextCommandCallingArgs args)
    {
        var player = args.Caller.Player;
        if (ClientConfig != null)
        {
            for (int j = 0; j < args.RawArgs.Length || j < 6; j++)
            {
                //casts the index j of the string to an int to be read. An input might be something like, "56812", (even though it's not in order), and it would parse each one into the int array waiting for it in config.
                //ClientConfig.Favorited_Slots[j] = Int32.Parse(args.RawArgs[j]);
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
    public override void StartServerSide(ICoreServerAPI sapi) //handles registration of all server-side events and message handlers
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

        HITConfig configPacket = JsonConvert.DeserializeObject<HITConfig>(packet.ConfigData); //deserialize the config object from the client's data packet
        if (watcher.ClientConfig != configPacket) //this check probably isn't necessary, but oh well, it isn't hurting anything
        {
            watcher.ClientConfig = configPacket;
            watcher.UpdateInventories(0);
            _sapi.Logger.Notification("[HIT] Client config updates registered, passing them on to server...");
        }

        var msg = watcher.GenerateUpdateMessage(); //now we generate a new message containing all of the player's updated tool rendering data
        ServerChannel.SendPacket(msg, fromplayer); //and send it back to the client
    }
    #endregion
}