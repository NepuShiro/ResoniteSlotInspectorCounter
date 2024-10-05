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
		public override string Version => "1.2.0";
		public override string Link => "https://github.com/NepuShiro/ResoniteSlotInspectorCounter";

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<bool> ENABLED = new("enabled", "Should the mod be enabled", () => true);

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<bool> ACTIVE_BOOL = new("bool", "Should the Active Toggle Button be enabled", () => true);

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<colorX> CLOSED_COLOR = new("closedColor", "Expanded Color", () => new colorX(1, 1, 1, 1, ColorProfile.Linear));

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<colorX> OPENED_COLOR = new("openedColor", "Collapsed Color", () => new colorX(0.6f, 0.6f, 0.6f, 1, ColorProfile.Linear));

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<colorX> EMPTY_COLOR = new("emptyColor", "Empty Color", () => new colorX(1, 1, 1, 1, ColorProfile.Linear));

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
			public static void Postfix(SlotInspector __instance, SyncRef<Slot> ____rootSlot, SyncRef<TextExpandIndicator> ____expanderIndicator)
			{
				try 
				{
					if (!Config.GetValue(ENABLED) || __instance == null) return;
					
					__instance.ReferenceID.ExtractIDs(out var position, out var user);
					User userByAllocationID = __instance.World.GetUserByAllocationID(user);

					if (userByAllocationID == null || position < userByAllocationID.AllocationIDStart || userByAllocationID != __instance.LocalUser) return;
					
					Slot rootSlot = ____rootSlot.Target;
					if (rootSlot != null)
					{
						int totalChildCount = CountSlots(rootSlot);

						string closedColor = $"<color={Config.GetValue(CLOSED_COLOR).ToHexString()}>{totalChildCount}</color>";
						string openedColor = $"<color={Config.GetValue(OPENED_COLOR).ToHexString()}>{totalChildCount}</color>";
						string emptyColor = $"<color={Config.GetValue(EMPTY_COLOR).ToHexString()}>{totalChildCount}</color>";

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
							__instance.World.RunInUpdates(5, () =>
							{
								try
								{
									Slot Hori = __instance.Slot.FindChild("Horizontal Layout");
									if (Hori != null && Hori.FindChild("Button: Active") == null)
									{
										var textSlot = Hori.FindChild("Text");
										if (textSlot == null)
										{
											Msg("Error: Text slot not found");
											return;
										}
										textSlot.OrderOffset = 1;

										UIBuilder uIBuilder = new(Hori);
										RadiantUI_Constants.SetupEditorStyle(uIBuilder);
										uIBuilder.Style.MinHeight = 24f;
										uIBuilder.Style.MinWidth = 24f;
										uIBuilder.Style.FlexibleHeight = 1f;

										if (rootSlot == null)
										{
											Msg("Error: rootSlot is null");
											return;
										}

										Checkbox DupleCheckbox = uIBuilder.Checkbox(rootSlot.ActiveSelf);
										if (DupleCheckbox == null || DupleCheckbox.Slot == null || DupleCheckbox.State == null)
										{
											Msg("Error: DupleCheckbox or DupleCheckbox.Slot or DupleCheckbox.State is null");
											return;
										}

										Button button = DupleCheckbox.Slot.GetComponent<Button>();
										if (button == null)
										{
											Msg("Error: Button component is null");
											return;
										}

										DupleCheckbox.Slot.Parent.Name = "Button: Active";
										DupleCheckbox.State.Value = rootSlot.ActiveSelf;

										colorX color2 = MemberEditor.GetFieldColor(rootSlot.ActiveSelf_Field);
										if (rootSlot.ActiveSelf_Field.IsDriven)
										{
											ValueCopy<bool> valcopy = button.Slot.AttachComponent<ValueCopy<bool>>();
											valcopy.WriteBack.Value = rootSlot.ActiveSelf_Field.IsHooked;
											valcopy.Source.Target = rootSlot.ActiveSelf_Field;
											valcopy.Target.Target = DupleCheckbox.State;

											button.SetColors(in color2);
										}
										else
										{
											DupleCheckbox.TargetState.Target = rootSlot.ActiveSelf_Field;
											button.SetColors(in color2);
										}
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
	}
}
