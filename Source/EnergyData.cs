
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimGPT
{
	public class EnergyData
	{
		public List<EnergyProducer> Producers { get; set; } = [];
		public List<EnergyConsumer> Consumers { get; set; } = [];
		public float TotalPowerGenerated { get; set; }
		public float TotalPowerNeeded { get; set; }
		public string PowerStatus { get; set; }

		public override string ToString()
		{
			if (TotalPowerGenerated == 0 && TotalPowerNeeded == 0) return "";

			var builder = new StringBuilder();
			builder.AppendLine($"Total Power Generated: {TotalPowerGenerated}");
			builder.AppendLine($"Total Power Needed: {TotalPowerNeeded}");
			builder.AppendLine(PowerStatus);

			if (Producers.Any())
			{
				builder.AppendLine("Power Generators:");
				foreach (var producer in Producers)
				{
					builder.AppendLine($"{producer.Label} (Power Output: {producer.PowerOutput})");
				}
			}
			else
			{
				builder.AppendLine("Power Generators: None");
			}

			if (Consumers.Any())
			{
				builder.AppendLine("Power Consumption:");
				foreach (var consumer in Consumers)
				{
					builder.AppendLine($"{consumer.Label} (Power Consumption: {consumer.PowerConsumed})");
				}
			}

			return builder.ToString();
		}
	}

	public class EnergyProducer
	{
		public string Label { get; set; }
		public float PowerOutput { get; set; }
	}

	public class EnergyConsumer
	{
		public string Label { get; set; }
		public float PowerConsumed { get; set; }
	}
}