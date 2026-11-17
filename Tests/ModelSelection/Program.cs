using OpenAI;
using RimGPT;

static class Program
{
    static int passed, failed;
    static AI ai;
    static Persona persona;
    static UserApiConfig config;

    static void Reset()
    {
        ai = new AI();
        persona = new Persona();
        config = new UserApiConfig();
        RimGPTMod.Settings = new TestSettings { userApiConfigs = [config], personas = [persona] };
        OpenAIApi.Requests.Clear();
        OpenAIApi.Respond = null;
        Tools.ConfigUpdates = 0;
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static async Task Test(string name, Func<Task> body)
    {
        Reset();
        try { await body(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
    }

    static Task<string> Evaluate() => ai.Evaluate(persona, [new Phrase()]);

    static async Task<int> Main()
    {
        await Test("one selection per narration request", async () =>
        {
            config.ModelSwitchRatio = 10;
            await Evaluate();
            Check(OpenAIApi.Requests.Count == 1 && Tools.ConfigUpdates == 1 && ai.modelSwitchCounter == 1,
                $"One request performed {Tools.ConfigUpdates} selections; counter={ai.modelSwitchCounter}");
        });
        await Test("plain primary keeps plain response format", async () =>
        {
            config.ModelSwitchRatio = 2;
            config.SecondaryModelId = "secondary-1106";
            await Evaluate();
            var request = OpenAIApi.Requests.Single();
            Check(request.Model == config.ModelId && request.ResponseFormat == null, "Response format was selected using a different model");
        });
        await Test("JSON secondary gets its own response format", async () =>
        {
            config.ModelSwitchRatio = 2;
            config.SecondaryModelId = "secondary-1106";
            ai.modelSwitchCounter = 1;
            await Evaluate();
            var request = OpenAIApi.Requests.Single();
            Check(request.Model == config.SecondaryModelId && request.ResponseFormat?.Type == "json_object", "Secondary JSON mode did not match the request model");
        });
        await Test("configured three-request cadence", async () =>
        {
            for (int i = 0; i < 6; i++) await Evaluate();
            var expected = new[] { config.ModelId, config.ModelId, config.SecondaryModelId, config.ModelId, config.ModelId, config.SecondaryModelId };
            Check(OpenAIApi.Requests.Select(r => r.Model).SequenceEqual(expected), "Expected two primary requests followed by one secondary request");
            Check(ai.modelSwitchCounter == 0 && Tools.ConfigUpdates == 6, "Cadence did not advance once per request");
        });
        await Test("condensation forces secondary and resets cadence", async () =>
        {
            ai.modelSwitchCounter = 1;
            ai.ReplaceHistory(new[] { "First event", "Second event" });
            await ai.CondenseHistory(persona);
            Check(OpenAIApi.Requests.Single().Model == config.SecondaryModelId && ai.modelSwitchCounter == 0, "Condensation missed the secondary model or failed to reset the cycle");
            await Evaluate();
            Check(OpenAIApi.Requests[1].Model == config.ModelId && ai.modelSwitchCounter == 1, "Narration did not restart the cycle after condensation");
        });
        await Test("disabled secondary does not change counter during condensation", async () =>
        {
            config.UseSecondaryModel = false;
            ai.modelSwitchCounter = 7;
            await ai.CondenseHistory(persona);
            Check(OpenAIApi.Requests.Single().Model == config.ModelId && ai.modelSwitchCounter == 7, "Primary-only condensation changed the dormant cadence");
        });
        await Test("counter above a lowered threshold recovers", async () =>
        {
            ai.modelSwitchCounter = 8;
            await ai.SimplePrompt("Offline request");
            Check(OpenAIApi.Requests.Single().Model == config.SecondaryModelId && ai.modelSwitchCounter == 0, "An overshot counter never reached the secondary model");
        });
        await Test("retry advances once per attempt", async () =>
        {
            config.ModelSwitchRatio = 2;
            OpenAIApi.Respond = count => count == 1 ? new CreateChatCompletionResponse() : OpenAIApi.Success();
            await Evaluate();
            Check(OpenAIApi.Requests.Select(r => r.Model).SequenceEqual(new[] { config.ModelId, config.SecondaryModelId }), "Retry did not follow request cadence");
            Check(Tools.ConfigUpdates == 2 && ai.modelSwitchCounter == 0, "Retry selected more than once per request");
        });
        await Test("ratio one retains existing secondary-only cadence", async () =>
        {
            config.ModelSwitchRatio = 1;
            await Evaluate();
            await Evaluate();
            Check(OpenAIApi.Requests.All(r => r.Model == config.SecondaryModelId) && ai.modelSwitchCounter == 0, "Ratio-one semantics changed");
        });
        Console.WriteLine($"{passed} passed; {failed} failed. All requests stayed in the local recorder.");
        return failed == 0 ? 0 : 1;
    }
}
