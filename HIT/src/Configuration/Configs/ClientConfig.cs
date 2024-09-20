using static Elephant.HIT.ModConstants;
using Newtonsoft.Json;
using Elephant.Configuration;
using ProtoBuf;
using System.Collections.Generic;


namespace Elephant.HIT
{
    public class ClientConfig : IModConfig
    {
        [JsonIgnore]
        public ConfigArgs Info { get; set; }

        public bool Forearm_Tools_Enabled { get; set; } //Bool to enable/disable the rendering of forearm tools
        public bool Tools_On_Back_Enabled { get; set; } //Bool to enable/disable the rendering of tools on the back 
        public bool Shields_Enabled { get; set; } //Bool to enable/disable all shield rendering

        public bool Favorited_Slots_Enabled { get; set; } //Bool to enable/disable the favorited slots feature
        public List<int> Favorited_Slots; //Int list to determine favorited hotbar slots. Defaults to slots 1-5


        public ClientConfig() { }
        public ClientConfig(ConfigArgs args)
        {
            Info = args;

            Forearm_Tools_Enabled = true;
            Tools_On_Back_Enabled = true;
            Shields_Enabled = true;
            Favorited_Slots_Enabled = false;
            Favorited_Slots = new List<int> { 0, 1, 2, 3, 4 };
        }
        public ClientConfig(ConfigArgs args, ClientConfig previousConfig = null) : this(args)
        {
            if (previousConfig == null) return;

            Forearm_Tools_Enabled = previousConfig.Forearm_Tools_Enabled;
            Tools_On_Back_Enabled = previousConfig.Tools_On_Back_Enabled;
            Shields_Enabled = previousConfig.Shields_Enabled;
            Favorited_Slots_Enabled = previousConfig.Favorited_Slots_Enabled;
            Favorited_Slots = previousConfig.Favorited_Slots;
        }
    }
}