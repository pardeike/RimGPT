using UnityEngine;
using Verse;

namespace RimGPT
{
	public class RimGPTSettings : ModSettings
	{
		public bool enabled = true;
		public string chatGPTKey = "";
		public string azureSpeechKey = "";
		public string azureSpeechRegion = "";
		public float speechVolume = 4f;
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
			Scribe_Values.Look(ref speechVolume, "speechVolume", 4f);
			Scribe_Values.Look(ref showAsText, "showAsText", true);
		}

		public bool IsConfigured =>
			 chatGPTKey?.Length > 0 && ((azureSpeechKey?.Length > 0 && azureSpeechRegion?.Length > 0) || showAsText);

		public void DoWindowContents(Rect inRect)
		{
			string prevKey;
			// Rect rect;

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
				Personas.UpdateVoiceInformation();
			list.Gap(6f);
			prevKey = azureSpeechKey;
			list.TextField(ref azureSpeechKey, "API Key (paste only)", true, () => azureSpeechKey = "");
			if (azureSpeechKey != "" && azureSpeechKey != prevKey)
				TTS.TestKey(new Persona(), () => Personas.UpdateVoiceInformation());

			/*
			list.Gap(16f);

			list.Label("FFFF00", "Azure - Voice");
			list.Languages(LanguageDatabase.AllLoadedLanguages, RimGPTMod.Settings.azureVoiceLanguage, l => l.DisplayName, l =>
			{
				RimGPTMod.Settings.azureVoiceLanguage = l == null ? "-" : l.FriendlyNameEnglish;
				Personas.UpdateVoiceInformation();
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
			*/

			list.End();
		}
	}
}