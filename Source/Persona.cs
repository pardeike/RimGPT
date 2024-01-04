using HarmonyLib;
using System;
using System.Linq;
using System.Xml.Linq;
using Verse;
using Verse.Noise;

namespace RimGPT
{
	public class Persona : IExposable
	{
		public class SettingAttribute : Attribute { }

		public string name = "RimGPT";
		public AI ai = new();
		public OrderedHashSet<Phrase> phrases = [];
		public string lastSpokenText = "";
		public DateTime nextPhraseTime = DateTime.Now;
		private int timesSkipped = 0;
		private int timesSpoken = 0;
		[Setting] public string azureVoiceLanguage = "-";
		[Setting] public string azureVoice = "en-CA-LiamNeural";
		[Setting] public string azureVoiceStyle = "default";
		[Setting] public float azureVoiceStyleDegree = 1f;

		[Setting] public float speechRate = 0f;
		[Setting] public float speechPitch = 0f;

		[Setting] public int phrasesLimit = 20;
		[Setting] public int phraseBatchSize = 20;
		[Setting] public float phraseDelayMin = 30f;
		[Setting] public float phraseDelayMax = 60f;
		[Setting] public int phraseMaxWordCount = 40;
		[Setting] public int historyMaxWordCount = 200;
		[Setting] public string personality = AI.defaultPersonality;
		[Setting] public string personalitySecondary = AI.defaultPersonalitySecondary;
		[Setting] public string personalityLanguage = "-";
		[Setting] public bool isChronicler = false;
		public int maximumSkipLimit = 20;
		public void ExposeData()
		{
			Scribe_Values.Look(ref name, "name", "RimGPT");

			Scribe_Values.Look(ref azureVoiceLanguage, "azureVoiceLanguage", "-");
			Scribe_Values.Look(ref azureVoice, "azureVoice", "en-CA-LiamNeural");
			Scribe_Values.Look(ref azureVoiceStyle, "azureVoiceStyle", "default");
			Scribe_Values.Look(ref azureVoiceStyleDegree, "azureVoiceStyleDegree", 1f);

			Scribe_Values.Look(ref speechRate, "speechRate", 0f);
			Scribe_Values.Look(ref speechPitch, "speechPitch", 0f);

			Scribe_Values.Look(ref phrasesLimit, "phrasesLimit", 20);
			Scribe_Values.Look(ref phraseBatchSize, "phraseBatchSize", 20);
			Scribe_Values.Look(ref phraseDelayMin, "phraseDelayMin", 10f);
			Scribe_Values.Look(ref phraseDelayMax, "phraseDelayMax", 20f);
			Scribe_Values.Look(ref phraseMaxWordCount, "phraseMaxWordCount", 40);
			Scribe_Values.Look(ref historyMaxWordCount, "historyMaxWordCount", 200);
			Scribe_Values.Look(ref isChronicler, "isChronicler", false);
			Scribe_Values.Look(ref personality, "personality", AI.defaultPersonality);
			Scribe_Values.Look(ref personalitySecondary, "personalitySecondary", AI.defaultPersonalitySecondary);
			Scribe_Values.Look(ref personalityLanguage, "personalityLanguage", "-");

			if (historyMaxWordCount < 200)
				historyMaxWordCount = 200;
			if (phraseBatchSize > phrasesLimit)
				phraseBatchSize = phrasesLimit;
			if (phraseDelayMin > phraseDelayMax)
				phraseDelayMin = phraseDelayMax;
			if (phraseDelayMax < phraseDelayMin)
				phraseDelayMax = phraseDelayMin;
		}

		public void AddPhrase(Phrase phrase)
		{
			//FileLog.Log($"{name}: ADD {phrase.text}");
			lock (phrases)
			{
				if (phrases.Contains(phrase))
					return;
				phrases.Add(phrase);
				for (var i = 0; i <= 5 && phrases.Count > phrasesLimit; i++)
				{
					var jOffset = 0;
					var stop = false;
					for (var j = 0; j < phrases.Count; j++)
						if (phrases[j].priority == i)
						{
							phrases.RemoveAt(j + jOffset);
							jOffset--;
							if (phrases.Count <= phrasesLimit)
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
		public void ScheduleNextJob()
		{
			int limit = getReasonableSkipLimit(phraseDelayMin, phraseDelayMax);

			if (Personas.isResetting)
			{
				ExtendWaitBeforeNextJob("Resetting gamestate");
			}
			// Check if there is already a completed job in the queue that hasn't started playback
			if (Personas.IsAnyCompletedJobWaiting())
			{
				ExtendWaitBeforeNextJob("Other personas are speaking");
				return;
			}
			// we could do the batching before-hand, but I figured we want the freshest data and we dont want any outstanding jbos.
			var batch = new Phrase[0];


			lock (phrases)
			{
				batch = phrases.Take(phraseBatchSize).ToArray();
				// avoid spam if there's no new phrases and this persona has already spoken recently.
				if (timesSpoken != 0 && batch.Length <= 1 && timesSkipped < limit)
				{
					ExtendWaitBeforeNextJob($"nothing new ({timesSkipped}/{limit})");
					return;
				}
				phrases.RemoveFromStart(phraseBatchSize);
			}

			// this gets overridden when createSpeechJob complets, much less than 5 mins - so its probably a safety measure to ensure we can 
			// call this again if the callback in createSpeechJob never executes.
			nextPhraseTime = DateTime.Now.AddMinutes(5);


			// Alternative Strategy, always talk anyway but remember the last thing said:
			// if persona has no new phrases, add a phrase of the last thing a recent persona said, to help with the conversation.
			// if (timesSpoken != 0 && batch.Length == 0 && Personas.lastSpeakingPersona != null && Personas.lastSpeakingPersona != this)
			// {
			// 	var lastSpokenPhrase = new Phrase
			// 	{
			// 		text = Personas.lastSpeakingPersona.lastSpokenText,
			// 		persona = Personas.lastSpeakingPersona,
			// 		priority = 3
			// 	};
			// 	batch.AddItem(lastSpokenPhrase);
			// }

			timesSkipped = 0;
			// Create the speech job immediately
			Personas.CreateSpeechJob(this, batch, e => Logger.Error(e), () =>
			{
				timesSpoken++;
				var secs = Rand.Range(phraseDelayMin, phraseDelayMax);
				nextPhraseTime = DateTime.Now.AddSeconds(secs);
			});

		}
		/// <summary>
		/// Calculates a reasonable limit for the number of times a job can be skipped based on given minimum and maximum delay values.
		/// The skip limit is determined by the mean delay and ranges from 1 to max, with shorter delays allowing more skips.
		/// </summary>
		/// <param name="a">The minimum delay before a phrase can be repeated, in seconds.</param>
		/// <param name="b">The maximum delay before a phrase can be repeated, in seconds.</param>
		/// <returns>An integer representing the calculated skip limit.</returns>
		public int getReasonableSkipLimit(float a, float b)
		{

			double meanDelayInSeconds = (a + b) / 2.0;

			if (meanDelayInSeconds <= 60)
			{
				return maximumSkipLimit;
			}
			else if (meanDelayInSeconds >= 1200)
			{
				// If mean delay is 120 seconds or more, return the minimum skip limit of 1
				return 1;
			}
			else
			{
				// Linearly interpolate the skip limit between 1 and max skip limit based on the mean delay
				double slope = (1 - maximumSkipLimit) / (1200.0 - 60.0);
				int limit = (int)Math.Round(5 + slope * (meanDelayInSeconds - 30));
				return Math.Max(1, Math.Min(limit, maximumSkipLimit)); // Ensure the skip limit stays within the range 1 to max
			}
		}


		public void ExtendWaitBeforeNextJob(string reason)
		{
			Log.Message($"Skipping {this.name}: {reason}");
			timesSkipped++;
			var secs = Rand.Range(phraseDelayMin, phraseDelayMax);
			nextPhraseTime = DateTime.Now.AddSeconds(secs);
		}
		public void Reset(string[] reason)
		{
			timesSkipped = 0;
			timesSpoken = 0;
			lock (phrases)
			{
				phrases.Clear();
				ai.ReplaceHistory(reason);
			}
		}

		public void Periodic()
		{
			if (RimGPTMod.Settings.azureSpeechRegion.NullOrEmpty())
				return;

			var now = DateTime.Now;
			if (now < nextPhraseTime || Personas.IsAudioQueueFull) return;

			var game = Current.Game;

			if (game != null && game.tickManager != null && game.tickManager.ticksGameInt > 100 && game.tickManager.Paused) return;

			ScheduleNextJob();
		}

		public string ToXml()
		{
			var personalityElement = new XElement("Personality");
			var fields = AccessTools.GetDeclaredFields(GetType())
				.Where(field => Attribute.IsDefined(field, typeof(SettingAttribute)));
			foreach (var field in fields)
			{
				var fieldElement = new XElement(field.Name, field.GetValue(this));
				personalityElement.Add(fieldElement);
			}
			return personalityElement.ToString();
		}

		public static void PersonalityFromXML(string xml, Persona persona)
		{
			var xDoc = XDocument.Parse(xml);
			var root = xDoc.Root;
			foreach (var element in root.Elements())
			{
				var field = AccessTools.DeclaredField(typeof(Persona), element.Name.LocalName);
				if (field == null || Attribute.IsDefined(field, typeof(SettingAttribute)) == false)
					continue;
				field.SetValue(persona, field.FieldType switch
				{
					Type t when t == typeof(float) => float.Parse(element.Value),
					Type t when t == typeof(int) => int.Parse(element.Value),
					Type t when t == typeof(long) => long.Parse(element.Value),
					Type t when t == typeof(bool) => bool.Parse(element.Value),
					Type t when t == typeof(string) => element.Value,
					_ => throw new NotImplementedException(field.FieldType.Name)
				});
			}
		}
	}
}