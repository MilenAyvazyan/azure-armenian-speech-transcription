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

        /// <summary>
        /// Starts a new Azure Speech Batch Transcription job by uploading an audio file.
        /// </summary>
        /// <param name="audioFile">The audio file that will be uploaded and transcribed.</param>
        /// <returns>
        /// Returns the Blob URL and Azure's transcription job response when the request is accepted.
        /// </returns>
        [HttpPost("start")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> StartTranscription(IFormFile audioFile)
        {
            try
            {
                // Step 1: Validation (Lana's part)
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

                // Step 4: Upload to Azure Blob Storage and start the batch transcription job (Sona's part)
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

                // 1. Ստանում ենք մաքուր JSON տեքստը Azure-ից
                string rawJson = azureResponse.GetRawText();
                Console.WriteLine("Azure Webhook called. Raw JSON received.");

                // 2. Կանչում ենք Մարտինի գրած Parser-ը
                var parser = new AzureTranscriptionParser();
                TranscriptionResultDto parsedResult = parser.Parse(rawJson);

                // 3. Տերմինալում տպում ենք (Debug-ի համար), որ տեսնենք՝ ճիշտ է parse եղել
                Console.WriteLine($"Transcription Status: {parsedResult.Status}");
                if (parsedResult.Utterances != null)
                {
                    Console.WriteLine($"Successfully parsed {parsedResult.Utterances.Count} utterances.");

                    foreach (var utterance in parsedResult.Utterances)
                    {
                        Console.WriteLine($"[{utterance.Speaker}]: {utterance.Text}");
                    }
                }

                // TODO: Այստեղ ապագայում parsedResult-ը կփոխանցես ձեր բազային կամ հաջորդ սերվիսին

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