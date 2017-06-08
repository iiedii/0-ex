using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Humanizer;
using System.Diagnostics;

namespace Ad_hoc_video_search_console
{
    public class NLPSentenceParser
    {
        List<HashSet<string>> nounPhraseList;
        HashSet<string> verbList;

        public NLPSentenceParser()
        {
            nounPhraseList = new List<HashSet<string>>();
            verbList = new HashSet<string>();
        }

        public bool ParseSentence(string sentence, StopWord stopword)
        {
            nounPhraseList.Clear();
            verbList.Clear();

            sentence = sentence.Trim();
            if (sentence == "")         // empty string
                return false;

            string[] splitSentence = sentence.Split(';');
            if (splitSentence.Length == 2)
            {
                splitSentence[0] = splitSentence[0].Trim();
                splitSentence[1] = splitSentence[1].Trim();

                //PluralizationService ps = PluralizationService.CreateService(new CultureInfo("en-US"));

                // Parse noun phrases
                if (splitSentence[0].StartsWith("NP:"))
                {
                    string nounSentence = splitSentence[0].Substring(3).Trim();
                    string[] nounPhrases = nounSentence.Split(',');

                    foreach (string _phrase in nounPhrases)
                    {
                        string phrase = _phrase.Trim();
                        string[] terms = phrase.Split(' ');

                        HashSet<string> termList = new HashSet<string>();
                        foreach (string _term in terms)
                        {
                            string term = _term.Trim();
                            if (term != "")
                            {
                                term = term.ToLower();                  // to lowercase
                                if (stopword.IsStopword(term))          // remove stop word
                                    continue;

                                term = term.Singularize(inputIsKnownToBePlural: false);            // plural to singular
                                termList.Add(term);
                            }
                        }
                        if (termList.Count != 0)
                            nounPhraseList.Add(termList);
                    }
                }
                else
                    if (splitSentence[0] != "")
                    throw new FormatException("NLP sentence does not start with \"NP:\".");

                // Parse verbs
                if (splitSentence[1].StartsWith("VB:"))
                {
                    string verbSentence = splitSentence[1].Substring(3).Trim();
                    string[] verbs = verbSentence.Split(',');

                    foreach (string _verb in verbs)
                    {
                        string verb = _verb.Trim();
                        if (verb != "")
                        {
                            verb = verb.ToLower();                  // to lowercase
                            if (stopword.IsStopword(verb))          // remove stop word
                                continue;

                            verbList.Add(verb);
                        }
                    }
                }
                else
                    if (splitSentence[1] != "")
                    throw new FormatException("Verbs in NLP sentence does not start with \"VB:\".");
            }
            else    // when no ; in splitSentence
            {
                Debug.Assert(splitSentence[0].StartsWith("VB:"));
                string verbSentence = splitSentence[0].Substring(3).Trim();
                string[] verbs = verbSentence.Split(',');

                foreach (string _verb in verbs)
                {
                    string verb = _verb.Trim();
                    if (verb != "")
                    {
                        verb = verb.ToLower();                  // to lowercase
                        if (stopword.IsStopword(verb))          // remove stop word
                            continue;

                        verbList.Add(verb);
                    }
                }
            }

            return true;            //! relaxed for human input queries
            //if (nounPhraseList.Count + verbList.Count > 0)
            //    return true;
            //else
            //    return false;
        }

        public static string MakeNLPSentence(List<HashSet<string>> nouns, HashSet<string> verbs)
        {
            if (nouns.Count == 0 && verbs.Count == 0)
                return "";

            string nlpSentence = "";
            if (nouns.Count != 0)
            {
                nlpSentence += "NP: ";

                foreach (HashSet<string> termList in nouns)
                {
                    string phrase = "";
                    foreach (string term in termList)
                        phrase += term + " ";

                    nlpSentence += phrase.Trim() + ", ";
                }

                nlpSentence = nlpSentence.TrimEnd(new char[] { ',', ' ' });
                nlpSentence += "; ";
            }

            if (verbs.Count != 0)
            {
                nlpSentence += "VB: ";

                foreach (string term in verbs)
                {
                    nlpSentence += term + ", ";
                }

                nlpSentence = nlpSentence.TrimEnd(new char[] { ',', ' ' });
            }

            return nlpSentence.Trim();
        }

        public List<HashSet<string>> NounPhraseList
        {
            get { return this.nounPhraseList; }
        }

        public HashSet<string> VerbList
        {
            get { return this.verbList; }
        }
    }
}
