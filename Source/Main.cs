using Brrainz;
using HarmonyLib;
using Verse;

namespace RimGPT
{
    public class RimGPTMod : Mod
    {
        public RimGPTMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("net.pardeike.rimworld.mod.RimGPT");
            harmony.PatchAll();
            CrossPromotion.Install(76561197973010050);

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                Log.Warning($"ExecuteWhenFinished end");
                Log.Warning($"Language: {Tools.Language}");

                if (Configuration.IsConfigured)
                {
                    PhraseManager.Add("Player has started the game and waits for a message.");
                    PhraseManager.Start();
                }
                else
                    Log.Error("You need to configure all API keys to use RimGPT");
            });
        }
    }
}