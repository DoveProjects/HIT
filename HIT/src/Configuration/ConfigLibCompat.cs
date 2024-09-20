using static Elephant.HIT.ModConstants;
using System;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Elephant.Configuration;
using ConfigLib;
using ImGuiNET;
using Vintagestory.API.Util;

namespace Elephant.HIT;

//Courtesy of https://github.com/maltiez2/ && https://github.com/jayugg/
public class ConfigLibCompat
{
    public ConfigLibModSystem ConfigLib { get; set; }

    internal string titlePrefix = $"{MOD_ID}:Config-Title.";
    internal string settingPrefix = $"{MOD_ID}:Config-Setting.";
    internal string propertyPrefix = $"{MOD_ID}:Server-Property.";

    internal string syncedTitle;
    internal string clientTitle;
    internal string serverTitle;

    public ConfigLibCompat(ICoreAPI api)
    {
        ConfigLib = api.ModLoader.GetModSystem<ConfigLibModSystem>();

        syncedTitle = Lang.Get(titlePrefix + "Synced");
        clientTitle = Lang.Get(titlePrefix + "Client");
        serverTitle = Lang.Get(titlePrefix + "Server");

        ConfigLib.RegisterCustomConfig(Lang.Get($"{MOD_ID}:Mod-Title"), (id, buttons) => EditSettings(id, buttons, api));
    }

    private void EditSettings(string id, ControlButtons buttons, ICoreAPI api)
    {
        Dictionary<string, IModConfig> configs = ConfigManager.ConfigsByName;
        if (buttons.Save) ConfigHelper.UpdateConfigDictionary(api, configs);
        if (buttons.Restore) ConfigHelper.ReadConfigDictionary(api, configs);
        if (buttons.Defaults) ConfigHelper.CloneConfigDictionary(api, configs);

        if (buttons.Save || buttons.Restore || buttons.Defaults) OnConfigsEdited(api, configs);
        BuildSettings(api, configs, id);
    }

    private void OnConfigsEdited(ICoreAPI api, Dictionary<string, IModConfig> configs)
    {
        foreach (var kvp in configs)
        {
            switch (kvp.Key)
            {
                case JSON_CONFIG_UNIVERSAL:
                    api.Event.PushEvent(EventIDs.Admin_Send_Config);
                    api.Event.PushEvent(EventIDs.Config_Reloaded);
                    break;
                case JSON_CONFIG_CLIENT:
                    api.Event.PushEvent(EventIDs.Client_Send_Config);
                    break;
                case JSON_CONFIG_SERVER:
                    api.Event.PushEvent(EventIDs.Admin_Send_Config);
                    break;
            }
        }
    }

    private void BuildSettings(ICoreAPI api, Dictionary<string, IModConfig> configs, string id)
    {
        foreach (var kvp in configs)
        {
            switch (kvp.Key)
            {
                case JSON_CONFIG_UNIVERSAL:
                    CoreConfigSettings(api, kvp.Value as UniversalConfig, id);
                    break;
                case JSON_CONFIG_CLIENT:
                    ClientConfigSettings(api, kvp.Value as ClientConfig, id);
                    break;
                case JSON_CONFIG_SERVER:
                    ServerConfigSettings(api, kvp.Value as ServerProperties, id);
                    break;
            }
        }
    }

    private void CoreConfigSettings(ICoreAPI api, UniversalConfig config, string id)
    {
        if (ImGui.CollapsingHeader(Lang.Get(syncedTitle) + $"##titlePrefix-{id}"))
        {
            //Set up further GUI elements here
        }
    }

    private void ClientConfigSettings(ICoreAPI api, ClientConfig config, string id)
    {
        if (api.Side == EnumAppSide.Client)
        {
            if (ImGui.CollapsingHeader(Lang.Get(clientTitle) + $"##titlePrefix-{id}"))
            {
                config.Forearm_Tools_Enabled = OnCheckBox(id, config.Forearm_Tools_Enabled, nameof(config.Forearm_Tools_Enabled));
                config.Tools_On_Back_Enabled = OnCheckBox(id, config.Tools_On_Back_Enabled, nameof(config.Tools_On_Back_Enabled));
                config.Shields_Enabled = OnCheckBox(id, config.Shields_Enabled, nameof(config.Shields_Enabled));

                ImGui.SeparatorText("Favorited Hotbar Slots");
                config.Favorited_Slots_Enabled = OnCheckBox(id, config.Favorited_Slots_Enabled, nameof(config.Favorited_Slots_Enabled));
                var favSlots = OnInputList(id, config.Favorited_Slots.Select(s => s.ToString()).ToList(), nameof(config.Favorited_Slots));
                config.Favorited_Slots = favSlots.ToList().ConvertAll<int>(obj => (obj.ToInt()));
            }
        }
    }

    private void ServerConfigSettings(ICoreAPI api, ServerProperties config, string id)
    {
        if (api.Side == EnumAppSide.Server || api is ICoreClientAPI { IsSinglePlayer: true })
        {
            if (ImGui.CollapsingHeader(Lang.Get(serverTitle) + $"##titlePrefix-{id}"))
            {
                //Set up further GUI elements here
            }
        }
    }

    #region Helpers
    private bool OnCheckBox(string id, bool value, string name, bool isDisabled = false)
    {
        bool newValue = value && !isDisabled;
        if (isDisabled)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * 0.5f);
        }
        if (ImGui.Checkbox(Lang.Get(settingPrefix + name) + $"##{name}-{id}", ref newValue))
        {
            if (isDisabled)
            {
                newValue = value;
            }
        }
        if (isDisabled)
        {
            ImGui.PopStyleVar();
        }
        return newValue;
    }

    private int OnInputInt(string id, int value, string name, int minValue = default)
    {
        int newValue = value;
        ImGui.InputInt(Lang.Get(settingPrefix + name) + $"##{name}-{id}", ref newValue, step: 1, step_fast: 10);
        return newValue < minValue ? minValue : newValue;
    }

    private float OnInputFloat(string id, float value, string name, float minValue = default)
    {
        float newValue = value;
        ImGui.InputFloat(Lang.Get(settingPrefix + name) + $"##{name}-{id}", ref newValue, step: 0.01f, step_fast: 1.0f);
        return newValue < minValue ? minValue : newValue;
    }

    private double OnInputDouble(string id, double value, string name, double minValue = default)
    {
        double newValue = value;
        ImGui.InputDouble(Lang.Get(settingPrefix + name) + $"##{name}-{id}", ref newValue, step: 0.01f, step_fast: 1.0f);
        return newValue < minValue ? minValue : newValue;
    }

    private string OnInputText(string id, string value, string name)
    {
        string newValue = value;
        ImGui.InputText(Lang.Get(settingPrefix + name) + $"##{name}-{id}", ref newValue, 64);
        return newValue;
    }

    private IEnumerable<string> OnInputTextMultiline(string id, IEnumerable<string> values, string name)
    {
        string newValue = values.Any() ? values.Aggregate((first, second) => $"{first}\n{second}") : "";
        ImGui.InputTextMultiline(Lang.Get(settingPrefix + name) + $"##{name}-{id}", ref newValue, 256, new(0, 0));
        return newValue.Split('\n', StringSplitOptions.RemoveEmptyEntries).AsEnumerable();
    }

    private T OnInputEnum<T>(string id, T value, string name) where T : Enum
    {
        string[] enumNames = Enum.GetNames(typeof(T));
        int index = Array.IndexOf(enumNames, value.ToString());

        if (ImGui.Combo(Lang.Get(settingPrefix + name) + $"##{name}-{id}", ref index, enumNames, enumNames.Length))
        {
            value = (T)Enum.Parse(typeof(T), enumNames[index]);
        }

        return value;
    }

    private List<string> OnInputList(string id, List<string> values, string name)
    {
        List<string> newValues = new List<string>(values);
        for (int i = 0; i < newValues.Count; i++)
        {
            string newValue = newValues[i];
            ImGui.InputText(Lang.Get(settingPrefix + name) + $" {i+1}##{name}-{id}-{i}", ref newValue, 64);
            newValues[i] = newValue;
        }

        if (ImGui.Button($"Add##{name}-{id}"))
        {
            if(newValues.Count <= 9) newValues.Add(newValues.Count.ToString());
        }
        ImGui.SameLine();
        if (ImGui.Button($"Remove##{name}-{id}"))
        {
            if (newValues.Count > 0) newValues.RemoveAt(newValues.Count - 1);
        }

        return newValues;
    }

    private List<T> OnInputList<T>(string id, List<T> values, string name) where T : struct, Enum
    {
        List<T> newValues = new List<T>(values);
        for (int i = 0; i < newValues.Count; i++)
        {
            string newValue = newValues[i].ToString();
            ImGui.InputText($"{name}[{i}]##{name}-{id}-{i}", ref newValue, 64);
            if (Enum.TryParse(newValue, out T parsedValue))
            {
                newValues[i] = parsedValue;
            }
        }

        if (ImGui.Button($"Add##{name}-{id}"))
        {
            newValues.Add(default);
        }

        return newValues;
    }
    #endregion
}