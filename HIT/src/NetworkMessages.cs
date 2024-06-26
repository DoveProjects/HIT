﻿using System.Collections.Generic;
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
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdatePlayerTools
{
    public string PlayerUid = null!;
    public int[] disabledSlots = null!;
    public BackPackType BackPackType = BackPackType.None;
    public Dictionary<int, SlotData> RenderedTools = null!;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class RequestToolsInfo
{
    public string PlayerUid = null!;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class HITPlayerData
{
    public bool IsDirty;

    [ProtoMember(1)]
    public bool ForearmsSheathDisabled;

    [ProtoMember(2)]
    public bool BackSheathDisabled;

    [ProtoMember(3)]
    public bool ShieldSlotDisabled;

    internal void MarkDirty()
    {
        if (!IsDirty)
        {
            IsDirty = true;
        }
    }
}