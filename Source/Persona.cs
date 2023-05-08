using Verse;
using System;
using System.Linq;
using HarmonyLib;
using System.Xml.Linq;

namespace RimGPT
{
	public class Persona : IExposable
	{
		public class SettingAttribute : Attribute { }

		public string name = "RimGPT";
		public AI ai = new();
		public OrderedHashSet<Phrase> phrases = new();
		public string lastSpokenText = "";
		public DateTime nextPhraseTime = DateTime.Now;

		[Setting] public string azureVoiceLanguage = "-";
		[Setting] public string azureVoice = "en-CA-LiamNeural";
		[Setting] public string azureVoiceStyle = "default";
		[Setting] public float azureVoiceStyleDegree = 1f;

		[Setting] public float speechRate = 0f;
		[Setting] public float speechPitch = -0f;

		[Setting] public int phrasesLimit = 20;
		[Setting] public int phraseBatchSize = 20;
		[Setting] public float phraseDelayMin = 5f;
		[Setting] public float phraseDelayMax = 5f;
		[Setting] public int phraseMaxWordCount = 40;
		[Setting] public int historyMaxWordCount = 200;
		[Setting] public string personality = AI.defaultPersonality;
		[Setting] public string personalityLanguage = "-";

		public void ExposeData()
		{
			Scribe_Values.Look(ref azureVoiceLanguage, "azureVoiceLanguage", "-");
			Scribe_Values.Look(ref azureVoice, "azureVoice", "en-CA-LiamNeural");
			Scribe_Values.Look(ref azureVoiceStyle, "azureVoiceStyle", "default");
			Scribe_Values.Look(ref azureVoiceStyleDegree, "azureVoiceStyleDegree", 1f);

			Scribe_Values.Look(ref speechRate, "speechRate", 0f);
			Scribe_Values.Look(ref speechPitch, "speechPitch", -0.1f);

			Scribe_Values.Look(ref phrasesLimit, "phrasesLimit", 40);
			Scribe_Values.Look(ref phraseBatchSize, "phraseBatchSize", 20);
			Scribe_Values.Look(ref phraseDelayMin, "phraseDelayMin", 2f);
			Scribe_Values.Look(ref phraseDelayMax, "phraseDelayMax", 10f);
			Scribe_Values.Look(ref phraseMaxWordCount, "phraseMaxWordCount", 50);
			Scribe_Values.Look(ref historyMaxWordCount, "historyMaxWordCount", 400);
			Scribe_Values.Look(ref personality, "personality", AI.defaultPersonality);
			Scribe_Values.Look(ref personalityLanguage, "personalityLanguage", "-");

			if (historyMaxWordCount < 200)
				historyMaxWordCount = 400;
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

		public void Reset(string reason)
		{
			lock (phrases)
			{
				phrases.Clear();
				ai.ReplaceHistory(reason);
			}
		}

		public void Periodic()
		{
			if (RimGPTMod.Settings.enabled == false)
				return;

			var now = DateTime.Now;
			if (now < nextPhraseTime || Personas.IsAudioQueueFull)
			{
				//FileLog.Log($"{name}: delayed by {((int)(nextPhraseTime - now).TotalSeconds)} secs");
				return;
			}

			var game = Current.Game;
			if (game != null && game.tickManager.ticksGameInt > 100 && game.tickManager.Paused)
			{
				//FileLog.Log($"{name}: paused");
				return;
			}

			var batch = new Phrase[0];
			lock (phrases)
			{
				batch = phrases.Take(phraseBatchSize).ToArray();
				phrases.RemoveFromStart(phraseBatchSize);
			}
			//FileLog.Log($"{name}: consumed {batch.Length}, remaining {phrases.Count}");
			if (batch.Length == 0)
				return;

			nextPhraseTime = DateTime.Now.AddMinutes(5);
			Personas.CreateSpeechJob(this, batch, e => Logger.Error(e), () =>
			{
				var secs = Rand.Range(phraseDelayMin, phraseDelayMax);
				//FileLog.Log($"{name}: nextPhraseTime now + {secs}");
				nextPhraseTime = DateTime.Now.AddSeconds(secs);
			});
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