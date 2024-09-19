using static Elephant.HIT.ModConstants;
using Newtonsoft.Json;
using Elephant.Configuration;


namespace Elephant.HIT
{
    public class ServerProperties : IModConfig
    {
        [JsonIgnore]
        public ConfigArgs Info { get; set; }
        public bool ExampleBool { get; set; } = true;

        /*----------------
         * 
         * Add config fields here
         * 
         -----------------*/

        public ServerProperties() { }
        public ServerProperties(ConfigArgs args)
        {
            Info = args;

            //Initialize all required defaults here
            ExampleBool = true;
        }
        public ServerProperties(ConfigArgs args, ServerProperties previousConfig = null) : this(args)
        {
            if (previousConfig == null) return;

            //Update all fields from the previousConfig here
            ExampleBool = previousConfig.ExampleBool;
        }
    }
}