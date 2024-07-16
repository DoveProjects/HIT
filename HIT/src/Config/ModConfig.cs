using System;
using System.IO;
using Vintagestory.API.Common;
using Ele.HIT;

namespace Ele.Configuration
{
    //Courtesy of https://github.com/Craluminum-Mods/ && https://github.com/Chronolegionnaire/
    public static class ModConfig
    {
        /// <summary>
        ///     Returns config file path - ModConfig/ModName.json by default
        ///     Optional configName param for nesting granular config files within a config folder
        /// </summary>
        public static string GetConfigPath(ICoreAPI api, string configName = null)
        {
            return configName == null ?
                $"{ModConstants.modName}.json" :
                Path.Combine(api.GetOrCreateDataPath(ModConstants.modName), $"{configName}.json");
        }

        /// <summary>
        ///     Returns a config class read from a json config file.
        ///     Will return an existing file or generate and return a new one if none exists.
        /// </summary>
        public static T ReadConfig<T>(ICoreAPI api, string jsonPath = null) where T : class, IModConfig
        {
            T config;
            try
            {
                config = LoadConfig<T>(api, jsonPath);

                if (config == null)
                {
                    GenerateConfig<T>(api, jsonPath);
                    config = LoadConfig<T>(api, jsonPath);
                }
                else
                {
                    GenerateConfig(api, jsonPath, config);
                }
            }
            catch
            {
                GenerateConfig<T>(api, jsonPath);
                config = LoadConfig<T>(api, jsonPath);
            }
            return config;
        }

        /// <summary>
        ///     Writes to a config file and returns the updated version.
        ///     Will update an existing file or generate and update a new one if none exists.
        /// </summary>
        public static T UpdateConfig<T>(ICoreAPI api, T newConfig, string jsonPath = null) where T : class, IModConfig
        {
            T config = ReadConfig<T>(api, jsonPath);
            try
            {
                if (config == newConfig)
                {
                    return newConfig;
                }
                else
                {
                    WriteConfig<T>(api, newConfig, jsonPath);
                    config = LoadConfig<T>(api, jsonPath);
                }
            }
            catch
            {
                GenerateConfig<T>(api, jsonPath);
                config = LoadConfig<T>(api, jsonPath);
            }
            return config;
        }

        public static void WriteConfig<T>(ICoreAPI api, T config, string jsonPath = null) where T : IModConfig
        {
            string configPath = GetConfigPath(api, jsonPath);
            api.StoreModConfig(config, configPath);
        }

        public static T LoadConfig<T>(ICoreAPI api, string jsonPath = null) where T : IModConfig
        {
            string configPath = GetConfigPath(api, jsonPath);
            return api.LoadModConfig<T>(configPath);
        }

        private static void GenerateConfig<T>(ICoreAPI api, string jsonPath = null, T previousConfig = null) where T : class, IModConfig
        {
            string configPath = GetConfigPath(api, jsonPath);
            api.StoreModConfig(CloneConfig(api, previousConfig), configPath);
        }

        private static T CloneConfig<T>(ICoreAPI api, T config = null) where T : class, IModConfig
        {
            return (T)Activator.CreateInstance(typeof(T), new object[] { api, config });
        }
    }
}