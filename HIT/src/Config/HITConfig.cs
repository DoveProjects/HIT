using System.Collections.Generic;
using Vintagestory.API.Common;

namespace HIT.Config
{
    public class HITConfig : IModConfig
    {
        public bool Favorited_Slots_Enabled { get; set; } = false; //Bool to enable/disable the favorited slots feature

        public int Favorited_Slot_1 = 0;
        public int Favorited_Slot_2 = 1;
        public int Favorited_Slot_3 = 2;
        public int Favorited_Slot_4 = 3;
        public int Favorited_Slot_5 = 4;

        public static int[] favorite_slots; //static field for use in code only, won't appear in config file

        public bool Forearm_Tools_Enabled { get; set; } = true; //Bool to enable/disable the rendering of forearm tools
        public bool Tools_On_Back_Enabled { get; set; } = true; //Bool to enable/disable the rendering of tools on the back
        public bool Shields_Enabled { get; set; } = true; //Bool to enable/disable all shield rendering

        public HITConfig(ICoreAPI api, HITConfig previousConfig = null)
        {
            if (previousConfig != null)
            {
                Forearm_Tools_Enabled = previousConfig.Forearm_Tools_Enabled;
                Tools_On_Back_Enabled = previousConfig.Tools_On_Back_Enabled;
                Shields_Enabled = previousConfig.Shields_Enabled;
                Favorited_Slots_Enabled = previousConfig.Favorited_Slots_Enabled;
                Favorited_Slot_1 = previousConfig.Favorited_Slot_1;
                Favorited_Slot_2 = previousConfig.Favorited_Slot_2;
                Favorited_Slot_3 = previousConfig.Favorited_Slot_3;
                Favorited_Slot_4 = previousConfig.Favorited_Slot_4;
                Favorited_Slot_5 = previousConfig.Favorited_Slot_5;
                favorite_slots = new int[5] { Favorited_Slot_1, Favorited_Slot_2, Favorited_Slot_3, Favorited_Slot_4, Favorited_Slot_5 };
            }
        }
    }
}