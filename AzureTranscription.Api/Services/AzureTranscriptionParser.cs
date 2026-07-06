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

            if (root.TryGetProperty("status", out JsonElement status))
            {
                result.Status = status.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("self", out JsonElement self))
            {
                result.JobId = self.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("recognizedPhrases", out JsonElement phrases))
            {
                foreach (JsonElement phrase in phrases.EnumerateArray())
                {
                    var utterance = new TranscriptionUtteranceDto();

                    if (phrase.TryGetProperty("speaker", out JsonElement speaker))
                    {
                        utterance.Speaker = $"Speaker {speaker.GetInt32()}";
                    }

                    // ՈՒՇԱԴՐՈՒԹՅՈՒՆ. Azure-ը այս թվերը երբեմն վերադարձնում է
                    // տասնորդական կետով (օրինակ՝ 3900000.0), ուստի GetInt64()-ի փոխարեն
                    // կարդում ենք որպես double և հետո վերածում long-ի
                    if (phrase.TryGetProperty("offsetInTicks", out JsonElement offset))
                    {
                        long offsetTicks = (long)offset.GetDouble();
                        utterance.Offset = TimeSpan.FromTicks(offsetTicks);
                    }

                    if (phrase.TryGetProperty("durationInTicks", out JsonElement duration))
                    {
                        long durationTicks = (long)duration.GetDouble();
                        utterance.Duration = TimeSpan.FromTicks(durationTicks);
                    }

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