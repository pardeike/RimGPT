using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimGPT
{
    public class OrderedHashSet<T> : KeyedCollection<T, T>
    {
        protected override T GetKeyForItem(T item) => item;

        public void RemoveFromStart(int max)
        {
            var count = Math.Min(max, Count);
            for (int i = 0; i < count; i++) RemoveAt(0);
        }
    }

    public static class PhraseManager
    {
        public static OrderedHashSet<string> phrases = new();
        public static readonly Regex tagRemover = new Regex("<color.+?>(.+?)</(?:color)?>", RegexOptions.Singleline);

        public static void Add(string phrase)
        {
            phrase = tagRemover.Replace(phrase, "$1");
            lock (phrases)
            {
                if (phrases.Contains(phrase)) return;
                phrases.Add(phrase);
                Log.Message(phrase);
            }
        }

        public static async Task<bool> Process()
        {
            string[] observations;
            lock (phrases)
            {
                observations = phrases.Take(Configuration.phraseBatchSize).ToArray();
                phrases.RemoveFromStart(Configuration.phraseBatchSize);
            }
            if (observations.Length == 0) return false;
            var result = await AI.Evaluate(observations);
            if (result != null)
                await TTS.PlayAzure(result, Configuration.azureVoice, Configuration.azureVoiceStyle);
            return true;
        }

        public static void Start()
        {
            var delay = 0;
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    if (phrases.Count < Configuration.phraseBatchSize)
                        await Task.Delay(delay);
                    delay += await Process() ? 1000 : -1000;
                    if (delay < Configuration.phraseDelayMin) delay = Configuration.phraseDelayMin;
                    if (delay > Configuration.phraseDelayMax) delay = Configuration.phraseDelayMax;
                }
            });
        }
    }
}