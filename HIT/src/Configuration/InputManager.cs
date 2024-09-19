using static Elephant.HIT.ModConstants;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common.CommandAbbr;
using Elephant.Extensions;
using Vintagestory.GameContent;
using System.Collections.Generic;

namespace Elephant.HIT;

/// <summary>
///     A place to instantiate user-driven inputs -> mainly chat commands and hotkeys.
/// </summary>
public class InputManager
{
    private static readonly string Lang_Prefix = $"{MOD_ID}:{INPUT_SETTINGS}";
    private static ICoreClientAPI _capi;
    private static ICoreServerAPI _sapi;

    public readonly Dictionary<int, int> HotbarMap = new Dictionary<int, int>(){
        {1, 0},
        {2, 1},
        {3, 2},
        {4, 3},
        {5, 4},
        {6, 5},
        {7, 6},
        {8, 7},
        {9, 8},
        {0, 9}
    };

    public InputManager(ICoreAPI api)
    {
        _capi = api as ICoreClientAPI;
        _sapi = api as ICoreServerAPI;

        RegisterParentCommand(_capi);

        //_capi.Input.RegisterHotKey("placeholder", "This is a placeholder hotkey.", GlKeys.Period, HotkeyType.DevTool);
        //_capi.Input.SetHotKeyHandler("placeholder", combo => false);
    }

    public static bool IsKeyComboActive(string key)
    {
        KeyCombination combo = _capi.Input.GetHotKeyByCode(key).CurrentMapping;
        return _capi.Input.KeyboardKeyState[combo.KeyCode];
    }

    /// <summary>
    ///     Registers a 'parent' command named after the modid, populated with any number of sub-commands.
    ///     The side of the api object passed determines the side of the command.
    /// </summary>
    private void RegisterParentCommand(ICoreAPI api)
    {
        api = api.GetSidedAPI();
        CommandArgumentParsers parsers = api.ChatCommands.Parsers;

        api.ChatCommands.Create($"{MOD_ID}")
            .WithDescription($"The {MOD_NAME} parent command.") //Description is for the command handbook
            .RequiresPrivilege(Privilege.chat)                  //Can't be used by chat-muted players
            .RequiresPlayer()                                   //Important, unless the commands are run through console
            .BeginSubCommand("disable")
                .WithDescription("Disables certain tool rendering settings")
                .WithArgs(parsers.OptionalWordRange("setting", new string[4] { "arms", "back", "shields", "favorites" }))
                .HandleWith((args) => { return OnToggleConfigSetting(args, false); }) //this sets the command to be handled by the 'OnToggleConfigSetting' method below
            .EndSub()
            .BeginSubCommand("enable")
                .WithDescription("Enables certain tool rendering settings")
                .WithArgs(parsers.OptionalWordRange("setting", new string[4] { "arms", "back", "shields", "favorites" }))
                .HandleWith((args) => { return OnToggleConfigSetting(args, true); }) //both sub-commands are handled by the same handler method
            .EndSub()
            .BeginSubCommand("set-favorites") //sets up a system that prefers specific slots above others instead of marching through the hotbar in order
                .WithDescription("Sets up to 5 (optional) favorited hotbar slots for selective tool rendering")
                .WithArgs(
                    parsers.OptionalIntRange("slot 1", 0, 9, -1),
                    parsers.OptionalIntRange("slot 2", 0, 9, -1),
                    parsers.OptionalIntRange("slot 3", 0, 9, -1),
                    parsers.OptionalIntRange("slot 4", 0, 9, -1),
                    parsers.OptionalIntRange("slot 5", 0, 9, -1))
                .HandleWith(new OnCommandDelegate(OnSetFavoriteSlots)) //standalone method, args are automatically passed as the only parameter
            .EndSub()
            .BeginSubCommand("reset")
                .WithDescription("Resets all rendering settings to their default values")
                .HandleWith(new OnCommandDelegate(OnConfigReset)) //standalone method, args are automatically passed as the only parameter
            .EndSub()
            .Validate();
    }

    //Handles the 'enable' and 'disable' sub-commands
    private TextCommandResult OnToggleConfigSetting(TextCommandCallingArgs args, bool toggled)
    {
        var player = args.Caller.Player;
        ClientConfig config = ConfigManager.ClientConfig;
        if (config != null) //if the player hasn't registered their client-side config, throw an error
        {
            var ChangedSetting = (string)args[0];
            switch (ChangedSetting) //switches through all valid sub-command arguments and sets the appropriate config value
            {
                case "arms":
                    config.Forearm_Tools_Enabled = toggled;
                    break;
                case "back":
                    config.Tools_On_Back_Enabled = toggled;
                    break;
                case "shields":
                    config.Shields_Enabled = toggled;
                    break;
                case "favorites":
                    config.Favorited_Slots_Enabled = toggled;
                    break;
                default:
                    return TextCommandResult.Error("");
            }
            _capi.Event.PushEvent(EventIDs.Client_Send_Config);
            return TextCommandResult.Success($"Config settings for {player.PlayerName} successfully updated.");
        }
        else
        {
            return TextCommandResult.Error("Client configs not initialized yet.");
        }
    }

    //Handles the 'set-favorites' sub-command
    private TextCommandResult OnSetFavoriteSlots(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player;
        ClientConfig config = ConfigManager.ClientConfig;
        if (config != null)
        {
            for (int j = 0; j < args.ArgCount; j++) //iterates through each arg, the total of which will always be 5 (will return -1 as a default value if none provided)
            {
                if ((int)args[j] != -1) //if an int was provided, update the index in the Favorited_Slots config array using the HotbarMap
                {
                    config.Favorited_Slots[j] = HotbarMap[(int)args[j]];
                }
                else //otherwise, updates the Favorited_Slots index to -1 to disable it
                {
                    config.Favorited_Slots[j] = -1;
                }
            }
            _capi.Event.PushEvent(EventIDs.Client_Send_Config);
            return TextCommandResult.Success($"Config settings for {player.PlayerName} successfully updated.");
        }
        else
        {
            return TextCommandResult.Error("No client configs available.");
        }
    }

    //A simple handler method that resets all config values by regenerating a new config
    private TextCommandResult OnConfigReset(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player;
        ClientConfig config = ConfigManager.ClientConfig;
        if (config != null)
        {
            config = new ClientConfig(config.Info);
            _capi.Event.PushEvent(EventIDs.Client_Send_Config);
            return TextCommandResult.Success($"Config settings reset to default.");
        }
        else
        {
            return TextCommandResult.Error("No client configs available.");
        }
    }
}