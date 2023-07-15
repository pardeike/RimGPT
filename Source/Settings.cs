using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace RimGPT
{
	public class RimGPTSettings : ModSettings
	{
		public List<Persona> personas = new()
		{
			new Persona()
			{
				name = "Tynan",
				azureVoiceLanguage = "English",
				azureVoice = "en-AU-TimNeural",
				azureVoiceStyle = "",
				azureVoiceStyleDegree = 1f,
				speechRate = 0.2f,
				speechPitch = -0.1f,
				phrasesLimit = 20,
				phraseBatchSize = 20,
				phraseDelayMin = 30f,
				phraseDelayMax = 60f,
				phraseMaxWordCount = 48,
				historyMaxWordCount = 800,
				personality = "You invented Rimworld. You will inform the player about what they should do next. You never talk to or about Brrainz. You soley address the player directly.",
				personalityLanguage = "English"
			},
			new Persona()
			{
				name = "Brrainz",
				azureVoiceLanguage = "English",
				azureVoice = "en-US-JasonNeural",
				azureVoiceStyle = "whispering",
				azureVoiceStyleDegree = 1.25f,
				speechRate = 0.2f,
				speechPitch = -0.1f,
				phrasesLimit = 20,
				phraseBatchSize = 20,
				phraseDelayMin = 25f,
				phraseDelayMax = 55f,
				phraseMaxWordCount = 24,
				historyMaxWordCount = 200,
				personality = "You are a mod developer. You mostly respond to Tynan but sometimes talk to the player. You are sceptical about everything Tynan says. You support everything the player does in the game.",
				personalityLanguage = "English"
			}
		};
		public bool enabled = true;
		public string chatGPTKey = "";
		public string chatGPTModel = Tools.chatGPTModels.First();
		public string azureSpeechKey = "";
		public string azureSpeechRegion = "";
		public float speechVolume = 4f;
		public bool showAsText = true;
		public long charactersSentOpenAI = 0;
		public long charactersSentAzure = 0;

		// for backwards compatibility --------
		public string azureVoiceLanguage;
		public string azureVoice;
		public string azureVoiceStyle;
		public float azureVoiceStyleDegree = 0;
		public float speechRate = 0;
		public float speechPitch = 0;
		public int phrasesLimit = 0;
		public int phraseBatchSize = 0;
		public float phraseDelayMin = 0;
		public float phraseDelayMax = 0;
		public int phraseMaxWordCount = 0;
		public int historyMaxWordCount = 0;
		public string personality;
		public string personalityLanguage;
		// ------------------------------------

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref personas, "personas", LookMode.Deep);
			Scribe_Values.Look(ref enabled, "enabled", true);
			Scribe_Values.Look(ref chatGPTKey, "chatGPTKey");
			Scribe_Values.Look(ref chatGPTModel, "chatGPTModel");
			Scribe_Values.Look(ref azureSpeechKey, "azureSpeechKey");
			Scribe_Values.Look(ref azureSpeechRegion, "azureSpeechRegion");
			Scribe_Values.Look(ref speechVolume, "speechVolume", 4f);
			Scribe_Values.Look(ref showAsText, "showAsText", true);

			// for backwards compatibility ---------------------------------------------
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
			Scribe_Values.Look(ref personality, "personality", AI.defaultPersonality);
			Scribe_Values.Look(ref personalityLanguage, "personalityLanguage", "-");
			// -------------------------------------------------------------------------

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (azureVoice != null && personas.NullOrEmpty())
				{
					chatGPTModel = Tools.chatGPTModels.First();
					personas ??= new List<Persona>();
					personas.Add(new Persona()
					{
						name = "Default",
						azureVoiceLanguage = azureVoiceLanguage,
						azureVoice = azureVoice,
						azureVoiceStyle = azureVoiceStyle,
						azureVoiceStyleDegree = azureVoiceStyleDegree,
						speechRate = speechRate,
						speechPitch = speechPitch,
						phrasesLimit = phrasesLimit,
						phraseBatchSize = phraseBatchSize,
						phraseDelayMin = phraseDelayMin,
						phraseDelayMax = phraseDelayMax,
						phraseMaxWordCount = phraseMaxWordCount,
						historyMaxWordCount = historyMaxWordCount,
						personality = personality,
						personalityLanguage = personalityLanguage
					});
				}
			}
		}

		public bool IsConfigured =>
			 chatGPTKey?.Length > 0 && ((azureSpeechKey?.Length > 0 && azureSpeechRegion?.Length > 0) || showAsText);

		private Vector2 scrollPosition = Vector2.zero;
		public static Persona selected = null;
		public static int selectedIndex = -1;
		public static readonly Color listBackground = new(32 / 255f, 36 / 255f, 40 / 255f);
		public static readonly Color highlightedBackground = new(74 / 255f, 74 / 255f, 74 / 255f, 0.5f);
		public static readonly Color background = new(74 / 255f, 74 / 255f, 74 / 255f);

		public void DoWindowContents(Rect inRect)
		{
			string prevKey;
			Rect rect;

			var list = new Listing_Standard { ColumnWidth = (inRect.width - Listing.ColumnSpacing) / 2f };
			list.Begin(inRect);

			// for three columns with 20px spacing
			var width = (list.ColumnWidth - 2 * 20) / 3;

			list.Label("FFFF00", "OpenAI - ChatGPT", $"{charactersSentOpenAI} chars sent");
			prevKey = chatGPTKey;
			list.TextField(ref chatGPTKey, "API Key (paste only)", true, () => chatGPTKey = "");
			if (chatGPTKey != "" && chatGPTKey != prevKey)
			{
				Tools.ReloadGPTModels();
				AI.TestKey(
					 response => LongEventHandler.ExecuteWhenFinished(() =>
					 {
						 var dialog = new Dialog_MessageBox(response);
						 Find.WindowStack.Add(dialog);
					 })
				);
			}

			if (chatGPTKey != "")
			{
				rect = list.GetRect(UX.ButtonHeight);
				if (Widgets.ButtonText(rect, chatGPTModel))
					UX.GPTVersionMenu(l => chatGPTModel = l);
			}

			list.Gap(16f);

			list.Label("FFFF00", "Azure - Speech Services", $"{charactersSentAzure} chars sent");
			var prevRegion = azureSpeechRegion;
			list.TextField(ref azureSpeechRegion, "Region");
			if (azureSpeechRegion != prevRegion)
				Personas.UpdateVoiceInformation();
			list.Gap(6f);
			prevKey = azureSpeechKey;
			list.TextField(ref azureSpeechKey, "API Key (paste only)", true, () => azureSpeechKey = "");
			if (azureSpeechKey != "" && azureSpeechKey != prevKey && azureSpeechRegion.NullOrEmpty() == false)
				TTS.TestKey(new Persona(), () => Personas.UpdateVoiceInformation());

			list.Gap(16f);

			list.Label("FFFF00", "Miscellaneous");
			list.Slider(ref speechVolume, 0f, 10f, f => $"Speech volume: {f.ToPercentage(false)}", 0.01f);
			list.CheckboxLabeled("Show speech as subtitles", ref showAsText);
			list.Gap(6f);
			rect = list.GetRect(UX.ButtonHeight);
			if (Widgets.ButtonText(rect, "Reset Stats"))
			{
				charactersSentOpenAI = 0;
				charactersSentAzure = 0;
			}

			list.Gap(16f);

			list.Label("FFFF00", "Personas");

			var height = inRect.height - UX.ButtonHeight - 24f - list.CurHeight;
			var outerRect = list.GetRect(height);
			var listHeight = height;
			var innerRect = new Rect(0f, 0f, list.ColumnWidth, listHeight);

			Widgets.DrawBoxSolid(outerRect, listBackground);
			Widgets.BeginScrollView(outerRect, ref scrollPosition, innerRect, true);

			var list2 = new Listing_Standard();
			list2.Begin(innerRect);

			var rowHeight = 24;
			var i = 0;
			var y = 0f;
			foreach (var persona in personas)
			{
				PersonaRow(new Rect(0, y, innerRect.width, rowHeight), persona, i++);
				y += rowHeight;
			}

			list2.End();

			Widgets.EndScrollView();

			var bRect = list.GetRect(24);
			if (Widgets.ButtonImage(bRect.LeftPartPixels(24), Graphics.ButtonAdd[1]))
			{
				Event.current.Use();
				selected = new Persona();
				personas.Add(selected);
				selectedIndex = personas.IndexOf(selected);
			}
			bRect.x += 32;
			if (Widgets.ButtonImage(bRect.LeftPartPixels(24), Graphics.ButtonDel[selected != null ? 1 : 0]))
			{
				Event.current.Use();
				_ = personas.Remove(selected);
				var newCount = personas.Count;
				if (newCount == 0)
				{
					selectedIndex = -1;
					selected = null;
				}
				else
				{
					while (newCount > 0 && selectedIndex >= newCount)
						selectedIndex--;
					selected = personas[selectedIndex];
				}
			}
			bRect.x += 32;
			var dupable = selected != null;
			if (Widgets.ButtonImage(bRect.LeftPartPixels(24), Graphics.ButtonDup[dupable ? 1 : 0]) && dupable)
			{
				Event.current.Use();
				var namePrefix = Regex.Replace(selected.name, @" \d+$", "");
				var existingNames = personas.Select(p => p.name).ToHashSet();
				for (var n = 1; true; n++)
				{
					var newName = $"{namePrefix} {n}";
					if (existingNames.Contains(newName) == false)
					{
						var xml = selected.ToXml();
						selected = new Persona();
						Persona.PersonalityFromXML(xml, selected);
						selected.name = newName;
						personas.Add(selected);
						selectedIndex = personas.IndexOf(selected);
						break;
					}
				}
			}

			list.NewColumn(); //------------------------------------------------------------------------------------------------------------------
			width = (list.ColumnWidth - 2 * 20) / 3;

			if (selected != null)
			{
				var curY = list.curY;
				_ = list.Label("Persona Name");
				list.curY = curY;
				var cw = list.ColumnWidth / 2.5f;
				list.curX += cw;
				list.ColumnWidth -= cw;
				selected.name = list.TextEntry(selected.name);
				list.curX -= cw;
				list.ColumnWidth += cw;
				list.Gap(16f);

				list.Languages(LanguageDatabase.AllLoadedLanguages, selected.azureVoiceLanguage, l => l.DisplayName, l =>
				{
					selected.azureVoiceLanguage = l == null ? "-" : l.FriendlyNameEnglish;
					Personas.UpdateVoiceInformation();
				}, width, 0);
				list.Voices(selected, width, 1);
				if (UX.HasVoiceStyles(selected))
					list.VoiceStyles(selected, width, 2);
				list.Gap(30f);

				list.Gap(16f);

				list.Slider(ref selected.azureVoiceStyleDegree, 0f, 2f, f => $"Style degree: {f.ToPercentage(false)}", 0.01f);
				list.Slider(ref selected.speechRate, -0.5f, 0.5f, f => $"Speech rate: {f.ToPercentage()}", 0.01f);
				list.Slider(ref selected.speechPitch, -0.5f, 0.5f, f => $"Speech pitch: {f.ToPercentage()}", 0.01f);

				list.Gap(16f);

				rect = list.GetRect(UX.ButtonHeight);
				rect.width = width;
				if (Widgets.ButtonText(rect, "Edit personality"))
					Dialog_Personality.Show(selected);
				rect.x += width + 20;
				if (Widgets.ButtonText(rect, selected.personalityLanguage == "-" ? "Game Language" : selected.personalityLanguage))
					UX.LanguageChoiceMenu(Tools.commonLanguages, l => l, l => selected.personalityLanguage = l ?? "-");
				rect.x += width + 20;
				if (Widgets.ButtonText(rect, "Test"))
					TTS.TestKey(selected, null);

				list.Gap(16f);

				_ = list.Label("Sending game information");
				list.Slider(ref selected.phrasesLimit, 1, 100, n => $"Max phrase count: {n}");
				selected.phraseBatchSize = Mathf.Min(selected.phraseBatchSize, selected.phrasesLimit);
				list.Slider(ref selected.phraseBatchSize, 1, selected.phrasesLimit, n => $"Batch size: {n} phrases");
				list.Gap(16f);
				_ = list.Label("Delay between comments");
				list.Slider(ref selected.phraseDelayMin, 5f, 1200f, f => $"Min: {Mathf.FloorToInt(f + 0.01f)} sec", 1f, 2);
				if (selected.phraseDelayMin > selected.phraseDelayMax) selected.phraseDelayMin = selected.phraseDelayMax;
				list.Slider(ref selected.phraseDelayMax, 5f, 1200f, f => $"Max: {Mathf.FloorToInt(f + 0.01f)} sec", 1f, 2);
				if (selected.phraseDelayMax < selected.phraseDelayMin) selected.phraseDelayMax = selected.phraseDelayMin;
				list.Gap(16f);
				_ = list.Label("Comments");
				list.Slider(ref selected.phraseMaxWordCount, 1, 160, n => $"Max words: {n}");
				list.Gap(16f);
				_ = list.Label("History");
				list.Slider(ref selected.historyMaxWordCount, 200, 1200, n => $"Max words: {n}");
				list.Gap(16f);

				width = (list.ColumnWidth - 2 * 20) / 3;
				rect = list.GetRect(UX.ButtonHeight);
				rect.width = width;
				if (Widgets.ButtonText(rect, "Copy"))
				{
					var share = selected.ToXml();
					if (share.NullOrEmpty() == false)
						GUIUtility.systemCopyBuffer = share;
				}
				rect.x += width + 20;
				if (Widgets.ButtonText(rect, "Paste"))
				{
					var text = GUIUtility.systemCopyBuffer;
					if (text.NullOrEmpty() == false)
						Persona.PersonalityFromXML(text, selected);
				}
				rect.x += width + 20;
				if (Widgets.ButtonText(rect, "Defaults"))
				{
					var xml = new Persona().ToXml();
					Persona.PersonalityFromXML(xml, selected);
				}
			}

			list.End();
		}

		public static void PersonaRow(Rect rect, Persona persona, int idx)
		{
			if (persona == selected)
				Widgets.DrawBoxSolid(rect, background);
			else if (Mouse.IsOver(rect))
				Widgets.DrawBoxSolid(rect, highlightedBackground);

			var tRect = rect;
			tRect.xMin += 3;
			tRect.yMin += 1;
			_ = Widgets.LabelFit(tRect, persona.name);

			if (Widgets.ButtonInvisible(rect))
			{
				selected = persona;
				selectedIndex = idx;
			}
		}
	}
}