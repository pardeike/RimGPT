using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

// Things to report:
// colonists breaking
// injuries
// raider ai
// player changes to config
// player designating (buttons & construction)
// messages top left
// messages right edge
// X vs Y message in the log (when X is not a colonist)

namespace RimGPT
{
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    [HarmonyPatch(new Type[] { typeof(Job), typeof(JobCondition), typeof(ThinkNode), typeof(bool), typeof(bool), typeof(ThinkTreeDef), typeof(JobTag?), typeof(bool), typeof(bool), typeof(bool?), typeof(bool), typeof(bool) })]
    public static class Pawn_JobTracker_StartJob_Patch
    {
        static string GetPawnType(Pawn pawn)
        {
            if (pawn.HostileTo(Faction.OfPlayer))
            {
                if (pawn.RaceProps.IsMechanoid) return "Mech";
                else return "Enemy";
            }
            if (pawn.IsColonist) return "Colonist";
            return "Visitor";
        }

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
            var defName = job.def.defName;
            if (defName == null) return;
            if (defName.StartsWith("Wait")) return;
            if (defName.StartsWith("Goto")) return;

            PhraseManager.Add($"{GetPawnType(pawn)} {pawn.LabelShortCap} {curDriver.GetReport()}");
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
            Pawn pawn = null;

            if (entry is BattleLogEntry_Event @event)
                pawn = @event.initiatorPawn;
            else if (entry is BattleLogEntry_DamageTaken damage)
                pawn = damage.initiatorPawn;
            else if (entry is BattleLogEntry_ExplosionImpact explosion)
                pawn = explosion.initiatorPawn;
            else if (entry is BattleLogEntry_MeleeCombat melee)
                pawn = melee.initiator;
            else if (entry is BattleLogEntry_RangedFire fire)
                pawn = fire.initiatorPawn;
            else if (entry is BattleLogEntry_RangedImpact impact)
                pawn = impact.initiatorPawn;
            else if (entry is BattleLogEntry_StateTransition transition)
                pawn = transition.subjectPawn;

            if (pawn == null || pawn.IsColonist == false) return;
            var text = entry.ToGameStringFromPOV(pawn, false);
            PhraseManager.Add(text);
            return;
        }
    }

    [HarmonyPatch(typeof(PlayLog), nameof(PlayLog.Add))]
    [HarmonyPatch(new Type[] { typeof(LogEntry) })]
    public static class PlayLog_Add_Patch
    {
        public static void Postfix(LogEntry entry)
        {
            if (entry is not PlayLogEntry_Interaction interaction)
                return;
            var pawn = interaction.initiator;
            if (pawn == null || pawn.IsColonist == false)
                return;
            var text = entry.ToGameStringFromPOV(pawn, false);
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
            PhraseManager.Add($"{pawn.LabelShortCap} thought \"{text}\"");
        }
    }

    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter))]
    [HarmonyPatch(new Type[] { typeof(Letter), typeof(string) })]
    public static class LetterStack_ReceiveLetter_Patch
    {
        public static void Postfix(Letter let)
        {
            if (let.CanShowInLetterStack == false) return;
            var c = let.def.color;
            var d1 = c.r - c.g;
            var a1 = Mathf.Abs(d1);
            var d2 = c.r - c.b;
            var a2 = Mathf.Abs(d2);
            var text = let.GetMouseoverText().Replace("\n", " ");
            var prefix = a1 < 0.125f && a2 < 0.125f ? "" : (d1 < 0 && d2 < 0 ? "Good news: " : "Bad news: ");
            PhraseManager.Add(prefix + text);
        }
    }
}