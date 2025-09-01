using System;
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
        public override string Name => "ResoniteSlotInspectorCounter";
        public override string Author => "NepuShiro, xLinka";
        public override string Version => "1.8.0";
        public override string Link => "https://github.com/NepuShiro/ResoniteSlotInspectorCounter";

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> ENABLED = new ModConfigurationKey<bool>("Enabled", "Should the mod be enabled", () => true);
        
        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> DYNVARS = new ModConfigurationKey<bool>("dynvars", "Create DynVars for the Inspector Root Slot Count?", () => true);

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
                    if (!ENABLED.Value || __instance == null) return;

                    SceneInspector inspector = __instance.Slot.GetComponentInParents<SceneInspector>();
                    if (inspector == null) return;

                    User userByAllocationID = inspector.Slot.World.GetUserByAllocationID(inspector.Slot.ReferenceID.User);
                    if (userByAllocationID == null || userByAllocationID != inspector.Slot.LocalUser) return;

                    Slot rootSlot = ____rootSlot.Target;
                    if (rootSlot == null) return;

                    int totalChildCount = CountSlots(rootSlot);

                    string closedColor = $"<color={CLOSED_COLOR.Value.ToHexString()}>{totalChildCount}</color>";
                    string openedColor = $"<color={OPENED_COLOR.Value.ToHexString()}>{totalChildCount}</color>";
                    string emptyColor = $"<color={EMPTY_COLOR.Value.ToHexString()}>{totalChildCount}</color>";

                    Slot inspectorRoot = inspector.Root.Target;
                    if (LERP_COLOR.Value)
                    {
                        colorX baseColor = GetColorBasedOnSlotCount(totalChildCount, LERP_COLOR_INSPECTROOTSLOT.Value ? CountSlots(inspectorRoot) : LERP_COLOR_ROOTSLOT.Value ? CountSlots(__instance.World.RootSlot) : LERP_COLOR_MAX.Value);

                        closedColor = $"<color={baseColor.ToHexString()}>{totalChildCount}</color>";
                        openedColor = $"<color={baseColor.MulValue(0.7f).ToHexString()}>{totalChildCount}</color>";
                        emptyColor = $"<color={baseColor.MulValue(0.85f).ToHexString()}>{totalChildCount}</color>";
                    }

                    if (DYNVARS.Value)
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
                            __instance.Slot.RunInUpdates(2, () =>
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

                    TextExpandIndicator expanderIndicator = ____expanderIndicator.Target;
                    if (expanderIndicator == null) return;

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
                catch (Exception e)
                {
                    Error($"Error in SlotInspector Postfix: {e}");
                }
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
            colorX green = LERP_MIN_COLOR.Value;
            colorX yellow = LERP_MID_COLOR.Value;
            colorX red = LERP_MAX_COLOR.Value;
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