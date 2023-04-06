using Brrainz;
using HarmonyLib;
using System.Collections.Generic;
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

            PhraseManager.Add("Player has started the game and waits for a welcome message.");
            PhraseManager.Start();
        }
    }

    /*
	public class Tracker : MapComponent
	{
		public class PawnInfo
		{

		}

		public Dictionary<Pawn, PawnInfo> pawnInfo = new();

		public Tracker(Map map) : base(map)
		{
		}

		public override void MapComponentTick()
		{
			base.MapComponentTick();

			var colonists = map.mapPawns.FreeColonistsSpawned;
			for(var i = 0; i < colonists.Count; i++)
			{

			}
		}
	}
	*/
}