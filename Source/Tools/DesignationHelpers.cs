using RimWorld;
using Verse;
using System.Collections.Generic;

namespace RimGPT
{
	public static class DesignationHelpers
	{

    private const int MaxEntries = 1000; // Maximum allowed entries before flushing.
  
    private static HashSet<IntVec3> trackedCanceledCells = new HashSet<IntVec3>();
    
    private static HashSet<int> trackedCanceledThingIDs = new HashSet<int>();

    public static void TrackCancelCell(IntVec3 cell)
    {
        lock (trackedCanceledCells)
        {
            if (trackedCanceledCells.Count > MaxEntries)
            {
                trackedCanceledCells.Clear();
            }
            trackedCanceledCells.Add(cell);

        }
    }

    public static void TrackCancelThing(Thing thing)
    {
        lock (trackedCanceledThingIDs)
        {
            if (trackedCanceledThingIDs.Count > MaxEntries)
            {
                trackedCanceledThingIDs.Clear();
            }
            trackedCanceledThingIDs.Add(thing.thingIDNumber);
        }
    }

    public static bool IsTrackedCancelCell(IntVec3 cell)
    {
        lock (trackedCanceledCells)
        {
            return trackedCanceledCells.Remove(cell);
        }
    }

    public static bool IsTrackedCancelThing(Thing thing)
    {
        lock (trackedCanceledThingIDs)
        {
            return trackedCanceledThingIDs.Remove(thing.thingIDNumber);
        }
    }


		public static (string order, string targetLabel) GetOrderAndTargetLabel(Designation des)
		{
			string translationKey;
			try
			{
				// Determine translation key.
				translationKey = des.def.defName == "HarvestPlant"
																&& des.target.Thing is Plant plant
																&& plant.def.plant.IsTree
						? "DesignatorHarvestWood"
						: $"Designator{des.def.defName}";
			}
			catch
			{
				translationKey = $"Designator{des.def.defName}"; // Fallback to generic designation if cast fails.
			}

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