using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimGPT
{
	public static class DesignationQueueManager
	{
		private static readonly Dictionary<(OrderType orderType, string workOrderVerb, string targetObject), int> designationCounts = [];
		private static readonly object designationLock = new();

		private static readonly int threshold = 90; // Threshold for sending messages
		private static readonly int timeLimitTicks = 600; // Time limit in game ticks (600 ticks = 10 seconds)
		private static int currentTickCounter = 0;

		public static void Update()
		{
			try
			{
				currentTickCounter++;

				if (currentTickCounter >= timeLimitTicks)
				{
					FlushQueue(forceFlush: true);
					currentTickCounter = 0;
				}
				else
				{
					lock (designationLock)
					{
						if (designationCounts.Any(kv => kv.Value >= threshold))
						{
							FlushQueue();
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"An error occurred during the updating of DesignationQueueManager {ex}");
			}
		}
		public static void FlushQueue(bool forceFlush = false)
		{
			try
			{
				var orders = new List<string>();

				var keysToRemove = new List<(OrderType orderType, string workOrderVerb, string targetObject)>();

				lock (designationLock)
				{
					foreach (var entry in designationCounts)
					{
						if (entry.Value >= threshold || forceFlush)
						{
							var message = $"{entry.Key.orderType.ToActionVerb()} '{entry.Key.workOrderVerb}' for";
							if (entry.Value > 1)
							{
								message += $" {entry.Value} {Tools.SimplePluralize(entry.Key.targetObject)}";
							}
							else
							{
								var indefiniteArticle = Tools.GetIndefiniteArticleFor(entry.Key.targetObject);
								message += $" {indefiniteArticle} {entry.Key.targetObject}";
							}
							orders.Add(message);

							keysToRemove.Add(entry.Key);
						}
					}

					foreach (var key in keysToRemove)
					{
						_ = designationCounts.Remove(key);
					}
				}

				var combinedMessage = GenText.ToCommaList(orders, true);
				if (!string.IsNullOrEmpty(combinedMessage) && combinedMessage != "none")
				{
					Personas.Add($"The player {combinedMessage}", 3);
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"An error occurred while flushing the queue in DesignationQueueManager {ex}");
			}
		}

		public static void EnqueueDesignation(OrderType orderType, string workOrderVerb, string targetObject)
		{
			try
			{
				var key = (orderType, workOrderVerb, targetObject);

				lock (designationLock)
				{
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
			catch (Exception ex)
			{
				Logger.Error($"An error occurred while enqueuing a designation in DesignationQueueManager {ex}");
			}
		}
	}

	public enum OrderType
	{
		Designate,
		Cancel
	}
	public static class OrderTypeExtensions
	{
		private static readonly Dictionary<OrderType, string> orderTypeStringMapping = new()
		{
			{ OrderType.Designate, "designated" },
			{ OrderType.Cancel, "cancelled" }
		};

		public static string ToActionVerb(this OrderType orderType) => orderTypeStringMapping.TryGetValue(orderType, out var stringValue) ? stringValue : null;
	}
}
