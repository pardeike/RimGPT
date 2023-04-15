using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static HarmonyLib.Code;

namespace RimGPT
{
	public static class UX
	{
		public static void SmallLabel(this Listing_Standard list, string text)
		{
			var rect = list.GetRect(20f);
			var anchor = Text.Anchor;
			var font = Text.Font;
			Text.Font = GameFont.Tiny;
			Text.Anchor = TextAnchor.UpperLeft;
			Widgets.Label(rect, text);
			Text.Anchor = anchor;
			Text.Font = font;
		}

		public static void Label(this Listing_Standard list, string hexColor, string textLeft, string textRight = "")
		{
			var size = Text.CalcSize(textLeft);
			var rect = list.GetRect(size.y);
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

		public static void Slider(this Listing_Standard list, ref int value, int min, int max, Func<string> label)
		{
			float f = value;
			var h = HorizontalSlider(list.GetRect(22f), ref f, min, max, label == null ? null : label(), 1f);
			value = (int)f;
			list.Gap(h);
		}

		public static void Slider(this Listing_Standard list, ref float value, float min, float max, Func<string> label, float roundTo = -1f)
		{
			var rect = list.GetRect(22f);
			var h = HorizontalSlider(rect, ref value, min, max, label == null ? null : label(), roundTo);
			list.Gap(h);
		}

		public static float HorizontalSlider(Rect rect, ref float value, float leftValue, float rightValue, string label, float roundTo = -1f)
		{
			if (label != null)
			{
				var anchor = Text.Anchor;
				var font = Text.Font;
				Text.Font = GameFont.Tiny;
				Text.Anchor = TextAnchor.UpperLeft;
				Widgets.Label(rect, label);
				Text.Anchor = anchor;
				Text.Font = font;

				rect.y += 18f;
			}
			value = GUI.HorizontalSlider(rect, value, leftValue, rightValue);
			if (roundTo > 0f)
				value = Mathf.RoundToInt(value / roundTo) * roundTo;
			return 4f + label != null ? 18f : 0f;
		}

		public static string TextAreaScrollable(Rect rect, string text, ref Vector2 scrollbarPosition)
		{
			Rect rect2 = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(Text.CalcHeight(text, rect.width) + 10f, rect.height));
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

		public static void LanguageChoiceMenu<T>(IEnumerable<T> languages, Func<T, string> itemFunc, Action<T> action)
		{
			var options = new List<FloatMenuOption> { new FloatMenuOption("Game Language", () => action(default)) };
			foreach (var language in languages)
				options.Add(new FloatMenuOption(itemFunc(language), () => action(language)));
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public static void Languages<T>(this Listing_Standard list, IEnumerable<T> languages, string label, Func<T, string> itemFunc, Action<T> action, float width, int column)
		{
			var rect = list.GetRect(30f);
			list.Gap(-30f);
			rect.width = width;
			rect.x += column * (width + 20);

			if (Widgets.ButtonText(rect, label == "-" ? "Game Language" : label))
				LanguageChoiceMenu(languages, itemFunc, action);
		}

		public static void Voices(this Listing_Standard list, float width, int column)
		{
			var rect = list.GetRect(30f);
			list.Gap(-30f);
			rect.width = width;
			rect.x += column * (width + 20);

			var currentVoice = Voice.From(RimGPTMod.Settings.azureVoice);
			if (Widgets.ButtonText(rect, currentVoice?.DisplayName ?? ""))
			{
				if (TTS.voices.NullOrEmpty())
					return;

				var options = new List<FloatMenuOption>();
				var voices = TTS.voices.Where(voice => voice.LocaleName.Contains(Tools.VoiceLanguage)).OrderBy(voice => voice.DisplayName);
				foreach (var voice in voices)
				{
					var hasStyles = voice.StyleList.NullOrEmpty() == false;
					var floatMenuOption = new FloatMenuOption(voice.DisplayName + (hasStyles ? " *" : ""), () =>
					{
						RimGPTMod.Settings.azureVoice = voice.ShortName;
						RimGPTMod.Settings.azureVoiceStyle = VoiceStyle.Values[0].Value;
					});
					var tooltip = $"{voice.Gender}, {voice.WordsPerMinute} Words/min, {voice.LocaleName}";
					floatMenuOption.tooltip = new TipSignal?(tooltip);
					options.Add(floatMenuOption);
				}
				Find.WindowStack.Add(new FloatMenu(options));
			}
		}

		public static bool HasVoiceStyles()
		{
			var currentVoice = Voice.From(RimGPTMod.Settings.azureVoice);
			var availableStyles = currentVoice?.StyleList;
			return availableStyles.NullOrEmpty() == false;
		}

		public static void VoiceStyles(this Listing_Standard list, float width, int column)
		{
			var rect = list.GetRect(30f);
			list.Gap(-30f);
			rect.width = width;
			rect.x += column * (width + 20);

			var currentVoice = Voice.From(RimGPTMod.Settings.azureVoice);
			var availableStyles = currentVoice?.StyleList;
			if (availableStyles.NullOrEmpty())
				availableStyles = new[] { "default" };
			var currentStyle = VoiceStyle.From(RimGPTMod.Settings.azureVoiceStyle);
			if (Widgets.ButtonText(rect, currentStyle?.Name ?? ""))
			{
				var options = new List<FloatMenuOption>();
				foreach (var styleName in availableStyles)
				{
					var style = VoiceStyle.From(styleName);
					var floatMenuOption = new FloatMenuOption(style.Name, () => RimGPTMod.Settings.azureVoiceStyle = style.Value);
					if (style.Tooltip != null)
						floatMenuOption.tooltip = new TipSignal?(style.Tooltip);
					options.Add(floatMenuOption);
				}
				Find.WindowStack.Add(new FloatMenu(options));
			}
		}
	}
}