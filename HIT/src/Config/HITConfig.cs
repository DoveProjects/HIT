using Vintagestory.API.Common;
using Newtonsoft.Json;
using IConfig;
using Vintagestory.API.Util;
using System.Text.Json.Serialization;

namespace HIT
{
    public class HITConfig : IModConfig
    {
        public bool Forearm_Tools_Enabled { get; set; } = true; //Bool to enable/disable the rendering of forearm tools
        public bool Tools_On_Back_Enabled { get; set; } = true; //Bool to enable/disable the rendering of tools on the back 
        public bool Shields_Enabled { get; set; } = true; //Bool to enable/disable all shield rendering


        public bool Favorited_Slots_Enabled { get; set; } = false; //Bool to enable/disable the favorited slots feature

        public int[] Favorited_Slots = new int[5] { 0, 1, 2, 3, 4 }; //Int array to determine favorited hotbar slots. Defaults to slots 1-5

        public HITConfig(ICoreAPI api, HITConfig previousConfig = null)
        {
            if (previousConfig != null)
            {
                Forearm_Tools_Enabled = previousConfig.Forearm_Tools_Enabled;
                Tools_On_Back_Enabled = previousConfig.Tools_On_Back_Enabled;
                Shields_Enabled = previousConfig.Shields_Enabled;
                Favorited_Slots_Enabled = previousConfig.Favorited_Slots_Enabled;
                Favorited_Slots = previousConfig.Favorited_Slots;
            }
        }
    }
}