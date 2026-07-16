using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AzureTranscription.Api.Services
{
    public interface ITranscriptionCorrectionService
    {
        string ApplyCorrections(string transcribedText);
    }

    /// <summary>
    /// Post-processing find-replace layer. Corrects known, recurring
    /// misrecognitions in Azure's transcription output based on observed
    /// wrong -> correct word/phrase pairs.
    ///
    /// NOTE: entries below were consolidated from three separate word-pair
    /// tables. One conflict was found during consolidation:
    ///   "մեզ" appeared mapped to two different corrections ("Մէ" and "Ես").
    ///   The first ("Մէ") was kept below — review and adjust if the other
    ///   mapping is the one you actually need, or handle both contextually.
    /// </summary>
    public class TranscriptionCorrectionService : ITranscriptionCorrectionService
    {
        // Ordered by descending key length so multi-word phrases are matched
        // before any shorter overlapping substrings.
        private static readonly List<KeyValuePair<string, string>> Corrections =
            new List<KeyValuePair<string, string>>
        {
            new("ուրելովեր թաս", "Ուր էլ որ երթաս"),
            new("սեղպտիկաս", "Ստեղ պտի գաս"),
            new("Լավի մատիս", "Լավ իմացիր"),
            new("տեսերհեկը", "Տես երեխեքդ"),
            new("լագոթորըդ", "Լագոթ հորդ"),
            new("Ինչս եմ", "Ինչ կսեմ"),
            new("Կեն էս", "Կենես"),
            new("Կլայի", "հլը"),
            new("Սրութեվին", "ձևին"),
            new("նայք", "Նայե"),
            new("բայքութը", "Վայ քու"),
            new("կամարդը", "Տղամարդ"),
            new("սողիտ", "Ըսողին"),
            new("նային", "Նայի"),
            new("դինչը", "Ինչխ"),
            new("խվորեց", "Վրեդ"),
            new("ամութից", "Ամոթից"),
            new("եթ", "Եդ"),
            new("նայի", "Նայե"),
            new("իրանուշը", "Սիրանուշը"),
            new("կեզի", "Քեզի"),
            new("դուն", "Տուն"),
            new("սողլողը", "թողնողը"),
            new("կգպնի", "գկպնի"),

            new("Մեկ ել, որ", "Մեգելոր"),
            new("չիշտ", "ճիշտ"),
            new("պտիխաղա", "Բդի խաղա"),
            new("չի դեմ", "չիդեմ"),
            new("մեջ ես", "մեջս"),
            new("կսել", "կսե"),
            new("այնչի", "ընչի"),
            new("պանում", "բանմ"),
            new("եկուց", "էգուց"),
            new("վրոնդ", "ֆռոնտ"),
            new("թերս", "դերս"),
            new("մտացել", "մդածել"),
            new("կարցնեմ", "կհարցնեմ"),

            new("Կրնախթ է", "Կռնա հաղթե"),
            new("Կրիվեն է", "Կռիվ ենե"),
            new("կհուսահատվիս", "Կհուսահադվիս"),
            new("ինչխթե", "Ինչղ թէ"),
            new("Սևասյանը", "Սովասթյանը"),
            new("թողնեմ", "Թողնե"),
            new("կուզեմ", "Գուզեմ"),
            new("Միզան սենք", "Մի զանսե"),
            new("Միզան սենովել", "Միզանսենով էլ"),
            new("ու ուզած այդ", "Ու ուզացդ"),
            new("Ու դա", "Կուդա"),
            new("Կվիչ է", "Կվիջե"),
            new("Հետ այս", "Հեդս"),
            new("վերտաս", "Կերթաս"),
            new("խաղաս", "Կխաղաս"),
            new("չայմ", "Չեմ"),
            new("կարնա", "Կռնա"),
            new("պիտի", "Բդի"),
            new("երտամ", "Էրթամ"),
            new("երտա", "Էրթա"),
            new("գործ", "Գորձ"),
            new("ունից", "Ունիս"),
            new("Հանձի", "ընձի"),
            new("լոստեղ", "ստեղ"),
            new("դղաջան", "տղա ջան"),
            new("դշնամուն", "թշնամուն"),
            new("ուբդի", "բդի"),
            new("հասկսար", "հասկցար"),
            new("բեզ", "ընբես"),

            new("ուրեմըն", "ուրեմն"),
            new("վատքիրասնի", "պատկերացնի"),
            new("իծաղալու", "ծիծաղալու"),
            new("մեկենայի", "մեքենայի"),  
        };

        public string ApplyCorrections(string transcribedText)
        {
            if (string.IsNullOrEmpty(transcribedText))
            {
                return transcribedText;
            }

            string result = transcribedText;

            foreach (var pair in Corrections)
            {
                string pattern = $@"\b{Regex.Escape(pair.Key)}\b";
                result = Regex.Replace(result, pattern, pair.Value);
            }

            return result;
        }
    }
}
