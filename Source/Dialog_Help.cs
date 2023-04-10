using System;
using UnityEngine;
using Verse;

namespace RimGPT
{
    public class Dialog_Help : Window
    {
        Vector2 scrollPosition;
        string topic = "";

        const string helpText = @"This mod utilizes two external APIs: openai.com for ChatGPT and Microsoft Azure for Text-to-Speech services. You must provide both keys yourself, as the overall demand for this free mod would be too costly.

Your task is to create both accounts, connect them with a credit card and add a minimum amount to their balance.

#   OpenAI

To enable ChatGPT functionality, visit https://platform.openai.com/account/api-keys and create a new secret key. Paste the key into the mod settings. If you're unable to create an account, try multiple times. Using API keys does in fact require a paid membership but its costs are minimal. For more information, refer to https://platform.openai.com/docs/api-reference/authentication and https://platform.openai.com/account/usage.

# Microsoft Azure

This mod uses Microsoft Azure's Cognitive Services to enable TTS (Text-to-Speech) through their Speech service API. Although account creation requires a credit card, 500,000 characters per month are free. To begin, go to https://azure.microsoft.com/en-gb/free/cognitive-services/ and click 'Start free'. Sign up or log in using your Microsoft or GitHub account. Locate 'Speech Services' and configure the settings. Create a resource group and a subscription (maybe use 'Budget' to set a monthly cost limit). Within the resource group, find 'Keys and Endpoint' where you can create a key and a location (e.g., centralus). Enter both into the mod settings.";

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