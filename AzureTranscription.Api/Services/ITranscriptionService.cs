using System.Threading.Tasks;

namespace AzureTranscription.Api.Services
{
    public interface ITranscriptionService
    {
        Task<string> UploadAudioToBlobAsync(string localFilePath);
        Task<string> StartBatchTranscriptionAsync(string audioUrl);

        /// <summary>
        /// Given the "self" URL of a transcription job (received from the webhook notification),
        /// checks whether it has succeeded, and if so, downloads and returns the actual
        /// transcription result JSON (containing recognizedPhrases).
        /// Returns null if the transcription is not yet in "Succeeded" state.
        /// </summary>
        Task<(string? resultJson, string status, string? audioUrl)> GetCompletedTranscriptionJsonAsync(string transcriptionSelfUrl);
    }
}