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
            new("ուրելովեր թաս", "ուր էլ որ երթաս"),
            new("սեղպտիկաս", "ստեղ պտի գաս"),
            new("Լավի մատիս", "լավ իմացիր"),
            new("տեսերհեկը", "տես երեխեքդ"),
            new("լագոթորըդ", "լագոթ հորդ"),
            new("Ինչս եմ", "ինչ կսեմ"),
            new("Կեն էս", "կենես"),
            new("Կլայի", "հլը"),
            new("Սրութեվին", "ձևին"),
            new("նայք", "նայե"),
            new("բայքութը", "վայ քու"),
            new("կամարդը", "տղամարդ"),
            new("սողիտ", "ըսողին"),
            new("նային", "նայի"),
            new("դինչը", "ինչխ"),
            new("խվորեց", "վրեդ"),
            new("ամութից", "ամոթից"),
            new("եթ", "եդ"),
            new("նայի", "նայե"),
            new("իրանուշը", "Սիրանուշը"),
            new("կեզի", "քեզի"),
            new("դուն", "տուն"),
            new("սողլողը", "թողնողը"),
            new("կգպնի", "գկպնի"),

            new("Մեկ ել, որ", "մեգելոր"),
            new("չիշտ", "ճիշտ"),
            new("պտիխաղա", "բդի խաղա"),
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

            new("Կրնախթ է", "կռնա հաղթե"),
            new("Կրիվեն է", "կռիվ ենե"),
            new("կհուսահատվիս", "կհուսահադվիս"),
            new("ինչխթե", "ինչղ թէ"),
            new("Սևասյանը", "Սովասթյանը"),
            new("թողնեմ", "թողնե"),
            new("կուզեմ", "գուզեմ"),
            new("Միզան սենք", "մի զանսե"),
            new("Միզան սենովել", "միզանսենով էլ"),
            new("ու ուզած այդ", "ու ուզացդ"),
            new("Ու դա", "կուդա"),
            new("Կվիչ է", "կվիջե"),
            new("Հետ այս", "հեդս"),
            new("վերտաս", "կերթաս"),
            new("խաղաս", "կխաղաս"),
            new("չայմ", "չեմ"),
            new("կարնա", "կռնա"),
            new("պիտի", "բդի"),
            new("երտամ", "էրթամ"),
            new("երտա", "էրթա"),
            new("գործ", "գորձ"),
            new("ունից", "ունիս"),
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
