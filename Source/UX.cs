using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimGPT
{
	public static class UX
	{
		public const float ButtonHeight = 24f;

		/*public static void SmallLabel(this Listing_Standard list, string text, string tooltip = null)
		{
			var rect = list.GetRect(20f);
			var anchor = Text.Anchor;
			var font = Text.Font;
			Text.Font = GameFont.Tiny;
			Text.Anchor = TextAnchor.UpperLeft;
			Widgets.Label(rect, text);
			Text.Anchor = anchor;
			Text.Font = font;
			if (tooltip != null)
				TooltipHandler.TipRegion(rect, tooltip);
		}*/

		public static void Label(this Listing_Standard list, string hexColor, string textLeft, string textRight = "", string tooltip = null)
		{
			var size = Text.CalcSize(textLeft);
			var rect = list.GetRect(size.y);
			if (tooltip != null)
				TooltipHandler.TipRegion(rect, tooltip);
			var anchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(rect.LeftPartPixels(size.x), new TaggedString($"<color=#{hexColor}>{textLeft}</color>"));
			size = Text.CalcSize(textRight);
			Text.Anchor = TextAnchor.MiddleRight;
			Widgets.Label(rect.RightPartPixels(size.x), textRight);
			Text.Anchor = anchor;
			list.Gap(6f);
		}

		public static void TextField(this Listing_Standard list, ref string text, string label = null, bool isPassword = false, Action resetCallback = null)
		{
			var rect = list.GetRect(20f);
			if (label != null)
			{
				var anchor = Text.Anchor;
				var font = Text.Font;
				Text.Font = GameFont.Tiny;
				Text.Anchor = TextAnchor.UpperLeft;
				Widgets.Label(rect, label);
				Text.Anchor = anchor;
				Text.Font = font;

				if (Widgets.ButtonText(rect.RightPartPixels(24), "?"))
					Dialog_Help.Show();
			}
			if (isPassword && text != "")
			{
				if (list.ButtonText("Clear"))
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Do you want to reset the key?", resetCallback));
			}
			else
				text = list.TextEntry(text);
		}

		public static void Slider(this Listing_Standard list, ref int value, int min, int max, Func<int, string> label, int logarithmic = 1, string tooltip = null)
		{
			var input = logarithmic != 1 ? Mathf.Log(value, logarithmic) : value;
			var min2 = logarithmic != 1 ? Mathf.Log(min, logarithmic) : min;
			var max2 = logarithmic != 1 ? Mathf.Log(max, logarithmic) : max;
			HorizontalSlider(list.GetRect(22f), ref input, min2, max2, label == null ? null : label(Mathf.FloorToInt((logarithmic != 1 ? Mathf.Pow(logarithmic, input) : input) + 0.001f)), 1f, tooltip);
			value = Mathf.FloorToInt((logarithmic != 1 ? Mathf.Pow(logarithmic, input) : input) + 0.001f);
			list.Gap(2f);
		}

		public static void Slider(this Listing_Standard list, ref float value, float min, float max, Func<float, string> label, float roundTo = -1f, int logarithmic = 1, string tooltip = null)
		{
			var input = logarithmic != 1 ? Mathf.Log(value, logarithmic) : value;
			var min2 = logarithmic != 1 ? Mathf.Log(min, logarithmic) : min;
			var max2 = logarithmic != 1 ? Mathf.Log(max, logarithmic) : max;
			HorizontalSlider(list.GetRect(22f), ref input, min2, max2, label == null ? null : label(logarithmic != 1 ? Mathf.Pow(logarithmic, input) : input), -1f, tooltip);
			value = Mathf.Max(min, Mathf.Min(max, logarithmic != 1 ? Mathf.Pow(logarithmic, input) : input));
			if (roundTo > 0f)
				value = Mathf.RoundToInt(value / roundTo) * roundTo;
			list.Gap(2f);
		}

		public static void HorizontalSlider(Rect rect, ref float value, float leftValue, float rightValue, string label, float roundTo = -1f, string tooltip = null)
		{
			var first = rect.width / 2.5f;
			var second = rect.width - first;

			var anchor = Text.Anchor;
			var font = Text.Font;
			Text.Font = GameFont.Tiny;
			Text.Anchor = TextAnchor.UpperLeft;
			var rect2 = rect.LeftPartPixels(first);
			rect2.y -= 2f;
			Widgets.Label(rect2, label);
			Text.Anchor = anchor;
			Text.Font = font;

			value = GUI.HorizontalSlider(rect.RightPartPixels(second), value, leftValue, rightValue);
			if (roundTo > 0f)
				value = Mathf.RoundToInt(value / roundTo) * roundTo;

			if (tooltip != null)
				TooltipHandler.TipRegion(rect, tooltip);
		}

		public static string TextAreaScrollable(Rect rect, string text, ref Vector2 scrollbarPosition)
		{
			var rect2 = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(Text.CalcHeight(text, rect.width) + 10f, rect.height));
			Widgets.BeginScrollView(rect, ref scrollbarPosition, rect2, true);
			var style = Text.textAreaStyles[1];
			style.padding = new RectOffset(8, 8, 8, 8);
			style.active.background = Texture2D.blackTexture;
			style.active.textColor = Color.white;
			var result = GUI.TextArea(rect2, text, style);
			Widgets.EndScrollView();
			return result;
		}

		public static string Float(this int value, int decimals, string unit = null)
			 => value.ToString("F" + decimals) + (unit == null ? "" : $" {unit}");

		public static int Milliseconds(this float f) => Mathf.FloorToInt(f * 1000);

		public static string ToPercentage(this float value, bool addPlus = true)
		{
			var percentageValue = value * 100;
			if (addPlus)
				return $"{percentageValue:+0.##;-0.##;0}%";
			return $"{percentageValue:0.##;-0.##;0}%";
		}

		public static void GPTVersionMenu(Action<string> action)
		{
			var options = new List<FloatMenuOption> { new FloatMenuOption("ChatGPT Version", () => action(default)) };
			foreach (var version in Tools.chatGPTModels)
			{
				var label = version;
				if (version.Contains("1106"))
					label = $"{version} [PREFERRED]";
				options.Add(new FloatMenuOption(label, () =>
				{
					if (version != default)
					{
						if (version.Contains("gpt-4"))
						{
							var window = Dialog_MessageBox.CreateConfirmation("GPT-4 can create more costs and is slower to answer, do you really want to proceed?", () => action(version), true, "Attention", WindowLayer.Super);
							Find.WindowStack.Add(window);
						}
						else
							action(version);
					}
				}));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public static void LanguageChoiceMenu<T>(IEnumerable<T> languages, Func<T, string> itemFunc, Action<T> action)
		{
			var options = new List<FloatMenuOption> { new FloatMenuOption("Game Language", () => action(default)) };
			foreach (var language in languages)
				options.Add(new FloatMenuOption(itemFunc(language), () => action(language)));
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public static void Languages<T>(this Listing_Standard list, IEnumerable<T> languages, string label, Func<T, string> itemFunc, Action<T> action, float width, int column)
		{
			var rect = list.GetRect(ButtonHeight);
			list.Gap(-ButtonHeight);
			rect.width = width;
			rect.x += column * (width + 20);

			if (Widgets.ButtonText(rect, label == "-" ? "Game Language" : label))
				LanguageChoiceMenu(languages, itemFunc, action);
		}

		public static void Voices(this Listing_Standard list, Persona persona, float width, int column)
		{
			var rect = list.GetRect(ButtonHeight);
			list.Gap(-ButtonHeight);
			rect.width = width;
			rect.x += column * (width + 20);

			var currentVoice = Voice.From(persona.azureVoice);
			if (Widgets.ButtonText(rect, currentVoice?.DisplayName ?? ""))
			{
				if (TTS.voices.NullOrEmpty())
					return;

				var options = new List<FloatMenuOption>();

				void AddVoice(Voice voice)
				{
					string country = null;
					var localeName = voice.LocaleName;
					var idx = localeName.LastIndexOf('(');
					if (idx >= 0 && localeName.EndsWith(")"))
						country = localeName.Substring(idx);
					var floatMenuOption = new FloatMenuOption(voice.DisplayName + (country != null ? $" {country}" : ""), () =>
					{
						persona.azureVoice = voice.ShortName;
						persona.azureVoiceStyle = VoiceStyle.Values[0].Value;
					});
					var tooltip = $"{voice.Gender}, {voice.WordsPerMinute} Words/min, {voice.LocaleName}";
					floatMenuOption.tooltip = new TipSignal?(tooltip);
					options.Add(floatMenuOption);
				}

				var voices = TTS.voices.Where(voice => voice.LocaleName.Contains(Tools.VoiceLanguage(persona))).OrderBy(voice => voice.DisplayName);
				var voicesWithStyles = voices.Where(voice => voice.StyleList.NullOrEmpty() == false);
				foreach (var voice in voicesWithStyles)
					AddVoice(voice);
				if (voicesWithStyles.Any())
					options.Add(new FloatMenuOption("-", () => { }));
				foreach (var voice in voices.Where(voice => voice.StyleList.NullOrEmpty()))
					AddVoice(voice);
				Find.WindowStack.Add(new FloatMenu(options));
			}
		}

		public static bool HasVoiceStyles(Persona persona)
		{
			var currentVoice = Voice.From(persona.azureVoice);
			var availableStyles = currentVoice?.StyleList;
			return availableStyles.NullOrEmpty() == false;
		}

		public static void VoiceStyles(this Listing_Standard list, Persona persona, float width, int column)
		{
			var rect = list.GetRect(ButtonHeight);
			list.Gap(-ButtonHeight);
			rect.width = width;
			rect.x += column * (width + 20);

			var currentVoice = Voice.From(persona.azureVoice);
			var availableStyles = currentVoice?.StyleList;
			if (availableStyles.NullOrEmpty())
				availableStyles = ["default"];
			var currentStyle = VoiceStyle.From(persona.azureVoiceStyle);
			if (Widgets.ButtonText(rect, currentStyle?.Name ?? ""))
			{
				var options = new List<FloatMenuOption>();
				foreach (var styleName in availableStyles)
				{
					var style = VoiceStyle.From(styleName);
					var floatMenuOption = new FloatMenuOption(style.Name, () => persona.azureVoiceStyle = style.Value);
					if (style.Tooltip != null)
						floatMenuOption.tooltip = new TipSignal?(style.Tooltip);
					options.Add(floatMenuOption);
				}
				Find.WindowStack.Add(new FloatMenu(options));
			}
		}
	}
}