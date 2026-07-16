using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using AzureTranscription.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzureTranscription.Api.Services
{
    public class TranscriptionService : ITranscriptionService
    {
        private readonly AzureSpeechServicesOptions _speechOptions;
        private readonly string? _blobConnectionString;
        private readonly string? _blobContainerName;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public TranscriptionService(
            IOptions<AzureSpeechServicesOptions> speechOptions,
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _speechOptions = speechOptions.Value;
            _configuration = configuration;
            _blobConnectionString = configuration["BlobStorage:ConnectionString"];
            _blobContainerName = configuration["BlobStorage:ContainerName"];
            _httpClient = httpClient;
        }

        public async Task<string> UploadAudioToBlobAsync(string localFilePath)
        {
            var blobServiceClient = new BlobServiceClient(_blobConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_blobContainerName);
            await containerClient.CreateIfNotExistsAsync();

            string blobName = $"{Guid.NewGuid()}_{Path.GetFileName(localFilePath)}";
            var blobClient = containerClient.GetBlobClient(blobName);

            using (var stream = File.OpenRead(localFilePath))
            {
                await blobClient.UploadAsync(stream, true);
            }

            // 7 օր vaidity, ոչ թե 2 ժամ, որպեսզի audio-ն continue to play after
            // transcription-ը ավարտվի և history-ում պահվի
            var sasUri = blobClient.GenerateSasUri(
                BlobSasPermissions.Read,
                DateTimeOffset.UtcNow.AddDays(7));

            return sasUri.ToString();
        }

        // modelSelfUrl: the full "self" URL of the model to use (Whisper base
        // model OR your Custom Speech model — caller decides which).
        // displayName: shown in Azure's own transcription job listing, useful
        // for telling jobs apart when debugging in Speech Studio.
        public async Task<string> StartBatchTranscriptionAsync(string audioUrl, string modelSelfUrl, string displayName)
        {
            var url = $"{_speechOptions.Endpoint}/speechtotext/v3.2/transcriptions";

            var requestBody = new
            {
                contentUrls = new[] { audioUrl },
                locale = "hy-AM",
                displayName = displayName,
                model = new
                {
                    self = modelSelfUrl
                },
                properties = new
                {
                    diarizationEnabled = true,
                    diarization = new
                    {
                        speakers = new
                        {
                            minCount = 1,
                            maxCount = 2
                        }
                    },
                    timeToLiveHours = 48
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _speechOptions.SubscriptionKey);
            request.Content = JsonContent.Create(requestBody);

            try
            {
                var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new ApplicationException($"Azure error {response.StatusCode}: {body}");
                }

                return body;
            }
            catch (TaskCanceledException)
            {
                throw new ApplicationException("Azure Speech Service-ը timeout տվեց (չպատասխանեց սահմանված ժամանակում)։");
            }
        }

        public async Task<(string? resultJson, string status, string? audioUrl)> GetCompletedTranscriptionJsonAsync(string transcriptionSelfUrl)
        {
            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, transcriptionSelfUrl);
            statusRequest.Headers.Add("Ocp-Apim-Subscription-Key", _speechOptions.SubscriptionKey);

            var statusResponse = await _httpClient.SendAsync(statusRequest);
            var statusBody = await statusResponse.Content.ReadAsStringAsync();

            if (!statusResponse.IsSuccessStatusCode)
            {
                throw new ApplicationException($"Azure error {statusResponse.StatusCode}: {statusBody}");
            }

            using var statusDoc = JsonDocument.Parse(statusBody);
            string status = statusDoc.RootElement.TryGetProperty("status", out var statusEl)
                ? statusEl.GetString() ?? ""
                : "";

            if (status == "Failed")
            {
                string failureReason = "Unknown";
                if (statusDoc.RootElement.TryGetProperty("properties", out var propsEl) &&
                    propsEl.TryGetProperty("error", out var errorEl) &&
                    errorEl.TryGetProperty("message", out var msgEl))
                {
                    failureReason = msgEl.GetString() ?? "Unknown";
                }
                throw new ApplicationException($"Azure transcription-ը ձախողվել է. {failureReason}");
            }

            if (status != "Succeeded")
            {
                return (null, status, null);
            }

            string filesUrl = statusDoc.RootElement.GetProperty("links").GetProperty("files").GetString()!;

            using var filesRequest = new HttpRequestMessage(HttpMethod.Get, filesUrl);
            filesRequest.Headers.Add("Ocp-Apim-Subscription-Key", _speechOptions.SubscriptionKey);

            var filesResponse = await _httpClient.SendAsync(filesRequest);
            var filesBody = await filesResponse.Content.ReadAsStringAsync();

            if (!filesResponse.IsSuccessStatusCode)
            {
                throw new ApplicationException($"Azure error {filesResponse.StatusCode}: {filesBody}");
            }

            using var filesDoc = JsonDocument.Parse(filesBody);

            string? contentUrl = null;
            foreach (var file in filesDoc.RootElement.GetProperty("values").EnumerateArray())
            {
                if (file.TryGetProperty("kind", out var kindEl) && kindEl.GetString() == "Transcription")
                {
                    contentUrl = file.GetProperty("links").GetProperty("contentUrl").GetString();
                    break;
                }
            }

            if (contentUrl == null)
            {
                throw new ApplicationException("Transcription result ֆայլը չգտնվեց Azure-ի ֆայլերի ցուցակում։");
            }

            var contentResponse = await _httpClient.GetAsync(contentUrl);
            var contentBody = await contentResponse.Content.ReadAsStringAsync();

            if (!contentResponse.IsSuccessStatusCode)
            {
                throw new ApplicationException($"Azure error downloading result {contentResponse.StatusCode}: {contentBody}");
            }

            string? audioUrl = null;
            try
            {
                using var resultDoc = JsonDocument.Parse(contentBody);
                if (resultDoc.RootElement.TryGetProperty("source", out var sourceEl))
                {
                    audioUrl = sourceEl.GetString();
                }
            }
            catch (JsonException)
            {
            }

            return (contentBody, status, audioUrl);
        }
    }
}