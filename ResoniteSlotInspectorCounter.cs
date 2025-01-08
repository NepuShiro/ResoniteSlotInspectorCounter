using System;
using System.Linq;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes;
using FrooxEngine.UIX;
using HarmonyLib;
using ResoniteModLoader;

namespace ResoniteSlotInspectorCounter
{
    public class ResoniteSlotInspectorCounter : ResoniteMod
    {
        public override string Name => "ResoniteSlotInspectorCounter";
        public override string Author => "NepuShiro, xLinka";
        public override string Version => "1.5.0";
        public override string Link => "https://github.com/NepuShiro/ResoniteSlotInspectorCounter";

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> ENABLED = new("Enabled", "Should the mod be enabled", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> ACTIVE_BOOL = new("Active Checkbox", "Should the Active Toggle Button be enabled", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> DYNVARS = new("Create DynVars", "Create DynVars for the Inspector Root Slot Count?", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<dummy> DUMMY0 = new("");

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<colorX> CLOSED_COLOR = new("closedColor", "Collapsed Color", () => new colorX(1, 1, 1, 1, ColorProfile.Linear));
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<colorX> OPENED_COLOR = new("openedColor", "Expanded Color", () => new colorX(0.6f, 0.6f, 0.6f, 1, ColorProfile.Linear));
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<colorX> EMPTY_COLOR = new("emptyColor", "Empty Color", () => new colorX(1, 1, 1, 1, ColorProfile.Linear));


        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<dummy> DUMMY1 = new("");

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> LERP_COLOR = new("Lerp Color", "Should the SlotCount color be lerped instead?", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> LERP_COLOR_ROOTSLOT = new("RootSlot Max", "Use the RootSlot's Slot count as the max?", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> LERP_COLOR_INSPECTROOTSLOT = new("Inspected Slot Max", "Use the Inspected Slot's Slot count as the max?", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<int> LERP_COLOR_MAX = new("Max SlotCount", "The amount of slots for the Color to be the Max Lerp Color", () => 10000);


        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<dummy> DUMMY2 = new("");

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<colorX> LERP_MIN_COLOR = new("Min Lerp Color", "Min Lerp Color", () => new colorX(0.0f, 1.0f, 0.0f, 1.0f, ColorProfile.Linear));
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<colorX> LERP_MID_COLOR = new("Mid Lerp Color", "Mid Lerp Color", () => new colorX(1.0f, 1.0f, 0.0f, 1.0f, ColorProfile.Linear));
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<colorX> LERP_MAX_COLOR = new("Max Lerp Color", "Max Lerp Color", () => new colorX(1.0f, 0.0f, 0.0f, 1.0f, ColorProfile.Linear));

        private static ModConfiguration Config;

        public override void OnEngineInit()
        {
            Config = GetConfiguration();
            Config.Save(true);

            Harmony harmony = new("net.NepuShiro.ResoniteSlotInspectorCounter");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(SlotInspector), "OnChanges")]
        class SlotInspector_Patch
        {
            public static void Postfix(SlotInspector __instance, SyncRef<Slot> ____rootSlot, SyncRef<TextExpandIndicator> ____expanderIndicator)
            {
                try
                {
                    if (!Config.GetValue(ENABLED) || __instance == null) return;

                    __instance.Slot.ReferenceID.ExtractIDs(out var position, out var user);
                    User userByAllocationID = __instance.Slot.World.GetUserByAllocationID(user);
                    if (userByAllocationID == null || position < userByAllocationID.AllocationIDStart || userByAllocationID != __instance.Slot.LocalUser) return;

                    Slot rootSlot = ____rootSlot.Target;
                    if (rootSlot != null)
                    {
                        int totalChildCount = CountSlots(rootSlot);

                        string closedColor = $"<color={Config.GetValue(CLOSED_COLOR).ToHexString()}>{totalChildCount}</color>";
                        string openedColor = $"<color={Config.GetValue(OPENED_COLOR).ToHexString()}>{totalChildCount}</color>";
                        string emptyColor = $"<color={Config.GetValue(EMPTY_COLOR).ToHexString()}>{totalChildCount}</color>";

                        SceneInspector inspector = __instance.Slot.GetComponentInParents<SceneInspector>();
                        if (inspector != null)
                        {
                            Slot inspectorRoot = inspector.Root.Target;
                            if (Config.GetValue(LERP_COLOR))
                            {
                                string hexedColor = GetColorBasedOnSlotCount(totalChildCount, Config.GetValue(LERP_COLOR_INSPECTROOTSLOT) ? CountSlots(inspectorRoot) : Config.GetValue(LERP_COLOR_ROOTSLOT) ? CountSlots(__instance.World.RootSlot) : Config.GetValue(LERP_COLOR_MAX)).ToHexString();

                                closedColor = $"<color={hexedColor}>{totalChildCount}</color>";
                                openedColor = $"<color={hexedColor}>{totalChildCount}</color>";
                                emptyColor = $"<color={hexedColor}>{totalChildCount}</color>";
                            }

                            if (Config.GetValue(DYNVARS))
                            {
                                DynamicVariableSpace dynVarSpace = inspector.Slot.GetComponentOrAttach<DynamicVariableSpace>();
                                if (string.IsNullOrEmpty(dynVarSpace.SpaceName.Value))
                                {
                                    dynVarSpace.SpaceName.Value = "Inspector";
                                    dynVarSpace.Persistent = false;
                                }

                                if (inspectorRoot != null && rootSlot == inspectorRoot)
                                {
                                    __instance.Slot.RunInUpdates(2, () =>
                                    {
                                        var slotvars = inspector.Slot.FindChildOrAdd("Slot Count Vars", false);

                                        var dynVarString = slotvars.GetComponentOrAttach<DynamicValueVariable<string>>();
                                        string dynVarStringName = $"{dynVarSpace.SpaceName.Value}/SlotCountString";
                                        if (string.IsNullOrEmpty(dynVarString.VariableName.Value))
                                        {
                                            dynVarString.Persistent = false;
                                            dynVarString.VariableName.Value = dynVarStringName;
                                            dynVarString.Value.Value = openedColor;
                                        }

                                        var dynVarInt = slotvars.GetComponentOrAttach<DynamicValueVariable<int>>();
                                        string dynVarIntName = $"{dynVarSpace.SpaceName.Value}/SlotCountInt";
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
                        }

                        TextExpandIndicator expanderIndicator = ____expanderIndicator.Target;
                        if (expanderIndicator == null) return;

                        expanderIndicator.Closed.Value = closedColor;
                        expanderIndicator.Opened.Value = openedColor;

                        ValueObjectInput<string> empty = expanderIndicator.Slot.GetComponent<ValueObjectInput<string>>();
                        if (empty != null || expanderIndicator.Empty.IsDriven)
                        {
                            empty.Value.Value = emptyColor;
                        }
                        else
                        {
                            expanderIndicator.Empty.Value = emptyColor;
                        }

                        if (Config.GetValue(ACTIVE_BOOL) && __instance.World != null)
                        {
                            __instance.World.RunSynchronously(() =>
                            {
                                try
                                {
                                    Slot Hori = __instance.Slot.FindChild("Horizontal Layout");
                                    if (Hori != null && Hori.GetComponent<BooleanMemberEditor>() == null)
                                    {
                                        var textSlot = Hori.FindChild("Text");
                                        if (textSlot == null)
                                        {
                                            Warn("Text slot not found");
                                            return;
                                        }
                                        textSlot.OrderOffset = 1;

                                        UIBuilder uIBuilder = new UIBuilder(Hori);
                                        RadiantUI_Constants.SetupEditorStyle(uIBuilder);
                                        uIBuilder.Style.MinHeight = 24f;
                                        uIBuilder.Style.MinWidth = 24f;
                                        uIBuilder.Style.FlexibleHeight = 1f;

                                        if (rootSlot == null)
                                        {
                                            Warn("rootSlot is null");
                                            return;
                                        }

                                        uIBuilder.BooleanMemberEditor(rootSlot.ActiveSelf_Field);
                                    }
                                }
                                catch (Exception e)
                                {
                                    Error($"Error in SlotInspector RunInUpdates: {e}");
                                }
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    Error($"Error in SlotInspector Postfix: {e}");
                }
            }
        }

        internal static int CountSlots(Slot slot)
        {
            if (slot == null) return 0;

            int slotCount = 0;

            slot.ForeachChild((child) => slotCount++);

            return slotCount;
        }

        internal static colorX Lerp(colorX a, colorX b, float t, ColorProfile colorProfile)
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

        internal static colorX GetColorBasedOnSlotCount(int slotCount, int maxSlotCount)
        {
            colorX green = Config.GetValue(LERP_MIN_COLOR);
            colorX yellow = Config.GetValue(LERP_MID_COLOR);
            colorX red = Config.GetValue(LERP_MAX_COLOR);
            ColorProfile profile = GetMostCommonProfile(green.Profile, yellow.Profile, red.Profile);

            float t = (float)slotCount / maxSlotCount;

            if (t <= 0.5f)
            {
                return Lerp(green, yellow, t * 2, profile);
            }
            else
            {
                return Lerp(yellow, red, (t - 0.5f) * 2, profile);
            }
        }

        private static ColorProfile GetMostCommonProfile(ColorProfile a, ColorProfile b, ColorProfile c)
        {
            var profiles = new[] { a, b, c };
            return profiles.GroupBy(p => p)
                           .OrderByDescending(g => g.Count())
                           .First()
                           .Key;
        }
    }
}
