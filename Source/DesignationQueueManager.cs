using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimGPT
{
	public static class DesignationQueueManager
	{
		private static readonly Dictionary<(string Action, string Label, string ThingLabel), int> designationCounts = new();

		private static readonly int threshold = 30; // Threshold for sending messages
		private static readonly int timeLimitTicks = 600; // Time limit in game ticks (600 ticks = 10 seconds)
		private static int currentTickCounter = 0;

		public static void Update()
		{
			currentTickCounter++;

			if (currentTickCounter >= timeLimitTicks)
			{
				FlushQueue(forceFlush: true);
				currentTickCounter = 0;
			}
			else if (designationCounts.Any(kv => kv.Value >= threshold))
			{
				FlushQueue();
			}
		}

		public static void FlushQueue(bool forceFlush = false)
		{
			var stringBuilder = new StringBuilder();

			var keysToRemove = new List<(string Action, string Label, string ThingLabel)>();

			foreach (var entry in designationCounts)
			{
				if (entry.Value >= threshold || forceFlush)
				{
					var actionText = entry.Key.Action == "Cancel" ? "cancelled" : "designated";
					var message = $"(player {actionText} {entry.Key.ThingLabel} x{entry.Value} for {entry.Key.Label})";
					stringBuilder.AppendLine(message);

					keysToRemove.Add(entry.Key);
				}
			}


			foreach (var key in keysToRemove)
			{
				designationCounts.Remove(key);
			}

			var combinedMessage = stringBuilder.ToString().TrimEnd();
			if (!string.IsNullOrEmpty(combinedMessage))
			{
				Personas.Add(combinedMessage, 3);
			}
		}

		public static void EnqueueDesignation(string action, string label, string thingLabel)
		{
			var key = (action, label, thingLabel);

			if (designationCounts.ContainsKey(key))
			{
				designationCounts[key]++;
			}
			else
			{
				designationCounts[key] = 1;
			}

			if (designationCounts[key] >= threshold)
			{
				FlushQueue();
			}
		}
	}
}