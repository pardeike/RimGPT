using HarmonyLib;
using System;
using System.Xml.Linq;
using UnityEngine;
using Verse;

namespace RimGPT
{
	public class ExternalSettingAttribute: Attribute { }

	public class RimGPTSettings : ModSettings
	{
		public bool enabled = true;
		public string chatGPTKey = "";
		public string azureSpeechKey = "";
		public string azureSpeechRegion = "";
		[ExternalSetting] public string azureVoiceLanguage = "-";
		[ExternalSetting] public string azureVoice = "en-CA-LiamNeural";
		[ExternalSetting] public string azureVoiceStyle = "default";
		[ExternalSetting] public float azureVoiceStyleDegree = 1f;
		[ExternalSetting] public string speechLanguage = "";
		public float speechVolume = 4f;
		[ExternalSetting] public float speechRate = 0f;
		[ExternalSetting] public float speechPitch = -0.1f;
		[ExternalSetting] public int phrasesLimit = 40;
		[ExternalSetting] public int phraseBatchSize = 20;
		[ExternalSetting] public float phraseDelayMin = 2f;
		[ExternalSetting] public float phraseDelayMax = 10f;
		[ExternalSetting] public int phraseMaxWordCount = 50;
		[ExternalSetting] public int historyMaxWordCount = 200;
		[ExternalSetting] public string personality = AI.defaultPersonality;
		[ExternalSetting] public string personalityLanguage = "-";
		public bool showAsText = true;
		public long charactersSentOpenAI = 0;
		public long charactersSentAzure = 0;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref enabled, "enabled", true);
			Scribe_Values.Look(ref chatGPTKey, "chatGPTKey");
			Scribe_Values.Look(ref azureSpeechKey, "azureSpeechKey");
			Scribe_Values.Look(ref azureSpeechRegion, "azureSpeechRegion");
			Scribe_Values.Look(ref azureVoiceLanguage, "azureVoiceLanguage", "-");
			Scribe_Values.Look(ref azureVoice, "azureVoice", "en-CA-LiamNeural");
			Scribe_Values.Look(ref azureVoiceStyle, "azureVoiceStyle", "default");
			Scribe_Values.Look(ref azureVoiceStyleDegree, "azureVoiceStyleDegree", 1f);
			Scribe_Values.Look(ref speechLanguage, "speechLanguage", "");
			Scribe_Values.Look(ref speechVolume, "speechVolume", 4f);
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
			Scribe_Values.Look(ref showAsText, "showAsText", true);

			if (historyMaxWordCount < 200) historyMaxWordCount = 400;
			if (phraseBatchSize > phrasesLimit) phraseBatchSize = phrasesLimit;
			if (phraseDelayMin > phraseDelayMax) phraseDelayMin = phraseDelayMax;
			if (phraseDelayMax < phraseDelayMin) phraseDelayMax = phraseDelayMin;
		}

		public string ToXml()
		{
			var personalityElement = new XElement("Personality");
			var fields = AccessTools.GetDeclaredFields(GetType());
			foreach (var field in fields)
			{
				if (Attribute.IsDefined(field, typeof(ExternalSettingAttribute)) == false) continue;
				var fieldElement = new XElement(field.Name, field.GetValue(this));
				personalityElement.Add(fieldElement);
			}
			return personalityElement.ToString();
		}

		public void UpdatePersonalityFromXML(string xml)
		{
			var xDoc = XDocument.Parse(xml);
			var root = xDoc.Root;
			foreach (var element in root.Elements())
			{
				var field = AccessTools.DeclaredField(typeof(RimGPTSettings), element.Name.LocalName);
				if (Attribute.IsDefined(field, typeof(ExternalSettingAttribute)) == false) continue;
				field?.SetValue(this, field.FieldType switch
				{
					Type t when t == typeof(float) => float.Parse(element.Value),
					Type t when t == typeof(int) => int.Parse(element.Value),
					Type t when t == typeof(long) => long.Parse(element.Value),
					Type t when t == typeof(bool) => bool.Parse(element.Value),
					Type t when t == typeof(string) => element.Value,
					_ => null
				});
			}
		}

		public bool IsConfigured =>
			 chatGPTKey?.Length > 0 && ((azureSpeechKey?.Length > 0 && azureSpeechRegion?.Length > 0) || showAsText);

		public void DoWindowContents(Rect inRect)
		{
			string prevKey;
			Rect rect;

			var list = new Listing_Standard { ColumnWidth = inRect.width / 2f };
			list.Begin(inRect);

			// for three columns with 20px spacing
			var width = (list.ColumnWidth - 2 * 20) / 3;

			list.Label("FFFF00", "OpenAI - ChatGPT", $"{charactersSentOpenAI} chars sent");
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

			list.Label("FFFF00", "Azure - Speech Services", $"{charactersSentAzure} chars sent");
			var prevRegion = azureSpeechRegion;
			list.TextField(ref azureSpeechRegion, "Region");
			if (azureSpeechRegion != prevRegion)
				TTS.LoadVoiceInformation();
			list.Gap(6f);
			prevKey = azureSpeechKey;
			list.TextField(ref azureSpeechKey, "API Key (paste only)", true, () => azureSpeechKey = "");
			if (azureSpeechKey != "" && azureSpeechKey != prevKey)
				TTS.TestKey(personalityLanguage, () => TTS.LoadVoiceInformation());

			list.Gap(16f);

			list.Label("FFFF00", "Azure - Voice");
			list.Languages(LanguageDatabase.AllLoadedLanguages, RimGPTMod.Settings.azureVoiceLanguage, l => l.DisplayName, l =>
			{
				RimGPTMod.Settings.azureVoiceLanguage = l == null ? "-" : l.FriendlyNameEnglish;
				TTS.LoadVoiceInformation();
			}, width, 0);
			list.Voices(width, 1);
			if (UX.HasVoiceStyles())
				list.VoiceStyles(width, 2);
			list.Gap(30f);

			list.Gap(16f);

			list.Slider(ref azureVoiceStyleDegree, 0f, 2f, () => $"Style degree: {azureVoiceStyleDegree.ToPercentage(false)}", 0.01f);
			list.Slider(ref speechVolume, 0f, 10f, () => $"Speech volume: {speechVolume.ToPercentage(false)}", 0.01f);
			list.Slider(ref speechRate, -0.5f, 0.5f, () => $"Speech rate: {speechRate.ToPercentage()}", 0.01f);
			list.Slider(ref speechPitch, -0.5f, 0.5f, () => $"Speech pitch: {speechPitch.ToPercentage()}", 0.01f);

			list.Gap(16f);

			rect = list.GetRect(30f);
			rect.width = width;
			if (Widgets.ButtonText(rect, "Test"))
				TTS.TestKey(personalityLanguage, null);
			rect.x += width + 20;
			if (Widgets.ButtonText(rect, "Reset Stats"))
			{
				charactersSentOpenAI = 0;
				charactersSentAzure = 0;
			}

			list.NewColumn();
			list.ColumnWidth -= 16f;

			list.Label("FFFF00", "Commentary");

			width = (list.ColumnWidth - 1 * 20) / 2;
			rect = list.GetRect(30f);
			rect.width = width;
			if (Widgets.ButtonText(rect, "Edit personality"))
				Dialog_Personality.Show();
			rect.x += width + 20;
			if (Widgets.ButtonText(rect, personalityLanguage == "-" ? "Game Language" : personalityLanguage))
				UX.LanguageChoiceMenu(Tools.commonLanguages, l => l, l => RimGPTMod.Settings.personalityLanguage = l ?? "-");

			list.Gap(16f);

			_ = list.Label("Sending game information");
			list.Slider(ref phrasesLimit, 1, 100, () => $"Maximum items: {phrasesLimit}");
			phraseBatchSize = Mathf.Min(phraseBatchSize, phrasesLimit);
			list.Slider(ref phraseBatchSize, 1, phrasesLimit, () => $"Batch size: {phraseBatchSize}");
			list.Gap(16f);
			_ = list.Label("Delay between comments");
			if (phraseDelayMin > phraseDelayMax) phraseDelayMin = phraseDelayMax;
			list.Slider(ref phraseDelayMin, 1f, phraseDelayMax, () => $"Minimum: {phraseDelayMin}", 0.1f);
			if (phraseDelayMax < phraseDelayMin) phraseDelayMax = phraseDelayMin;
			list.Slider(ref phraseDelayMax, phraseDelayMin, 100f, () => $"Maximum: {phraseDelayMax}", 0.1f);
			list.Gap(16f);
			_ = list.Label("Comments");
			list.Slider(ref phraseMaxWordCount, 1, 160, () => $"Maximum word count: {phraseMaxWordCount}");
			list.Gap(16f);
			_ = list.Label("History");
			list.Slider(ref historyMaxWordCount, 200, 1200, () => $"Maximum word count: {historyMaxWordCount}");
			list.Gap(16f);

			list.CheckboxLabeled("Show as text", ref showAsText);

			list.Gap(16f);

			width = (list.ColumnWidth - 2 * 20) / 3;
			rect = list.GetRect(30f);
			rect.width = width;
			if (Widgets.ButtonText(rect, "Copy"))
			{
				var share = ToXml();
				if (share.NullOrEmpty() == false)
					GUIUtility.systemCopyBuffer = share;
			}
			rect.x += width + 20;
			if (Widgets.ButtonText(rect, "Paste"))
			{
				var text = GUIUtility.systemCopyBuffer;
				if (text.NullOrEmpty() == false)
					UpdatePersonalityFromXML(text);
			}
			rect.x += width + 20;
			if (Widgets.ButtonText(rect, "Defaults"))
			{
				azureVoiceLanguage = "-";
				azureVoice = "en-CA-LiamNeural";
				azureVoiceStyle = "default";
				azureVoiceStyleDegree = 1f;
				speechVolume = 4f;
				speechRate = 0f;
				speechPitch = -0.1f;
				phrasesLimit = 40;
				phraseBatchSize = 20;
				phraseDelayMin = 2f;
				phraseDelayMax = 10f;
				phraseMaxWordCount = 50;
				historyMaxWordCount = 400;
				personality = AI.defaultPersonality;
				personalityLanguage = "-";
				showAsText = true;
			}

			list.End();
		}
	}
}