using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

// TODO: things to report:
// - injuries
// - raider ai
// - player changes to config
// - player designating (buttons & construction)

namespace RimGPT
{
	// run our logger
	//
	[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.LongEventsOnGUI))]
	public static class LongEventHandler_LongEventsOnGUI_Patch
	{
		public static void Postfix()
		{
			Logger.Log();
		}
	}

	// add welcome - need to configure message
	//
	[HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
	public static class MainMenuDrawer_MainMenuOnGUI_Patch
	{
		static bool showWelcome = true;
		static readonly Color background = new(0f, 0f, 0f, 0.8f);

		public static void Postfix()
		{
			if (showWelcome == false || RimGPTMod.Settings.IsConfigured)
			{
				UIRoot_Play_UIRootOnGUI_Patch.Postfix();
				return;
			}

			var (sw, sh) = (UI.screenWidth, UI.screenHeight);
			var (w, h) = (360, 120);
			var rect = new Rect((sw - w) / 2, (sh - h) / 2, w, h);
			var welcome = "Welcome to RimGPT. You need to configure the mod before you can use it. Click here.";

			Widgets.DrawBoxSolidWithOutline(rect, background, Color.white);
			if (Mouse.IsOver(rect) && Input.GetMouseButton(0))
			{
				showWelcome = false;
				Find.WindowStack.Add(new Dialog_ModSettings(RimGPTMod.self));
			}
			var anchor = Text.Anchor;
			var font = Text.Font;
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect.ExpandedBy(-20, 0), welcome);
			Text.Anchor = anchor;
			Text.Font = font;
		}
	}

	// add toggle button to play settings
	//
	[StaticConstructorOnStartup]
	[HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
	public static class PlaySettings_DoPlaySettingsGlobalControls_Patch
	{
		static readonly Texture2D icon = ContentFinder<Texture2D>.Get("ToggleAI");
		public static void Postfix(WidgetRow row, bool worldView)
		{
			if (worldView)
				return;
			var previousState = RimGPTMod.Settings.enabled;
			row.ToggleableIcon(ref RimGPTMod.Settings.enabled, icon, $"RimGPT is {(RimGPTMod.Settings.enabled ? "ON" : "OFF")}".Translate(), SoundDefOf.Mouseover_ButtonToggle);
			if (previousState != RimGPTMod.Settings.enabled)
				RimGPTMod.Settings.Write();
		}
	}

	// draw spoken content as text
	//
	[HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI))]
	public static class UIRoot_Play_UIRootOnGUI_Patch
	{
		static readonly Color background = new(0f, 0f, 0f, 0.4f);

		public static void Postfix()
		{
			var welcome = Personas.currentText;
			if (welcome == "")
				return;

			var (sw, sh) = (UI.screenWidth, UI.screenHeight);
			var (w, h) = (800, 180);
			var rect = new Rect((sw - w) / 2, (sh - h) / 2 + sh / 3, w, h);

			Widgets.DrawBoxSolid(rect, background);
			var anchor = Text.Anchor;
			var font = Text.Font;
			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect.ExpandedBy(-20, 0), welcome);
			Text.Anchor = anchor;
			Text.Font = font;
		}
	}

	// reset history when going back to the main menu
	//
	[HarmonyPatch(typeof(GenScene), nameof(GenScene.GoToMainMenu))]
	public static class GenScene_GoToMainMenu_Patch
	{
		public static void Postfix()
		{
			Personas.Reset("The player went to the main menu");
		}
	}

	// reset history when a game is loaded
	//
	[HarmonyPatch(typeof(GameDataSaveLoader), nameof(GameDataSaveLoader.LoadGame), [typeof(string)])]
	public static class GameDataSaveLoader_LoadGame_Patch
	{
		public static void Postfix(string saveFileName)
		{
			Personas.Reset($"The player loaded the game file '{saveFileName}'");
		}
	}

	// send game started info
	//
	[HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
	public static class Game_FinalizeInit_Patch
	{
		public static void Postfix(Game __instance)
		{
			var colonists = __instance.Maps.SelectMany(m => m.mapPawns.FreeColonists).Join(c => c.LabelShortCap);
			Personas.Add($"{"GeneratingWorld".Translate()}. {"ColonistsSection".Translate()}: {colonists}", 5);
		}
	}

	// send mod changes
	//
	[HarmonyPatch(typeof(Page_ModsConfig), nameof(Page_ModsConfig.DoModList))]
	public static class Page_ModsConfig_DoModList_Patch
	{
		static HashSet<string> prevModList = null;

		public static void Postfix(Rect modListArea, List<ModMetaData> modList)
		{
			if (modListArea.x == 0)
				return;

			var newModList = new HashSet<string>(modList.Select(m => m.Name));
			prevModList ??= newModList;

			var removedMods = prevModList.Except(newModList).Join();
			if (removedMods != "")
				Personas.Add($"The player removed these mods: {removedMods}", 1);

			var addedMods = newModList.Except(prevModList).Join();
			if (addedMods != "")
				Personas.Add($"The player added these mods: {addedMods}", 1);

			prevModList = newModList;
		}
	}

	// send scenario selection
	//
	[HarmonyPatch(typeof(Page_SelectScenario), nameof(Page_SelectScenario.DoScenarioListEntry))]
	public static class Page_SelectScenario_DoScenarioListEntry_Patch
	{
		public static void Postfix(Page_SelectScenario __instance)
		{
			Differ.IfChangedPersonasAdd("scenario", __instance.curScen?.name, "The player chose scenario '{VALUE}'", 5);
		}
	}

	// send storyteller selection
	//
	[HarmonyPatch(typeof(StorytellerUI), nameof(StorytellerUI.DrawStorytellerSelectionInterface))]
	public static class StorytellerUI_DrawStorytellerSelectionInterface_Patch
	{
		public static void Postfix(StorytellerDef chosenStoryteller, DifficultyDef difficulty)
		{
			Differ.IfChangedPersonasAdd("storyteller", chosenStoryteller?.LabelCap.ToString(), "The player chose storyteller '{VALUE}'", 5);
			Differ.IfChangedPersonasAdd("difficulty", difficulty?.LabelCap.ToString(), "The player chose difficulty '{VALUE}'", 5);
		}
	}

	// send randomizing colonist
	//
	[HarmonyPatch(typeof(StartingPawnUtility), nameof(StartingPawnUtility.RandomizeInPlace))]
	public static class StartingPawnUtility_RandomizeInPlace_Patch
	{
		public static void Postfix(Pawn p, Pawn __result)
		{
			Personas.Add($"Player clicks 'Randomize' and replaces {p.LabelShortCap} with {__result.LabelShortCap}", 1);
		}
	}

	// send colonist details while choosing starting pawns
	//
	[HarmonyPatch(typeof(Page_ConfigureStartingPawns), nameof(Page_ConfigureStartingPawns.DrawPortraitArea))]
	public static class Page_ConfigureStartingPawns_DrawPortraitArea_Patch
	{
		static Pawn pawn = null;
		static readonly Debouncer debouncer = new(1000);

		static string StartingColonists()
		{
			return Find.GameInitData.startingAndOptionalPawns.Take(Find.GameInitData.startingPawnCount).Join(p => p.LabelShortCap);
		}

		public static void Postfix(Pawn ___curPawn)
		{
			if (___curPawn == pawn)
				return;
			pawn = ___curPawn;

			debouncer.Debounce(() =>
			{
				var backstory = pawn.story.GetBackstory(BackstorySlot.Adulthood) ?? pawn.story.GetBackstory(BackstorySlot.Childhood);
				var allTraits = pawn.story.traits.allTraits;
				var traits = allTraits.Count > 0 ? ", " + allTraits.Select(t => t.LabelCap).ToCommaList() : "";

				var disabled = CharacterCardUtility.WorkTagsFrom(pawn.CombinedDisabledWorkTags).Select(t => t.ToString()).ToCommaList();
				if (disabled.Any())
					disabled = $"{"IncapableOf".Translate(pawn)} {disabled}";
				if (disabled.Any())
					disabled = $", {disabled}";

				static string SkillName(SkillDef def, int level) => level == 0 ? $"No {def.LabelCap}" : $"{def.LabelCap}:{pawn.skills.GetSkill(def).Level}";
				var skills = pawn.skills.skills.Select(s => SkillName(s.def, pawn.skills.GetSkill(s.def).Level)).ToCommaList();
				if (skills.Any())
					skills = $", {skills}";

				var stats = $"{pawn.gender}, {pawn.ageTracker.AgeBiologicalYears}, {backstory.TitleCapFor(pawn.gender)}{traits}{disabled}{skills}";
				Personas.Add($"Player considers {___curPawn.LabelShortCap} ({stats}) for this new game", 1);
			});

			Differ.IfChangedPersonasAdd("starting-pawns", StartingColonists(), "Current starting colonists: {VALUE}", 2);
		}
	}

	// send game seed changes
	//
	[HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.DoWindowContents))]
	public static class Page_CreateWorldParams_DoWindowContents_Patch
	{
		static readonly Debouncer debouncer = new(2000);

		static string TextField(Rect rect, string text)
		{
			var result = Widgets.TextField(rect, text);
			if (result != text)
				debouncer.Debounce(() => Personas.Add($"Player changed the game seed to '{result}'", 2));
			return result;
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var from = SymbolExtensions.GetMethodInfo(() => Widgets.TextField(default, default));
			var to = SymbolExtensions.GetMethodInfo(() => TextField(default, default));
			return instructions.MethodReplacer(from, to);
		}
	}

	// send starting tile selection
	//
	[HarmonyPatch(typeof(WITab_Terrain), nameof(WITab_Terrain.FillTab))]
	public static class WorldInterface_SelectedTile_Setter_Patch
	{
		public static void Postfix(WITab_Terrain __instance)
		{
			var selTile = __instance.SelTile;
			var selTileID = __instance.SelTileID;
			var type = selTile.biome.LabelCap.ToString();
			var hills = selTile.hilliness.GetLabelCap();
			var stones = (from rt in Find.World.NaturalRockTypesIn(selTileID) select rt.label).ToCommaList(true, false).CapitalizeFirst();
			var grow = Zone_Growing.GrowingQuadrumsDescription(selTileID);
			var description = $"{type}, {hills}, {stones}, growing: {grow}";
			Differ.IfChangedPersonasAdd("starting-tile", description, "Player changed the starting tile to '{VALUE}'", 2);
		}
	}

	// send started jobs
	//
	[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
	[HarmonyPatch(new Type[] { typeof(Job), typeof(JobCondition), typeof(ThinkNode), typeof(bool), typeof(bool), typeof(ThinkTreeDef), typeof(JobTag?), typeof(bool), typeof(bool), typeof(bool?), typeof(bool), typeof(bool) })]
	public static class Pawn_JobTracker_StartJob_Patch
	{
		static void Handle(Pawn_JobTracker tracker, JobDriver curDriver)
		{
			tracker.curDriver = curDriver;

			var pawn = tracker.pawn;
			if (pawn == null || pawn.AnimalOrWildMan())
				return;

			var job = curDriver.job;
			if (job == null)
				return;

			var workType = job.workGiverDef?.workType;
			if (workType == WorkTypeDefOf.Hauling)
				return;
			if (workType == WorkTypeDefOf.Construction)
				return;
			if (workType == WorkTypeDefOf.PlantCutting)
				return;
			if (workType == WorkTypeDefOf.Mining)
				return;
			if (workType == Defs.Cleaning)
				return;

			var defName = job.def.defName;
			if (defName == null)
				return;
			if (defName.StartsWith("Wait"))
				return;
			if (defName.StartsWith("Goto"))
				return;

			var report = curDriver.GetReport();
			report = report.Replace(pawn.LabelShortCap, pawn.NameAndType());
			if (job.targetA.Thing is Pawn target)
				report = report.Replace(target.LabelShortCap, target.NameAndType());

			Personas.Add($"{pawn.NameAndType()} {report}", 3);
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return new CodeMatcher(instructions)
				 .MatchStartForward(new CodeMatch(CodeInstruction.StoreField(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.curDriver))))
				 .SetInstruction(CodeInstruction.Call(() => Handle(null, null)))
				 .Instructions();
		}
	}

	// send pawn log
	//
	[HarmonyPatch]
	public static class Battle_Add_Patch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			yield return SymbolExtensions.GetMethodInfo(() => new Battle().Add(null));
			yield return SymbolExtensions.GetMethodInfo(() => new PlayLog().Add(null));
		}

		public static void Postfix(LogEntry entry)
		{
			string text;
			Tools.ExtractPawnsFromLog(entry, out var from, out var to);
			text = entry.ToGameStringFromPOVWithType(from);
			if (text != null)
				Personas.Add(text, 1);
			text = entry.ToGameStringFromPOVWithType(to);
			if (text != null)
				Personas.Add(text, 1);
		}
	}

	// send pawn text motes
	//
	[HarmonyPatch(typeof(MoteMaker), nameof(MoteMaker.ThrowText))]
	[HarmonyPatch(new Type[] { typeof(Vector3), typeof(Map), typeof(string), typeof(float) })]
	public static class MoteMaker_ThrowText_Patch
	{
		public static void Postfix(Vector3 loc, Map map, string text)
		{
			var pawns = map.mapPawns.FreeColonistsSpawned.Where(pawn => (pawn.DrawPos - loc).MagnitudeHorizontalSquared() < 4f);
			if (pawns.Count() != 1)
				return;
			var pawn = pawns.First();
			text = text.Replace("\n", " ");
			Personas.Add($"{pawn.NameAndType()}: \"{text}\"", 0);
		}
	}

	// send game letters
	//
	[HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter))]
	[HarmonyPatch(new Type[] { typeof(Letter), typeof(string) })]
	public static class LetterStack_ReceiveLetter_Patch
	{
		public static void Postfix(Letter let)
		{
			if (let.CanShowInLetterStack == false)
				return;
			var label = let.Label;
			var text = let.GetMouseoverText().Replace("\n", " ");
			Personas.Add($"{Tools.Strings.information}: {label} - {text}", 3);
		}
	}

	// send game alerts
	//
	[HarmonyPatch(typeof(AlertsReadout), nameof(AlertsReadout.CheckAddOrRemoveAlert))]
	public static class AlertsReadout_CheckAddOrRemoveAlert_Patch
	{
		public static void Prefix(Alert alert, List<Alert> ___activeAlerts, out (bool, string) __state)
		{
			__state = (___activeAlerts.Contains(alert), alert.Label);
		}

		public static void Postfix(Alert alert, List<Alert> ___activeAlerts, (bool, string) __state)
		{
			var wasInList = __state.Item1;
			var isInList = ___activeAlerts.Contains(alert);
			if (wasInList == false && isInList)
				Personas.Add($"{Tools.Strings.information}: {alert.Label}", 4);
			if (wasInList && isInList == false)
			{
				var alertLabel = __state.Item2;
				Personas.Add($"{Tools.Strings.completed}: {alertLabel}", 5);
			}
		}
	}

	// send game messages
	//
	[HarmonyPatch(typeof(Messages), nameof(Messages.Message))]
	[HarmonyPatch(new Type[] { typeof(Message), typeof(bool) })]
	public static class Messages_Message_Patch
	{
		public static void Postfix(Message msg)
		{
			Personas.Add(msg.text, 2);
		}
	}

	// send finished construction
	//
	[HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
	public static class TaleRecorder_RecordTale_Patch
	{
		public static void Postfix(Frame __instance, Pawn worker)
		{
			var def = __instance.BuildDef;
			if (def == null || def == Defs.Wall || worker == null)
				return;
			var makeStr = "RecipeMakeJobString".Translate(def.LabelCap);
			Personas.Add($"{worker.NameAndType()}: {Tools.Strings.finished} {makeStr}", 1);
		}
	}

	// send work priority changes
	//
	[HarmonyPatch(typeof(WidgetsWork), nameof(WidgetsWork.DrawWorkBoxFor))]
	public static class WidgetsWork_DrawWorkBoxFor_Patch
	{
		public static void SetPriority(Pawn_WorkSettings instance, WorkTypeDef w, int priority)
		{
			// Capture the old priority before setting the new one
			int oldPriority = instance.GetPriority(w);

			// Now set the new priority
			instance.SetPriority(w, priority);

			var workType = w.labelShort.CapitalizeFirst();
			var colonistName = instance.pawn.NameAndType();

			// Create a status message based on the new priority
			string importanceMessage;
			switch (priority)
			{
				case 1:
					importanceMessage = "most important";
					break;
				case 2:
					importanceMessage = "more important";
					break;
				case 3:
					importanceMessage = "less important";
					break;
				case 4:
					importanceMessage = "least important";
					break;
				default:
					importanceMessage = "of undefined importance";
					break;
			}

			string actionMessage;

			// Determine if the job was just added or removed
			if (priority == 0 && oldPriority > 0)
			{
				// If new priority is 0 and old priority was greater than 0, the job was removed.
				actionMessage = $"The priority for {colonistName} for {workType} was removed.";
			}
			else if (oldPriority == 0 && priority > 0)
			{
				// If old priority was 0 and new priority is greater than 0, the job was added.
				actionMessage = $"The priority for {colonistName} for {workType} became {importanceMessage}.";
			}
			else
			{
				// Otherwise, the priority level changed.
				actionMessage = $"The priority for {colonistName} for {workType} became {importanceMessage}.";
			}

			// Finally, add the detailed message with the appropriate priority change.
			Personas.Add($"{actionMessage} (Priority: {priority})", 2);
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var from = SymbolExtensions.GetMethodInfo(() => new Pawn_WorkSettings().SetPriority(null, 0));
			var to = SymbolExtensions.GetMethodInfo(() => SetPriority(null, null, 0));
			return instructions.MethodReplacer(from, to);
		}
	}

	// send resource count from time to time
	//
	[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootUpdate))]
	public static class UIRoot_UIRootUpdate_Patch
	{
		static int lastTicks = 0;
		static int lastTotal = -1;
		static readonly HashSet<ThingCategoryDef> thingCategories =
		[
			ThingCategoryDefOf.Foods,
			ThingCategoryDefOf.FoodMeals,
			ThingCategoryDefOf.Medicine,
			ThingCategoryDefOf.StoneBlocks,
			ThingCategoryDefOf.Manufactured,
			ThingCategoryDefOf.ResourcesRaw
		];

		public static bool Reportable(this KeyValuePair<ThingDef, int> pair)
		{
			if (pair.Value == 0)
				return false;
			var hashSet = pair.Key.thingCategories.ToHashSet();
			return hashSet.Intersect(thingCategories).Any();
		}

		public static void Postfix()
		{
			var map = Find.CurrentMap;
			if (map == null)
				return;

			lastTicks++;
			if (lastTicks >= 12000)
			{
				lastTicks = 0;
				var amounts = Find.CurrentMap.resourceCounter.AllCountedAmounts.Where(Reportable).ToArray();
				var total = amounts.Sum(pair => pair.Value);
				if (amounts.Any() && total != lastTotal)
				{
					lastTotal = total;
					var colonistCount = Find.CurrentMap.mapPawns.FreeColonistsCount;
					var amountList = amounts.Join(pair => $"{pair.Value} {pair.Key.LabelCap}");
					Personas.Add($"Minor update: total {colonistCount} colonists, {amountList}", 2);
				}
			}
		}
	}


	// Send info about rooms from time to time
	//
	[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootUpdate))]
	public static class UIRoot_UIRootUpdate_Rooms_Update
	{
		static int roomsUpdateTicks = -1;

		public static void Postfix()
		{
			var map = Find.CurrentMap;
			if (map == null)
			{
				return;
			}

			if (map.areaManager == null)
			{
				Log.Message("AreaManager is null");
				return;
			}

			Area_Home homeArea = map.areaManager.Home;

			// we probably want to limit this to player owned map
			if (map.ParentFaction != Faction.OfPlayer)
				return;

			roomsUpdateTicks++;

			if (roomsUpdateTicks % 24000 != 0) return;

			var allRooms = map.regionGrid.allRooms;
			var roomsList = new List<string>();
			// we need to avoid using rooms that are not in the player faction, possible not in the Home Area?
			foreach (Room room in allRooms)
			{
				string roleLabel = room.Role?.label ?? "Undefined";
				if (roleLabel == "Undefined" || roleLabel == "none" || roleLabel == "room")
				{
					continue;
				}
				foreach (IntVec3 cell in room.Cells)
				{

					if (homeArea[cell])
					{

						float lightLevel = map.glowGrid.GameGlowAt(cell);

						var statsStringBuilder = new System.Text.StringBuilder();
						if (room.stats != null)
						{
							foreach (var statPair in room.stats)
							{
								if (statPair.Key != null)
									statsStringBuilder.Append($"{statPair.Key.label} is {statPair.Value}, ");
							}
						}

						string statsString = statsStringBuilder.ToString().TrimEnd(',', ' ');
						// how to add the final and conjunction at the end

						string article = "The ";

						// Check if 'roleLabel' contains "'s" anywhere to indicate possessive form
						if (roleLabel.IndexOf("'s", StringComparison.OrdinalIgnoreCase) != -1)
						{
							// It is possessive, so no article needed
							article = "";
						}

						string sentenceEnding = ", and ";
						string constructedSentence = $"{article}{roleLabel} has a brightness level of {lightLevel}, its " +
													statsString.TrimEnd(sentenceEnding.ToCharArray()) + ".";

						roomsList.Add(constructedSentence);

						break; // We have our information, so we can exit the loop.
					}
				}
			}


			if (roomsList.Count > 0)
			{
				string message = String.Join("\n", roomsList); // Joins the room names with a comma separator
				Personas.Add("Minor Update, Notable Rooms in the Colony: " + message, 1);
			}
		}
	}

	// send research info from time to time
	//
	[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootUpdate))]
	public static class UIRoot_UIRootUpdate_Prod_Update
	{
		static int prodUpdateTicks = -1;

		public static void Postfix()
		{

			var map = Find.CurrentMap;
			if (map == null || !map.IsPlayerHome) return;

			prodUpdateTicks++;

			// use in-game map ticks as we dont want to factor this in if paused or not on the map.
			if (prodUpdateTicks % 24000 != 0) return;

			List<string> messages = new List<string>();

			// already researched projects
			var completedResearch = DefDatabase<ResearchProjectDef>.AllDefsListForReading
										.Where(research => research.IsFinished);
			string completedResearchNames = string.Join(", ", completedResearch.Select(r => r.label));
			messages.Add("Research Completed: " + completedResearchNames);

			// Now do current research
			ResearchProjectDef currentResearch = Find.ResearchManager.currentProj;
			string researchInProgress = currentResearch != null ? currentResearch.label : "None";
			messages.Add("Current Research: " + researchInProgress);

			// Now do available research that is not locked
			var availableResearch = DefDatabase<ResearchProjectDef>.AllDefsListForReading
										.Where(research => !research.IsFinished && research.PrerequisitesCompleted);
			string availableResearchNames = string.Join(", ", availableResearch.Select(r => r.label));
			messages.Add("Available Research: " + availableResearchNames);

			// Calling Personas.Add method (assuming it exists) and fixing the missing '+' for concatenation
			Personas.Add("Minor Update: " + String.Join("\n", messages), 1);
		}
	}

	// send an energy info from time to time
	//
	[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootUpdate))]
	public static class UIRoot_UIRootUpdate_Power_Update
	{
		static int powerUpdateTicks = -1;

		public static void Postfix()
		{
			var map = Find.CurrentMap;
			if (map == null || !map.IsPlayerHome) return;

			powerUpdateTicks++;

			// Use in-game map ticks as we don't want to factor this in if paused or not on the map.
			if (powerUpdateTicks % 6000 != 0) return;
			List<string> messages = new List<string>();

			// Power generation calculations
			float totalPowerGenerated = 0f;
			bool hasPowerGenerators = false;

			List<string> powerGeneratorBuildings = new List<string>();
			var allPowerGeneratingBuildings = map.listerBuildings.allBuildingsColonist
				.Where(building => building.GetComp<CompPowerPlant>() != null);

			foreach (Building powerPlant in allPowerGeneratingBuildings)
			{
				CompPowerPlant compPowerPlant = powerPlant.GetComp<CompPowerPlant>();
				totalPowerGenerated += compPowerPlant.PowerOn ? compPowerPlant.PowerOutput : 0f;
				string message = $"{powerPlant.Label} (Power Output: {compPowerPlant.PowerOutput})";
				powerGeneratorBuildings.Add(message);
			}

			hasPowerGenerators = allPowerGeneratingBuildings.Any();

			if (!hasPowerGenerators)
			{
				messages.Add("Power Generators: None");
			}
			else
			{
				messages.Add("Power Generators: " + string.Join(", ", powerGeneratorBuildings));
			}


			float totalPowerNeeds = CalculateTotalPowerNeeds(map, messages);

			var powerDelta = totalPowerGenerated - totalPowerNeeds;
			(string powerStatus, int priority) = DeterminePowerStatus(powerDelta);

			var allBuildingsWithPowerThatUseFuelComp = map.listerBuildings.allBuildingsColonist
				// to avoid getting stuff like braziers and torches, we want to make sure the refuelable generates power.
				.Where(building => building.GetComp<CompRefuelable>() != null && building.GetComp<CompPowerPlant>() != null).ToList();

			if (allBuildingsWithPowerThatUseFuelComp.Count > 0 && powerDelta > 500)
			{
				powerStatus = "Excessive surplus, fuel is being wasted.";
				priority = 3;
			}

			string totalPowerNeedsMessage = $"Total Power needs: {totalPowerNeeds}, Total Power Generated: {totalPowerGenerated}";
			messages.Add(totalPowerNeedsMessage);
			if (totalPowerNeeds > 0 && totalPowerGenerated > 0) {
				// dont talk about power if there is no power
				Personas.Add("Energy Analysis: " + powerStatus + "\n" + string.Join(", ", messages), priority);
			} else {
				Logger.Message("Skip Power Generation evaluation.");
			}
			
		}



		private static float CalculateTotalPowerNeeds(Map map, List<string> messages)
		{
			float totalPowerNeeds = 0f;
			var allBuildingsWithPowerComp = map.listerBuildings.allBuildingsColonist
				.Where(building =>
				{
					var compPowerTrader = building.GetComp<CompPowerTrader>();
					return compPowerTrader != null && compPowerTrader.PowerOutput < 0;
				}).ToList();

			// Create a list to hold power consumption messages for each building
			List<string> powerConsumptionMessages = new List<string>();

			foreach (Building building in allBuildingsWithPowerComp)
			{
				CompPowerTrader compPowerTrader = building.GetComp<CompPowerTrader>();
				float powerConsumed = compPowerTrader.PowerOn ? compPowerTrader.Props.basePowerConsumption : 0f;
				totalPowerNeeds += powerConsumed;

				// Add each building's power consumption details to the list
				string message = $"{building.Label} (Power Consumption: {powerConsumed})";
				powerConsumptionMessages.Add(message);
			}

			// Add the list of power consumption messages to the main messages list
			if (powerConsumptionMessages.Any())
			{
				messages.Add("Power Consumption: " + string.Join(", ", powerConsumptionMessages));
			}

			return totalPowerNeeds;
		}
		private static (string, int) DeterminePowerStatus(float powerDelta)
		{
			string powerStatus;
			int priority;
			if (powerDelta < 0)
			{
				powerStatus = "Failure";
				priority = 3;
			}
			else if (powerDelta < 200)
			{
				powerStatus = "Unstable (Brownouts possible)";
				priority = 3;
			}
			else if (powerDelta > 700)
			{
				powerStatus = "Surplus";
				priority = 4;
			}
			else
			{
				powerStatus = "Stable";
				priority = 4;
			}
			return (powerStatus, priority);
		}
	}

	// send weather info, and update the colony setting from time to time
	//
	[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootUpdate))]
	public static class UIRoot_UIRootUpdate_WeatherReport_Patch
	{
		static int weatherTicks = -1; // Initialize to -1 so that the first tick will increment to 0

		public static void Postfix()
		{
			var map = Find.CurrentMap;
			if (map == null) return;

			weatherTicks++;

			if (weatherTicks % 12000 == 0)
			{
				WeatherDef currentWeather = map.weatherManager.curWeather;
				Season currentSeason = GenLocalDate.Season(map);
				string seasonName = currentSeason.LabelCap();
				int tileIndex = map.Tile; // Assuming 'map' is a Map object representing your current game map.
				Vector2 tileLatLong = Find.WorldGrid.LongLatOf(tileIndex);
				long currentTicks = Find.TickManager.TicksAbs;
				string fullDateString = GenDate.DateFullStringAt(currentTicks, tileLatLong);
				// Calculate the age of the colony in years, quadrums, and days
				int totalDays = GenDate.DaysPassed;
				int years = totalDays / 60; // Assuming 60 days per year by default
				int quadrums = (totalDays % 60) / 15; // Assuming 15 days per quadrums
				int days = (totalDays % 60) % 15; // Remaining days after accounting for years and quadrums

				string settlementName = map.Parent.LabelCap;

				// Get the biome's name and description
				BiomeDef biome = map.Biome;
				string biomeName = biome.LabelCap;
				string biomeDescription = biome.description;

				// Retrieve the season names for each quadrum

				// Retrieve the month names and season names for each quadrum
				List<string> quadrumsMonthsSeasons = new List<string>();
				for (int quadrumIndex = 0; quadrumIndex < 4; quadrumIndex++)
				{
					Quadrum quadrum = (Quadrum)quadrumIndex; // Explicitly cast int to Quadrum (which is actually a byte underneath)
					string quadrumLabel = quadrum.Label();   // Use the Label() extension method already provided in RimWorld

					// Convert quadrumIndex to byte since Quadrum is a byte and calculate ticks for the season
					Season season = GenDate.Season((long)(((byte)quadrum * 15 + 5) * GenDate.TicksPerDay), tileLatLong);

					// Assuming there's a label property or method like 'Label' or 'LabelCap' on season, use it here
					string seasonLabel = season.ToString(); // Replace this with actual method/property to get season name if necessary

					quadrumsMonthsSeasons.Add($"{quadrumLabel} is {seasonLabel}");
				}
				string quadrumsMonthsSeasonsString = string.Join(", ", quadrumsMonthsSeasons);


				string message = $"Current Season: {seasonName}, Yearly Seasons Overview: {quadrumsMonthsSeasonsString}\n " +
								$"Each Quadrum lasts 15 days, and there are 4 Quadrums per year\n" +
								$"Today is: {fullDateString}, The current Settlement name is: {settlementName}\n " +
								$"Our colony is {years} years {quadrums} quadrums {days} days old\n " +
								$"Current weather: {currentWeather.LabelCap}\n " +
								$"Temperature: {map.mapTemperature.OutdoorTemp:0.#}°C\n " +
								$"Area: {biomeName}, {biomeDescription}";
				RecordKeeper.ColonySetting = message;
				Personas.Add(message, 3);
			}
		}
	}

	// update the colony roster
	[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootUpdate))]
	public static class UIRoot_UIRootUpdate_ColonistRoster_Patch
	{
		static int rosterTicks = -1; // Initialize to -1 so that the first tick will increment to 0

		public static void Postfix()
		{
			var map = Find.CurrentMap;
			if (map == null) return;

			rosterTicks++;

			// Collect on the first tick and then after each period of 500 ticks
			if (rosterTicks % 500 == 0)
			{
				var colonists = new List<RecordKeeper.ColonistData>();

				foreach (Pawn colonist in map.mapPawns.FreeColonists)
				{
					var data = new RecordKeeper.ColonistData
					{
						Name = colonist.Name.ToStringFull,
						Gender = colonist.gender.ToString(),
						Age = colonist.ageTracker.AgeBiologicalYears,
						Mood = colonist.needs.mood?.CurLevelPercentage.ToStringPercent() ?? "Unknown",
						Skills = new Dictionary<string, RecordKeeper.SkillData>(),
						Traits = new List<string>(),
						// Adding Health information
						Health = new List<RecordKeeper.HediffData>()
					};

					foreach (SkillRecord skill in colonist.skills.skills)
					{
						data.Skills.Add(skill.def.LabelCap, new RecordKeeper.SkillData
						{
							Level = skill.Level,
							XpSinceLastLevel = skill.xpSinceLastLevel,
							XpRequiredForLevelUp = skill.XpRequiredForLevelUp
						});
					}

					foreach (Trait trait in colonist.story.traits.allTraits)
					{
						data.Traits.Add(trait.Label);
					}

					// Include health information by iterating through Hediffs
					foreach (Hediff hediff in colonist.health.hediffSet.hediffs)
					{
						var hediffData = new RecordKeeper.HediffData
						{
							Label = hediff.Label,
							Severity = hediff.Severity,
							IsPermanent = hediff.IsPermanent(),
							// Initialize Immunity with a default value or null
							Immunity = null
						};

						// Check if the Hediff supports immunity and if so, add that information
						if (hediff is HediffWithComps hediffWithComps)
						{
							var immunityComp = hediffWithComps.TryGetComp<HediffComp_Immunizable>();
							if (immunityComp != null)
							{
								hediffData.Immunity = immunityComp.Immunity;
							}
						}

						data.Health.Add(hediffData);
					}

					colonists.Add(data);
				}

				// Save the data to memory in RecordKeeper
				RecordKeeper.CollectColonistData(colonists);
				string[] colonistDataArray = RecordKeeper.FetchColonistData();
				string colonistDataString = String.Join("\n\n", colonistDataArray);
				Log.Message(colonistDataString);
			}
		}
	}

	// send colonist interactions
	[HarmonyPatch(typeof(Pawn_InteractionsTracker))]
	public static class Pawn_InteractionsTracker_TryInteractWith_Patch
	{
		[HarmonyPatch("TryInteractWith", new Type[] { typeof(Pawn), typeof(InteractionDef) })]
		public static void Postfix(Pawn recipient, Pawn ___pawn, bool __result, InteractionDef intDef)
		{
			// Ensure the interaction was successful
			if (!__result)
				return;

			// At least one pawn should be of the player's faction
			if (___pawn.Faction != Faction.OfPlayer && recipient.Faction != Faction.OfPlayer)
				return;

			var map = Find.CurrentMap;
			if (map == null)
				return;

			int opinionOfRecipient = ___pawn.relations.OpinionOf(recipient);
			int opinionOfPawn = recipient.relations.OpinionOf(___pawn);

			// Construct a message that includes the type and name of each pawn
			string pawnType = GetPawnType(___pawn);
			string recipientType = GetPawnType(recipient);

			string message = $"{pawnType} '{___pawn.Name.ToStringShort}' interacted with {recipientType} '{recipient.Name.ToStringShort}'. " +
							$"Opinions --- {___pawn.Name.ToStringShort}'s opinion of {recipient.Name.ToStringShort}: {opinionOfRecipient}, " +
							$"{recipient.Name.ToStringShort}'s opinion of {___pawn.Name.ToStringShort}: {opinionOfPawn}. " +
							$"Interaction: '{intDef?.label ?? "something"}' initiated by {___pawn.Name.ToStringShort}.";

			Personas.Add(message, 2);
		}

		private static string GetPawnType(Pawn pawn)
		{
			if (pawn.IsColonist)
				return "colonist";
			if (pawn.IsPrisoner)
				return "prisoner";
			if (pawn.IsSlave)
				return "slave";
			// This could be expanded with more types or different logic to determine the type.
			return "visitor";
		}
	}

	// add a keyboard shortcut to the mod settings dialog
	//
	[HarmonyPatch(typeof(GlobalControls), nameof(GlobalControls.GlobalControlsOnGUI))]
	public static class GlobalControls_GlobalControlsOnGUI_Patch
	{
		public static void Postfix()
		{
			if (Event.current.type == EventType.KeyDown && Defs.Command_OpenRimGPT.KeyDownEvent)
			{
				var stack = Find.WindowStack;
				if (stack.IsOpen<Dialog_ModSettings>() == false)
				{
					var me = LoadedModManager.GetMod<RimGPTMod>();
					var dialog = new Dialog_ModSettings(me);
					stack.Add(dialog);
				}
				Event.current.Use();
			}
		}
	}

	// send colonist opinions of eachother from time to time
	[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootUpdate))]
	public static class UIRoot_UIRootUpdate_OpinionReport_Patch
	{
		static int opinionTicks = -1;
		private static Dictionary<(Pawn, Pawn), int> lastOpinions = new Dictionary<(Pawn, Pawn), int>();

		public static void Postfix()
		{
			try
			{
				var map = Find.CurrentMap;
				if (map == null) return;

				opinionTicks++;

				if (opinionTicks % 12000 == 0)
				{
					List<string> opinionMessages = new List<string>();
					var freeColonistsCopy = map.mapPawns.FreeColonists.ToList();

					foreach (var colonist in freeColonistsCopy)
					{
						foreach (var otherColonist in freeColonistsCopy)
						{
							if (colonist != otherColonist)
							{
								int currentOpinion = colonist.relations.OpinionOf(otherColonist);

								if (lastOpinions.TryGetValue((colonist, otherColonist), out int previousOpinion))
								{
									string trend = currentOpinion == previousOpinion ? "stayed the same" :
												   currentOpinion > previousOpinion ? "improved" : "worsened";

									opinionMessages.Add($"{colonist.Name} current opinion of {otherColonist.Name}: {currentOpinion} (has {trend} since yesterday)");
								}
								else
								{
									opinionMessages.Add($"{colonist.Name} current opinion of {otherColonist.Name}: {currentOpinion}");
								}

								lastOpinions[(colonist, otherColonist)] = currentOpinion;
							}
						}
					}

					if (opinionMessages.Any())
					{
						string consolidatedMessage = string.Join("\n", opinionMessages);
						Personas.Add(consolidatedMessage, 2);
					}
				}
			}
			catch (Exception e)
			{
				Log.Error($"Error in UIRoot_UIRootUpdate_OpinionReport_Patch: {e.Message}");
			}
		}
	}

	// send colonist thoughts and mood effects from time to time
	[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootUpdate))]
	public static class UIRoot_UIRootUpdate_ColonistThoughts_Patch
	{
		static int thoughtsTicks = -1;  // Initialize to -1 so that the first tick will increment to 0

		public static void Postfix()
		{
			var map = Find.CurrentMap;
			if (map == null || !map.IsPlayerHome) return;

			thoughtsTicks++;
			if (thoughtsTicks % 12000 == 0)
			{

				foreach (Pawn colonist in map.mapPawns.FreeColonists)
				{
					// A dictionary to hold the sum of mood effects for each thought type.
					Dictionary<string, (int Count, float MoodEffect)> thoughtCounts = new Dictionary<string, (int, float)>();

					foreach (Thought_Memory thought in colonist.needs.mood.thoughts.memories.Memories)
					{
						string label = thought.LabelCap;
						float moodEffect = thought.MoodOffset();

						if (thoughtCounts.ContainsKey(label))
						{
							thoughtCounts[label] = (thoughtCounts[label].Count + 1, thoughtCounts[label].MoodEffect + moodEffect);
						}
						else
						{
							thoughtCounts.Add(label, (1, moodEffect));
						}
					}

					List<string> formattedThoughts = new List<string>
						{
							$"Thoughts of {colonist.Name.ToStringShort}:"
						};

					// Iterate over the dictionary to create the de-duplicated and summarized list of thoughts.
					foreach (var kvp in thoughtCounts)
					{
						string plural = kvp.Value.Count > 1 ? "(x" + kvp.Value.Count + ")" : "";
						formattedThoughts.Add($"{kvp.Key}{plural}: Mood Effect {kvp.Value.MoodEffect.ToStringWithSign()}");
					}

					string thoughtsMessage = string.Join("\n", formattedThoughts);

					Personas.Add(thoughtsMessage, 2);
				}
			}
		}
	}
}

