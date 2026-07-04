using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AzureTranscription.Api.Services;

namespace AzureTranscription.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranscriptionController : ControllerBase
    {
        private readonly IFileValidationService _fileValidationService;

        public TranscriptionController(IFileValidationService fileValidationService)
        {
            _fileValidationService = fileValidationService;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartTranscription(IFormFile audioFile)
        {
            try
            {
                // Step 1: Validation
                var (isValid, errorMessage) = _fileValidationService.ValidateAudioFile(audioFile);
                if (!isValid)
                {
                    return BadRequest(new { error = errorMessage, status = 400 });
                }

                // Step 2: Build Safe Temp Directory
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(audioFile.FileName)}";
                var uploadPath = Path.Combine(Path.GetTempPath(), "AzureTranscriptionUploads");

                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                var filePath = Path.Combine(uploadPath, fileName);

                // Step 3: Write file safely and forcefully close the streams right away
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await audioFile.CopyToAsync(stream);
                    await stream.FlushAsync(); // Force Windows to finish writing right now
                } // The stream is fully closed and disposed here

                var fakeTranscriptionId = Guid.NewGuid().ToString();

                return Accepted(new
                {
                    transcriptionId = fakeTranscriptionId,
                    fileName = fileName,
                    savedToTempDirectory = filePath,
                    status = "Processing",
                    message = "The file has been verified, safely written to temp storage, and transcription has begun"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "A filesystem error occurred on the server.",
                    details = ex.Message,
                    status = 500
                });
            }
        }

        [HttpPost("webhook")]
        public IActionResult AzureWebhook([FromBody] System.Text.Json.JsonElement azureResponse)
        {
            try
            {
                if (azureResponse.ValueKind == System.Text.Json.JsonValueKind.Undefined)
                {
                    return BadRequest("Empty request from Azure");
                }

                Console.WriteLine("Azure Webhook called. Data received");
                return Ok(new { message = "The data was successfully received by the backend" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}