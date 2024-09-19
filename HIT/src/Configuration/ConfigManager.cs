using static Elephant.HIT.ModConstants;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using Elephant.Configuration;
using Elephant.Extensions;
using Newtonsoft.Json;
using ProtoBuf;
using Vintagestory.API.Util;
using System.Net.Sockets;

namespace Elephant.HIT;

//Courtesy of https://github.com/jayugg/
public class ConfigManager : ModSystem
{
    private static ICoreAPI _api;
    private static IClientNetworkChannel _clientChannel;
    private static IServerNetworkChannel _serverChannel;
    private static bool activeSync = false;

    public static IList<ConfigArgs> ConfigInfo { get; set; }
    public static Dictionary<string, IModConfig> ConfigsByName { get; set; }

    public override double ExecuteOrder() => 0.02;
    public override void Start(ICoreAPI api)
    {
        _api = api;

        /* 
        *   Use this list to create arguments for synced, client, and server configs.
        *   The method that follows will initialize one config per set of arguments provided.
        *   These default arguments will create one file per type, named after the constants in ModConstants.
        */
        ConfigInfo = new List<ConfigArgs>()
        {
            new ConfigArgs(api, EnumAppSide.Client)
        };
        ConfigsByName = ConfigHelper.InitConfigDictionary(api, ConfigInfo);
        if (api.ModLoader.IsModEnabled("configlib"))
        {
            _ = new ConfigLibCompat(api);
        }
    }

    public static UniversalConfig UniversalConfig
    {
        get
        {
            return ConfigsByName.Where(e => e.Value is UniversalConfig)
                .Select(e => (UniversalConfig)e.Value)
                .FirstOrDefault();
        }
        set
        {
            activeSync = true;
            value.Info ??= ConfigInfo.FirstOrDefault(e => e.Side == EnumAppSide.Universal);
            ConfigsByName[value.Info.Name] = ConfigHelper.UpdateConfig<UniversalConfig>(_api, value);
            ((UniversalConfig)ConfigsByName[value.Info.Name]).Info = value.Info;
        }
    }

    public static ClientConfig ClientConfig
    {
        get
        {
            return ConfigsByName.Where(e => e.Value is ClientConfig)
                .Select(e => (ClientConfig)e.Value)
                .FirstOrDefault();
        }
        set
        {
            value.Info ??= ConfigInfo.FirstOrDefault(e => e.Side == EnumAppSide.Client);
            ConfigsByName[value.Info.Name] = ConfigHelper.UpdateConfig<ClientConfig>(_api, value);
            ((ClientConfig)ConfigsByName[value.Info.Name]).Info = value.Info;
        }
    }

    public static ServerProperties ServerProperties
    {
        get
        {
            return ConfigsByName.Where(e => e.Value is ServerProperties)
                .Select(e => (ServerProperties)e.Value)
                .FirstOrDefault();
        }
        set
        {
            value.Info ??= ConfigInfo.FirstOrDefault(e => e.Side == EnumAppSide.Server);
            ConfigsByName[value.Info.Name] = ConfigHelper.UpdateConfig<ServerProperties>(_api, value);
            ((ServerProperties)ConfigsByName[value.Info.Name]).Info = value.Info;
        }
    }

    /*
    * <----------------------------------------Client---------------------------------------->
    */
    public override void StartClientSide(ICoreClientAPI capi)
    {
        _clientChannel = capi.Network.RegisterChannel(NETWORK_CHANNEL_CONFIG)
             .RegisterMessageType<ClientConfigUpdated>()
             .RegisterMessageType<UniversalConfig>()
             .SetMessageHandler<UniversalConfig>(ReceiveConfigFromServer);
        capi.Event.RegisterEventBusListener(SendClientConfig, filterByEventName: EventIDs.Client_Send_Config);
        capi.Event.RegisterEventBusListener(SendAdminConfig, filterByEventName: EventIDs.Admin_Send_Config);

        _ = new InputManager(capi, true, false);
    }

    private static void SendAdminConfig(string eventname, ref EnumHandling handling, IAttribute data)
    {
        _api.Log("Sending synced config from player admin...");
        _clientChannel?.SendPacket(UniversalConfig);
    }

    private static void SendClientConfig(string eventname, ref EnumHandling handling, IAttribute data)
    {
        ModMain.ClientChannel.SendPacket(new ClientConfigUpdated()
        {
            ConfigData = JsonConvert.SerializeObject(ClientConfig)
        });
    }

    private static void ReceiveConfigFromServer(UniversalConfig packet)
    {
        if (ConfigsByName.TryGetValue(packet.Info.Name, out var config))
        {
            UniversalConfig = packet;
            _api?.Event.PushEvent(EventIDs.Config_Reloaded);
        }
    }

    /*
    * <----------------------------------------Server---------------------------------------->
    */
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        _serverChannel = sapi.Network.RegisterChannel(NETWORK_CHANNEL_CONFIG)
            .RegisterMessageType<UniversalConfig>()
            .SetMessageHandler<UniversalConfig>(ReceiveConfigFromAdmin);

        if (activeSync) sapi.Event.PlayerJoin += SendConfigFromServer;
        sapi.Event.RegisterEventBusListener(SendConfigToAllPlayers, filterByEventName: EventIDs.Config_Reloaded);
    }

    private static void ReceiveConfigFromAdmin(IServerPlayer fromplayer, UniversalConfig packet)
    {
        if (fromplayer.HasPrivilege("controlserver"))
        {
            _api.Log($"Receiving configs from player admin: {fromplayer.PlayerName}. Syncing with server...");
            if (ConfigsByName.TryGetValue(packet.Info.Name, out var config))
            {
                UniversalConfig = packet;
                _api?.Event.PushEvent(EventIDs.Config_Reloaded);
            }
        }
    }

    private static void SendConfigToAllPlayers(string eventname, ref EnumHandling handling, IAttribute data)
    {
        _api.Log("Syncing configs across all server players...");
        if (_api?.World == null) return;
        foreach (var player in _api.World.AllPlayers)
        {
            if (player is not IServerPlayer serverPlayer) continue;
            SendConfigFromServer(serverPlayer);
        }
    }

    private static void SendConfigFromServer(IServerPlayer toPlayer)
    {
        _serverChannel?.SendPacket(UniversalConfig, toPlayer);
    }
}