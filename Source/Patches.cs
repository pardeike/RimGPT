using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using static Verse.DamageWorker;

// Things to report:
// injuries
// raider ai
// player changes to config
// player designating (buttons & construction)

namespace RimGPT
{
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    [HarmonyPatch(new Type[] { typeof(Job), typeof(JobCondition), typeof(ThinkNode), typeof(bool), typeof(bool), typeof(ThinkTreeDef), typeof(JobTag?), typeof(bool), typeof(bool), typeof(bool?), typeof(bool), typeof(bool) })]
    public static class Pawn_JobTracker_StartJob_Patch
    {
        static string GetTarget(Job job)
        {
            if (job.targetA.IsValid == false) return "";
            var thing = job.targetA.Thing;
            if (thing == null) return "";
            return thing.def.LabelCap.ToString();
        }

        static void Handle(Pawn_JobTracker tracker, JobDriver curDriver)
        {
            tracker.curDriver = curDriver;

            var pawn = tracker.pawn;
            if (pawn == null || pawn.AnimalOrWildMan()) return;

            var job = curDriver.job;
            if (job == null) return;

            var workType = job.workGiverDef?.workType;
            if (workType == WorkTypeDefOf.Hauling) return;
            if (workType == WorkTypeDefOf.Construction) return;
            if (workType == WorkTypeDefOf.PlantCutting) return;
            if (workType == WorkTypeDefOf.Mining) return;
            if (workType == Defs.Cleaning) return;

            var defName = job.def.defName;
            if (defName == null) return;
            if (defName.StartsWith("Wait")) return;
            if (defName.StartsWith("Goto")) return;

            var report = curDriver.GetReport();
            report = report.Replace(pawn.LabelShortCap, pawn.NameAndType());
            var target = job.targetA.Thing as Pawn;
            if (target != null) report = report.Replace(target.LabelShortCap, target.NameAndType());

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

    [HarmonyPatch(typeof(Battle), nameof(Battle.Add))]
    [HarmonyPatch(new Type[] { typeof(LogEntry) })]
    public static class Battle_Add_Patch
    {
        public static void Postfix(LogEntry entry)
        {
            Tools.ExtractPawnsFromLog(entry, out var from, out var to);

            if (from?.IsColonist == true)
            {
                var text = entry.ToGameStringFromPOVWithType(from, to);
                PhraseManager.Add(text);
            }
            if (to?.IsColonist == true)
            {
                var text = entry.ToGameStringFromPOVWithType(to, from);
                PhraseManager.Add(text);
            }
        }
    }

    [HarmonyPatch(typeof(PlayLog), nameof(PlayLog.Add))]
    [HarmonyPatch(new Type[] { typeof(LogEntry) })]
    public static class PlayLog_Add_Patch
    {
        public static void Postfix(LogEntry entry)
        {
            Tools.ExtractPawnsFromLog(entry, out var from, out var to);

            if (from?.IsColonist == true)
            {
                var text = entry.ToGameStringFromPOVWithType(from, to);
                PhraseManager.Add(text);
            }
            if (to?.IsColonist == true)
            {
                var text = entry.ToGameStringFromPOVWithType(to, from);
                PhraseManager.Add(text);
            }
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
            if (let.CanShowInLetterStack == false) return;
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
            if (def == Defs.Wall) return;
            var makeStr = "RecipeMakeJobString".Translate(def.LabelCap);
            PhraseManager.Add($"{worker.NameAndType()}: {Tools.Strings.finished} {makeStr}");
        }
    }

    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.SetPriority))]
    public static class Pawn_WorkSettings_SetPriority_Patch
    {
        public static void Postfix(Pawn ___pawn, WorkTypeDef w, int priority)
        {
            var workType = w.labelShort.CapitalizeFirst();
            PhraseManager.Add($"{___pawn.NameAndType()}: {Tools.Strings.priority} {workType} = {priority}");
        }
    }
}