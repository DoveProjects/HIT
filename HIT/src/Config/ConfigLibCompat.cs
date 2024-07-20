using static Ele.HIT.ModConstants;
using System;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using ConfigLib;
using ImGuiNET;
using Ele.HIT;



namespace Ele.Configuration
{
    public class ConfigLibCompat
    {
        public ConfigLibModSystem ConfigLib { get; set; }
        private const string settingPrefix = $"{MOD_ID}:Config.Setting.";

        public ConfigLibCompat(ICoreAPI api)
        {
            ConfigLib = api.ModLoader.GetModSystem<ConfigLibModSystem>();
            ConfigLib.RegisterCustomConfig(MOD_NAME, (id, buttons) => EditConfig(id, buttons, api));
        }

        private void EditConfig(string id, ControlButtons buttons, ICoreAPI api)
        {
            if (buttons.Save) ModMain.ClientConfig = ConfigHelper.UpdateConfig(api, ModMain.ClientConfig);
            if (buttons.Restore) ModMain.ClientConfig = ConfigHelper.ReadConfig<ModConfig>(api, ConfigHelper.GetConfigPath(api));
            if (buttons.Defaults) ModMain.ClientConfig = new(api);
            Edit(api, ModMain.ClientConfig, id);
        }

        private void Edit(ICoreAPI api, ModConfig config, string id)
        {
            ImGui.TextWrapped($"{Lang.Get("mod-title")} Settings");

            config.Forearm_Tools_Enabled = OnCheckBox(id, config.Forearm_Tools_Enabled, nameof(config.Forearm_Tools_Enabled));
            config.Tools_On_Back_Enabled = OnCheckBox(id, config.Tools_On_Back_Enabled, nameof(config.Tools_On_Back_Enabled));
            config.Shields_Enabled = OnCheckBox(id, config.Shields_Enabled, nameof(config.Shields_Enabled));

            ImGui.SeparatorText("Favorited Hotbar Slots");
            config.Favorited_Slots_Enabled = OnCheckBox(id, config.Favorited_Slots_Enabled, nameof(config.Favorited_Slots_Enabled));
        }

        /// <summary>
        ///     Helper methods for setting up GUI elements
        /// </summary>
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
                ImGui.InputText($"{name}[{i}]##{name}-{id}-{i}", ref newValue, 64);
                newValues[i] = newValue;
            }

            if (ImGui.Button($"Add##{name}-{id}"))
            {
                newValues.Add("");
            }

            return newValues;
        }
    }
}
