using System.Collections.Generic;
using ProtoBuf;

namespace Elephant.HIT;

public enum BackPackType //initializing a var to hold the variations of the backpack for later checking
{
    None,
    Leather,
    Hunter,
    EternalPack,
    EternalBackPack
}

//these are network messages that are sent back and forth between the client and server, using the ProtoBuf library
//look up the Network API page on the modding wiki for more information
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)] 
public class SlotData //a custom data structure for relevant hotbar slot info
{
    public string Code = null!;
    public byte[] StackData = null!;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdatePlayerTools //sent from server -> client so the client's renderer knows which tools to update
{
    public string PlayerUid = null!;
    public BackPackType BackPackType = BackPackType.None;
    public Dictionary<int, SlotData> RenderedTools = null!;
}

/*[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class RequestToolsInfo //sent from client -> server when the server 'requests' rendering data from each client
{
    public string PlayerUid = null!;
    public string ConfigData = null!;
}*/

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ClientConfigUpdated
{
    public string ConfigData = null!;
}