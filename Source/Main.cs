using Brrainz;
using HarmonyLib;
using OpenAI;
using UnityEngine;
using Verse;

namespace RimGPT
{
    public class RimGPTMod : Mod
    {
        public static RimGPTSettings Settings;
        public static Mod self;

        public RimGPTMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimGPTSettings>();

            var harmony = new Harmony("net.pardeike.rimworld.mod.RimGPT");
            harmony.PatchAll();
            CrossPromotion.Install(76561197973010050);

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                TTS.LoadVoiceInformation();
                if (RimGPTMod.Settings.IsConfigured)
                    PhraseManager.Add("Player has launched Rimworld.");
                PhraseManager.Start();
            });

            self = this;
        }

        public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);
        public override string SettingsCategory() => "RimGPT";
    }
}