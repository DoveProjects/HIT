using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;

namespace Elephant.HIT
{
    public static class ModConstants
    {
        public const string MOD_NAME = "HarpersTools"; //<--Cannot contain spaces
        internal const string ORG_ID = "elephantstudios"; //<--Cannot contain spaces
        internal static string MOD_ID;
        internal static string DISPLAY_NAME;
        internal static string HARMONY_ID;

        internal static string NETWORK_CHANNEL_MAIN;
        internal static string NETWORK_CHANNEL_CONFIG;

        internal const string JSON_CONFIG_UNIVERSAL = "Core-Settings";
        internal const string JSON_CONFIG_CLIENT = "Client-Settings";
        internal const string JSON_CONFIG_SERVER = "Server-Properties";
        internal const string INPUT_SETTINGS = "Input-Settings";

        internal static void Init(ModInfo modInfo)
        {
            MOD_ID = modInfo.ModID;
            DISPLAY_NAME = modInfo.Name;
            HARMONY_ID = $"com.{ORG_ID}.{MOD_ID}";

            NETWORK_CHANNEL_MAIN = $"{MOD_ID}:main";
            NETWORK_CHANNEL_CONFIG = $"{MOD_ID}:config";
        }

        public class EventIDs
        {
            internal static string Config_Reloaded = $"{MOD_ID}:configreloaded";
            internal static string Admin_Send_Config = $"{MOD_ID}:adminsendconfig";
            internal static string Client_Send_Config = $"{MOD_ID}:clientsendconfig";
        }

        internal static Dictionary<int, int> HotbarMap = new Dictionary<int, int>(){
            {1, 0},
            {2, 1},
            {3, 2},
            {4, 3},
            {5, 4},
            {6, 5},
            {7, 6},
            {8, 7},
            {9, 8},
            {0, 9}
        };
    }
}
