using Newtonsoft.Json;
using RimGPT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace OpenAI
{
	public static class OpenAIApi
	{
		//private static readonly bool loadApiFromFile = false;
		public static List<ApiConfig> apiConfigs = ApiTools.GetApiConfigs();

		/// <summary> The current API configuration being used. ApiConfig contains ApiKeys, Endpoints, BaseURLs, Ports and a list of Models for each API Provider.</summary>
		public static ApiConfig currentConfig = apiConfigs.GetConfig(RimGPTMod.Settings.ApiProviderPrimary);

		public static void SwitchConfig(string provider)
		{
			currentConfig = apiConfigs.GetConfig(provider);
		}

		/// <summary>
		/// Initializes the OpenAIApi with specified API key, organization, and base URL.
		/// A new Configuration is created if the API key is not provided or empty.
		/// </summary>
		//public OpenAIApi(string apiKey, string organization = null, Provider apiProvider = Provider.OpenAI, int? port = null)
		//{
		//	if (apiProvider.NeedsApiKey() && string.IsNullOrEmpty(apiKey))
		//	{
		//		if (loadApiFromFile)
		//			apiKey = Configuration.GetApiKeyFromFile();

		//		if (loadApiFromFile == false || apiKey == null)
		//			Logger.Error($"Api Key for {apiProvider} could not be loaded.");
		//	}

		//	apiConfig = new ApiConfig(apiProvider, apiKey, organization, port);
		//	currentConfig.Endpoints = new Endpoints(apiConfig);
		//}

		public static JsonSerializerSettings settings = Configuration.JsonSerializerSettings;

		/// <summary>Processes the given UnityWebRequest and returns the response.</summary>
		/// <typeparam name="T">The type of the response.</typeparam>
		/// <param name="request">The UnityWebRequest to process.</param>
		/// <param name="errorCallback">Action to call in case of an error.</param>
		/// <returns>A Task containing the response of type T.</returns>
		private static async Task<T> ProcessRequest<T>(UnityWebRequest request, Action<string> errorCallback = null)
		{
			try
			{
				UnityWebRequestAsyncOperation asyncOperation = request.SendWebRequest();
				while (!asyncOperation.isDone && RimGPTMod.Running)
					await Task.Delay(200);
			}
			catch (Exception exception)
			{
				string error = $"Error communicating with OpenAI: {exception}";
				errorCallback?.Invoke(error);
				return default;
			}

			string response = request.downloadHandler.text.Trim();
			//if (Tools.DEBUG) Logger.Message($"response: {response}"); // TEMP
			if (response != null)
			{
				if (response.StartsWith("{") == false)
					response = "{" + response;
				if (response.Contains("}") == false)
					response += "}";
			}
			long code = request.responseCode;
			if (code >= 300)
			{
				string error = $"Got {code} response from OpenAI: {response}";
				errorCallback?.Invoke(error);
				return default;
			}

			try
			{
				return JsonConvert.DeserializeObject<T>(response, settings);
			}
			catch (Exception)
			{
				string error = $"Error while decoding response from OpenAI: {response}";
				errorCallback?.Invoke(error);
				return default;
			}
		}

		#region Dispatch Requests

		/// <summary>Dispatches an HTTP request to the specified path with the specified method and optional payload.</summary>
		/// <param name="path">The path to send the request to.</param>
		/// <param name="method">The HTTP method to use for the request.</param>
		/// <param name="payload">An optional byte array of json payload to include in the request.</param>
		/// <param name="errorCallback">Action to call in case of an error.</param>
		/// <param name="form">Optional multipart form data for the request.</param>
		/// <typeparam name="T">Response type of the request.</typeparam>
		/// <returns>A Task containing the response from the request as the specified type.</returns>
		private static async Task<T> DispatchRequest<T>(string path, string method, byte[] payload = null, Action<string> errorCallback = null, List<IMultipartFormSection> form = null) where T : IResponse
		{
			using (UnityWebRequest request = new UnityWebRequest(path, method))
			{
				request.SetHeaders(currentConfig, ContentType.ApplicationJson);
				request.downloadHandler = new DownloadHandlerBuffer();

				if (payload != null)
					request.uploadHandler = new UploadHandlerRaw(payload) { contentType = ContentType.ApplicationJson };

				if (form != null)
				{
					byte[] boundary = UnityWebRequest.GenerateBoundary();
					byte[] formSections = UnityWebRequest.SerializeFormSections(form, boundary);
					string contentType = $"{ContentType.MultipartFormData}; boundary={Encoding.UTF8.GetString(boundary)}";
					request.uploadHandler = new UploadHandlerRaw(formSections) { contentType = contentType };
				}
				return await ProcessRequest<T>(request, errorCallback);
			}
		}

		/// <summary>Dispatches an HTTP request to the specified path with the specified method and optional payload.</summary>
		/// <param name="path">The path to send the request to.</param>
		/// <param name="method">The HTTP method to use for the request.</param>
		/// <param name="onResponse">A callback function to be called when a response is updated.</param>
		/// <param name="onComplete">A callback function to be called when the request is complete.</param>
		/// <param name="token">A cancellation token to cancel the request.</param>
		/// <param name="payload">An optional byte array of json payload to include in the request.</param>
		/// <param name="errorCallback">Action to call in case of an error.</param>
		private static async Task DispatchRequest<T>(string path, string method, Action<List<T>> onResponse, Action onComplete, CancellationTokenSource token, byte[] payload = null, Action<string> errorCallback = null) where T : IResponse
		{
			using (UnityWebRequest request = UnityWebRequest.Put(path, payload))
			{
				request.method = method;
				request.SetHeaders(currentConfig, ContentType.ApplicationJson);

				UnityWebRequestAsyncOperation asyncOperation = request.SendWebRequest();

				do
				{
					List<T> dataList = new List<T>();
					string[] lines = request.downloadHandler.text.Split('\n').Where(line => line != "").ToArray();

					foreach (string line in lines)
					{
						string value = line.Replace("data: ", "");

						if (value.Contains("[DONE]"))
						{
							onComplete?.Invoke();
							break;
						}

						T data = JsonConvert.DeserializeObject<T>(value, settings);

						if (data?.Error != null)
						{
							ApiError apiError = data.Error;
							string error = $"Error Message: {apiError.Message}\nError Type: {apiError.Type}\n";
							errorCallback.Invoke(error);
						}
						else
						{
							dataList.Add(data);
						}
					}
					onResponse?.Invoke(dataList);

					await Task.Yield();
				}
				while (!asyncOperation.isDone && !token.IsCancellationRequested);

				onComplete?.Invoke();
			}
		}

		#endregion Dispatch Requests

		/// <summary>Create byte array payload from the given request object that contains the parameters. </summary>
		/// <param name="request">The request object that contains the parameters of the payload.</param>
		/// <typeparam name="T">type of the request object.</typeparam>
		/// <returns>Byte array payload.</returns>
		private static byte[] CreatePayload<T>(T request)
		{
			if (request == null)
				Logger.Error($"Request object cannot be null. Type: {nameof(request)}");

			string json = JsonConvert.SerializeObject(request, settings);
			//if (Tools.DEBUG) Logger.Message($"Reqeust Payload: {json}"); // TEMP
			//if (Tools.DEBUG) Logger.Message($"Current Config: {JsonConvert.SerializeObject(currentConfig, settings)}"); // TEMP
			return Encoding.UTF8.GetBytes(json);
		}

		/// <summary>Lists the currently available models and provides basic information about each.</summary>
		public static async Task<ListModelsResponse> ListModels()
		{
			return await DispatchRequest<ListModelsResponse>
				(currentConfig.Endpoints.ListModels, UnityWebRequest.kHttpVerbGET);
		}

		/// <summary>Retrieves a model instance, providing basic information about the model such as the owner and permissioning.</summary>
		/// <param name="id">The ID of the model to use for this request</param>
		/// <returns>See <see cref="Model"/></returns>
		public static async Task<OpenAIModel> RetrieveModel(string id)
		{
			return await DispatchRequest<OpenAIModelResponse>
				(currentConfig.Endpoints.RetrieveModel(id), UnityWebRequest.kHttpVerbGET);
		}

		/// <summary>Creates a completion for the provided prompt and parameters.</summary>
		/// <param name="request">See <see cref="CreateCompletionRequest"/></param>
		/// <returns>See <see cref="CreateCompletionResponse"/></returns>
		public static async Task<CreateCompletionResponse> CreateCompletion(CreateCompletionRequest request)
		{
			byte[] payload = CreatePayload(request);
			return await DispatchRequest<CreateCompletionResponse>
				(currentConfig.Endpoints.CreateCompletion, UnityWebRequest.kHttpVerbPOST, payload);
		}

		/// <summary>Creates a chat completion request as in ChatGPT.</summary>
		/// <param name="request">See <see cref="CreateChatCompletionRequest"/></param>
		/// <param name="onResponse">Callback function that will be called when stream response is updated.</param>
		/// <param name="onComplete">Callback function that will be called when stream response is completed.</param>
		/// <param name="token">Cancellation token to cancel the request.</param>
		public static async Task CreateCompletionAsync(CreateCompletionRequest request, Action<List<CreateCompletionResponse>> onResponse, Action onComplete, CancellationTokenSource token)
		{
			request.Stream = true;
			byte[] payload = CreatePayload(request);

			// In the original Unity plugin, this was not awaited and the method was void instead of async Task
			await DispatchRequest
				(currentConfig.Endpoints.CreateCompletion, UnityWebRequest.kHttpVerbPOST, onResponse, onComplete, token, payload);
		}

		/// <summary>Creates a chat completion request as in ChatGPT.</summary>
		/// <param name="request">See <see cref="CreateChatCompletionRequest"/></param>
		/// <returns>See <see cref="CreateChatCompletionResponse"/></returns>
		public static async Task<CreateChatCompletionResponse> CreateChatCompletion(CreateChatCompletionRequest request, Action<string> errorCallback)
		{
			// if (Tools.DEBUG) Logger.Message($"request: {JsonConvert.SerializeObject(request, settings)}"); // TEMP
			byte[] payload = CreatePayload(request);
			var response = await DispatchRequest<CreateChatCompletionResponse>
				(currentConfig.Endpoints.CreateChatCompletion, UnityWebRequest.kHttpVerbPOST, payload, errorCallback);
			return response;
		}

		/// <summary> Creates a chat completion request as in ChatGPT. </summary>
		/// <param name="request">See <see cref="CreateChatCompletionRequest"/></param>
		/// <param name="onResponse">Callback function that will be called when stream response is updated.</param>
		/// <param name="onComplete">Callback function that will be called when stream response is completed.</param>
		/// <param name="token">Cancellation token to cancel the request.</param>
		public static async Task CreateChatCompletionAsync(CreateChatCompletionRequest request, Action<List<CreateChatCompletionResponse>> onResponse, Action onComplete, CancellationTokenSource token)
		{
			request.Stream = true;
			byte[] payload = CreatePayload(request);

			// In the original Unity plugin, this was not awaited and the method was void instead of async Task
			await DispatchRequest
				(currentConfig.Endpoints.CreateChatCompletion, UnityWebRequest.kHttpVerbPOST, onResponse, onComplete, token, payload);
		}

		/// <summary>Creates a new edit for the provided input, instruction, and parameters.</summary>
		/// <param name="request">See <see cref="CreateEditRequest"/></param>
		/// <returns>See <see cref="CreateEditResponse"/></returns>
		public static async Task<CreateEditResponse> CreateEdit(CreateEditRequest request)
		{
			byte[] payload = CreatePayload(request);
			return await DispatchRequest<CreateEditResponse>
				(currentConfig.Endpoints.CreateEdit, UnityWebRequest.kHttpVerbPOST, payload);
		}

		/// <summary>Creates an image given a prompt.</summary>
		/// <param name="request">See <see cref="CreateImageRequest"/></param>
		/// <returns>See <see cref="CreateImageResponse"/></returns>
		public static async Task<CreateImageResponse> CreateImage(CreateImageRequest request)
		{
			byte[] payload = CreatePayload(request);
			return await DispatchRequest<CreateImageResponse>
				(currentConfig.Endpoints.CreateImage, UnityWebRequest.kHttpVerbPOST, payload);
		}

		/// <summary>Creates an edited or extended image given an original image and a prompt.</summary>
		/// <param name="request">See <see cref="CreateImageEditRequest"/></param>
		/// <returns>See <see cref="CreateImageResponse"/></returns>
		public static async Task<CreateImageResponse> CreateImageEdit(CreateImageEditRequest request)
		{
			List<IMultipartFormSection> form = new List<IMultipartFormSection>();
			form.AddFile(request.Image, "image", "image/png");
			form.AddFile(request.Mask, "mask", "image/png");
			form.AddValue(request.Prompt, "prompt");
			form.AddValue(request.N, "n");
			form.AddValue(request.Size, "size");
			form.AddValue(request.ResponseFormat, "response_format");

			return await DispatchRequest<CreateImageResponse>
				(currentConfig.Endpoints.CreateImageEdit, UnityWebRequest.kHttpVerbPOST, form: form);
		}

		/// <summary>Creates a variation of a given image.</summary>
		/// <param name="request">See <see cref="CreateImageVariationRequest"/></param>
		/// <returns>See <see cref="CreateImageResponse"/></returns>
		public static async Task<CreateImageResponse> CreateImageVariation(CreateImageVariationRequest request)
		{
			List<IMultipartFormSection> form = new List<IMultipartFormSection>();
			form.AddFile(request.Image, "image", "image/png");
			form.AddValue(request.N, "n");
			form.AddValue(request.Size, "size");
			form.AddValue(request.ResponseFormat, "response_format");
			form.AddValue(request.User, "user");

			return await DispatchRequest<CreateImageResponse>
				(currentConfig.Endpoints.CreateImageVariation, UnityWebRequest.kHttpVerbPOST, form: form);
		}

		/// <summary>Creates an embedding vector representing the input text.</summary>
		/// <param name="request">See <see cref="CreateEmbeddingsRequest"/></param>
		/// <returns>See <see cref="CreateEmbeddingsResponse"/></returns>
		public static async Task<CreateEmbeddingsResponse> CreateEmbeddings(CreateEmbeddingsRequest request)
		{
			byte[] payload = CreatePayload(request);
			return await DispatchRequest<CreateEmbeddingsResponse>
				(currentConfig.Endpoints.CreateEmbeddings, UnityWebRequest.kHttpVerbPOST, payload);
		}

		/// <summary>Transcribes audio into the input language.</summary>
		/// <param name="request">See <see cref="CreateAudioTranscriptionsRequest"/></param>
		/// <returns>See <see cref="CreateAudioResponse"/></returns>
		public static async Task<CreateAudioResponse> CreateAudioTranscription(CreateAudioTranscriptionsRequest request)
		{
			List<IMultipartFormSection> form = new List<IMultipartFormSection>();
			if (string.IsNullOrEmpty(request.File))
			{
				form.AddData(request.FileData, "file", $"audio/{Path.GetExtension(request.File)}");
			}
			else
			{
				form.AddFile(request.File, "file", $"audio/{Path.GetExtension(request.File)}");
			}
			form.AddValue(request.Model, "model");
			form.AddValue(request.Prompt, "prompt");
			form.AddValue(request.ResponseFormat, "response_format");
			form.AddValue(request.Temperature, "temperature");
			form.AddValue(request.Language, "language");

			return await DispatchRequest<CreateAudioResponse>
				(currentConfig.Endpoints.CreateAudioTranscription, UnityWebRequest.kHttpVerbPOST, form: form);
		}

		/// <summary>Translates audio into English.</summary>
		/// <param name="request">See <see cref="CreateAudioTranslationRequest"/></param>
		/// <returns>See <see cref="CreateAudioResponse"/></returns>
		public static async Task<CreateAudioResponse> CreateAudioTranslation(CreateAudioTranslationRequest request)
		{
			List<IMultipartFormSection> form = new List<IMultipartFormSection>();
			if (string.IsNullOrEmpty(request.File))
			{
				form.AddData(request.FileData, "file", $"audio/{Path.GetExtension(request.File)}");
			}
			else
			{
				form.AddFile(request.File, "file", $"audio/{Path.GetExtension(request.File)}");
			}
			form.AddValue(request.Model, "model");
			form.AddValue(request.Prompt, "prompt");
			form.AddValue(request.ResponseFormat, "response_format");
			form.AddValue(request.Temperature, "temperature");

			return await DispatchRequest<CreateAudioResponse>
				(currentConfig.Endpoints.CreateAudioTranslation, UnityWebRequest.kHttpVerbPOST, form: form);
		}

		/// <summary>Returns a list of files that belong to the user's organization.</summary>
		/// <returns>See <see cref="ListFilesResponse"/></returns>
		public static async Task<ListFilesResponse> ListFiles()
		{
			return await DispatchRequest<ListFilesResponse>
				(currentConfig.Endpoints.ListFiles, UnityWebRequest.kHttpVerbGET);
		}

		/// <summary>
		/// Upload a file that contains document(s) to be used across various endpoints/features.
		/// Currently, the size of all the files uploaded by one organization can be up to 1 GB.
		/// Please contact us if you need to increase the storage limit.
		/// </summary>
		/// <param name="request">See <see cref="CreateFileRequest"/></param>
		/// <returns>See <see cref="OpenAIFile"/></returns>
		public static async Task<OpenAIFile> CreateFile(CreateFileRequest request)
		{
			List<IMultipartFormSection> form = new List<IMultipartFormSection>();
			form.AddFile(request.File, "file", "application/json");
			form.AddValue(request.Purpose, "purpose");

			return await DispatchRequest<OpenAIFileResponse>
				(currentConfig.Endpoints.CreateFile, UnityWebRequest.kHttpVerbPOST, form: form);
		}

		/// <summary> Delete a file.</summary>
		/// <param name="id">The ID of the file to use for this request</param>
		/// <returns>See <see cref="DeleteResponse"/></returns>
		public static async Task<DeleteResponse> DeleteFile(string id)
		{
			return await DispatchRequest<DeleteResponse>
				(currentConfig.Endpoints.DeleteFile(id), UnityWebRequest.kHttpVerbDELETE);
		}

		/// <summary>Returns information about a specific file.</summary>
		/// <param name="id">The ID of the file to use for this request</param>
		/// <returns>See <see cref="OpenAIFile"/></returns>
		public static async Task<OpenAIFile> RetrieveFile(string id)
		{
			return await DispatchRequest<OpenAIFileResponse>
				(currentConfig.Endpoints.RetrieveFile(id), UnityWebRequest.kHttpVerbGET);
		}

		/// <summary>Returns the contents of the specified file</summary>
		/// <param name="id">The ID of the file to use for this request</param>
		/// <returns>See <see cref="OpenAIFile"/></returns>
		public static async Task<OpenAIFile> DownloadFile(string id)
		{
			return await DispatchRequest<OpenAIFileResponse>
				(currentConfig.Endpoints.DownloadFile(id), UnityWebRequest.kHttpVerbGET);
		}

		/// <summary>
		/// Manage fine-tuning jobs to tailor a model to your specific training data.
		/// Related guide: <a href="https://beta.openai.com/docs/guides/fine-tuning">Fine-tune models</a>
		/// </summary>
		/// <param name="request">See <see cref="CreateFineTuneRequest"/></param>
		/// <returns>See <see cref="FineTune"/></returns>
		public static async Task<FineTune> CreateFineTune(CreateFineTuneRequest request)
		{
			byte[] payload = CreatePayload(request);
			return await DispatchRequest<FineTuneResponse>
				(currentConfig.Endpoints.CreateFineTune, UnityWebRequest.kHttpVerbPOST, payload);
		}

		/// <summary>List your organization's fine-tuning jobs</summary>
		/// <returns>See <see cref="ListFineTunesResponse"/></returns>
		public static async Task<ListFineTunesResponse> ListFineTunes()
		{
			return await DispatchRequest<ListFineTunesResponse>
				(currentConfig.Endpoints.ListFineTunes, UnityWebRequest.kHttpVerbGET);
		}

		/// <summary>Gets info about the fine-tune job.</summary>
		/// <param name="id">The ID of the fine-tune job</param>
		/// <returns>See <see cref="FineTune"/></returns>
		public static async Task<FineTune> RetrieveFineTune(string id)
		{
			return await DispatchRequest<FineTuneResponse>
				(currentConfig.Endpoints.RetrieveFineTune(id), UnityWebRequest.kHttpVerbGET);
		}

		/// <summary>Immediately cancel a fine-tune job.</summary>
		/// <param name="id">The ID of the fine-tune job to cancel</param>
		/// <returns>See <see cref="FineTune"/></returns>
		public static async Task<FineTune> CancelFineTune(string id)
		{
			return await DispatchRequest<FineTuneResponse>
				(currentConfig.Endpoints.CancelFineTune(id), UnityWebRequest.kHttpVerbPOST);
		}

		/// <summary>Get fine-grained status updates for a fine-tune job.</summary>
		/// <param name="id">The ID of the fine-tune job to get events for.</param>
		/// <param name="stream">Whether to stream events for the fine-tune job.
		/// If set to true, events will be sent as data-only <a href="https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events/Using_server-sent_events#event_stream_format">server-sent events</a> as they become available.
		/// The stream will terminate with a data: [DONE] message when the job is finished (succeeded, cancelled, or failed).
		/// If set to false, only events generated so far will be returned.</param>
		/// <returns>See <see cref="ListFineTuneEventsResponse"/></returns>
		public static async Task<ListFineTuneEventsResponse> ListFineTuneEvents(string id, bool stream = false)
		{
			return await DispatchRequest<ListFineTuneEventsResponse>
				(currentConfig.Endpoints.ListFineTuneEvents(id, stream), UnityWebRequest.kHttpVerbGET);
		}

		/// <summary>Delete a fine-tuned model. You must have the Owner role in your organization. </summary>
		/// <param name="model">The model to delete</param>
		/// <returns>See <see cref="DeleteResponse"/></returns>
		public static async Task<DeleteResponse> DeleteFineTunedModel(string model)
		{
			return await DispatchRequest<DeleteResponse>
				(currentConfig.Endpoints.DeleteFineTunedModel(model), UnityWebRequest.kHttpVerbDELETE);
		}

		/// <summary>Classifies if text violates OpenAI's Content Policy</summary>
		/// <param name="request">See <see cref="CreateModerationRequest"/></param>
		/// <returns>See <see cref="CreateModerationResponse"/></returns>
		public static async Task<CreateModerationResponse> CreateModeration(CreateModerationRequest request)
		{
			byte[] payload = CreatePayload(request);
			return await DispatchRequest<CreateModerationResponse>
				(currentConfig.Endpoints.CreateModeration, UnityWebRequest.kHttpVerbPOST, payload);
		}
	}
}