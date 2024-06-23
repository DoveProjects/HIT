using System.Collections.Generic;
using Vintagestory.API.Common;

namespace HIT.Config
{
    public class HITConfig : IModConfig
    {
        public bool Favorited_Slots_Enabled { get; set; } = false;

        //public List<int> Favorited_Slots = DefaultFavoritedSlots();
        public int Favorited_Slot_1 = 0;
        public int Favorited_Slot_2 = 1;
        public int Favorited_Slot_3 = 2;
        public int Favorited_Slot_4 = 3;

        public static int[] favorite_slots;


        public HITConfig(ICoreAPI api, HITConfig previousConfig = null)
        {
            if (previousConfig != null)
            {
                Favorited_Slots_Enabled = previousConfig.Favorited_Slots_Enabled;
                Favorited_Slot_1 = previousConfig.Favorited_Slot_1;
                Favorited_Slot_2 = previousConfig.Favorited_Slot_2;
                Favorited_Slot_3 = previousConfig.Favorited_Slot_3;
                Favorited_Slot_4 = previousConfig.Favorited_Slot_4;
                favorite_slots = new int[4] { Favorited_Slot_1, Favorited_Slot_2, Favorited_Slot_3, Favorited_Slot_4 };
            }
        }
    }
}