using UnityEngine;
using Verse;

namespace RimGPT
{
	public class Dialog_Personality : Window
	{
		public Persona persona;
		public string text;
		Vector2 scrollPosition;

		public override Vector2 InitialSize => new(640f, 460f);

		public Dialog_Personality(Persona persona)
		{
			this.persona = persona;
			text = persona.personality;
			doCloseX = false;
			doCloseButton = false;
			forcePause = true;
			absorbInputAroundWindow = true;
			onlyOneOfTypeAllowed = true;
			closeOnAccept = false;
			closeOnCancel = false;
		}

		public static void Show(Persona persona) => Find.WindowStack?.Add(new Dialog_Personality(persona));

		public void Save()
		{
			persona.personality = text;
			Close();
		}

		public override void OnAcceptKeyPressed()
		{
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
			text = UX.TextAreaScrollable(textRect, text, ref scrollPosition);
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