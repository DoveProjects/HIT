using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using HIT.Config;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace HIT;
public class HITModSystem : ModSystem
{
    public static HITConfig HITConfig { get; private set; }

    public const int TotalSlots = 5;
    public const int ShieldSlotId = 4;
    public static string HITModSystemDataKey;

    private const string ChannelName = "tool_renderer_mod";
    private ICoreClientAPI _capi = null!;
    private ICoreServerAPI _sapi = null!;

    private readonly Dictionary<string, ToolRenderer> _rendererByPlayer = new();
    private readonly Dictionary<string, PlayerToolWatcher> _watcherByPlayer = new();
    private string configFileName = "harpers_immersive_tools.json";

    internal IClientNetworkChannel ClientChannel = null!;
    internal static IServerNetworkChannel ServerChannel = null!;

    public override void StartClientSide(ICoreClientAPI capi)
    {
        _capi = capi;
        _capi.Event.PlayerEntitySpawn += EventOnPlayerEntitySpawn;
        _capi.Event.PlayerEntityDespawn += EventOnPlayerEntityDespawn;
        ClientChannel = _capi.Network
            .RegisterChannel(ChannelName)
            .RegisterMessageType<RequestToolsInfo>()
            .RegisterMessageType<UpdatePlayerTools>()
            .SetMessageHandler<UpdatePlayerTools>(HandleDataFromServer);
    }

    private void EventOnPlayerEntityDespawn(IClientPlayer byplayer)
    {
        if (!_rendererByPlayer.TryGetValue(byplayer.PlayerUID, out var renderer)) return;
        renderer.Dispose();

        _rendererByPlayer.Remove(byplayer.PlayerUID);

    }

    private void EventOnPlayerEntitySpawn(IClientPlayer byplayer)
    {
        _rendererByPlayer[byplayer.PlayerUID] = new ToolRenderer(_capi, byplayer);
        ClientChannel.SendPacket(new RequestToolsInfo()
        {
            PlayerUid = byplayer.PlayerUID
        });
    }

    private void HandleDataFromServer(UpdatePlayerTools packet)
    {
        if (_rendererByPlayer.TryGetValue(packet.PlayerUid, out var renderer))
        {
            renderer.UpdateRenderedTools(packet);
        }
    }


    public override void StartServerSide(ICoreServerAPI sapi)
    {
        _sapi = sapi;
        _sapi.Event.GameWorldSave += GameWorldSave;
        _sapi.Event.PlayerNowPlaying += EventOnPlayerNowPlaying;
        _sapi.Event.PlayerDisconnect += EventOnPlayerDisconnect;
        ServerChannel = _sapi.Network
            .RegisterChannel(ChannelName)
            .RegisterMessageType<RequestToolsInfo>()
            .RegisterMessageType<UpdatePlayerTools>()
            .SetMessageHandler<RequestToolsInfo>(HandleClientDataRequest);

        CommandArgumentParsers parsers = _capi.ChatCommands.Parsers;

        _sapi.ChatCommands.Create("HIT")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .BeginSubCommand("disable")
                    .WithDescription("disables rendering")
                    .WithArgs(parsers.OptionalWordRange("arms", "back", "shield"))
                    .HandleWith(OnDisabledSettingsChanged)
                .EndSub()
                .Validate();
    }

    private TextCommandResult OnDisabledSettingsChanged(TextCommandCallingArgs args)
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
    }
    private void EventOnPlayerDisconnect(IServerPlayer byplayer)
    {
        if (!_watcherByPlayer.TryGetValue(byplayer.PlayerUID, out var watcher)) return;

        watcher.Dispose();
        _watcherByPlayer.Remove(byplayer.PlayerUID);
    }

    private void EventOnPlayerNowPlaying(IServerPlayer byplayer)
    {
        _watcherByPlayer[byplayer.PlayerUID] = new PlayerToolWatcher(byplayer, PlayerConfig.GetPlayerDataByUid(byplayer.PlayerUID));
    }

    private void HandleClientDataRequest(IServerPlayer fromplayer, RequestToolsInfo packet)
    {
        if (!_watcherByPlayer.TryGetValue(packet.PlayerUid, out var watcher)) return;

        var msg = watcher.GenerateUpdateMessage();
        ServerChannel.SendPacket(msg, fromplayer);
    }
    public override void AssetsFinalize(ICoreAPI api)
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

    }
}