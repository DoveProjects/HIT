using System;
using System.IO;

namespace Ele.HIT
{
    public static class ModConstants
    {
        public const string MOD_NAME = "HarpersTools";
        public const string MOD_ID = "hit";
        public const string mainChannel = $"{MOD_ID}:main";

        public class EventIDs
        {
            public const string configReloaded = $"{MOD_ID}:configreloaded";
        }
    }
}
