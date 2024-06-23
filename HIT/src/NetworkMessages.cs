using System.Collections.Generic;
using ProtoBuf;

namespace HIT;

public enum BackPackType //initializing a var to hold the variations of the backpack for later checking
{
    None,
    Leather,
    Hunter
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SlotData
{
    public string Code = null!;
    public byte[] StackData = null!;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdatePlayerTools
{
    public string PlayerUid = null!;
    public BackPackType BackPackType = BackPackType.None;
    public Dictionary<int, SlotData> RenderedTools = null!;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class RequestToolsInfo
{
    public string PlayerUid = null!;
}