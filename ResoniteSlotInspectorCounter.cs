﻿using System;
using System.Linq;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes;
using FrooxEngine.UIX;
using HarmonyLib;
using Renderite.Shared;
using ResoniteModLoader;

namespace ResoniteSlotInspectorCounter
{
    public class ResoniteSlotInspectorCounter : ResoniteMod
    {
        internal const string VERSION_CONSTANT = "1.9.2";
        public override string Name => "ResoniteSlotInspectorCounter";
        public override string Author => "NepuShiro, LeCloutPanda, xLinka";
        public override string Version => VERSION_CONSTANT;
        public override string Link => "https://github.com/NepuShiro/ResoniteSlotInspectorCounter";

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> ENABLED = new ModConfigurationKey<bool>("Enabled", "Should the mod be enabled", () => true);
        
        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> RunInUpdates = new ModConfigurationKey<bool>("RunInUpdates", "RunInUpdates? Enable this if you're having issues with things applying.", () => false);
        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<int> RunInUpdatesAmount = new ModConfigurationKey<int>("RunInUpdatesAmount", "Amount of updates to wait.", () => 1);

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> DYNVARS = new ModConfigurationKey<bool>("dynvars", "Create DynVars for the Inspector Root Slot Count?", () => true);
        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> REPLACE_VANILLA_BUTTON = new ModConfigurationKey<bool>("Replace Vanilla Dropdown", "Should the mod replace the vanilla dropdown or create a new button", () => true);

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<dummy> DUMMY0 = new ModConfigurationKey<dummy>("-- Non Lerped Colors --", "-- Non Lerped Colors --");

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<colorX> CLOSED_COLOR = new ModConfigurationKey<colorX>("closedColor", "Collapsed Color", () => new colorX(1, 1, 1, 1, ColorProfile.Linear));
        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<colorX> OPENED_COLOR = new ModConfigurationKey<colorX>("openedColor", "Expanded Color", () => new colorX(0.6f, 0.6f, 0.6f, 1, ColorProfile.Linear));
        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<colorX> EMPTY_COLOR = new ModConfigurationKey<colorX>("emptyColor", "Empty Color", () => new colorX(1, 1, 1, 1, ColorProfile.Linear));

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<dummy> DUMMY1 = new ModConfigurationKey<dummy>("-- Lerped Color --", "-- Lerped Color --");

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> LERP_COLOR = new ModConfigurationKey<bool>("colorLerp", "Should the SlotCount color be lerped instead?", () => false);
        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> LERP_COLOR_ROOTSLOT = new ModConfigurationKey<bool>("useRootSlot", "Use the RootSlot's Slot count as the max?", () => false);
        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> LERP_COLOR_INSPECTROOTSLOT = new ModConfigurationKey<bool>("useInspectedSlot", "Use the Inspected Slot's Slot count as the max?", () => false);
        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<int> LERP_COLOR_MAX = new ModConfigurationKey<int>("maxSlotCount", "The amount of slots for the Color to be the Max Lerp Color", () => 10000);

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<dummy> DUMMY2 = new ModConfigurationKey<dummy>("-- Lerped Colors --", "-- Lerped Colors --");

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<colorX> LERP_MIN_COLOR = new ModConfigurationKey<colorX>("Min Lerp Color", "Min Lerp Color", () => new colorX(0.0f, 1.0f, 0.0f, 1.0f, ColorProfile.Linear));
        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<colorX> LERP_MID_COLOR = new ModConfigurationKey<colorX>("Mid Lerp Color", "Mid Lerp Color", () => new colorX(1.0f, 1.0f, 0.0f, 1.0f, ColorProfile.Linear));
        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<colorX> LERP_MAX_COLOR = new ModConfigurationKey<colorX>("Max Lerp Color", "Max Lerp Color", () => new colorX(1.0f, 0.0f, 0.0f, 1.0f, ColorProfile.Linear));

        private static ModConfiguration _config;

        public override void OnEngineInit()
        {
            _config = GetConfiguration();
            _config!.Save(true);

            Harmony harmony = new Harmony("net.NepuShiro.ResoniteSlotInspectorCounter");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(SlotInspector), "OnChanges")]
        private class SlotInspector_Patch
        {
            public static void Postfix(SlotInspector __instance, SyncRef<Slot> ____rootSlot, SyncRef<TextExpandIndicator> ____expanderIndicator)
            {
                try
                {
                    if (!_config.GetValue(ENABLED)) return;

                    SceneInspector inspector = __instance?.Slot?.GetComponentInParents<SceneInspector>();
                    Slot insSlot = inspector?.Slot;
                    if (insSlot == null) return;
        
                    insSlot.ReferenceID.ExtractIDs(out ulong position, out byte allocationId);
                    User user = insSlot.World.GetUserByAllocationID(allocationId);
                    if (user == null || position < user.AllocationIDStart || !user.IsLocalUser && user != insSlot.World.LocalUser) return;

                    if (_config.GetValue(RunInUpdates))
                    {
                        __instance.Slot.RunInUpdates(_config.GetValue(RunInUpdatesAmount), () => ExpanderReplacer(__instance, inspector, ____rootSlot, ____expanderIndicator));
                    }
                    else
                    {
                        ExpanderReplacer(__instance, inspector, ____rootSlot, ____expanderIndicator);
                    }
                }
                catch (Exception e)
                {
                    Error($"Error in SlotInspector Postfix: {e}");
                }
            }
        }

        private static void ExpanderReplacer(SlotInspector instance, SceneInspector inspector, SyncRef<Slot> _rootSlot, SyncRef<TextExpandIndicator> _expanderIndicator)
        {
            Slot rootSlot = _rootSlot.Target;
            if (rootSlot == null) return;

            int totalChildCount = CountSlots(rootSlot);

            string closedColor = $"<color={_config.GetValue(CLOSED_COLOR).ToHexString()}>{totalChildCount}</color>";
            string openedColor = $"<color={_config.GetValue(OPENED_COLOR).ToHexString()}>{totalChildCount}</color>";
            string emptyColor = $"<color={_config.GetValue(EMPTY_COLOR).ToHexString()}>{totalChildCount}</color>";

            Slot inspectorRoot = inspector.Root.Target;
            
            if (_config.GetValue(LERP_COLOR))
            {
                colorX baseColor = GetColorBasedOnSlotCount(totalChildCount, _config.GetValue(LERP_COLOR_INSPECTROOTSLOT) ? CountSlots(inspectorRoot) : _config.GetValue(LERP_COLOR_ROOTSLOT) ? CountSlots(instance.World.RootSlot) : _config.GetValue(LERP_COLOR_MAX));

                closedColor = $"<color={baseColor.ToHexString()}>{totalChildCount}</color>";
                openedColor = $"<color={baseColor.MulValue(0.7f).ToHexString()}>{totalChildCount}</color>";
                emptyColor = $"<color={baseColor.MulValue(0.85f).ToHexString()}>{totalChildCount}</color>";
            }

            if (_config.GetValue(DYNVARS))
            {
                DynamicVariableSpace dynVarSpace = inspector.Slot.GetComponentOrAttach<DynamicVariableSpace>();
                string dynVarSpaceName = dynVarSpace.SpaceName.Value;
                if (string.IsNullOrEmpty(dynVarSpaceName))
                {
                    dynVarSpace.SpaceName.Value = "Inspector";
                    dynVarSpace.Persistent = false;
                }

                if (inspectorRoot != null && rootSlot == inspectorRoot)
                {
                    instance.Slot.RunInUpdates(2, () =>
                    {
                        Slot slotvars = inspector.Slot.FindChildOrAdd("Slot Count Vars", false);

                        DynamicValueVariable<string> dynVarString = slotvars.GetComponentOrAttach<DynamicValueVariable<string>>();
                        string dynVarStringName = $"{dynVarSpaceName}/SlotCountString";
                        if (string.IsNullOrEmpty(dynVarString.VariableName.Value))
                        {
                            dynVarString.Persistent = false;
                            dynVarString.VariableName.Value = dynVarStringName;
                            dynVarString.Value.Value = openedColor;
                        }

                        DynamicValueVariable<int> dynVarInt = slotvars.GetComponentOrAttach<DynamicValueVariable<int>>();
                        string dynVarIntName = $"{dynVarSpaceName}/SlotCountInt";
                        if (string.IsNullOrEmpty(dynVarInt.VariableName.Value))
                        {
                            dynVarInt.Persistent = false;
                            dynVarInt.VariableName.Value = dynVarIntName;
                            dynVarInt.Value.Value = totalChildCount;
                        }

                        inspector.Slot.WriteDynamicVariable(dynVarStringName, openedColor);
                        inspector.Slot.WriteDynamicVariable(dynVarIntName, totalChildCount);
                    });
                }
            }

            TextExpandIndicator expanderIndicator = _expanderIndicator.Target;
            if (expanderIndicator == null) return;

            if (_config.GetValue(REPLACE_VANILLA_BUTTON))
            {
                Slot newExpander = expanderIndicator.Slot.Parent.FindChild("RSIC - Button");
                if (newExpander == null)
                {
                    newExpander = expanderIndicator.Slot.Duplicate();
                    newExpander.Name = "RSIC - Button";
                }

                newExpander.OrderOffset = -1;

                TextExpandIndicator fallbackIndicator = expanderIndicator;
                expanderIndicator = newExpander.GetComponent<TextExpandIndicator>();
                if (expanderIndicator == null)
                {
                    newExpander.Destroy();
                    expanderIndicator = fallbackIndicator;
                }
            }

            expanderIndicator.Closed.Value = closedColor;
            expanderIndicator.Opened.Value = openedColor;

            ValueObjectInput<string> empty = expanderIndicator.Slot.GetComponent<ValueObjectInput<string>>();
            if (empty != null || expanderIndicator.Empty.IsLinked)
            {
                empty.Value.Value = emptyColor;
            }
            else
            {
                expanderIndicator.Empty.Value = emptyColor;
            }
        }

        private static int CountSlots(Slot slot)
        {
            if (slot == null) return 0;

            int slotCount = 0;

            slot.ForeachChild(_ => slotCount++);

            return slotCount;
        }

        private static colorX Lerp(colorX a, colorX b, float t, ColorProfile colorProfile)
        {
            // t = t * t * (3 - 2 * t);
            return new colorX(
                a.r + (b.r - a.r) * t,
                a.g + (b.g - a.g) * t,
                a.b + (b.b - a.b) * t,
                a.a + (b.a - a.a) * t,
                colorProfile
            );
        }

        private static colorX GetColorBasedOnSlotCount(int slotCount, int maxSlotCount)
        {
            colorX green = _config.GetValue(LERP_MIN_COLOR);
            colorX yellow = _config.GetValue(LERP_MID_COLOR);
            colorX red = _config.GetValue(LERP_MAX_COLOR);
            ColorProfile profile = GetMostCommonProfile(green.Profile, yellow.Profile, red.Profile);

            float t = (float)slotCount / maxSlotCount;

            return t <= 0.5f
                    ? Lerp(green, yellow, t * 2, profile)
                    : Lerp(yellow, red, (t - 0.5f) * 2, profile);
        }

        private static ColorProfile GetMostCommonProfile(ColorProfile a, ColorProfile b, ColorProfile c)
        {
            ColorProfile[] profiles = { a, b, c };

            return profiles.GroupBy(p => p)
                           .OrderByDescending(g => g.Count())
                           .First()
                           .Key;
        }
    }
}