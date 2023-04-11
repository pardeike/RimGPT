using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Verse;
using static Verse.HediffCompProperties_RandomizeSeverityPhases;

namespace RimGPT
{
	public class OrderedHashSet<T> : KeyedCollection<T, T>
	{
		protected override T GetKeyForItem(T item) => item;

		public void RemoveFromStart(int max)
		{
			var count = Math.Min(max, Count);
			for (int i = 0; i < count; i++)
				RemoveAt(0);
		}
	}

	public static class PhraseManager
	{
		public static OrderedHashSet<string> phrases = new();
		public static readonly Regex tagRemover = new("<color.+?>(.+?)</(?:color)?>", RegexOptions.Singleline);
		public static CancellationTokenSource cancelTokenSource = new();
		public static void CancelWaiting() => cancelTokenSource?.Cancel();

		public static void Add(string phrase)
		{
			phrase = tagRemover.Replace(phrase, "$1");
			lock (phrases)
			{
				if (phrases.Contains(phrase))
					return;
				phrases.Add(phrase);
				Log.Message(phrase);
			}
		}

		public static void Immediate(string phrase)
		{
			phrase = tagRemover.Replace(phrase, "$1");
			lock (phrases)
			{
				phrases.Clear();
				phrases.Add(phrase);
				Log.Message(phrase);
				CancelWaiting();
			}
		}

		public static void ResetHistory()
		{
			phrases.Clear();
		}

		public static async Task<bool> Process()
		{
			string[] observations;
			lock (phrases)
			{
				observations = phrases.Take(RimGPTMod.Settings.phraseBatchSize).ToArray();
				phrases.RemoveFromStart(RimGPTMod.Settings.phraseBatchSize);
			}
			if (observations.Length == 0)
				return false;
			var result = await AI.Evaluate(observations);
			if (result != null)
				await TTS.PlayAzure(result, true, error => Log.Error(error));
			return true;
		}

		public static void Start()
		{
			var delay = 0;
			_ = Task.Run(async () =>
			{
				while (true)
				{
					if (RimGPTMod.Settings.IsConfigured == false)
					{
						await Task.Delay(1000);
						continue;
					}

					if (phrases.Count < RimGPTMod.Settings.phraseBatchSize)
					{
						try { await Task.Delay(delay, cancelTokenSource.Token); }
						catch (TaskCanceledException) { delay = 0; }
						cancelTokenSource.Dispose();
						cancelTokenSource = new();
					}

					delay += await Process() ? 1000 : -1000;
					if (delay < RimGPTMod.Settings.phraseDelayMin)
						delay = RimGPTMod.Settings.phraseDelayMin.Milliseconds();
					if (delay > RimGPTMod.Settings.phraseDelayMax)
						delay = RimGPTMod.Settings.phraseDelayMax.Milliseconds();
				}
			});
		}
	}
}