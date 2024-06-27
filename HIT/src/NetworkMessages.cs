using System.Collections.Generic;
using HIT.Config;
using ProtoBuf;

namespace HIT;

public enum BackPackType //initializing a var to hold the variations of the backpack for later checking
{
    None,
    Leather,
    Hunter
}

//these send messages back from the server and clientside to properly grab things with a light load + other formats for cacheing
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)] 
public class SlotData 
{
    public string Code = null!;
    public byte[] StackData = null!;
    public int HotbarID;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdatePlayerTools
{
    //public HITConfig ClientConfig = null!;
    public string PlayerUid = null!;
    public BackPackType BackPackType = BackPackType.None;
    public Dictionary<int, SlotData> RenderedTools = null!;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class RequestToolsInfo
{
    public string PlayerUid = null!;
    //public HITConfig ClientConfig = null!;
}

/*[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class HITPlayerData
{
    public bool IsDirty;
    public bool[] DisabledSettings = null!;

    internal void MarkDirty()
    {
        if (!IsDirty)
        {
            IsDirty = true;
        }
    }
}*/