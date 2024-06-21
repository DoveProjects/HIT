using System.Collections.Generic;
using Vintagestory.API.Common;

namespace HIT.Config
{
    public class HITConfig : IModConfig
    {
        public bool Favorited_Slots_Enabled { get; set; } = false;

        public List<int> Favorited_Slots = DefaultFavoritedSlots();

        public HITConfig(ICoreAPI api, HITConfig previousConfig = null)
        {
            if (previousConfig != null)
            {
                Favorited_Slots_Enabled = previousConfig.Favorited_Slots_Enabled;
                Favorited_Slots = previousConfig.Favorited_Slots;
            }
        }
        public static List<int> DefaultFavoritedSlots()
        {
            return new() { 0, 1, 2, 3 };
        }
    }
}