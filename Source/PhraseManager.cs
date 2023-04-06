using System;
using System.Collections.ObjectModel;
using System.Linq;
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
        public const int batchSize = 25;
        public const int minDelayTicks = 5000;
        public const int maxDelayTicks = 15000;

        public static OrderedHashSet<string> phrases = new();

        public static void Add(string phrase)
        {
            lock (phrases)
            {
                if (phrases.Contains(phrase)) return;
                phrases.Add(phrase);
                Log.Warning(phrase);
            }
        }

        public static async Task<bool> Process()
        {
            string[] observations;
            lock (phrases)
            {
                observations = phrases.Take(batchSize).ToArray();
                phrases.RemoveFromStart(batchSize);
            }
            if (observations.Length == 0) return false;
            var result = await AI.Evaluate(observations);
            await TTS.PlayAzure(result);
            return true;
        }

        public static void Start()
        {
            var delay = 0;
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(delay);
                    delay += await Process() ? 1000 : -1000;
                    if (delay < minDelayTicks) delay = minDelayTicks;
                    if (delay > maxDelayTicks) delay = maxDelayTicks;
                }
            });
        }
    }
}