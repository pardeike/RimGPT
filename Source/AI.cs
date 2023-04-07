using HarmonyLib;
using Newtonsoft.Json;
using OpenAI;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;

namespace RimGPT
{
    public static class AI
    {
        struct Input
        {
            public string[] happenings;
            public string[] history;
        }

        struct Output
        {
            public string comment;
            public string history;
        }

        // we need to assign the fields somewhere or the compiler will complain that they are not used
        private static readonly Output outputDummy = new Output() { comment = "", history = "" };

        private static readonly OpenAIApi openAI = new(Configuration.chatGPTKey);
        private static string commentName = "comment";
        private static string historyName = "history";
        private static string happeningsName = "happenings";
        private static List<string> history = new();

        private static readonly string systemPrompt =
@$"You are an experienced player of the game RimWorld.
You are funny and good at assessing situations.
You will repeatedly receive input from an ongoing Rimworld game.
Here are the rules you must follow:

Rule: Your input is in json that matches this model:
```cs
struct Input {{
  public string[] {happeningsName};
  public string[] {historyName};
}}
```

Rule: Your output is in json that matches this model:
```cs
struct Output {{
  public string {commentName};
  public string {historyName};
}}
```

Rule: '{happeningsName}' are things that happened in the current game

Rule: Items in '{happeningsName}' are machine generated from typical game output

Rule: You pick one item from '{happeningsName}' to focus on and discuss its consequences or relations to previous things

Rule: '{commentName}' should be funny

Rule: '{commentName}' must not be longer than {Configuration.phraseMaxWordCount} words

Rule: Don't use words like 'looks like' or 'meanwhile'

Rule: Try to generate fluent sentences that don't contain too much punctuation so the text to speech engine reads with less pauses

Rule: 'Input.{historyName}' represents all information about the game progress so far

Rule: 'Output.{historyName}' should be information you want to remember in condensed form, use up to {Configuration.historyMaxWordCount} words

Important rule: you ONLY answer in json as defined in the rules!";

        public static async Task<string> Evaluate(string[] observations)
        {
            var input = new Input()
            {
                happenings = observations,
                history = history.ToArray()
            };
            var content = JsonConvert.SerializeObject(input);
            Log.Warning($"INPUT: {content}");

            var observationString = observations.Join(o => $"- {o}", "\n");
            var completionResponse = await openAI.CreateChatCompletion(new CreateChatCompletionRequest()
            {
                Model = "gpt-3.5-turbo",
                Messages = new List<ChatMessage>()
                {
                    new ChatMessage()
                    {
                        Role = "system",
                        Content = systemPrompt
                    },
                    new ChatMessage()
                    {
                        Role = "user",
                        Content = content
                    }
                }
            });

            if (completionResponse.Choices?.Count > 0)
            {
                var response = completionResponse.Choices[0].Message.Content;
                Log.Warning($"OUTPUT: {response}");
                var output = JsonConvert.DeserializeObject<Output>(response);
                history.Add(output.history);
                var oversize = history.Count - Configuration.historyMaxItemCount;
                if (oversize > 0)
                    history.RemoveRange(0, oversize);
                return output.comment;
            }
            return null;
        }
    }
}