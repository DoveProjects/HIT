using static Elephant.HIT.ModConstants;
using Vintagestory.API.Common;
using Elephant.Extensions;
using ProtoBuf;

namespace Elephant.Configuration
{
    [ProtoContract]
    public class ConfigArgs
    {
        [ProtoMember(1)]
        public EnumAppSide Side { get; set; }
        [ProtoMember(2)]
        public string Name { get; set; }
        [ProtoMember(3)]
        public string Folder { get; set; }
        [ProtoMember(4)]
        public string JsonPath { get; set; }


        /// <summary>
        ///   Multi-option constructor for initializing any kind of config
        /// </summary>
        /// <param name="side">Determines config side. Only leave empty if generating a singular default config.</param>
        /// <param name="name">Optional config file name to be used instead of a default</param>
        /// <param name="folder">Optional config folder name to be used instead of a default</param>
        public ConfigArgs(ICoreAPI api, EnumAppSide? side = null, string name = null, string folder = null)
        {
            Side = side ?? EnumAppSide.Universal;
            Name = GetConfigName(side, name);
            Folder = folder ?? $"{MOD_NAME}";
            JsonPath = Name == MOD_NAME ?
                $"{Name}.json" :
                $"{Folder}/{Name}.json";
            api.Log($"Initialized Config Data: {Side}, {Name}, {Folder}, {JsonPath}");
        }
        public ConfigArgs() { }

        /// <summary>
        ///   Returns the default name of a config depending on its side.
        /// </summary>
        /// <param name="side"></param>
        /// <param name="name">Optional name to be used instead of a default</param>
        public static string GetConfigName(EnumAppSide? side = null, string name = null)
        {
            switch (side)
            {
                case EnumAppSide.Universal:
                    return name == null ? JSON_CONFIG_UNIVERSAL : name;
                case EnumAppSide.Client:
                    return name == null ? JSON_CONFIG_CLIENT : name;
                case EnumAppSide.Server:
                    return name == null ? JSON_CONFIG_SERVER : $"{JSON_CONFIG_SERVER}.{name}";
                default:
                    return MOD_NAME;
            }
        }
    }
}