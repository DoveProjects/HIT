using System;
using System.IO;

namespace Ele.HIT
{
    public static class ModConstants
    {
        public const string modName = "HarpersTools";
        public const string modDomain = "hit";
        public const string mainChannel = $"{modDomain}:main";

        public class EventIDs
        {
            public const string configReloaded = $"{modDomain}:configreloaded";
        }
    }
}
