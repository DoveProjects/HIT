using System;
using Vintagestory.API.Common;

namespace IConfig
{
    public static class ModConfig
    {
        public static T ReadConfig<T>(ICoreAPI api, string jsonConfig) where T : class, IModConfig
        {
            T config;

            try
            {
                config = LoadConfig<T>(api, jsonConfig);

                if (config == null)
                {
                    GenerateConfig<T>(api, jsonConfig);
                    config = LoadConfig<T>(api, jsonConfig);
                }
                else
                {
                    GenerateConfig(api, jsonConfig, config);
                }
            }
            catch
            {
                GenerateConfig<T>(api, jsonConfig);
                config = LoadConfig<T>(api, jsonConfig);
            }

            return config;
        }

        public static void SaveConfig<T>(ICoreAPI api, T configClass, string jsonConfig) where T : IModConfig
        {
            api.StoreModConfig<T>(configClass, jsonConfig);
        }

        public static T LoadConfig<T>(ICoreAPI api, string jsonConfig) where T : IModConfig
        {
            return api.LoadModConfig<T>(jsonConfig);
        }

        public static void GenerateConfig<T>(ICoreAPI api, string jsonConfig, T previousConfig = null) where T : class, IModConfig
        {
            api.StoreModConfig(CloneConfig<T>(api, previousConfig), jsonConfig);
        }

        private static T CloneConfig<T>(ICoreAPI api, T config = null) where T : class, IModConfig
        {
            return (T)Activator.CreateInstance(typeof(T), new object[] { api, config });
        }
    }
}