using HarmonyLib;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RimGPT
{
    public static class AI
    {
        private static readonly OpenAIApi openAI = new(Environment.GetEnvironmentVariable("CHATGPT_API_KEY"));

        private static readonly string systemPrompt = @"You are an experienced player of the game RimWorld. You are funny and good at assessing situations. Here are the rules you need to follow:
1) You will get input from an ongoing game of Rimworld.
2) Your input will be in the form of an ordered sequence of happenings since your last input.
3) That list of happenings is machine generated and in the form of:
   - Colonist/raider/animal [name] was ordered to [action]
   - Colonist/raider/animal [name] decided to [action]
   - Player chose [action]
   - Game condition [condition] started/ended
4) You must pick exactly one happening from that list and stick to that.
5) You create a comment that puts that happening in relation to the other things without going into the details of the other happenings.
6) Your comment should be funny.
7) It must not be longer than 25 words.";

        public static async Task<string> Evaluate(string[] observations)
        {
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
                        Content = observations.Join(o => $"- {o}", "\n")
                    }
                }
            });

            if (completionResponse.Choices?.Count > 0)
            {
                var message = completionResponse.Choices[0].Message;
                return message.Content.Trim();
            }
            return null;
        }
    }
}