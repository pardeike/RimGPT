using Brrainz;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimGPT
{
    public class RimGPTMod : Mod
    {
        public static RimGPTSettings Settings;

        public RimGPTMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimGPTSettings>();

            var harmony = new Harmony("net.pardeike.rimworld.mod.RimGPT");
            harmony.PatchAll();
            CrossPromotion.Install(76561197973010050);

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (RimGPTMod.Settings.IsConfigured)
                {
                    TTS.LoadVoiceInformation();

                    PhraseManager.Add("Player has started the game and waits for a message.");
                    PhraseManager.Start();
                }
                else
                    Log.Error("You need to configure all API keys to use RimGPT");
            });
        }

        public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);
        public override string SettingsCategory() => "RimGPT";
    }
}