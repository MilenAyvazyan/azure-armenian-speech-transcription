using System.Threading.Tasks;

namespace AzureTranscription.Api.Services
{
    public interface ITranscriptionService
    {
        Task<string> UploadAudioToBlobAsync(string localFilePath);
        Task<string> StartBatchTranscriptionAsync(string audioUrl);
    }
}