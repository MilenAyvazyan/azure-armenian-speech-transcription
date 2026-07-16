using System.Threading.Tasks;

namespace AzureTranscription.Api.Services
{
    public interface ITranscriptionService
    {
        Task<string> UploadAudioToBlobAsync(string localFilePath);

        Task<string> StartBatchTranscriptionAsync(string audioUrl, string modelSelfUrl, string displayName);

        Task<(string? resultJson, string status, string? audioUrl)> GetCompletedTranscriptionJsonAsync(string transcriptionSelfUrl);
    }
}
