using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NMeCab.Specialized;
using Wacton.Desu.Japanese;

namespace Kawazu
{
    
    /// <summary>
    /// The main class of Kawazu library. Please call Dispose when finish using it or use the Using statement
    /// </summary>
    public class KawazuConverter: IDisposable
    {
        private readonly MeCabIpaDicTagger _tagger;
        private readonly List<IJapaneseEntry> _japEntries;

        public KawazuConverter()
        {
            _tagger = MeCabIpaDicTagger.Create();
            JapaneseDictionary japDico = new JapaneseDictionary();
            _japEntries = japDico.GetEntries().ToList();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tagger?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~KawazuConverter()
        {
            Dispose(false);
        }

        /// <summary>
        /// Get the raw result from the word Separator.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="to"></param>
        /// <param name="mode"></param>
        /// <param name="system"></param>
        /// <param name="delimiterStart"></param>
        /// <param name="delimiterEnd"></param>
        /// <returns>List of word divisions</returns>
        public async Task<List<Division>> GetDivisions(
            string str,
            To to = To.Hiragana,
            Mode mode = Mode.Normal,
            RomajiSystem system = RomajiSystem.Hepburn,
            string delimiterStart = "(",
            string delimiterEnd = ")")
        {
            var result = await Task.Run(() =>
            {
                var nodes = _tagger.Parse(str); // Parse
                var builder = new StringBuilder(); // StringBuilder for the final output string.
                var text = nodes.Select(node => new Division(node, Utilities.GetTextType(node.Surface), system))
                    .ToList();
                return text;
            });

            return result;
        }

        /// <summary>
        /// Convert the given sentence into chosen form.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="to"></param>
        /// <param name="mode"></param>
        /// <param name="system"></param>
        /// <param name="delimiterStart"></param>
        /// <param name="delimiterEnd"></param>
        /// <returns>Convert result string</returns>
        public async Task<string> Convert(
            string str,
            To to=To.Hiragana,
            Mode mode=Mode.Normal,
            RomajiSystem system=RomajiSystem.Hepburn,
            string delimiterStart="(",
            string delimiterEnd=")")
        {
            var result = await Task.Run(() =>
            {
                var nodes = _tagger.Parse(str); // Parse
                var builder = new StringBuilder(); // StringBuilder for the final output string.
                var text = nodes.Select(node => new Division(node, Utilities.GetTextType(node.Surface), system))
                    .ToList();

                // Detect solo kanjis, group them in sequentials blocks and search those blocks in the japanese dictionary from Desu
                List<Division> textAnalyzed = new List<Division>();
                string block = "";
                foreach (var div in text)
                {
                    if (div.Surface.Length > 1 || !Utilities.IsKanji(div.Surface[0])) // Not a kanji or multiple kanjis --> end of current block
                    {
                        if (!string.IsNullOrEmpty(block)) // If a block has begun, analyze it
                        {
                            textAnalyzed.AddRange(AnalyzeBlock(block));
                            block = "";
                        }
                        textAnalyzed.Add(div);
                    }
                    else
                    {
                        block += div.Surface; // Add kanji to current block
                    }
                }
                if (!string.IsNullOrEmpty(block))
                {
                    textAnalyzed.AddRange(AnalyzeBlock(block));
                }
                text = textAnalyzed; // Replace with analyzed text

                switch (to)
                {
                    case To.Romaji:
                        var isPreviousEndsInTsu = false;
                        switch (mode)
                        {
                            case Mode.Normal:
                                foreach (var division in text)
                                {
                                    if (division.IsEndsInTsu)
                                    {
                                        isPreviousEndsInTsu = true;
                                        division.RemoveAt(division.Count - 1);
                                        builder.Append(division.RomaReading);
                                        continue;
                                    }

                                    if (isPreviousEndsInTsu)
                                    {
                                        builder.Append(division.RomaReading.First());
                                        isPreviousEndsInTsu = false;
                                    }
                                    builder.Append(division.RomaReading);
                                }
                                break;
                            case Mode.Spaced:
                                foreach (var division in text)
                                {
                                    if (division.IsEndsInTsu)
                                    {
                                        isPreviousEndsInTsu = true;
                                        division.RemoveAt(division.Count - 1);
                                        builder.Append(division.RomaReading);
                                        continue;
                                    }

                                    if (isPreviousEndsInTsu)
                                    {
                                        builder.Append(division.RomaReading.First());
                                        isPreviousEndsInTsu = false;
                                    }
                                    builder.Append(division.RomaReading).Append(" ");
                                }
                                break;
                            case Mode.Okurigana:
                                foreach (var ele in text.SelectMany(division => division))
                                {
                                    if (ele.Type == TextType.PureKanji)
                                    {
                                        builder.Append(ele.Element).Append(delimiterStart).Append(ele.RomaNotation)
                                            .Append(delimiterEnd);
                                    }
                                    else
                                    {
                                        builder.Append(ele.Element);
                                    }
                                }
                                break;
                            case Mode.Furigana:
                                foreach (var ele in text.SelectMany(division => division))
                                {
                                    if (ele.Type == TextType.PureKanji)
                                    {
                                        builder.Append("<ruby>").Append(ele.Element).Append("<rp>")
                                            .Append(delimiterStart).Append("</rp>").Append("<rt>")
                                            .Append(ele.RomaNotation).Append("</rt>").Append("<rp>")
                                            .Append(delimiterEnd).Append("</rp>").Append("</ruby>");
                                    }
                                    else
                                    {
                                        if (ele.Element.Last() == 'っ' || ele.Element.Last() == 'ッ')
                                        {
                                            builder.Append(ele.Element.Last());
                                            isPreviousEndsInTsu = true;
                                            continue;
                                        }

                                        if (isPreviousEndsInTsu)
                                        {
                                            builder.Append("<ruby>").Append(ele.Element).Append("<rp>")
                                                .Append(delimiterStart).Append("</rp>").Append("<rt>")
                                                .Append(ele.RomaNotation.First())
                                                .Append(ele.RomaNotation).Append("</rt>").Append("<rp>")
                                                .Append(delimiterEnd).Append("</rp>").Append("</ruby>");
                                            isPreviousEndsInTsu = false;
                                            continue;
                                        }
                                        builder.Append("<ruby>").Append(ele.Element).Append("<rp>")
                                            .Append(delimiterStart).Append("</rp>").Append("<rt>")
                                            .Append(ele.RomaNotation).Append("</rt>").Append("<rp>")
                                            .Append(delimiterEnd).Append("</rp>").Append("</ruby>");
                                    }
                                }
                                break;
                        }
                        break;
                    case To.Katakana:
                        switch (mode)
                        {
                            case Mode.Normal:
                                foreach (var division in text)
                                {
                                    builder.Append(division.KataReading);
                                }
                                break;
                            case Mode.Spaced:
                                foreach (var division in text)
                                {
                                    builder.Append(division.KataReading).Append(" ");
                                }
                                break;
                            case Mode.Okurigana:
                                foreach (var ele in text.SelectMany(division => division))
                                {
                                    if (ele.Type == TextType.PureKanji)
                                    {
                                        builder.Append(ele.Element).Append(delimiterStart).Append(ele.KataNotation)
                                            .Append(delimiterEnd);
                                    }
                                    else
                                    {
                                        builder.Append(ele.Element);
                                    }
                                }
                                break;
                            case Mode.Furigana:
                                foreach (var ele in text.SelectMany(division => division))
                                {
                                    if (ele.Type == TextType.PureKanji)
                                    {
                                        builder.Append("<ruby>").Append(ele.Element).Append("<rp>")
                                            .Append(delimiterStart).Append("</rp>").Append("<rt>")
                                            .Append(ele.KataNotation).Append("</rt>").Append("<rp>")
                                            .Append(delimiterEnd).Append("</rp>").Append("</ruby>");
                                    }
                                    else
                                    {
                                        builder.Append(ele.Element);
                                    }
                                }
                                break;
                        }
                        break;
                    case To.Hiragana:
                        switch (mode)
                        {
                            case Mode.Normal:
                                foreach (var division in text)
                                {
                                    builder.Append(division.HiraReading);
                                }
                                break;
                            case Mode.Spaced:
                                foreach (var division in text)
                                {
                                    builder.Append(division.HiraReading).Append(" ");
                                }
                                break;
                            case Mode.Okurigana:
                                foreach (var ele in text.SelectMany(division => division))
                                {
                                    if (ele.Type == TextType.PureKanji)
                                    {
                                        builder.Append(ele.Element).Append(delimiterStart).Append(ele.HiraNotation)
                                            .Append(delimiterEnd);
                                    }
                                    else
                                    {
                                        builder.Append(ele.Element);
                                    }
                                }
                                break;
                            case Mode.Furigana:
                                foreach (var ele in text.SelectMany(division => division))
                                {
                                    if (ele.Type == TextType.PureKanji)
                                    {
                                        builder.Append("<ruby>").Append(ele.Element).Append("<rp>")
                                            .Append(delimiterStart).Append("</rp>").Append("<rt>")
                                            .Append(ele.HiraNotation).Append("</rt>").Append("<rp>")
                                            .Append(delimiterEnd).Append("</rp>").Append("</ruby>");
                                    }
                                    else
                                    {
                                        builder.Append(ele.Element);
                                    }
                                }
                                break;
                        }
                        break;
                }

                return builder.ToString();
            });
            
            return result;
        }

        private List<Division> AnalyzeBlock(string block)
        {
            List<Division> lstNodes = new List<Division>();
            string _trash = "";
            IJapaneseEntry entryFound;
            do
            {
                entryFound = _japEntries.FirstOrDefault(j => j.Kanjis.Any(k => k.Text == block));
                if (entryFound != null)
                {
                    // Found a match in the dictionary --> take the first reading (arbitrary choice)
                    string entryHira = entryFound.Readings.ToList()[0].Text;
                    JapaneseElement node = new JapaneseElement(block, Utilities.ToRawKatakana(entryHira), Utilities.ToRawKatakana(entryHira), TextType.PureKanji);
                    lstNodes.Add(new Division(new List<JapaneseElement>() { node }));
                }
                else // No match found for this list of kanjis --> remove the last kanji and retry
                {
                    _trash = block.Substring(block.Length - 1, 1) + _trash;
                    block = block.Substring(0, block.Length - 1);
                }
            } while (!string.IsNullOrEmpty(block) && entryFound == null);
            if (!string.IsNullOrEmpty(_trash)) // if trash is not empty (meaning some kanjis were removed) --> analyze it
            {
                lstNodes.AddRange(AnalyzeBlock(_trash));
            }
            return lstNodes;
        }
    }

    /// <summary>
    /// The target form of the sentence.
    /// </summary>
    public enum To
    {
        Hiragana,
        Katakana,
        Romaji
    }

    /// <summary>
    /// The presentation method of the result.
    /// </summary>
    public enum Mode
    {
        Normal,
        Spaced,
        Okurigana,
        Furigana
    }

    /// <summary>
    /// The writing systems of romaji.
    /// </summary>
    public enum RomajiSystem
    {
        Nippon,
        Passport,
        Hepburn
    }

    /// <summary>
    /// The composition of a word or a element.
    /// </summary>
    public enum TextType
    {
        PureKanji,
        KanjiKanaMixed,
        PureKana,
        Others
    }
}