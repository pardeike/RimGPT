using RimWorld;
using Verse;

namespace RimGPT
{
	public static class DesignationHelpers
	{
		public static (string order, string targetLabel) GetOrderAndTargetLabel(Designation des)
		{
			// Determine translation key.
			var translationKey = des.def.defName == "HarvestPlant"
											&& des.target.Thing is Plant plant
											&& plant.def.plant.IsTree
				 ? "DesignatorHarvestWood"
				 : $"Designator{des.def.defName}";

			// Find a valid translation if available.
			var validKey = Tools.FindValidTranslationKey(translationKey);

			// Determine the order string using the valid translation key or fallbacks.
			string order = !string.IsNullOrEmpty(validKey) ? validKey.Translate() :
								!string.IsNullOrEmpty(des.def.label) ? des.def.label.Translate() :
								!string.IsNullOrEmpty(des.def.LabelCap) ? des.def.LabelCap :
								des.def.defName;

			// Determine the target label and optionally prepend stuff label if it exists.
			var stuffLabel = des.target.Thing?.Stuff?.label ?? "";
			var baseTargetLabel = des.target.HasThing
											 ? des.target.Thing.LabelShort ?? "something"
											 : des.Map.terrainGrid.TerrainAt(des.target.Cell).label ?? "area";

			// Combine the stuff label with the base target label, if stuff label exists.
			var targetLabel = string.IsNullOrEmpty(stuffLabel) ? baseTargetLabel : $"{stuffLabel} {baseTargetLabel}";

			return (order, targetLabel);
		}
	}
}