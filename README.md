# Research: Azure AI Model for Armenian Transcription

Research task for the **Smart Auto-service** project — evaluating Azure AI Speech Services for Armenian audio transcription, including support for Yerevan and Gyumri (Shirak) dialects.

---

## 1. Project Main Goal & Objectives

The primary goal of this project is to research, evaluate, and implement a robust Speech-to-Text solution using **Azure AI Speech Services** to transcribe Armenian audio recordings. The project focuses on three core pillars:

- **High-Quality Armenian Transcription** — Exploring Azure AI's capacity to recognize and accurately transcribe the Armenian language (`hy-AM`), including non-standard speaking styles, fast speech, and heavy background noise.
- **Dialect & Slang Adaptability** — Ensuring the system correctly processes regional phonetic variations, with a strict focus on effectively transcribing both Yerevan slang and the Gyumri (Shirak) dialect, preventing contextual distortion.
- **Secure and Scalable Backend Architecture** — Designing a lightweight, asynchronous (Batch) data flow using the Azure Speech REST API v3.2 and the OpenAI Whisper Large V2 model. This guarantees stable multi-user concurrency, supports word-level timestamps, and fully complies with ISO/IEC 27001 principles.

---

## 2. Exploring Azure AI Speech Service for Audio Transcription

Azure AI Speech is a cloud-based service by Microsoft that processes audio files and converts them into text. It was selected as the primary tool because it offers high accuracy and native support for Armenian (`hy-AM`).

**Speech Studio** was used as a working environment to test various models, verify diarization accuracy (identifying and separating individual speakers), and examine the structure of returned data.

### ISO/IEC 27001 Principles

ISO/IEC 27001 is a globally recognized standard for information security management. Azure holds this certification, confirming rigorous security audits and reliability. In practice, this means every audio file processed remains our exclusive property — Azure cannot use the data to train its models or improve services without explicit authorization, ensuring full data privacy and compliance.

---

## 3. How Azure AI Handles Armenian Audio

Azure AI recognizes Armenian as a distinct language model, trained to understand the Armenian alphabet, orthography, and core vocabulary. Using large-scale neural networks, it automatically inserts punctuation, keeping transcripts readable and well-structured.

The model performs exceptionally well with **literary Armenian**, but may face challenges with dialectal expressions or foreign-language terms (e.g., Russian loanwords) — an important factor for future model refinement.

---

## 4. Yerevan vs. Gyumri Dialects — Impact on Transcription Quality

Armenian dialects may decrease transcription accuracy since the Azure language model is primarily based on standard literary Armenian.

- **Contextual Importance** — Whether the system correctly interprets dialectal words from sentence context, or misreads them as similar-sounding literary words.
- **System Stability Objective** — Determines whether Custom Speech model training is required to increase accuracy.
- **Custom Training Analysis** — Custom training could improve dialect understanding, but it is costly and requires large datasets, making it impractical for this project.

---

## 5. Testing Sample Audio (Yerevan & Gyumri Speakers)

An in-depth technical and qualitative comparison of Azure AI Speech models was conducted using audio samples spanning both dialects — movie dialogue (Gyumri dialect, background noise), rapid conversational segments (Yerevan slang, background music), and clear literary speech.

### 5.1. Model Comparison

| Metric / Parameter | **20240228 Whisper Large V2 (Batch)** | **20220525 Azure Base Model** |
|---|---|---|
| **Gyumri Dialect Recognition** | High — accurately captures phonetic shifts (e.g., "ինչխթե", "կվիջ է") | Low — automatically replaces dialectal words with literary ones, distorting meaning |
| **Yerevan Slang & Conversational Style** | Good — retains authentic conversational structure (e.g., "պրոպկայով") | Medium — struggles with slang and non-standard vocabulary |
| **Literary Armenian** | Satisfactory — may include minor typos | Ideal — exceptional accuracy and grammatically perfect punctuation |
| **Noise Robustness** | High — filters background music, static, traffic noise | Low — error rates spike significantly with background noise |
| **Speaker Diarization** | Supported (Batch) — precise speaker count definition | Supported — less stable for dynamic diarization in live calls |

### 5.2. Alternative Approach Considered — Rejected

A **"Regional Hybrid Approach"** (Yerevan → Azure Base, Gyumri → Whisper) was evaluated and rejected:

- Caller metadata (e.g., phone number prefix) is not a reliable indicator of a speaker's actual dialect.
- The Azure Base model fails to handle Yerevan slang and rapid speech effectively, making it unsuitable as a primary engine regardless of routing.

### 5.3. Final Technical Conclusion

Selected: **20240228 Whisper Large V2** model with an **Asynchronous (Batch Transcription)** flow — the only solution ensuring maximum noise stability, accurate handling of both dialects, and robust Speaker Diarization with word-level timestamps.

---

## 6. Required Azure AI Endpoints

Using the **Azure Speech REST API v3.2**:

### 6.1. Create Transcription Job
`POST` — Sends the audio file URL (e.g., Azure Blob Storage) plus parameters (`Language: hy-AM`, `Model: Whisper Large V2`). Returns a unique **Transcription ID**.

### 6.2. Get Transcription Status
`GET` — Polls the job status using the Transcription ID. Returns one of: `NotStarted`, `Running`, `Succeeded`, `Failed`.

### 6.3. Get Transcription Files / Results
`GET` — Once `Succeeded`, retrieves the link to the final JSON file containing the transcript, speaker diarization, and word-level timestamps.

---

## 7. Required Request Data

The Batch API requires a URL to the audio file rather than raw binary data.

**Key parameters:**
- `contentUrls` — Cloud URL of the audio file (e.g., Azure Blob Storage with active SAS Token)
- `locale` — `hy-AM` (Armenian, Armenia)
- `model` — ID for the Whisper Large V2 model
- `properties.diarizationEnabled` — `true`, to separate speakers
- `properties.wordLevelTimestampsEnabled` — `true`, for word-level timing

**Example payload:**

```json
{
  "contentUrls": [
    "https://mystorageaccount.blob.core.windows.net/audio/sample_mono.wav"
  ],
  "locale": "hy-AM",
  "displayName": "Armenian_Call_Transcription_Job",
  "model": {
    "self": "https://westeurope.api.cognitive.microsoft.com/speechtotext/v3.2/models/base/20240228_Whisper_Large_V2"
  },
  "properties": {
    "diarizationEnabled": true,
    "wordLevelTimestampsEnabled": true
  }
}
```

---

## 8. Response Format

Azure returns results in structured JSON:

- `text` — the complete recognized transcript
- `duration` — total audio length (ISO 8601 format)
- `confidence` — certainty score of the transcription
- `speaker` — speaker identifier (when diarization is enabled)

**Example (dialogue "Mer Mankutyan Tangon", model 20240228 Whisper Large V2):**

- `duration`: `PT32S`
- `confidence`: `0.67696434`
- `speaker`: `2`

---

## 9. Synchronous vs. Asynchronous Flow

### 9.1. Synchronous (Real-time) — Rejected
- The Real-time API does not support the Whisper model; the available Base model gives insufficient quality for Armenian dialects.
- Requires an open connection per user — high concurrency risks server overload/crashes.
- Susceptible to network timeout errors even with short (40s) files.

### 9.2. Asynchronous (Batch) — Selected
- Only method that enables use of Whisper Large V2 in Azure.
- Jobs are queued and processed in the background, keeping the system stable under peak load.
- Scales easily to longer audio files in the future without compromising stability.

### 9.3. Final Selected Flow
**Asynchronous (Batch Transcription)**, powered by Azure Speech REST API v3.2 + OpenAI Whisper Large V2.

---

## 10. Transcription Volume & Cost Estimation

### 10.1. Pricing Metric
Priced by processed audio minutes/hours (not tokens):
- **Standard S0 Batch Transcription**: $0.36/hour ($0.006/minute)

### 10.2. Testing Volume Estimation
- Baseline test files: 172 seconds (~2.8 minutes) total.
- Allocated buffer: **2 hours (120 minutes)** for initial backend integration/testing, accounting for repeated API calls during development and error-handling tests (corrupted/empty/various-size files).

### 10.3. Future Scalability Risk
- Phase 1 (40s file limit): 2 hours is sufficient for ~150–200 test requests.
- Future phase (2–3 min call-center recordings): projected volume rises to **10 hours (600 minutes)**.

### 10.4. Cost Calculation

| Scenario | Volume | Cost |
|---|---|---|
| Baseline files | 2.8 min | ~$0.02 |
| Current phase testing | 2 hours | $0.72 |
| Future phase testing | 10 hours | $3.60 |

All costs are well within the $200 testing deposit.

---

## 11. Audio File Format, Size & Duration Limitations

### Test File Inventory

| File | Size | Duration | Format |
|---|---|---|---|
| mer_siro_tangon_1 | 2.06 MB | 00:00:24 | WAV |
| Hin_oreri_ergy_2 | 2.01 MB | 00:00:23 | WAV |
| mer_mankutyan_tangon_3 | 2.73 MB | 00:00:32 | WAV |
| green_card_4 | 3.44 MB | 00:00:40 | WAV |
| Karo_5 | 1.37 MB | 00:00:16 | WAV |
| EMOJI_SKETCH_SHOW_6 | 3.16 MB | 00:00:37 | WAV |

### 11.1. Format: WAV vs. MP3
- **.wav** — preferred; lossless, improves accuracy for dialects/rapid speech.
- **.mp3** — supported; smaller size but lossy compression can slightly reduce precision.

### 11.2. Channels: Mono vs. Stereo
- **Mono is required** — Batch Transcription with Whisper returns an error on stereo files.
- Diarization still works reliably in mono via voice frequency/timbre analysis.

### 11.3. Macro Limitations
- **Duration**: max 40 seconds per file (current testing phase)
- **File size**: recommended under 20 MB for REST API async processing (well within limits for 40s WAV files)

---

## 12. Integration Complexity Comparison

### 12.1. Azure Speech SDK (Python)
Event-driven, optimized for real-time/streaming audio. Introduces unnecessary overhead and rigid Microsoft-specific dependencies for file-based batch processing.

### 12.2. Azure Speech REST API v3.2
Requires manual HTTP payload construction and JSON parsing, but gives full control over async batch queues using standard libraries (`requests`/`httpx`) — lightweight, no unnecessary intermediaries.

### Final Decision
**Azure Speech REST API v3.2** — most optimal, flexible, and scalable choice given the asynchronous Whisper-based architecture.

---

## 13. Final Selected Azure AI Approach

| Aspect | Decision |
|---|---|
| **Service & Model** | Azure AI Speech — OpenAI Whisper Large V2 (`20240228`) |
| **Data Flow** | Asynchronous (Batch Transcription) |
| **Integration Method** | Azure Speech REST API v3.2 |
| **Key Functionalities** | Speaker Diarization + Word-level Timestamps |

This model was chosen for its superior ability to recognize Armenian conversational styles, slang, and the Yerevan/Gyumri regional dialects specifically.

---

## 14. Research Summary

Within the scope of the **Smart Auto-service** project, Azure AI Speech was researched to identify the most effective solution for automated Armenian speech-to-text transcription.

**Key Findings:**

- **Model Selection** — OpenAI Whisper Large V2 outperforms the standard Azure Base model on literary Armenian, Yerevan slang, and the Gyumri dialect, while offering superior noise robustness.
- **Architectural Solution** — Asynchronous (Batch Transcription) via Azure Speech REST API v3.2 prevents server overload and ensures stability under peak traffic.
- **Technical Requirements** — Input files must be WAV format, Mono channel; ≤40 seconds duration; the solution complies with ISO/IEC 27001 information security standards.
- **Cost Efficiency** — Projected testing costs are minimal (well under the $200 deposit).
- **Final Recommendation** — Asynchronous architecture powered by Whisper Large V2, enabling precise Speaker Diarization and Word-level Timestamps as the foundation for a reliable automated voice service.

---

## Endpoints Overview

| Endpoint | Method | Purpose |
|---|---|---|
| Create Transcription Job | `POST` | Submit audio URL + parameters, receive Transcription ID |
| Get Transcription Status | `GET` | Poll job status (`NotStarted` / `Running` / `Succeeded` / `Failed`) |
| Get Transcription Files/Results | `GET` | Retrieve final JSON transcript once job succeeds |

---

## Repository Links

- **GitHub:** (https://github.com/MilenAyvazyan/azure-armenian-speech-transcription)
- **Trello board:** (https://trello.com/b/v3QajW4R/actual-solutions-speech-to-text-project)
- **The full report (PDF) is available here:** (https://github.com/MilenAyvazyan/azure-armenian-speech-transcription/blob/main/docs/Research_Report.pdf)
