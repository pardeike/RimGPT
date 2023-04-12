using HarmonyLib;
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using UnityEngine;
using Verse;

namespace RimGPT
{
	public class RimGPTSettings : ModSettings
	{
		[Serializable]
		public class Personality
		{
			public string azureVoice;
			public string azureVoiceStyle;
			public float azureVoiceStyleDegree;
			public float speechRate;
			public float speechPitch;
			public int phraseBatchSize;
			public float phraseDelayMin;
			public float phraseDelayMax;
			public int phraseMaxWordCount;
			public int historyMaxWordCount;
			public string personality;

			public Personality() { }

			public Personality(RimGPTSettings settings)
			{
				Traverse.IterateFields(this, settings, (to, from) => to.SetValue(from.GetValue()));
			}

			public string ToXml()
			{
				var serializer = new XmlSerializer(typeof(Personality));
				var stringBuilder = new StringBuilder();
				var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true };
				using var writer = XmlWriter.Create(stringBuilder, settings);
				var ns = new XmlSerializerNamespaces();
				ns.Add("", "");
				serializer.Serialize(writer, this, ns);
				return stringBuilder.ToString();
			}

			public static Personality FromXml(string xml)
			{
				var serializer = new XmlSerializer(typeof(Personality));
				using var reader = new StringReader(xml);
				return (Personality)serializer.Deserialize(reader);
			}
		}

		public string chatGPTKey = "";
		public string azureSpeechKey = "";
		public string azureSpeechRegion = "";
		public string azureVoice = "en-CA-LiamNeural";
		public string azureVoiceStyle = "default";
		public float azureVoiceStyleDegree = 1f;
		public float speechVolume = 4f;
		public float speechRate = 0f;
		public float speechPitch = -0.1f;
		public int phraseBatchSize = 20;
		public float phraseDelayMin = 2f;
		public float phraseDelayMax = 10f;
		public int phraseMaxWordCount = 50;
		public int historyMaxWordCount = 200;
		public string personality = AI.defaultPersonality;
		public bool showAsText = false;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref chatGPTKey, "chatGPTKey");
			Scribe_Values.Look(ref azureSpeechKey, "azureSpeechKey");
			Scribe_Values.Look(ref azureSpeechRegion, "azureSpeechRegion");
			Scribe_Values.Look(ref azureVoice, "azureVoice", "en-CA-LiamNeural");
			Scribe_Values.Look(ref azureVoiceStyle, "azureVoiceStyle", "default");
			Scribe_Values.Look(ref azureVoiceStyleDegree, "azureVoiceStyleDegree", 1f);
			Scribe_Values.Look(ref speechVolume, "speechVolume", 4f);
			Scribe_Values.Look(ref speechRate, "speechRate", 0f);
			Scribe_Values.Look(ref speechPitch, "speechPitch", -0.1f);
			Scribe_Values.Look(ref phraseBatchSize, "phraseBatchSize", 20);
			Scribe_Values.Look(ref phraseDelayMin, "phraseDelayMin", 2f);
			Scribe_Values.Look(ref phraseDelayMax, "phraseDelayMax", 10f);
			Scribe_Values.Look(ref phraseMaxWordCount, "phraseMaxWordCount", 50);
			Scribe_Values.Look(ref historyMaxWordCount, "historyMaxWordCount", 400);
			Scribe_Values.Look(ref personality, "personality", AI.defaultPersonality);
			Scribe_Values.Look(ref showAsText, "showAsText", false);

			if (historyMaxWordCount < 200) historyMaxWordCount = 400;
		}

		public bool IsConfigured =>
			 chatGPTKey?.Length > 0 && ((azureSpeechKey?.Length > 0 && azureSpeechRegion?.Length > 0) || showAsText);

		public void DoWindowContents(Rect inRect)
		{
			string prevKey;

			var list = new Listing_Standard { ColumnWidth = inRect.width / 2f };
			list.Begin(inRect);

			list.Label("OpenAI - ChatGPT", "FFFF00");
			prevKey = chatGPTKey;
			list.TextField(ref chatGPTKey, "API Key (paste only)", true, () => chatGPTKey = "");
			if (chatGPTKey != "" && chatGPTKey != prevKey)
				AI.TestKey(
					 response => LongEventHandler.ExecuteWhenFinished(() =>
					 {
						 var dialog = new Dialog_MessageBox(response);
						 Find.WindowStack.Add(dialog);
					 })
				);

			list.Gap(16f);

			list.Label("Azure - Speech Services", "FFFF00");
			var prevRegion = azureSpeechRegion;
			list.TextField(ref azureSpeechRegion, "Region");
			if (azureSpeechRegion != prevRegion)
				TTS.LoadVoiceInformation();
			list.Gap(6f);
			prevKey = azureSpeechKey;
			list.TextField(ref azureSpeechKey, "API Key (paste only)", true, () => azureSpeechKey = "");
			if (azureSpeechKey != "" && azureSpeechKey != prevKey)
				TTS.TestKey(() => TTS.LoadVoiceInformation());

			list.Gap(16f);

			list.Label("Azure - Voice", "FFFF00");
			list.Voices(); // list.TextField(ref azureVoice, "Voice");
			list.Gap(6f);
			if (UX.HasVoiceStyles())
				list.VoiceStyles(); // list.TextField(ref azureVoiceStyle, "Style");

			list.Gap(16f);

			list.Slider(ref azureVoiceStyleDegree, 0f, 2f, () => $"Style degree: {azureVoiceStyleDegree.ToPercentage(false)}", 0.01f);
			list.Slider(ref speechVolume, 0f, 10f, () => $"Speech volume: {speechVolume.ToPercentage(false)}", 0.01f);
			list.Slider(ref speechRate, -0.5f, 0.5f, () => $"Speech rate: {speechRate.ToPercentage()}", 0.01f);
			list.Slider(ref speechPitch, -0.5f, 0.5f, () => $"Speech pitch: {speechPitch.ToPercentage()}", 0.01f);
			if (list.ButtonText("Test", null, 0.2f))
				TTS.TestKey(null);

			list.NewColumn();

			list.Label("Commentary", "FFFF00");

			if (list.ButtonText("Edit personality", null, 0.4f))
				Dialog_Personality.Show();

			list.Gap(16f);

			_ = list.Label("Sending game information");
			list.Slider(ref phraseBatchSize, 1, 100, () => $"Maximum batch size: {phraseBatchSize}");
			list.Gap(16f);
			_ = list.Label("Delay between comments");
			list.Slider(ref phraseDelayMin, 1f, 100f, () => $"Minimum: {phraseDelayMin}");
			list.Slider(ref phraseDelayMax, 1f, 100f, () => $"Maximum: {phraseDelayMax}");
			list.Gap(16f);
			_ = list.Label("Comments");
			list.Slider(ref phraseMaxWordCount, 1, 160, () => $"Maximum word count: {phraseMaxWordCount}");
			list.Gap(16f);
			_ = list.Label("History");
			list.Slider(ref historyMaxWordCount, 200, 1200, () => $"Maximum word count: {historyMaxWordCount}");
			list.Gap(16f);

			list.ColumnWidth -= 16f;
			list.CheckboxLabeled("Show as text", ref showAsText);
			list.ColumnWidth += 16f;

			list.Gap(16f);

			var width = (list.ColumnWidth - 16 - 2 * 20) / 3;
			var rect = list.GetRect(30f);
			rect.width = width;
			var offset = width + 20;
			if (Widgets.ButtonText(rect, "Copy"))
			{
				var share = new Personality(this).ToXml();
				if (share.NullOrEmpty() == false)
					GUIUtility.systemCopyBuffer = share;
			}
			rect.x += offset;
			if (Widgets.ButtonText(rect, "Paste"))
			{
				var text = GUIUtility.systemCopyBuffer;
				if (text.NullOrEmpty() == false)
				{
					var share = Personality.FromXml(text);
					if (share != null)
						Traverse.IterateFields(share, this, (from, to) => to.SetValue(from.GetValue()));
				}
			}
			rect.x += offset;
			if (Widgets.ButtonText(rect, "Defaults"))
			{
				azureVoice = "en-CA-LiamNeural";
				azureVoiceStyle = "default";
				azureVoiceStyleDegree = 1f;
				speechVolume = 4f;
				speechRate = 0f;
				speechPitch = -0.1f;
				phraseBatchSize = 20;
				phraseDelayMin = 2f;
				phraseDelayMax = 10f;
				phraseMaxWordCount = 50;
				historyMaxWordCount = 400;
				personality = AI.defaultPersonality;
				showAsText = false;
			}

			list.End();
		}
	}
}