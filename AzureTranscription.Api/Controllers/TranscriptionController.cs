using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AzureTranscription.Api.Services;
using AzureTranscription.Api.DTOs;

namespace AzureTranscription.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranscriptionController : ControllerBase
    {
        private readonly IFileValidationService _fileValidationService;
        private readonly ITranscriptionService _transcriptionService;

        public TranscriptionController(
            IFileValidationService fileValidationService,
            ITranscriptionService transcriptionService)
        {
            _fileValidationService = fileValidationService;
            _transcriptionService = transcriptionService;
        }

        [HttpPost("start")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> StartTranscription(IFormFile audioFile)
        {
            try
            {
                var (isValid, errorMessage) = _fileValidationService.ValidateAudioFile(audioFile);
                if (!isValid)
                {
                    return BadRequest(new { error = errorMessage, status = 400 });
                }

                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(audioFile.FileName)}";
                var uploadPath = Path.Combine(Path.GetTempPath(), "AzureTranscriptionUploads");

                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await audioFile.CopyToAsync(stream);
                    await stream.FlushAsync();
                }

                try
                {
                    var blobUrl = await _transcriptionService.UploadAudioToBlobAsync(filePath);
                    var azureResponse = await _transcriptionService.StartBatchTranscriptionAsync(blobUrl);

                    return Accepted(new
                    {
                        fileName = fileName,
                        blobUrl = blobUrl,
                        azureResponse = azureResponse,
                        status = "Processing",
                        message = "The file was uploaded to Azure Blob Storage and transcription has started."
                    });
                }
                catch (ApplicationException ex)
                {
                    return StatusCode(502, new
                    {
                        error = "Azure Speech Service-ի հետ կապի խնդիր։",
                        details = ex.Message,
                        status = 502
                    });
                }
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AzureWebhook()
        {
            // --- Validation/challenge հարցման մշակում ---
            if (Request.Query.ContainsKey("validationToken"))
            {
                string token = Request.Query["validationToken"].ToString();
                Console.WriteLine($"Azure validation challenge received. Token: {token}");
                return Content(token, "text/plain");
            }

            // --- Իրական completion notification-ի մշակում ---
            try
            {
                using var reader = new StreamReader(Request.Body);
                string rawJson = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(rawJson))
                {
                    return BadRequest("Empty request from Azure");
                }

                Console.WriteLine("Azure Webhook called. Raw JSON received.");
                Console.WriteLine(rawJson);

                using var notificationDoc = System.Text.Json.JsonDocument.Parse(rawJson);

                if (!notificationDoc.RootElement.TryGetProperty("self", out var selfEl))
                {
                    return BadRequest("Azure notification-ում 'self' դաշտը չկա։");
                }

                string transcriptionSelfUrl = selfEl.GetString() ?? "";

                // Ստանում ենք իրական transcription-ի արդյունքը (կամ null, եթե դեռ պատրաստ չէ)
                var (resultJson, transcriptionStatus) = await _transcriptionService.GetCompletedTranscriptionJsonAsync(transcriptionSelfUrl);

                Console.WriteLine("========== RESULT JSON START ==========");
                Console.WriteLine(resultJson);
                Console.WriteLine("========== RESULT JSON END ==========");

                if (resultJson == null)
                {
                    Console.WriteLine($"Transcription դեռ Succeeded վիճակում չէ (ընթացիկ status. {transcriptionStatus})։");
                    return Ok(new
                    {
                        message = "Notification-ը ստացվեց, բայց transcription-ը դեռ Succeeded չէ։",
                        status = transcriptionStatus
                    });
                }

                var parser = new AzureTranscriptionParser();
                TranscriptionResultDto parsedResult = parser.Parse(resultJson);

                Console.WriteLine($"Transcription Status: {parsedResult.Status}");
                if (parsedResult.Utterances != null)
                {
                    Console.WriteLine($"Successfully parsed {parsedResult.Utterances.Count} utterances.");

                    foreach (var utterance in parsedResult.Utterances)
                    {
                        Console.WriteLine($"[{utterance.Speaker}]: {utterance.Text}");
                    }
                }

                return Ok(new
                {
                    message = "The data was successfully received and parsed by the backend",
                    status = parsedResult.Status,
                    utterancesCount = parsedResult.Utterances?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parser Error in Webhook: {ex.Message}");
                return StatusCode(500, new
                {
                    error = "An error occurred while parsing the Azure transcription database payload.",
                    details = ex.Message,
                    status = 500
                });
            }
        }
    }
}