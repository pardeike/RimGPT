using System;
using UnityEngine;
using Verse;

namespace RimGPT
{
    public class Dialog_Help : Window
    {
        Vector2 scrollPosition;
        string topic = "";

        const string helpText = @"

";

        public override Vector2 InitialSize => new(640f, 460f);

        public Dialog_Help(string topic)
        {
            this.topic = topic;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            onlyOneOfTypeAllowed = true;
            closeOnAccept = true;
            closeOnCancel = true;
        }

        public static void Show(string topic) => Find.WindowStack?.Add(new Dialog_Help(topic));

        public override void DoWindowContents(Rect inRect)
        {
            var y = inRect.y;

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, y, inRect.width, 42f), "RimGPT Help");
            y += 42f;

            var textRect = new Rect(inRect.x, y, inRect.width, inRect.height - y);
            Widgets.TextAreaScrollable(textRect, helpText, ref scrollPosition);
        }
    }
}