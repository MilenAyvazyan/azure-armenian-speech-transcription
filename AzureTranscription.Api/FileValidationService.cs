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
        private static readonly string[] AllowedExtensions = {".wav" };


        private const long MaxFileSizeBytes = 50 * 1024 * 1024;

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
                return (false, "File size exceeds the maximum limit allowed (50 MB).");
            }

            // 3. Extension / Format Validation
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(fileExtension))
            {
                return (false, $"Unsupported file type '{fileExtension}'. Only .wav format is allowed.");
            }

            return (true, string.Empty);
        }
    }
}