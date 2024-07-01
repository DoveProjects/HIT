using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace HIT;

public class PlayerToolWatcher
{
    private readonly IPlayer _player; //player
    private readonly ItemSlot[] _bodyArray = new ItemSlot[HITModSystem.TotalSlots]; //body array that's used to check if a "slot" (sheath) is filled or not
    private readonly List<IInventory> _inventories; //declared here for use in combining inventories for XSkills Compat (adds extra inv)
    private readonly IInventory _backpacks; //used for updates on if the backpack changed (since hotbar.SlotModified only returns for the 0-9 hotbar)
    private BackPackType _backPackType;
    public HITConfig ClientConfig;
    public PlayerToolWatcher(IPlayer player, HITConfig ClientConfig)
    {
        _player = player;
        _inventories = GetToolInventories(); //adds the IInventories to inventories
        this.ClientConfig = ClientConfig;

        _backpacks = _player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
        if (_backpacks != null)
        {
            CheckBackpackType();
            _backpacks.SlotModified += BackpacksOnSlotModified;
        }

        foreach (var inventory in _inventories) //add any slots modified to inventory (removal of excess handled in disposal)
        {
            inventory.SlotModified += UpdateInventories;
        }
        UpdateInventories(0);
    }

    private void CheckBackpackType()
    {
        if (_backpacks == null) return; //return null so whatever called it knows no backpack exists
        _backPackType = BackPackType.None; //reset var to prevent overflow
        if (_backpacks.Any(slot => slot is ItemSlotBackpack && slot.Itemstack?.Collectible?.Code?.Path == "backpack")) //if the path just has backpack it's a leather backpack
        {
            _backPackType = BackPackType.Leather;
        }
        else if (_backpacks.Any(slot => slot is ItemSlotBackpack && slot.Itemstack?.Collectible?.Code?.Path == "hunterbackpack")) //else if the path has hunterbackpack it's self explanatory
        {
            _backPackType = BackPackType.Hunter;
        }

    }
    private void BackpacksOnSlotModified(int slotId) //when backpack slots are modified
    {
        var currentBackpack = _backPackType; //if the backpack is the same type as the current backpack don't do anything, else, update.
        CheckBackpackType();

        if (currentBackpack != _backPackType) UpdateInventories(0);


    }

    private List<IInventory> GetToolInventories()
    {
        List<IInventory> inventories = new() { //inventories set to hotbar
            _player.InventoryManager.GetHotbarInventory()
        };

        if (_player.Entity.Api.ModLoader.IsModEnabled("xskills")) //checking if xskills is loaded, then adds it to the inventories/hotbar
        {
            var possibleInventory = _player.InventoryManager.GetOwnInventory("xskillshotbar");
            if (possibleInventory != null) inventories.Add(possibleInventory);
        }

        return inventories;
    }

    private void TryOccupySlot(ItemSlot itemSlot, int[] fitsInto, ItemSlot[] slotsArray)
    {
        foreach (var i in fitsInto) //loops through the list of sheaths
        {
            if (slotsArray[i] != null) continue; //going through the bodyArray and using fitsInto just as a list of what sheath the weapontype can fit into
            slotsArray[i] = itemSlot; //if fits, it fits
            break;
        }
    }

    private void UpdateInventories(int slotId)
    {
        Array.Clear(_bodyArray, 0, _bodyArray.Length); //clears bodyArray to not return false positives
        foreach (var inventory in _inventories) //updates inventory + extraInvs (e.g. XSkills)
        {
            UpdateInventory(inventory);
        }

        HITModSystem.ServerChannel.BroadcastPacket(GenerateUpdateMessage());//Broadcasts every time inventory shifts
    }

    private void UpdateInventory(IInventory inventory)
    {
        foreach (ItemSlot itemSlot in inventory) //loop through inv
        {
            if (itemSlot.Itemstack == null) continue; //if blank slot, skip
            if (ClientConfig.Favorited_Slots_Enabled) //if favorited slots enabled in config, skip if slot isn't favorited
            {
                if (Array.IndexOf(ClientConfig.Favorited_Slots, inventory.GetSlotId(itemSlot)) == -1) continue;
            }

            if (itemSlot.Itemstack.Collectible is ItemShield) 
            {
                if (ClientConfig.Shields_Enabled) //if shield rendering enabled in config, try to occupy the shield slot (4)
                TryOccupySlot(itemSlot, new[] { 4 }, _bodyArray);
                continue;
            }

            if (itemSlot.Itemstack.Collectible.Tool != null) //if its a tool
            {
                switch (itemSlot.Itemstack.Collectible.Tool) //switch case to determine "size", whether it can fit on arms (0/1) or back (2,3)
                {
                    case EnumTool.Knife:
                    case EnumTool.Chisel:
                        if (ClientConfig.Forearm_Tools_Enabled) //if forearm rendering enabled in config, try to occupy a sheath slot
                        TryOccupySlot(itemSlot, new[] { 0, 1 }, _bodyArray);
                        break;
                    default:
                        if (ClientConfig.Tools_On_Back_Enabled) //if tool rendering on back enabled in config, try to occupy a sheath slot
                        TryOccupySlot(itemSlot, new[] { 2, 3 }, _bodyArray);
                        break;
                }
            }
        }
    }

    public UpdatePlayerTools GenerateUpdateMessage()
    {
        return new UpdatePlayerTools()
        {
            BackPackType = _backPackType,
            PlayerUid = _player.PlayerUID,
            RenderedTools = _bodyArray.Select(
                    (slot, index) =>
                        new
                        {
                            Key = index,
                            Value = slot?.Itemstack.Collectible == null //if slot full, and it's a type collectible (for shields AND tools) create a new slot in dict
                                ? null
                                : new SlotData() //slotdata contains the name of the tool, the type of the itemStack, and its currently occupied hotbar slot
                                {
                                    Code = slot.Itemstack.Collectible.Code.ToString(),
                                    StackData = slot.Itemstack.ToBytes(),
                                    HotbarID = slot.Inventory.GetSlotId(slot)
                                }
                        })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value) //cacheing the tool into a dictionary
        };
    }

    public void Dispose() //to prevent memory leaks due to the models not being unrendered
    {
        for (var i = _inventories.Count - 1; i >= 0; i--) //dump inventory meshes
        {
            _inventories[i].SlotModified -= UpdateInventories;
        }

        if (_backpacks != null) //remove the mesh from the slot that's been changed
        {
            _backpacks.SlotModified -= BackpacksOnSlotModified;
        }
    }
}