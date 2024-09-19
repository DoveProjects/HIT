using static Elephant.HIT.ModConstants;
using ProtoBuf;
using Newtonsoft.Json;
using Elephant.Configuration;


namespace Elephant.HIT
{
    [ProtoContract]
    public class UniversalConfig : IModConfig
    {
        [JsonIgnore]
        [ProtoMember(1)]
        public ConfigArgs Info { get; set; }

        // Tag Proto Booleans with IsRequired = true to prevent false values not being sent
        [ProtoMember(2, IsRequired = true)]
        public bool ExampleBool { get; set; }

        /*----------------
         * 
         * Add more synced fields here
         * 
         -----------------*/

        public UniversalConfig() { }
        public UniversalConfig(ConfigArgs args)
        {
            Info = args;
            
            //Initialize all required defaults here
            ExampleBool = true;
        }

        public UniversalConfig(ConfigArgs args, UniversalConfig previousConfig = null) : this(args)
        {
            if (previousConfig == null) return;

            //Update all fields from the previousConfig here
            ExampleBool = previousConfig.ExampleBool;
        }
    }
}