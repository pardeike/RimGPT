using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace RimGPT
{
	public class OrderedHashSet<T> : KeyedCollection<T, T>
	{
		protected override T GetKeyForItem(T item) => item;

		public void RemoveFromStart(int max)
		{
			if (max <= 0) return;
			var count = Math.Min(max, Count);
			for (int i = 0; i < count; i++)
				RemoveAt(0);
		}
	}

	public static class PhraseManager
	{
		public struct Phrase: IEquatable<Phrase>
		{
			public string text;
			public int priority;

			public Phrase(string text, int priority = 0)
			{
				this.text = text;
				this.priority = priority;
			}

			public bool Equals(Phrase other) => text == other.text;
		}

		public static OrderedHashSet<Phrase> phrases = new();
		public static readonly Regex tagRemover = new("<color.+?>(.+?)</(?:color)?>", RegexOptions.Singleline);
		public static CancellationTokenSource cancelTokenSource = new();
		public static void CancelWaiting() => cancelTokenSource?.Cancel();

		public static void Add(string text, int priority = 0)
		{
			text = tagRemover.Replace(text, "$1");
			var phrase = new Phrase(text, priority);
			Log.Message($"PRIO-{phrase.priority} {phrase.text}");

			lock (phrases)
			{
				if (phrases.Contains(phrase))
					return;
				phrases.Add(phrase);
				for (var i = 0; i <= 5 && phrases.Count > RimGPTMod.Settings.phrasesLimit; i++)
				{
					var jOffset = 0;
					var stop = false;
					for (var j = 0; j < phrases.Count; j++)
						if (phrases[j].priority == i)
						{
							phrases.RemoveAt(j + jOffset);
							jOffset--;
							if (phrases.Count <= RimGPTMod.Settings.phrasesLimit)
							{
								stop = true;
								break;
							}
						}
					if (stop)
						break;
				}
			}
		}

		public static void Immediate(string text)
		{
			text = tagRemover.Replace(text, "$1");
			var phrase = new Phrase(text);
			Log.Message($"PRIO-{phrase.priority} {phrase.text}");

			lock (phrases)
			{
				phrases.Clear();
				phrases.Add(phrase);
				CancelWaiting();
			}
		}

		public static void ResetHistory()
		{
			phrases.Clear();
		}

		public static async Task<bool> Process()
		{
			Phrase[] observations;
			lock (phrases)
			{
				observations = phrases.Take(RimGPTMod.Settings.phraseBatchSize).ToArray();
				phrases.RemoveFromStart(RimGPTMod.Settings.phraseBatchSize);
			}
			if (observations.Length == 0)
				return false;
			if (RimGPTMod.Settings.enabled == false)
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
					try
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
					catch (Exception exception)
					{
						Log.Error($"Main task error caught: {exception}");
					}
				}
			});
		}
	}
}