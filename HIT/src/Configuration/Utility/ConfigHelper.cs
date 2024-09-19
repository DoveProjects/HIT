using static Elephant.HIT.ModConstants;
using System;
using System.Linq;
using Vintagestory.API.Common;
using System.Collections.Generic;
using Elephant.HIT;

namespace Elephant.Configuration
{
    //Courtesy of https://github.com/Craluminum-Mods/
    public static class ConfigHelper
    {
        public static Dictionary<string, IModConfig> InitConfigDictionary(ICoreAPI api, IList<ConfigArgs> args)
        {
            var dict = new Dictionary<string, IModConfig>();
            foreach (ConfigArgs arg in args)
            {
                switch (arg.Side)
                {
                    case EnumAppSide.Universal:
                        var universal = ReadConfig<UniversalConfig>(api, arg);
                        universal.Info = arg;
                        dict.Add(arg.Name, universal);
                        break;
                    case EnumAppSide.Client:
                        var client = ReadConfig<ClientConfig>(api, arg);
                        client.Info = arg;
                        dict.Add(arg.Name, client);
                        break;
                    case EnumAppSide.Server:
                        var properties = ReadConfig<ServerProperties>(api, arg);
                        properties.Info = arg;
                        dict.Add(arg.Name, properties);
                        break;
                }
            }
            return dict;
        }

        public static void ReadConfigDictionary(ICoreAPI api, Dictionary<string, IModConfig> configs)
        {
            foreach (var (name, config) in configs.Select(x => (x.Key, x.Value)))
            {
                if (config is UniversalConfig core) ConfigManager.UniversalConfig = ReadConfig<UniversalConfig>(api, core.Info);
                if (config is ClientConfig client) ConfigManager.ClientConfig = ReadConfig<ClientConfig>(api, client.Info);
                if (config is ServerProperties prop) ConfigManager.ServerProperties = ReadConfig<ServerProperties>(api, prop.Info);
            }
        }

        public static void UpdateConfigDictionary(ICoreAPI api, Dictionary<string, IModConfig> configs)
        {
            foreach (var (name, config) in configs.Select(x => (x.Key, x.Value)))
            {
                if (config is UniversalConfig core) ConfigManager.UniversalConfig = core;
                if (config is ClientConfig client) ConfigManager.ClientConfig = client;
                if (config is ServerProperties prop) ConfigManager.ServerProperties = prop;
            }
        }

        public static void CloneConfigDictionary(ICoreAPI api, Dictionary<string, IModConfig> configs)
        {
            foreach (var (name, config) in configs.Select(x => (x.Key, x.Value)))
            {
                if (config is UniversalConfig core) ConfigManager.UniversalConfig = CloneConfig<UniversalConfig>(core.Info);
                if (config is ClientConfig client) ConfigManager.ClientConfig = CloneConfig<ClientConfig>(client.Info);
                if (config is ServerProperties prop) ConfigManager.ServerProperties = CloneConfig<ServerProperties>(prop.Info);
            }
        }

        /// <summary>
        ///     Returns a config class read from a json config file.
        ///     Will return an existing class or generate and return a new one.
        /// </summary>
        public static T ReadConfig<T>(ICoreAPI api, ConfigArgs args) where T : class, IModConfig
        {
            T config;
            try
            {
                config = LoadConfig<T>(api, args.JsonPath);

                if (config == null)
                {
                    GenerateConfig<T>(api, args);
                    config = LoadConfig<T>(api, args.JsonPath);
                }
                else
                {
                    GenerateConfig(api, args, config);
                }
            }
            catch
            {
                GenerateConfig<T>(api, args);
                config = LoadConfig<T>(api, args.JsonPath);
            }
            return config;
        }

        /// <summary>
        ///     Writes to a config file and returns the updated version.
        /// </summary>
        public static T UpdateConfig<T>(ICoreAPI api, T newConfig) where T : class, IModConfig
        {
            var args = newConfig.Info;
            WriteConfig<T>(api, newConfig);
            return LoadConfig<T>(api, args.JsonPath);
        }

        public static void WriteConfig<T>(ICoreAPI api, T config) where T : IModConfig
        {
            var args = config.Info;
            api.StoreModConfig(config, args.JsonPath);
        }

        public static T LoadConfig<T>(ICoreAPI api, string jsonPath) where T : IModConfig
        {
            return api.LoadModConfig<T>(jsonPath);
        }

        private static void GenerateConfig<T>(ICoreAPI api, ConfigArgs args, T previousConfig = null) where T : class, IModConfig
        {
            api.StoreModConfig(CloneConfig(args, previousConfig), args.JsonPath);
        }

        private static T CloneConfig<T>(ConfigArgs args, T config = null) where T : class, IModConfig
        {
            return (T)Activator.CreateInstance(typeof(T), new object[] { args, config });
        }
    }
}