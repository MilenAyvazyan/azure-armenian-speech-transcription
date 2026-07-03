using Microsoft.AspNetCore.Mvc;

namespace AzureTranscription.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranscriptionController : ControllerBase
    {
        [HttpPost("start")]
        public IActionResult StartTranscription(IFormFile audioFile)
        {
            // 1. Initial file check (this will be filled in later by Lana)
            if (audioFile == null || audioFile.Length == 0)
            {
                return BadRequest("The audio file was not found or was empty.");
            }

            var fakeTranscriptionId = Guid.NewGuid().ToString();

            return Accepted(new 
            { 
                transcriptionId = fakeTranscriptionId, 
                status = "Processing",
                message = "The file has been retrieved, transcription has begun"
            });
        }

        [HttpPost("webhook")]
        public IActionResult AzureWebhook([FromBody] System.Text.Json.JsonElement azureResponse)
        {
            if (azureResponse.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            {
                return BadRequest("Empty request from Azure");
            }

            Console.WriteLine("Azure Webhook called. Data received");
            Console.WriteLine(azureResponse.ToString());

            return Ok(new { message = "The data was successfully received by the backend" });
        }
    }
}
