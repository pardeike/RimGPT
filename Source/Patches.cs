using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using Verse;
using Verse.AI;
using static Verse.HediffCompProperties_RandomizeSeverityPhases;

// Things to report:
// injuries
// raider ai
// player changes to config
// player designating (buttons & construction)

namespace RimGPT
{
	[HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
	public static class MainMenuDrawer_MainMenuOnGUI_Patch
	{
		static bool showWelcome = true;
		static readonly Color background = new(0f, 0f, 0f, 0.8f);

		public static void Postfix()
		{
			if (showWelcome == false)
				return;
			if (RimGPTMod.Settings.IsConfigured)
				return;
			var (sw, sh) = (UI.screenWidth, UI.screenHeight);
			var (w, h) = (360, 120);
			var rect = new Rect((sw - w) / 2, (sh - h) / 2, w, h);
			var welcome = "Welcome to RimGPT. You need to configure the mod before you can use it.";

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

	[HarmonyPatch]
	public static class InitGameStart_LoadGame_Patch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.DeclaredConstructor(typeof(Page_SelectScenario), new Type[0]);
			yield return SymbolExtensions.GetMethodInfo(() => GameDataSaveLoader.LoadGame(""));
		}

		public static void Postfix()
		{
			Log.Message("* resetting chat history");
			AI.ResetHistory();
			PhraseManager.ResetHistory();
		}
	}

	[HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
	public static class Game_FinalizeInit_Patch
	{
		public static void Postfix(Game __instance)
		{
			var colonists = __instance.Maps.SelectMany(m => m.mapPawns.FreeColonists).Join(c => c.LabelShortCap);
			PhraseManager.Add($"{"GeneratingWorld".Translate()}. {"ColonistsSection".Translate()}: {colonists}");
		}
	}

	[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
	[HarmonyPatch(new Type[] { typeof(Job), typeof(JobCondition), typeof(ThinkNode), typeof(bool), typeof(bool), typeof(ThinkTreeDef), typeof(JobTag?), typeof(bool), typeof(bool), typeof(bool?), typeof(bool), typeof(bool) })]
	public static class Pawn_JobTracker_StartJob_Patch
	{
		static string GetTarget(Job job)
		{
			if (job.targetA.IsValid == false)
				return "";
			var thing = job.targetA.Thing;
			if (thing == null)
				return "";
			return thing.def.LabelCap.ToString();
		}

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

			PhraseManager.Add($"{pawn.NameAndType()} {report}");
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return new CodeMatcher(instructions)
				 .MatchStartForward(new CodeMatch(CodeInstruction.StoreField(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.curDriver))))
				 .SetInstruction(CodeInstruction.Call(() => Handle(null, null)))
				 .Instructions();
		}
	}

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
				PhraseManager.Add(text);
			text = entry.ToGameStringFromPOVWithType(to);
			if (text != null)
				PhraseManager.Add(text);
		}
	}

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
			PhraseManager.Add($"{pawn.NameAndType()}: \"{text}\"");
		}
	}

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
			PhraseManager.Add($"{Tools.Strings.information}: {label} - {text}");
		}
	}

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
				PhraseManager.Add($"{Tools.Strings.information}: {alert.Label}");
			if (wasInList && isInList == false)
			{
				var alertLabel = __state.Item2;
				PhraseManager.Add($"{Tools.Strings.completed}: {alertLabel}");
			}
		}
	}

	[HarmonyPatch(typeof(Messages), nameof(Messages.Message))]
	[HarmonyPatch(new Type[] { typeof(Message), typeof(bool) })]
	public static class Messages_Message_Patch
	{
		public static void Postfix(Message msg)
		{
			PhraseManager.Add(msg.text);
		}
	}

	[HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
	public static class TaleRecorder_RecordTale_Patch
	{
		public static void Postfix(Frame __instance, Pawn worker)
		{
			var def = __instance.BuildDef;
			if (def == null || def == Defs.Wall || worker == null)
				return;
			var makeStr = "RecipeMakeJobString".Translate(def.LabelCap);
			PhraseManager.Add($"{worker.NameAndType()}: {Tools.Strings.finished} {makeStr}");
		}
	}

	[HarmonyPatch(typeof(WidgetsWork), nameof(WidgetsWork.DrawWorkBoxFor))]
	public static class WidgetsWork_DrawWorkBoxFor_Patch
	{
		public static void SetPriority(Pawn_WorkSettings instance, WorkTypeDef w, int priority)
		{
			instance.SetPriority(w, priority);
			var workType = w.labelShort.CapitalizeFirst();
			PhraseManager.Add($"{instance.pawn.NameAndType()}: {Tools.Strings.priority} {workType} = {priority}");
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var from = SymbolExtensions.GetMethodInfo(() => new Pawn_WorkSettings().SetPriority(null, 0));
			var to = SymbolExtensions.GetMethodInfo(() => SetPriority(null, null, 0));
			return instructions.MethodReplacer(from, to);
		}
	}

	[HarmonyPatch(typeof(Page_ConfigureStartingPawns), nameof(Page_ConfigureStartingPawns.DrawPortraitArea))]
	public static class Page_ConfigureStartingPawns_DrawPortraitArea_Patch
	{
		static Pawn pawn = null;
		static readonly Debouncer debouncer = new(1000);

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
				PhraseManager.Immediate($"Should I choose {___curPawn.LabelShortCap} ({stats}) for this new game?");
			});
		}
	}
}