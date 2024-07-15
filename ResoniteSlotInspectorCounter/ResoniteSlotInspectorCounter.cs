using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes;
using FrooxEngine.UIX;
using HarmonyLib;
using ResoniteModLoader;
using System.Reflection;

namespace ResoniteSlotInspectorCounter
{
	public class ResoniteSlotInspectorCounter : ResoniteMod
	{
		public override string Name => "ResoniteSlotInspectorCounter";
		public override string Author => "NepuShiro, xLinka";
		public override string Version => "1.0.0";
		public override string Link => "https://github.com/NepuShiro/ResoniteSlotInspectorCounter";

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<bool> enabled = new("enabled", "Should the mod be enabled", () => true);

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<colorX> CLOSED_COLOR = new("closedColor", "Expanded Color", () => new colorX(1, 0, 1, 1, ColorProfile.Linear));

        [AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<colorX> OPENED_COLOR = new("openedColor", "Collapsed Color", () => new colorX((float)0.6, 0, (float)0.6, 1, ColorProfile.Linear));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<colorX> EMPTY_COLOR = new("emptyColor", "Empty Color", () => new colorX(1, 0, 1, 1, ColorProfile.Linear));

        private static ModConfiguration Config;

		public override void OnEngineInit()
		{
			Config = GetConfiguration();
			Config.Save(true);
			Harmony harmony = new("net.nepushiro.ResoniteSlotInspectorCounter");
			harmony.PatchAll();
		}

		[HarmonyPatch(typeof(SlotInspector), "OnChanges")]
		class SlotInspector_Patch
		{
			public static void Postfix(SlotInspector __instance)
			{
				if (!Config.GetValue(enabled))
				{
					return;
				}
				
				__instance.ReferenceID.ExtractIDs(out var position, out var user);
                User userByAllocationID = __instance.World.GetUserByAllocationID(user);

                if (userByAllocationID == __instance.LocalUser)
				{
                    Slot rootSlot = __instance._rootSlot.Target;
                    if (rootSlot != null)
                    {
                        int totalChildCount = CountChildrenRecursively(rootSlot);

						string closedColor = $"<color={Config.GetValue(CLOSED_COLOR).ToHexString()}>{totalChildCount}</color>";
						string openedColor = $"<color={Config.GetValue(OPENED_COLOR).ToHexString()}>{totalChildCount}</color>";
						string emptyColor = $"<color={Config.GetValue(EMPTY_COLOR).ToHexString()}>{totalChildCount}</color>";

                        var text = __instance.Slot.GetComponentInChildren<TextExpandIndicator>();
                        text.Closed.Value = closedColor;
						text.Opened.Value = openedColor;

						var empty = text.Slot.GetComponent<ValueObjectInput<string>>();
						if (empty != null || text.Empty.IsDriven) 
							empty.Value.Value = emptyColor;
						else 
							text.Empty.Value = emptyColor;
					}
				}
			}

			private static int CountChildrenRecursively(Slot slot)
			{
				int count = slot.ChildrenCount;
				foreach (Slot child in slot.Children)
				{
					count += CountChildrenRecursively(child);
				}
				return count;
			}
		}
	}
}