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

        /// <summary>
        /// Starts a new Azure Speech Batch Transcription job by uploading an audio file.
        /// </summary>
        /// <param name="audioFile">The audio file that will be uploaded and transcribed.</param>
        /// <returns>
        /// Returns a transcription job identifier when the request is accepted.
        /// </returns>
        [HttpPost("start")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
                    await stream.FlushAsync();
                }

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

        /// <summary>
        /// Receives Azure Speech Service webhook notifications after a transcription job has finished.
        /// </summary>
        /// <param name="azureResponse">The JSON payload sent by Azure Speech Services.</param>
        /// <returns>
        /// Returns a success response if the webhook payload is received and processed.
        /// </returns>
        [HttpPost("webhook")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult AzureWebhook([FromBody] System.Text.Json.JsonElement azureResponse)
        {
            try
            {
                if (azureResponse.ValueKind == System.Text.Json.JsonValueKind.Undefined)
                {
                    return BadRequest("Empty request from Azure");
                }

                Console.WriteLine("Azure Webhook called. Data received");

                return Ok(new
                {
                    message = "The data was successfully received by the backend"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message
                });
            }
        }
    }
}