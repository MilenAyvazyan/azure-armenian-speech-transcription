using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;

namespace AzureTranscription.Api.Services
{
    public interface IFileValidationService
    {
        (bool IsValid, string ErrorMessage) ValidateAudioFile(IFormFile file);
    }

    public class FileValidationService : IFileValidationService
    {
        // Allowed formats specified in your tasks
        private static readonly string[] AllowedExtensions = { ".mp3", ".wav" };

        // Strict duration constraints from your task description: Max 40 seconds.
        // Approximate max sizes to prevent massive file security risks:
        // .mp3 (~128kbps) for 40s is < 1MB. .wav (uncompressed) for 40s is < 8MB.
        // Let's set a conservative safety cap at 10 Megabytes.
        private const long MaxFileSizeBytes = 10 * 1024 * 1024;

        public (bool IsValid, string ErrorMessage) ValidateAudioFile(IFormFile file)
        {
            // 1. Check if empty or null (Failed Upload handling)
            if (file == null || file.Length == 0)
            {
                return (false, "The audio file was not provided, is corrupted, or is empty.");
            }

            // 2. Size Validation
            if (file.Length > MaxFileSizeBytes)
            {
                return (false, "File size exceeds the maximum limit allowed (10 MB).");
            }

            // 3. Extension / Format Validation
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(fileExtension))
            {
                return (false, $"Unsupported file type '{fileExtension}'. Only .mp3 and .wav formats are allowed.");
            }

            return (true, string.Empty);
        }
    }
}