using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using System.IO;
using Vintagestory.API.MathTools;
using System.Net.Sockets;
using IConfig;

namespace HIT;
public class HITModSystem : ModSystem
{
    public static HITConfig ClientConfig { get; private set; }

    public const int TotalSlots = 5;
    public const int ShieldSlotId = 4;
    //private static string HITModSystemDataKey;

    private const string configFileName = "harpers_immersive_tools.json";
    private const string ChannelName = "harpers_tools_mod";
    private ICoreClientAPI _capi = null!;
    private ICoreServerAPI _sapi = null!;

    private readonly Dictionary<string, ToolRenderer> _rendererByPlayer = new();
    private readonly Dictionary<string, PlayerToolWatcher> _watcherByPlayer = new();

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
        RegisterClientCommand(capi);

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
        _rendererByPlayer[byplayer.PlayerUID] = new ToolRenderer(_capi, byplayer, ModConfig.LoadConfig<HITConfig>(_capi, configFileName));
        ClientChannel.SendPacket(new RequestToolsInfo()
        {
            PlayerUid = byplayer.PlayerUID,
            //ClientConfig = ModConfig.ReadConfig<HITConfig>(_capi, configFileName)
        });
        _capi.Logger.Notification($"[HIT] {byplayer.PlayerName} loaded in. Grabbing client-side configs.");
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

    private void RegisterClientCommand(ICoreClientAPI capi)
    {
        capi.Logger.Notification("[HIT] Registering client-side rendering command.");
        CommandArgumentParsers parsers = capi.ChatCommands.Parsers;

        capi.ChatCommands.Create("hit")
            .WithDescription("hit parent command")
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .BeginSubCommand("disable")
                .WithDescription("disables rendering of different sheath types")
                .WithArgs(parsers.OptionalWordRange("sheath", new string[3] { "arms", "back", "shield" }))
                .HandleWith((args) =>
                {
                    var player = args.Caller.Player;
                    if (_rendererByPlayer.TryGetValue(player.PlayerUID, out var renderer))
                    {
                        var ChangedSetting = (string)args[0];
                        switch (ChangedSetting)
                        {
                            case "arms":
                                renderer.ClientConfig.Forearm_Tools_Enabled = true;
                                break;
                            case "back":
                                renderer.ClientConfig.Tools_On_Back_Enabled = true;
                                break;
                            case "shield":
                                renderer.ClientConfig.Shields_Enabled = true;
                                break;
                            default:
                                return TextCommandResult.Error("");
                        }
                        //ModConfig.GenerateConfig<HITConfig>(_capi, configFileName);
                        return TextCommandResult.Success($"Rendering settings for {player.PlayerName} successfully updated. \nWill take effect on hotbar refresh.");
                    } 
                    else
                    {
                        return TextCommandResult.Error("No client configs available.");
                    }
                        
                })
            .EndSub()
            .Validate();
    }
    #endregion

    #region Server
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        _sapi = sapi;
        //_sapi.Event.GameWorldSave += GameWorldSave;
        _sapi.Event.PlayerNowPlaying += EventOnPlayerNowPlaying;
        _sapi.Event.PlayerDisconnect += EventOnPlayerDisconnect;
        ServerChannel = _sapi.Network
            .RegisterChannel(ChannelName)
            .RegisterMessageType<RequestToolsInfo>()
            .RegisterMessageType<UpdatePlayerTools>()
            .SetMessageHandler<RequestToolsInfo>(HandleClientDataRequest);

        /*CommandArgumentParsers parsers = _capi.ChatCommands.Parsers;

        _sapi.ChatCommands.Create("HIT")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .BeginSubCommand("disable")
                    .WithDescription("disables rendering")
                    .WithArgs(parsers.OptionalWordRange("arms", "back", "shield"))
                    .HandleWith(OnDisabledSettingsChanged)
                .EndSub()
                .Validate();*/
    }

    /*private TextCommandResult OnDisabledSettingsChanged(TextCommandCallingArgs args)
    {
        string ChangedSetting = (string)args[0];
        if (ChangedSetting == "arms")
        {
            //change bool array[0] to true
            return HITModSystem.ServerChannel.BroadcastPacket(GenerateUpdateMessage());
        }
        if (ChangedSetting == "back")
        {
            //change bool array[1] to true
        }
        if (ChangedSetting == "shield")
        {
            //change bool array[2] to true
        }
        if (args.Parsers[0].IsMissing)
        {
            return  UpdateDisabledConfig(4);
        }
    }*/

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