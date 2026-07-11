using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AzureTranscription.Api.Services;
using AzureTranscription.Api.DTOs;
using AzureTranscription.Api.Models;
using Microsoft.Extensions.Logging;

namespace AzureTranscription.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranscriptionController : ControllerBase
    {
        private readonly IFileValidationService _fileValidationService;
        private readonly ITranscriptionService _transcriptionService;
        private readonly IMongoService _mongoService;
        private readonly ILogger<TranscriptionController> _logger;

        public TranscriptionController(
            IFileValidationService fileValidationService,
            ITranscriptionService transcriptionService,
            IMongoService mongoService,
            ILogger<TranscriptionController> logger)
        {
            _fileValidationService = fileValidationService;
            _transcriptionService = transcriptionService;
            _mongoService = mongoService;
            _logger = logger;
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

                    string? azureJobUrl = null;
                    using (var doc = System.Text.Json.JsonDocument.Parse(azureResponse))
                    {
                        if (doc.RootElement.TryGetProperty("self", out var selfEl))
                        {
                            azureJobUrl = selfEl.GetString();
                        }
                    }

                    string recordId = "";
                    if (!string.IsNullOrEmpty(azureJobUrl))
                    {
                        recordId = await _mongoService.CreateProcessingRecordAsync(fileName, azureJobUrl);
                    }

                    return Accepted(new
                    {
                        id = recordId,
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

        [HttpGet("history")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetHistory()
        {
            try
            {
                var history = await _mongoService.GetAllHistoryAsync();
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transcription history: {Message}", ex.Message);
                return StatusCode(500, new
                {
                    error = "Չհաջողվեց ստանալ պատմությունը MongoDB-ից։",
                    details = ex.Message,
                    status = 500
                });
            }
        }
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTranscriptionById(string id)
        {
            var record = await _mongoService.GetByIdAsync(id);
            if (record == null)
            {
                return NotFound(new { error = "Transcript not found.", status = 404 });
            }
            return Ok(record);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteTranscription(string id)
        {
            try
            {
                var deleted = await _mongoService.DeleteByIdAsync(id);
                if (!deleted)
                {
                    return NotFound(new { error = "Transcript not found.", status = 404 });
                }
                return Ok(new { message = "Transcript deleted.", id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transcription {Id}: {Message}", id, ex.Message);
                return StatusCode(500, new
                {
                    error = "Failed to delete transcript from the database.",
                    details = ex.Message,
                    status = 500
                });
            }
        }

        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ClearHistory()
        {
            try
            {
                var deletedCount = await _mongoService.DeleteAllAsync();
                return Ok(new { message = "History cleared.", deletedCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing transcription history: {Message}", ex.Message);
                return StatusCode(500, new
                {
                    error = "Failed to clear history from the database.",
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
                _logger.LogInformation($"Azure validation challenge received. Token: {token}");
                return Content(token, "text/plain");
            }

            try
            {
                using var reader = new StreamReader(Request.Body);
                string rawJson = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(rawJson))
                {
                    return BadRequest("Empty request from Azure");
                }

                _logger.LogInformation("Azure Webhook called. Raw JSON: {RawJson}", rawJson);

                using var notificationDoc = System.Text.Json.JsonDocument.Parse(rawJson);

                if (!notificationDoc.RootElement.TryGetProperty("self", out var selfEl))
                {
                    return BadRequest("Azure notification-ում 'self' դաշտը չկա։");
                }

                string transcriptionSelfUrl = selfEl.GetString() ?? "";

                var (resultJson, transcriptionStatus, audioUrl) = await _transcriptionService.GetCompletedTranscriptionJsonAsync(transcriptionSelfUrl);

                _logger.LogInformation("Result JSON: {ResultJson}", resultJson);

                if (resultJson == null)
                {
                    Console.WriteLine($"Transcription դեռ Succeeded վիճակում չէ (ընթացիկ status. {transcriptionStatus})։");
                    _logger.LogInformation("Transcription is not yet Succeeded. Current status: {Status}", transcriptionStatus);
                    return Ok(new
                    {
                        message = "Notification-ը ստացվեց, բայց transcription-ը դեռ Succeeded չէ։",
                        status = transcriptionStatus
                    });
                }

                var parser = new AzureTranscriptionParser();
                TranscriptionResultDto parsedResult = parser.Parse(resultJson);

                string transcriptText = parsedResult.Utterances != null
                    ? string.Join(" ", parsedResult.Utterances.Select(u => u.Text))
                    : "No text found";

                string finalStatus = string.IsNullOrEmpty(parsedResult.Status) ? "Succeeded" : parsedResult.Status;
                await _mongoService.UpdateResultByAzureJobUrlAsync(transcriptionSelfUrl, transcriptText, finalStatus);
                _logger.LogInformation("Transcription record updated in MongoDB (id linked via AzureJobUrl)!");

                Console.WriteLine($"Transcription Status: {parsedResult.Status}");
                _logger.LogInformation("Transcription Status: {Status}", parsedResult.Status);

                if (parsedResult.Utterances != null)
                {
                    Console.WriteLine($"Successfully parsed {parsedResult.Utterances.Count} utterances.");
                    _logger.LogInformation("Successfully parsed {Count} utterances.", parsedResult.Utterances.Count);

                    foreach (var utterance in parsedResult.Utterances)
                    {
                        Console.WriteLine($"[{utterance.Speaker}]: {utterance.Text}");
                        _logger.LogInformation("[{Speaker}]: {Text}", utterance.Speaker, utterance.Text);
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
                _logger.LogError(ex, "Parser Error in Webhook: {Message}", ex.Message);
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