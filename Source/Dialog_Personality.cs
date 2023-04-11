using UnityEngine;
using Verse;

namespace RimGPT
{
	public class Dialog_Personality : Window
	{
		public string text;
		Vector2 scrollPosition;

		public override Vector2 InitialSize => new(640f, 460f);

		public Dialog_Personality()
		{
			this.text = RimGPTMod.Settings.personality;
			doCloseX = true;
			forcePause = true;
			absorbInputAroundWindow = true;
			onlyOneOfTypeAllowed = true;
			closeOnAccept = true;
			closeOnCancel = true;
		}

		public static void Show() => Find.WindowStack?.Add(new Dialog_Personality());

		public void Save()
		{
			RimGPTMod.Settings.personality = text;
			Close();
		}

		public override void DoWindowContents(Rect inRect)
		{
			var y = inRect.y;

			Text.Font = GameFont.Small;
			Widgets.Label(new Rect(0f, y, inRect.width, 42f), "RimGPT Personality");
			y += 42f;

			var textRect = new Rect(inRect.x, y, inRect.width, inRect.height - y);
			var rect = textRect.BottomPartPixels(44).LeftPartPixels(120);
			textRect.yMax -= 60;
			text = Widgets.TextAreaScrollable(textRect, text, ref scrollPosition);
			if (Widgets.ButtonText(rect, "Save"))
				Save();
			rect.x += 140;
			if (Widgets.ButtonText(rect, "Default"))
				text = AI.defaultPersonality;
			rect.x += 140;
			if (Widgets.ButtonText(rect, "Cancel"))
				Close();
		}
	}
}