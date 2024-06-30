using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Common.CommandAbbr;
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
        RegisterHITClientCommand(capi);

        _capi.Event.PlayerEntitySpawn += EventOnPlayerEntitySpawn;
        _capi.Event.PlayerEntityDespawn += EventOnPlayerEntityDespawn;
        ClientChannel = _capi.Network
            .RegisterChannel(ChannelName)
            .RegisterMessageType<RequestToolsInfo>()
            .RegisterMessageType<UpdatePlayerTools>()
            .SetMessageHandler<UpdatePlayerTools>(HandleDataFromServer);
    }
    private void EventOnPlayerEntitySpawn(IClientPlayer byplayer)
    {
        ClientConfig = ModConfig.LoadConfig<HITConfig>(_capi, configFileName);
        _rendererByPlayer[byplayer.PlayerUID] = new ToolRenderer(_capi, byplayer, ClientConfig);
        ClientChannel.SendPacket(new RequestToolsInfo()
        {
            PlayerUid = byplayer.PlayerUID
        });
    }

    private void EventOnPlayerEntityDespawn(IClientPlayer byplayer)
    {
        if (!_rendererByPlayer.TryGetValue(byplayer.PlayerUID, out var renderer)) return;
        renderer.Dispose();

        _rendererByPlayer.Remove(byplayer.PlayerUID);

    }

    private void HandleDataFromServer(UpdatePlayerTools packet)
    {
        if (_rendererByPlayer.TryGetValue(packet.PlayerUid, out var renderer))
        {
            renderer.UpdateRenderedTools(packet);
        }
    }

    private void RegisterHITClientCommand(ICoreClientAPI capi)
    {
        capi.Logger.Notification("[HIT] Registering client-side rendering command.");
        CommandArgumentParsers parsers = capi.ChatCommands.Parsers;

        capi.ChatCommands.Create("hit")
            .WithDescription("hit parent command")
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .BeginSubCommand("disable")
                .WithDescription("Disables certain tool rendering settings")
                .WithArgs(parsers.OptionalWordRange("setting", new string[4] { "arms", "back", "shields", "favorites" }))
                .HandleWith((args) => { return OnToggleConfigSetting(capi, args, false); })
            .EndSub()
            .BeginSubCommand("enable")
                .WithDescription("Enables certain tool rendering settings")
                .WithArgs(parsers.OptionalWordRange("setting", new string[4] { "arms", "back", "shields", "favorites" } ))
                .HandleWith((args) => { return OnToggleConfigSetting(capi, args, true); })
            .EndSub()
            .BeginSubCommand("set-favorites")
                .WithDescription("Sets up to 5 favorited hotbar slots for selective tool rendering [Default 1-5]")
                .HandleWith((args) => { return OnSetFavoriteSlots(capi, args); })
            .EndSub()
            .Validate();
    }

    private TextCommandResult OnToggleConfigSetting(ICoreClientAPI capi, TextCommandCallingArgs args, bool toggled)
    {
        var player = args.Caller.Player;
        if (_rendererByPlayer.TryGetValue(player.PlayerUID, out var renderer))
        {
            var ChangedSetting = (string)args[0];
            switch (ChangedSetting)
            {
                case "arms":
                    renderer.ClientConfig.Forearm_Tools_Enabled = toggled;
                    break;
                case "back":
                    renderer.ClientConfig.Tools_On_Back_Enabled = toggled;
                    break;
                case "shields":
                    renderer.ClientConfig.Shields_Enabled = toggled;
                    break;
                case "favorites":
                    renderer.ClientConfig.Favorited_Slots_Enabled = toggled;
                    break;
                default:
                    return TextCommandResult.Error("");
            }
            ModConfig.SaveConfig<HITConfig>(capi, renderer.ClientConfig, configFileName);
            return TextCommandResult.Success($"Rendering settings for {player.PlayerName} successfully updated. \nWill take effect on hotbar refresh.");
        }
        else
        {
            return TextCommandResult.Error("No client configs available.");
        }
    }

    private TextCommandResult OnSetFavoriteSlots(ICoreClientAPI capi, TextCommandCallingArgs args)
    {
        var player = args.Caller.Player;
        if (_rendererByPlayer.TryGetValue(player.PlayerUID, out var renderer))
        {
            //TO-DO: figure out the best way to get a set of int args through commands
            return TextCommandResult.Success();
        }
        else
        {
            return TextCommandResult.Error("No client configs available.");
        }
    }
    #endregion

    #region Server
    public override void StartServerSide(ICoreServerAPI sapi)
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

    private void EventOnPlayerDisconnect(IServerPlayer byplayer)
    {
        if (!_watcherByPlayer.TryGetValue(byplayer.PlayerUID, out var watcher)) return;

        watcher.Dispose();
        _watcherByPlayer.Remove(byplayer.PlayerUID);
    }

    private void EventOnPlayerNowPlaying(IServerPlayer byplayer)
    {
        _watcherByPlayer[byplayer.PlayerUID] = new PlayerToolWatcher(byplayer);
    }

    private void HandleClientDataRequest(IServerPlayer fromplayer, RequestToolsInfo packet)
    {
        if (!_watcherByPlayer.TryGetValue(packet.PlayerUid, out var watcher)) return;

        var msg = watcher.GenerateUpdateMessage();
        ServerChannel.SendPacket(msg, fromplayer);
    }
    #endregion

    /*public override void AssetsFinalize(ICoreAPI api)
    {
        PlayerConfig = new HITPlayerConfig();
        HITConfig = ModConfig.ReadConfig<HITConfig>(api, configFileName); //initialize the config
    }

    internal static HITPlayerConfig PlayerConfig { get; private set; } = null!;
    private void GameWorldSave()
    {
        PlayerConfig.GameWorldSave(_sapi);
    }

    internal class HITPlayerConfig
    {
        public Dictionary<string, HITPlayerData> Players = new();

        public HITPlayerData? GetPlayerDataByUid(string playerUid, bool shouldCreate)
        {
            if (!Players.TryGetValue(playerUid, out var playerData) && shouldCreate)
            {
                playerData = new HITPlayerData();
                playerData.MarkDirty();
                Add(playerUid, playerData);
            }
            return playerData;  
        }

        public HITPlayerData GetPlayerDataByUid(string playerUid)
        {
            return GetPlayerDataByUid(playerUid, true)!;
        }

        internal void GameWorldSave(ICoreServerAPI api)
        {
            foreach (KeyValuePair<string, HITPlayerData> playerData in Players)
            {
                if (playerData.Value.IsDirty)
                {
                    playerData.Value.IsDirty = false;
                    var data = SerializerUtil.Serialize(playerData.Value);
                    var player = api.World.PlayerByUid(playerData.Key);
                    player.WorldData.SetModdata(HITModSystem.HITModSystemDataKey, data);
                }
            }
        }

        public void Add(string playerUid, HITPlayerData playerData)
        {
            Players.Add(playerUid, playerData);
        }

    }*/
}