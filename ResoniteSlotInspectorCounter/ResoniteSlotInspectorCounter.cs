using System;
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
		public override string Version => "1.0.0";
		public override string Link => "https://github.com/NepuShiro/ResoniteSlotInspectorCounter";

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<bool> ENABLED = new("enabled", "Should the mod be enabled", () => true);

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<bool> BOOL = new("bool", "Should the Active Toggle Button be enabled", () => true);

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<colorX> CLOSED_COLOR = new("closedColor", "Expanded Color", () => new colorX(1, 0, 1, 1, ColorProfile.Linear));

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<colorX> OPENED_COLOR = new("openedColor", "Collapsed Color", () => new colorX(0.6f, 0, 0.6f, 1, ColorProfile.Linear));

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
				if (!Config.GetValue(ENABLED))
				{
					return;
				}

				Slot Inspector = __instance.Slot.GetObjectRoot();

				__instance.ReferenceID.ExtractIDs(out var position, out var user);
				User userByAllocationID = __instance.World.GetUserByAllocationID(user);

				if (userByAllocationID == __instance.LocalUser)
				{
					Slot rootSlot = __instance._rootSlot.Target;
					Slot instanceSlot = __instance.Slot;
					if (rootSlot != null)
					{
						int totalChildCount = CountChildrenRecursively(rootSlot);

						string closedColor = $"<color={Config.GetValue(CLOSED_COLOR).ToHexString()}>{totalChildCount}</color>";
						string openedColor = $"<color={Config.GetValue(OPENED_COLOR).ToHexString()}>{totalChildCount}</color>";
						string emptyColor = $"<color={Config.GetValue(EMPTY_COLOR).ToHexString()}>{totalChildCount}</color>";

						TextExpandIndicator text = instanceSlot.GetComponentInChildren<TextExpandIndicator>();
						text.Closed.Value = closedColor;
						text.Opened.Value = openedColor;

						ValueObjectInput<string> empty = text.Slot.GetComponent<ValueObjectInput<string> >();
						if (empty != null || text.Empty.IsDriven)
						{
							empty.Value.Value = emptyColor;
						}
						else
						{
							text.Empty.Value = emptyColor;
						}

						if (Config.GetValue(BOOL))
						{
							try
							{
								__instance.RunInUpdates(5, () =>
									{
										Slot Hori = instanceSlot.FindChild("Horizontal Layout");
										if (Hori.FindChild("Button: Active") == null)
										{
											Hori.FindChild("Text").OrderOffset = 1;

											UIBuilder uIBuilder = new(Hori);
											RadiantUI_Constants.SetupEditorStyle(uIBuilder);
											uIBuilder.Style.MinHeight = 24f;
											uIBuilder.Style.MinWidth = 24f;
											uIBuilder.Style.FlexibleHeight = 1f;
											Checkbox DupleCheckbox = uIBuilder.Checkbox(
												rootSlot.ActiveSelf
											);
											Button button =
												DupleCheckbox.Slot.GetComponent<Button>();
											DupleCheckbox.Slot.Parent.Name = "Button: Active";
											DupleCheckbox.State.Value = rootSlot.ActiveSelf;

											colorX color2 = MemberEditor.GetFieldColor(
												rootSlot.ActiveSelf_Field
											);
											if (rootSlot.ActiveSelf_Field.IsDriven)
											{
												ValueCopy<bool> valcopy =
													button.Slot.AttachComponent<ValueCopy<bool>>();
												valcopy.WriteBack.Value = rootSlot
													.ActiveSelf_Field
													.IsHooked;
												valcopy.Source.Value = rootSlot
													.ActiveSelf_Field
													.ReferenceID;
												valcopy.Target.Value = DupleCheckbox
													.State
													.ReferenceID;

												button.SetColors(in color2);
											}
											else
											{
												DupleCheckbox.TargetState.Value = rootSlot
													.ActiveSelf_Field
													.ReferenceID;

												button.SetColors(in color2);
											}
										}
									}
								);
							}
							catch (Exception e)
							{
								UniLog.Error($"Error while trying to add the Active Checkbox button: {e}");
							}
						}
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
