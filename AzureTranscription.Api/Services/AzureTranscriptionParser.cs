using System;
using System.Text.Json;
using AzureTranscription.Api.DTOs;

namespace AzureTranscription.Api.Services
{
    public class AzureTranscriptionParser
    {
        public TranscriptionResultDto Parse(string json)
        {
            var result = new TranscriptionResultDto();

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            // Read transcription status
            if (root.TryGetProperty("status", out JsonElement status))
            {
                result.Status = status.GetString() ?? string.Empty;
            }

            // Read job ID (if available)
            if (root.TryGetProperty("self", out JsonElement self))
            {
                result.JobId = self.GetString() ?? string.Empty;
            }

            // Read recognized phrases
            if (root.TryGetProperty("recognizedPhrases", out JsonElement phrases))
            {
                foreach (JsonElement phrase in phrases.EnumerateArray())
                {
                    var utterance = new TranscriptionUtteranceDto();

                    // Speaker
                    if (phrase.TryGetProperty("speaker", out JsonElement speaker))
                    {
                        utterance.Speaker = $"Speaker {speaker.GetInt32()}";
                    }

                    // Offset
                    if (phrase.TryGetProperty("offsetInTicks", out JsonElement offset))
                    {
                        utterance.Offset = TimeSpan.FromTicks(offset.GetInt64());
                    }

                    // Duration
                    if (phrase.TryGetProperty("durationInTicks", out JsonElement duration))
                    {
                        utterance.Duration = TimeSpan.FromTicks(duration.GetInt64());
                    }

                    // Transcribed text
                    if (phrase.TryGetProperty("nBest", out JsonElement nBest) &&
                        nBest.ValueKind == JsonValueKind.Array &&
                        nBest.GetArrayLength() > 0)
                    {
                        JsonElement best = nBest[0];

                        if (best.TryGetProperty("display", out JsonElement display))
                        {
                            utterance.Text = display.GetString() ?? string.Empty;
                        }
                    }

                    result.Utterances.Add(utterance);
                }
            }

            return result;
        }
    }
}