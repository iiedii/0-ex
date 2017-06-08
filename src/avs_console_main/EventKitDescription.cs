using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Humanizer;

namespace Ad_hoc_video_search_console
{
    public class EventKitDescription
    {
        HashSet<string> eventName;
        List<HashSet<string>> eventDefinition_nouns;
        HashSet<string> eventDefinition_verbs;
        List<HashSet<string>> eventExplication_nouns;
        HashSet<string> eventExplication_verbs;
        EventEvidence eventEvidence;
        HashSet<string> wordList_withoutExplication;
        HashSet<string> verbList;
        Dictionary<string, int> tfTable;
        Dictionary<string, double> normTfTable;

        public EventKitDescription()
        {
            eventName = new HashSet<string>();
            eventDefinition_nouns = new List<HashSet<string>>();
            eventDefinition_verbs = new HashSet<string>();
            eventExplication_nouns = new List<HashSet<string>>();
            eventExplication_verbs = new HashSet<string>();
            eventEvidence = new EventEvidence();
            wordList_withoutExplication = new HashSet<string>();
            verbList = new HashSet<string>();
            tfTable = new Dictionary<string, int>();
            normTfTable = new Dictionary<string, double>();
        }

        public bool ParseDescription(string description, StopWord stopword, bool isMEDQuery)
        {
            description = description.Trim();
            if (!description.StartsWith("Query title:") && !description.StartsWith("Query Title:"))
                throw new FormatException("Event kit description does not start with formal head.");

            // -------------- Retrieve substring of each type -------------- //
            int pos_eventName = 0;
            int pos_definition = description.IndexOf("Definition:");
            int pos_explication = -10000;
            int pos_evidence = -10000;
            if (isMEDQuery)
            {
                pos_explication = description.IndexOf("Explication:");
                pos_evidence = description.IndexOf("Evidential Description:");
            }
            if (pos_definition < 1)
                throw new FormatException("One or more types of event description are missing.");

            int pos_eventName_string = pos_eventName + 12;
            int pos_definition_string = pos_definition + 11;
            int pos_explication_string = pos_explication + 12;
            int pos_evidence_string = pos_evidence + 23;

            int length_eventName_string = pos_definition - pos_eventName_string;
            int length_definition_string = -10000;
            if (!isMEDQuery)
                length_definition_string = description.Length - pos_definition_string;
            else
                length_definition_string = pos_explication - pos_definition_string;
            int length_explication_string = pos_evidence - pos_explication_string;
            int length_evidence_string = description.Length - pos_evidence_string;
            if (length_eventName_string < 0 || length_definition_string < 0)
                throw new FormatException("Types of event description are in wrong order.");

            string description_eventName = description.Substring(pos_eventName_string, length_eventName_string).Trim();
            string description_definition = description.Substring(pos_definition_string, length_definition_string).Trim();
            string description_explication = null;
            string description_evidence = null;
            if (isMEDQuery)
            {
                description_explication = description.Substring(pos_explication_string, length_explication_string).Trim();
                description_evidence = description.Substring(pos_evidence_string, length_evidence_string).Trim();
            }


            // ---------------------- Parse ------------------------- //
            NLPSentenceParser nlpParser = new NLPSentenceParser();

            if (!parseEventName(description_eventName, stopword))
                throw new FormatException("Failed to parse query title.");

            if (nlpParser.ParseSentence(description_definition, stopword))
            {
                eventDefinition_nouns = new List<HashSet<string>>(nlpParser.NounPhraseList);
                eventDefinition_verbs = new HashSet<string>(nlpParser.VerbList);
            }
            else
                throw new FormatException("Failed to parse query definition.");


            if (isMEDQuery)
            {
                if (nlpParser.ParseSentence(description_explication, stopword))
                {
                    eventExplication_nouns = new List<HashSet<string>>(nlpParser.NounPhraseList);
                    eventExplication_verbs = new HashSet<string>(nlpParser.VerbList);
                }

                if (!eventEvidence.ParseDescription(description_evidence, stopword))
                    throw new FormatException("Failed to parse evidential description in the query.");
            }


            MakeWordList(isMEDQuery);

            return true;
        }

        private bool parseEventName(string nameStr, StopWord stopword)
        {
            eventName.Clear();
            //PluralizationService ps = PluralizationService.CreateService(new CultureInfo("en-US"));

            string[] terms = nameStr.Split(' ');
            foreach (string _term in terms)
            {
                string term = _term.Trim();
                if (term != "")
                {
                    term = term.ToLower();                          // to lowercase
                    if (stopword.IsStopword(term))                  // remove stop word
                        continue;

                    if (term.StartsWith("+"))                       // in support of force non-stop word
                        term = term.TrimStart('+');
                    term = term.Singularize(inputIsKnownToBePlural: false);                    // plural to singular
                    eventName.Add(term);
                }
            }

            if (eventName.Count != 0)
                return true;
            else
                return false;
        }

        public void MakeWordList(bool isMEDQuery)
        {
            wordList_withoutExplication.Clear();
            verbList.Clear();
            tfTable.Clear();

            foreach (string term in eventName)
            {
                wordList_withoutExplication.Add(term);
                addTermToTFTable(term);
            }

            foreach (HashSet<string> termList in eventDefinition_nouns)
            {
                foreach (string term in termList)
                {
                    wordList_withoutExplication.Add(term);
                    addTermToTFTable(term);
                }
            }
            foreach (string term in eventDefinition_verbs)
            {
                wordList_withoutExplication.Add(term);
                verbList.Add(term);
                addTermToTFTable(term);
            }

            foreach (HashSet<string> termList in eventExplication_nouns)
            {
                foreach (string term in termList)
                {
                    addTermToTFTable(term);
                }
            }
            foreach (string term in eventExplication_verbs)
            {
                verbList.Add(term);
                addTermToTFTable(term);
            }

            if (isMEDQuery)
            {
                foreach (string term in eventEvidence.WordList_WithoutAudio)
                {
                    wordList_withoutExplication.Add(term);
                }

                foreach (string verb in eventEvidence.VerbList)
                {
                    verbList.Add(verb);
                }

                foreach (string term in eventEvidence.WordList)
                {
                    addTermToTFTable(term);
                }
            }

            MakeNormTfTable();
        }

        public void MakeNormTfTable()
        {
            normTfTable.Clear();

            int maxFreq = 0;
            foreach (KeyValuePair<string, int> kvp in tfTable)
            {
                int freq = kvp.Value;

                if (freq > maxFreq)
                    maxFreq = freq;
            }

            foreach (KeyValuePair<string, int> kvp in tfTable)
            {
                string term = kvp.Key;
                int freq = kvp.Value;
                double normFreq = (double)freq / (double)maxFreq;
                normTfTable.Add(term, normFreq);
            }
        }

        private void addTermToTFTable(string term)
        {
            if (!tfTable.ContainsKey(term))
                tfTable.Add(term, 1);
            else
                tfTable[term]++;
        }


        public string PrintEventDescription()
        {
            string description = "Query title: ";
            foreach (string term in eventName)
                description += term + " ";
            description = description.Trim() + "\n";

            description += "Definition: " + NLPSentenceParser.MakeNLPSentence(eventDefinition_nouns, eventDefinition_verbs) + "\n";
            //description += "Explication: " + NLPSentenceParser.MakeNLPSentence(eventExplication_nouns, eventExplication_verbs) + "\n";
            //description += eventEvidence.PrintEvidence();

            return description;
        }

        /////////////////////////////////////////////////////////////////////////////////////

        public HashSet<string> WordList_WithoutExplication
        {
            get { return this.wordList_withoutExplication; }
        }

        public HashSet<string> VerbList
        {
            get { return this.verbList; }
        }

        public HashSet<string> EventNameTermSet
        {
            get { return this.eventName; }
        }

        public HashSet<string> EventDefinitionTermSet
        {
            get
            {
                HashSet<string> termSet = new HashSet<string>();
                foreach (HashSet<string> nounPhrases in eventDefinition_nouns)
                {
                    foreach (string noun in nounPhrases)
                    {
                        termSet.Add(noun);
                    }
                }
                foreach (string verb in eventDefinition_verbs)
                {
                    termSet.Add(verb);
                }
                return termSet;
            }
        }

        public Dictionary<string, int> TfTable
        {
            get { return this.tfTable; }
        }

        public Dictionary<string, double> NormTfTable
        {
            get { return this.normTfTable; }
        }

        public EventEvidence Evidence
        {
            get { return this.eventEvidence; }
        }

        public string EventNameString
        {
            get
            {
                string eventNameStr = "";
                foreach (string term in eventName)
                {
                    eventNameStr += term + " ";
                }
                return eventNameStr.Trim();
            }
        }
    }
}