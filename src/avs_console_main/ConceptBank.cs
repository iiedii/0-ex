using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Humanizer;

namespace Ad_hoc_video_search_console
{
    public class ConceptBank
    {
        List<List<HashSet<string>>> conceptList;
        HashSet<string> conceptPrintableNameList;
        HashSet<string> wordList;
        HashSet<string> wordList_imageNet;

        public ConceptBank()
        {
            conceptList = new List<List<HashSet<string>>>();
            conceptPrintableNameList = new HashSet<string>();
            wordList = new HashSet<string>();
            wordList_imageNet = new HashSet<string>();
        }

        public void ReadConceptList(string conceptListFile, bool isWordNetSynsets, StopWord stopword)
        {
            conceptList.Clear();

            using (StreamReader srConceptList = new StreamReader(conceptListFile))
            {
                string line = null;
                int lineID = 0;
                while ((line = srConceptList.ReadLine()) != null)
                {
                    List<HashSet<string>> conceptNames = new List<HashSet<string>>();

                    line = line.Trim();
                    lineID++;

                    if (line.StartsWith(";"))               // comment line
                        continue;

                    string[] splitLine = line.Split(',');

                    int nParsedPhrase = 0;
                    foreach (string _phrase in splitLine)
                    {
                        if (isWordNetSynsets && nParsedPhrase >= 1)           // for exact WordNet synsets, collect only the first phrase
                            break;

                        HashSet<string> conceptName = new HashSet<string>();

                        string phrase = _phrase.Trim();
                        if (phrase == "")
                            continue;

                        string[] splitPhrase;
                        if (!isWordNetSynsets)
                            splitPhrase = phrase.Split(new char[] { ' ', '_', '-', '/' });
                        else
                            splitPhrase = phrase.Split(' ');

                        foreach (string _term in splitPhrase)
                        {
                            string term = _term.Trim();
                            if (term != "")
                            {
                                term = term.ToLower();          // to lowercase
                                if (!isWordNetSynsets)
                                {
                                    term = term.Singularize(inputIsKnownToBePlural: false);       // plural to singular
                                    //if (stopword.IsStopword(term))          // exclude stopword
                                    //    continue;
                                }
                                if (term != "")
                                    conceptName.Add(term);
                            }
                        }

                        if (conceptName.Count != 0)
                        {
                            conceptNames.Add(conceptName);
                            nParsedPhrase++;
                        }
                        else
                            throw new FormatException(String.Format("Error reading concept list at Line {0}.", lineID));
                    }

                    if (conceptNames.Count != 0)
                        conceptList.Add(conceptNames);
                    else
                        throw new FormatException(String.Format("Error reading concept list at Line {0}.", lineID));
                }
            }

            MakeWordList();
            MakePrintableNameList();
        }

        public void ReadConceptList_Silent(string conceptListFile, bool isWordNetSynsets, StopWord stopword)
        {
            conceptList.Clear();

            using (StreamReader srConceptList = new StreamReader(conceptListFile))
            {
                string line = null;
                int lineID = 0;
                while ((line = srConceptList.ReadLine()) != null)
                {
                    List<HashSet<string>> conceptNames = new List<HashSet<string>>();

                    line = line.Trim();
                    lineID++;

                    if (line.StartsWith(";"))               // comment line
                        continue;

                    string[] splitLine = line.Split(',');

                    int nParsedPhrase = 0;
                    foreach (string _phrase in splitLine)
                    {
                        if (isWordNetSynsets && nParsedPhrase >= 1)           // for exact WordNet synsets, collect only the first phrase
                            break;

                        HashSet<string> conceptName = new HashSet<string>();

                        string phrase = _phrase.Trim();
                        if (phrase == "")
                            continue;

                        string[] splitPhrase;
                        if (!isWordNetSynsets)
                            splitPhrase = phrase.Split(new char[] { ' ', '_', '-', '/' });
                        else
                            splitPhrase = phrase.Split(' ');

                        foreach (string _term in splitPhrase)
                        {
                            string term = _term.Trim();
                            if (term != "")
                            {
                                term = term.ToLower();          // to lowercase
                                if (!isWordNetSynsets)
                                {
                                    term = term.Singularize(inputIsKnownToBePlural: false);       // plural to singular
                                    //if (stopword.IsStopword(term))          // exclude stopword
                                    //    continue;
                                }
                                if (term != "")
                                    conceptName.Add(term);
                            }
                        }

                        if (conceptName.Count != 0)
                        {
                            conceptNames.Add(conceptName);
                            nParsedPhrase++;
                        }
                        else
                            throw new FormatException(String.Format("Error reading concept list at Line {0}.", lineID));
                    }

                    if (conceptNames.Count != 0)
                        conceptList.Add(conceptNames);
                    else
                        throw new FormatException(String.Format("Error reading concept list at Line {0}.", lineID));
                }
            }

            makeWordList_silent();
            MakePrintableNameList();
        }

        public void ReadConceptList(List<string> conceptNameList, bool isWordNetSynsets, StopWord stopword)
        {
            conceptList.Clear();

            int lineID = 0;
            foreach (string line in conceptNameList)
            {
                List<HashSet<string>> conceptNames = new List<HashSet<string>>();

                lineID++;

                if (line.StartsWith(";"))               // comment line: specified concept name starts with ';'
                    continue;

                string[] splitLine = line.Split(',');

                int nParsedPhrase = 0;
                foreach (string _phrase in splitLine)
                {
                    if (isWordNetSynsets && nParsedPhrase >= 1)           // for exact WordNet synsets, collect only the first phrase
                        break;

                    HashSet<string> conceptName = new HashSet<string>();

                    string phrase = _phrase.Trim();
                    if (phrase == "")
                        continue;

                    string[] splitPhrase;
                    if (!isWordNetSynsets)
                        splitPhrase = phrase.Split(new char[] { ' ', '_', '-', '/' });
                    else
                        splitPhrase = phrase.Split(' ');

                    foreach (string _term in splitPhrase)
                    {
                        string term = _term.Trim();
                        if (term != "")
                        {
                            term = term.ToLower();          // to lowercase
                            if (!isWordNetSynsets)
                            {
                                term = term.Singularize(inputIsKnownToBePlural: false);       // plural to singular
                                //if (stopword.IsStopword(term))          // exclude stopword
                                //    continue;
                            }
                            if (term != "")
                                conceptName.Add(term);
                        }
                    }

                    if (conceptName.Count != 0)
                    {
                        conceptNames.Add(conceptName);
                        nParsedPhrase++;
                    }
                    else
                        throw new FormatException(String.Format("Error reading concept list at Line {0}.", lineID));
                }

                if (conceptNames.Count != 0)
                    conceptList.Add(conceptNames);
                else
                    throw new FormatException(String.Format("Error reading concept list at Line {0}.", lineID));
            }

            MakeWordList();
            MakePrintableNameList();
        }

        public void ReadConceptList_Silent(List<string> conceptNameList, bool isWordNetSynsets, StopWord stopword)
        {
            conceptList.Clear();

            int lineID = 0;
            foreach (string line in conceptNameList)
            {
                List<HashSet<string>> conceptNames = new List<HashSet<string>>();

                lineID++;

                if (line.StartsWith(";"))               // comment line: specified concept name starts with ';'
                    continue;

                string[] splitLine = line.Split(',');

                int nParsedPhrase = 0;
                foreach (string _phrase in splitLine)
                {
                    if (isWordNetSynsets && nParsedPhrase >= 1)           // for exact WordNet synsets, collect only the first phrase
                        break;

                    HashSet<string> conceptName = new HashSet<string>();

                    string phrase = _phrase.Trim();
                    if (phrase == "")
                        continue;

                    string[] splitPhrase;
                    if (!isWordNetSynsets)
                        splitPhrase = phrase.Split(new char[] { ' ', '_', '-', '/' });
                    else
                        splitPhrase = phrase.Split(' ');

                    foreach (string _term in splitPhrase)
                    {
                        string term = _term.Trim();
                        if (term != "")
                        {
                            term = term.ToLower();          // to lowercase
                            if (!isWordNetSynsets)
                            {
                                term = term.Singularize(inputIsKnownToBePlural: false);       // plural to singular
                                //if (stopword.IsStopword(term))          // exclude stopword
                                //    continue;
                            }
                            if (term != "")
                                conceptName.Add(term);
                        }
                    }

                    if (conceptName.Count != 0)
                    {
                        conceptNames.Add(conceptName);
                        nParsedPhrase++;
                    }
                    else
                        throw new FormatException(String.Format("Error reading concept list at Line {0}.", lineID));
                }

                if (conceptNames.Count != 0)
                    conceptList.Add(conceptNames);
                else
                    throw new FormatException(String.Format("Error reading concept list at Line {0}.", lineID));
            }

            makeWordList_silent();
            MakePrintableNameList();
        }

        public void MakeWordList()
        {
            wordList.Clear();
            wordList_imageNet.Clear();

            foreach (List<HashSet<string>> conceptNames in conceptList)
            {
                foreach (HashSet<string> conceptName in conceptNames)
                {
                    string phrase = "";
                    foreach (string term in conceptName)
                    {
                        wordList.Add(term);

                        phrase += term + "_";
                    }
                    if (wordList_imageNet.Add(phrase.Trim('_')) == false && conceptNames.Count == 1)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Duplicate concept in concept bank: " + phrase.Trim('_'));
                        Console.ResetColor();
                    }
                }
            }
        }

        private void makeWordList_silent()
        {
            wordList.Clear();
            wordList_imageNet.Clear();

            foreach (List<HashSet<string>> conceptNames in conceptList)
            {
                foreach (HashSet<string> conceptName in conceptNames)
                {
                    string phrase = "";
                    foreach (string term in conceptName)
                    {
                        wordList.Add(term);

                        phrase += term + "_";
                    }
                    if (wordList_imageNet.Add(phrase.Trim('_')) == false && conceptNames.Count == 1)
                    {
                        //Console.ForegroundColor = ConsoleColor.Red;
                        //Console.WriteLine("Duplicate concept in concept bank: " + phrase.Trim('_'));
                        //Console.ResetColor();
                    }
                }
            }
        }

        public void MakePrintableNameList()
        {
            conceptPrintableNameList.Clear();

            int conceptID = 1;
            foreach (List<HashSet<string>> concept in conceptList)
            {
                string printableName = getPrintableConceptName(concept);

                conceptPrintableNameList.Add(printableName);
                conceptID++;
            }
        }

        public bool IsInConceptBank(List<HashSet<string>> concept)
        {
            string targetConceptName = getPrintableConceptName(concept);

            if (conceptPrintableNameList.Contains(targetConceptName))
                return true;
            else
                return false;
        }

        public void PrintConceptList(string conceptListFile)
        {
            using (StreamWriter swConceptList = new StreamWriter(conceptListFile))
            {
                foreach (string line in conceptPrintableNameList)
                {
                    swConceptList.WriteLine(line);
                }
            }
        }

        private string getPrintableConceptName(List<HashSet<string>> concept)
        {
            string printableName = "";
            foreach (HashSet<string> conceptNamePhrase in concept)
            {
                foreach (string term in conceptNamePhrase)
                {
                    printableName += term + " ";
                }
                printableName = printableName.Trim() + ", ";
            }
            return printableName.TrimEnd(new char[] { ' ', ',' });
        }

        /////////////////////////////////////////////////////////////////////////////////////

        public List<List<HashSet<string>>> ConceptList
        {
            get { return this.conceptList; }
        }

        public HashSet<string> WordList
        {
            get { return this.wordList; }
        }

        public HashSet<string> WordList_ImageNet
        {
            get { return this.wordList_imageNet; }
        }

        public int Count
        {
            get { return this.conceptList.Count; }
        }
    }
}
