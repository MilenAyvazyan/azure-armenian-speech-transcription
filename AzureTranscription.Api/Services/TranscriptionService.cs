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
using System.Threading.Tasks;

namespace AzureTranscription.Api.Services
{
    public class TranscriptionService : ITranscriptionService
    {
        private readonly AzureSpeechServicesOptions _speechOptions;
        private readonly string? _blobConnectionString;
        private readonly string? _blobContainerName;
        private readonly HttpClient _httpClient;

        public TranscriptionService(
            IOptions<AzureSpeechServicesOptions> speechOptions,
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _speechOptions = speechOptions.Value;
            _blobConnectionString = configuration["AzureBlobStorage:ConnectionString"];
            _blobContainerName = configuration["AzureBlobStorage:ContainerName"];
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

            var sasUri = blobClient.GenerateSasUri(
                BlobSasPermissions.Read,
                DateTimeOffset.UtcNow.AddHours(2));

            return sasUri.ToString();
        }

        public async Task<string> StartBatchTranscriptionAsync(string audioUrl)
        {
            var url = $"{_speechOptions.Endpoint}/speechtotext/v3.2/transcriptions";

            var requestBody = new
            {
                contentUrls = new[] { audioUrl },
                locale = "hy-AM",
                displayName = "Armenian Transcription",
                properties = new
                {
                    diarizationEnabled = true,
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
                throw new ApplicationException("Azure Speech Service-ը timeout տվեց։");
            }
        }
    }
}