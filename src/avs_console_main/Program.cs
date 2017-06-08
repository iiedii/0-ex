﻿//#define DOONEEVENT
//#define DEBUG

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Humanizer;

namespace Ad_hoc_video_search_console
{
    class Program
    {
        // Inputs for processing STEP 1, for ImageNet concepts, set isWordNetSynsets true for exact synset match
        private static string queryDescriptionFile = @"";
        private static string conceptListFile = @"";
        private static string conceptGlobalBanFile = @"";
        private static string conceptBanPerQueryFile = @"";
        private static string conceptHandPickedPerQueryFile = @"";
        private static string idfTableFile = @"";

        // Inputs for processing STEP 2
        private static string similarityTableFile = @"";
        private static string mergedInfo_nConceptsFile = @"";

        // Inputs for processing STEP 3
        private static string conceptResp_TableFile = @"";
        private static string conceptResp_VideoListFile = @"";
        private static string irrelevantEnergyScoreFile = @"";
        private static string irrelevantConceptTableFile = @"";
        private static string groundTruthFile = @"";

        // Output
        private static string mappingResultDir = @"";

        // Controller
        private static int processingStep = 1;              // step 1: create the pair-wise query-to-concept matching table for similarity calculation
                                                            // step 2: rank concepts given the similarity
                                                            // step 3: rank videos and calculate MAP given the concept weights
                                                            // step 4: a combination of step 2 and 3
        private static int switch_tfidf = 2;                // TFIDF switch:   0 - off, 1 - TF only, 2 - TFIDF; default: 2
        private static int switch_similarity = 4;           // switch for ranking similarity between concepts and query description:
                                                            //    pooling across query terms in each query, for each concept, select the terms with
                                                            //       1 - MAX similarity, 2 - MEAN similarity, 3 - MAX + TOP similarity;  for 3, parameter inside
                                                            //    for each query term, select the most correlated concept; then for each concept, pooling across the selections
                                                            //       4 - SUM UP, 5 - AVERAGE, 6 - MAX; default: 4

        // Parameters
        private static bool isMEDQuery = false;                     // set true if the queries are MED events
        private static bool isWordNetSynsets = false;               // set true for parsing exact WordNet synsets (e.g. ImageNet concepts)
        private static bool isUseConceptBlacklist = false;          // exclude concepts in the blacklist; only valid when isWordNetSynsets = false
        private static bool isUseHandPickedConcepts = false;        // specify hand-picked concepts by concept ID
        private static int thresh_rerankByGroup = 300;              // re-ranking in the top k results, by minor group ID set in hand-picked concepts (starting from group ID 1); default: 0
        private static bool isUsePerEventConceptResponse = false;   // use when pooling method differs, e.g. evidence pooling will result in event-specialized score table; the path of the per-event score table should contain event ID identifier %EID
        private static bool isVerbFix = true;                       // verbs in event description must have context constraints when do the mapping; default: true
        private static bool isMergedConceptDataset = true;          // if it is a merged dataset, provide mergedInfo_nConceptsFile
        private static bool isCalcMAP = false;                      // show the MAP for all or the top k concepts
        private static bool isCalcInfMAP = false;                   // show the inferred MAP for all or the top k concepts
        private static bool isTestRandomResult = false;             // randomize the video ranking list and show the MAP
        private static bool isOffsetByIrrelevConceptEnergy = false; // if set true you need to specify the weight multiplied to the energy score for each video; experimental; default: false
        private static double weight_irrelevantEnergy = 10.0;       // default: 10.0, enabled only when isOffsetByIrrelevConceptEnergy is true; experimental
        private static double thresh_irrelevantConcepts = -1.0;     // for using irrelevant concepts; >0 to enable, -1.0 to disable; default: -1.0; obsolete because it doesn't work
        private static int[] testID_k_range = { };                  // test with multiple score tables (k = #clusters in avg-max pooling); leave empty {} if not used; this does not support top concept selection
        private static int nThreadsForSimilarityRanking = 4;        // number of threads for concept similarity ranking

        //    - Top selection control
        private static int nTopConceptsOnly = -1;                                   // select only the top concepts from ranking list, -1 to disable; normally the top 10 or so would achieve the best performance
        private static bool isExtendTopConceptsByConceptSimilarity = true;          // extend nTopConceptOnly selections by considering the concept similarity scores; valid only when nTopConceptOnly > 0 or top concept selection is enabled; default: true
        private static bool isDoTopConceptSelection = false;                        // selection test for using only the top concepts
        //    - Weight control
        private static double weightMultiplier_queryDefinitionTerms = 5.0;          // when calculating the concept similarity, weight the terms in the query definition higher
        private static double weightMultiplier_queryTitleTerms = 1.0;               // when calculating the concept similarity, weight the query title higher
        private static double weightMultiplier_queryTitleConcept = -1.0;            //! only implemented for switch_similarity = 4; weight the concept with a name that exactly matches the query title; default: -1.0 to disable
        private static double weightMultiplier_highWeightConcepts = 2.0;            // weight the predefined concepts higher
        private static HashSet<string> highWeightConceptSet = null;
        private static double weightMultiplier_lowWeightConcepts = 0.5;             // weight the predefined concepts lower
        private static HashSet<string> lowWeightConceptSet = null;
        private static bool weightMultiplier_queryTitleTerms_isWeightedByQueryTitleConcept = false;
        //    - Misc.
        private static int queryIDOffset = 500;                                     // realQueryID = queryID + queryIDOffset, for TRECVID evaluation
        private static string stopWordFile = @"";



        static void Main(string[] args)
        {
            Console.WriteLine(new string('*', 79));
            Console.WriteLine();

            try
            {
                LoadConfigFile("config.ini");
                FormatPaths();
                ParseEventNLPFile();
                ReadConceptList();
                ReadConceptBlacklist();
                CreateWordList();
                LoadIDFTable();
                if (isMergedConceptDataset)
                    LoadMergedDatasetInfo();

                while ((processingStep = getCommandFromUserInput()) > 0)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("Processing step " + processingStep);
                    Console.ResetColor();

                    //if (isUseEachQueryTermOnce && switch_similarity == 1)
                    //{
                    //    Console.ForegroundColor = ConsoleColor.Yellow;
                    //    Console.WriteLine("WARNING: isEachQueryTermUsedOnce is ENABLED.");
                    //    Console.ResetColor();
                    //}


                    if (processingStep == 1)
                        CreateEventConceptTermTable();
                    else if (processingStep == 2)
                    {
                        ReadEventConceptSimilarityTable();
                        RankConceptsForAllEvents(nTopConceptsOnly, null);
                    }

                    if (processingStep == 3 || processingStep == 4)
                    {
                        ReadEventConceptSimilarityTable();
                        if (!isDoTopConceptSelection)
                        {
                            RankConceptsForAllEvents(nTopConceptsOnly, null);

                            List<int> testID_k = new List<int>();
                            if (testID_k_range.Length == 2)
                            {
                                for (int i = testID_k_range[0]; i <= testID_k_range[1]; i++)
                                    testID_k.Add(i);
                            }
                            else
                                testID_k.Add(0);            // testID_k == 0 => no multiple score tables

                            foreach (int k in testID_k)
                            {
                                string conceptResp_TableFile_thisTest = null, conceptResp_VideoListFile_thisTest = null;
                                if (k != 0)
                                {
                                    if (!isUsePerEventConceptResponse)
                                    {
                                        conceptResp_TableFile_thisTest = conceptResp_TableFile.Replace("%d", k.ToString());
                                        conceptResp_VideoListFile_thisTest = conceptResp_VideoListFile.Replace("%d", k.ToString());
                                        if (!File.Exists(conceptResp_TableFile_thisTest))
                                            continue;
                                    }

                                    Console.WriteLine("K = " + k);
                                }
                                else
                                {
                                    conceptResp_TableFile_thisTest = conceptResp_TableFile;
                                    conceptResp_VideoListFile_thisTest = conceptResp_VideoListFile;
                                }


                                if (!isUsePerEventConceptResponse)
                                {
                                    if (k != 0)                 // always read having multiple score tables
                                        ReadVideoConceptScoreTable(conceptResp_TableFile_thisTest, conceptResp_VideoListFile_thisTest);
                                    else if (video_conceptScoreList != null && !video_conceptScoreList.ContainsKey(0))      // read only when no preload
                                        ReadVideoConceptScoreTable(conceptResp_TableFile_thisTest, conceptResp_VideoListFile_thisTest);
                                }
                                if (!RankVideosForAllEvents(k))
                                {
                                    Console.WriteLine("   - skipped as score table is not found.");
                                    Console.WriteLine();
                                    continue;
                                }
                                if (isCalcMAP)
                                {
                                    ReadGroundTruth();
                                    CalculateMAPForAllEvents(nTopConceptsOnly, k);
                                }
                                if (isCalcInfMAP)
                                {
                                    ReadGroundTruth();
                                    CalculateInfAPForAllEvents(nTopConceptsOnly, k);
                                }
                            }
                        }
                        else    // do top concept selection
                        {
                            Debug.Assert(testID_k_range.Length != 2);           // does not support tests with multiple score tables
                            int[] nTopSelection = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 30, 50, 100 };
                            ConcurrentDictionary<int, Dictionary<string, double>> conceptSimilarityList_allEvents_noTopSelection = new ConcurrentDictionary<int, Dictionary<string, double>>();       // for temp storage to speed up, in the format of eventID : concepts' similarities
                            foreach (int nTop in nTopSelection)
                            {
                                RankConceptsForAllEvents(nTop, conceptSimilarityList_allEvents_noTopSelection);
                                if (!isUsePerEventConceptResponse && video_conceptScoreList != null && !video_conceptScoreList.ContainsKey(0))      // read only when no preload
                                    ReadVideoConceptScoreTable(conceptResp_TableFile, conceptResp_VideoListFile);
                                RankVideosForAllEvents(0);
                                if (isCalcMAP)
                                {
                                    ReadGroundTruth();
                                    CalculateMAPForAllEvents(nTop, 0);
                                }
                                if (isCalcInfMAP)
                                {
                                    ReadGroundTruth();
                                    CalculateInfAPForAllEvents(nTop, 0);
                                }
                            }
                        }
                    }
                    Console.WriteLine("<CommandFinished>");
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("ERR: " + ex.Message);
                throw ex;
            }

            Console.WriteLine("\nJob done.");
        }

        // Return processing step which is used in the interactive ranking, return 0 to exit
        private static int getCommandFromUserInput()
        {
            bool flagGetNextInput = true;
            int processingStep = 0;
            while (flagGetNextInput)
            {
                Console.Write(">>> ");
                string consoleLine = Console.ReadLine();
                string[] splitLine = consoleLine.Split(' ');
                string command = splitLine[0].Trim();

                if (command == "--loadscoretable")
                {
                    Debug.Assert(testID_k_range.Length == 0);           // no support for multiple score tables
                    ReadVideoConceptScoreTable(conceptResp_TableFile, conceptResp_VideoListFile);
                    Console.WriteLine("<CommandFinished>");
                }
                else if (command == "--procstep")
                {
                    try
                    {
                        processingStep = int.Parse(splitLine[1]);
                    }
                    catch (Exception)
                    {
                        ;
                    }

                    if (processingStep > 0 && processingStep <= 4)
                    {
                        if (processingStep == 3 || processingStep == 4)
                            if (video_conceptScoreList == null)
                            {
                                Console.WriteLine("Use --loadscoretable first.");
                                continue;
                            }
                        flagGetNextInput = false;
                    }
                    else
                        Console.WriteLine("Wrong processing step.");
                }
                else if (command == "--reloadquery")
                {
                    ParseEventNLPFile();
                    CreateWordList();
                    Console.WriteLine("<CommandFinished>");
                }
                else if (command == "--exit")
                {
                    processingStep = 0;
                    flagGetNextInput = false;
                }
                else
                {
                    if (command != "")
                        Console.WriteLine("Wrong command.");
                }
            }

            return processingStep;
        }



        public static void LoadMergedDatasetInfo()
        {
            mergedInfo_nConceptsInOrigin = new List<int>();
            Console.WriteLine("Loading merged dataset info...");

            using (StreamReader srInfo = new StreamReader(mergedInfo_nConceptsFile))
            {
                string line = null;
                int nTotalConcepts = 0;
                while ((line = srInfo.ReadLine()) != null)
                {
                    int nConcepts = int.Parse(line);
                    Debug.Assert(nConcepts > 0);

                    mergedInfo_nConceptsInOrigin.Add(nConcepts);
                    nTotalConcepts += nConcepts;
                }
                Debug.Assert(nTotalConcepts == conceptBank.Count);
            }

            Console.WriteLine();
        }

        public static void LoadIDFTable()
        {
            idfTable = new Dictionary<string, double>();

            Console.WriteLine("Loading IDF table...");
            using (StreamReader srIDF = new StreamReader(idfTableFile))
            {
                string line = null;
                while ((line = srIDF.ReadLine()) != null)
                {
                    if (line.StartsWith(";"))
                    {
                        if (line.StartsWith("; #TotalDocs = "))
                            idf_nTotalDocs = int.Parse(line.Substring(15));
                        if (line.StartsWith("; LogBase = "))
                            idf_logBase = double.Parse(line.Substring(12));

                        continue;
                    }

                    string[] splitLine = line.Split('\t');
                    Debug.Assert(splitLine.Length == 2);
                    idfTable.Add(splitLine[0], double.Parse(splitLine[1]));
                }
            }

            if (idf_nTotalDocs == 0 || idf_logBase == 0.0)
                throw new FormatException("Wrong IDF table file: missing header.");

            Console.WriteLine("Done. #TotalDocs = " + idf_nTotalDocs);
            Console.WriteLine();
        }

        private static void shuffleList<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = randgen.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }


        public static void CalculateInfAPForAllEvents(int nTopConcepts, int testID_k)
        {
            Console.WriteLine("Calculating infAP for each query...");
            string infAPFile;
            if (testID_k > 0)
            {
                if (nTopConcepts > 0)
                    infAPFile = mappingResultDir + "Result_infAP_Top" + nTopConcepts + "_k" + testID_k + ".txt";
                else
                    infAPFile = mappingResultDir + "Result_infAP_k" + testID_k + ".txt";
            }
            else
            {
                if (nTopConcepts > 0)
                    infAPFile = mappingResultDir + "Result_infAP_Top" + nTopConcepts + ".txt";
                else
                    infAPFile = mappingResultDir + "Result_infAP" + ".txt";
            }

            using (StreamWriter swAP = new StreamWriter(infAPFile))
            {
                swAP.WriteLine("<EventID>\t<EventName>\t<infAP>");

                int eventIndex;
                double meanInfAP = 0.0;
                for (eventIndex = 0; eventIndex < videoRankingList_allEvents.Count; eventIndex++)
                {
                    List<string> videoRankingList = videoRankingList_allEvents[eventIndex + 1];
                    if (isTestRandomResult)
                        shuffleList<string>(videoRankingList);

                    double infAP = 0.0;
                    int num_dPool = 0;
                    int num_dRel = 0;
                    int num_dNonrel = 0;
                    int rank_k = 0;

                    HashSet<string> usedRealVideoNames = new HashSet<string>();
                    foreach (string videoName in videoRankingList)
                    {
                        string[] splitVideoName = videoName.Split('_');
                        string realVideoName = videoName;
                        if (splitVideoName.Length == 4)                 //! handle NRKF keyframes
                            realVideoName = splitVideoName[0] + "_" + splitVideoName[1] + "_RKF";
                        Debug.Assert(realVideoName.EndsWith("_RKF"));
                        if (!usedRealVideoNames.Contains(realVideoName))
                            usedRealVideoNames.Add(realVideoName);
                        else
                            continue;                                   //! skip duplicates (use the highest score for the same shot among RKF and NRKF)

                        rank_k++;
                        if (event_positiveTruthList[eventIndex].Contains(realVideoName))
                        {
                            if (rank_k != 1)
                                infAP += 1.0 / rank_k + ((double)(rank_k - 1) / rank_k) * ((double)(num_dPool) / (rank_k - 1) * ((num_dRel + 0.001) / (num_dRel + num_dNonrel + 0.002)));
                            else
                                infAP += 1.0;
                        }

                        if (event_positiveTruthList[eventIndex].Contains(realVideoName))
                        {
                            num_dPool++;
                            num_dRel++;
                        }
                        else if (event_negativeTruthList[eventIndex].Contains(realVideoName))
                        {
                            num_dPool++;
                            num_dNonrel++;
                        }
                        else if (event_unjudgedTruthList[eventIndex].Contains(realVideoName))
                        {
                            num_dPool++;
                        }
                    }

                    infAP /= event_positiveTruthList[eventIndex].Count;
                    meanInfAP += infAP;

                    string eventName = eventkitList[eventIndex].EventNameString;
                    swAP.WriteLine("{0}\t{1}\t{2}", eventIndex + 1, eventName.Replace(' ', '_'), infAP);
                }
                meanInfAP /= eventIndex;
                swAP.WriteLine();
                swAP.WriteLine("====================================");
                swAP.WriteLine("MinfAP =\t" + meanInfAP);

                Console.ForegroundColor = ConsoleColor.Green;
                if (nTopConcepts > 0)
                    Console.WriteLine("\nWith selector of Top {0} concepts:", nTopConcepts);
                Console.WriteLine("MinfAP = " + meanInfAP);
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        public static void CalculateMAPForAllEvents(int nTopConcepts, int testID_k)
        {
            Console.WriteLine("Calculating MAP for each event...");
            string averagePrecisionFile;

            if (testID_k > 0)
            {
                if (nTopConcepts > 0)
                    averagePrecisionFile = mappingResultDir + "Result_MAP_Top" + nTopConcepts + "_k" + testID_k + ".txt";
                else
                    averagePrecisionFile = mappingResultDir + "Result_MAP_k" + testID_k + ".txt";
            }
            else
            {
                if (nTopConcepts > 0)
                    averagePrecisionFile = mappingResultDir + "Result_MAP_Top" + nTopConcepts + ".txt";
                else
                    averagePrecisionFile = mappingResultDir + "Result_MAP" + ".txt";
            }

            using (StreamWriter swAP = new StreamWriter(averagePrecisionFile))
            {
                swAP.WriteLine("<EventID>\t<EventName>\t<AP>");

                int eventIndex;
                double meanAveragePrecision = 0.0;
                for (eventIndex = 0; eventIndex < videoRankingList_allEvents.Count; eventIndex++)
                {
                    List<string> videoRankingList = videoRankingList_allEvents[eventIndex + 1];
                    if (isTestRandomResult)
                        shuffleList<string>(videoRankingList);

                    int count_correct = 0;
                    int count_parsed = 0;
                    int count_groundtruth = event_positiveTruthList[eventIndex].Count;
                    double averagePrecision = 0.0;

                    foreach (string videoName in videoRankingList)
                    {
                        if (event_positiveTruthList[eventIndex].Contains(videoName))
                        {
                            count_correct++;
                            count_parsed++;
                            double precision = (double)count_correct / (double)count_parsed;
                            averagePrecision += precision;
                        }
                        else if (event_negativeTruthList[eventIndex].Contains(videoName))
                        {
                            count_parsed++;
                        }
                        else
                        {
                            ;       // skip as not given in the ground truth
                        }

                        if (count_correct == count_groundtruth)
                            break;
                    }

                    averagePrecision /= count_groundtruth;
                    meanAveragePrecision += averagePrecision;

                    string eventName = eventkitList[eventIndex].EventNameString;
                    swAP.WriteLine("{0}\t{1}\t{2}", eventIndex + 1, eventName.Replace(' ', '_'), averagePrecision);
                }
                meanAveragePrecision /= eventIndex;
                swAP.WriteLine();
                swAP.WriteLine("====================================");
                swAP.WriteLine("MAP =\t" + meanAveragePrecision);

                Console.ForegroundColor = ConsoleColor.Green;
                if (nTopConcepts > 0)
                    Console.WriteLine("\nWith selector of Top {0} concepts:", nTopConcepts);
                Console.WriteLine("MAP = " + meanAveragePrecision);
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        public static void ReadGroundTruth()
        {
            Console.WriteLine("Loading ground truth...");

            event_positiveTruthList = new Dictionary<int, HashSet<string>>();
            event_negativeTruthList = new Dictionary<int, HashSet<string>>();
            event_unjudgedTruthList = new Dictionary<int, HashSet<string>>();

            if (!isMEDQuery)
            {
                using (StreamReader srGroundTruth = new StreamReader(groundTruthFile))
                {
                    string line = null;
                    //bool isFirstLine = true;
                    while ((line = srGroundTruth.ReadLine()) != null)
                    {
                        //if (isFirstLine)
                        //{
                        //    isFirstLine = false;
                        //    continue;
                        //}

                        string[] splitLine = line.Trim().Split(',');
                        Debug.Assert(splitLine.Length == 4);
                        Debug.Assert(splitLine[1] == "0");

                        string videoIDStr = splitLine[2];
                        string videoName = videoIDStr + "_RKF";
                        int eventIndex = int.Parse(splitLine[0]) - queryIDOffset - 1;               //!
                        string posNegFlag = splitLine[3];

                        if (posNegFlag == "0")              // irrelevant
                        {
                            if (!event_negativeTruthList.ContainsKey(eventIndex))
                                event_negativeTruthList.Add(eventIndex, new HashSet<string>());
                            event_negativeTruthList[eventIndex].Add(videoName);
                        }
                        else if (posNegFlag == "1")         // relevant
                        {
                            if (!event_positiveTruthList.ContainsKey(eventIndex))
                                event_positiveTruthList.Add(eventIndex, new HashSet<string>());
                            event_positiveTruthList[eventIndex].Add(videoName);
                        }
                        else if (posNegFlag == "-1")        // not judged
                        {
                            if (!event_unjudgedTruthList.ContainsKey(eventIndex))
                                event_unjudgedTruthList.Add(eventIndex, new HashSet<string>());
                            event_unjudgedTruthList[eventIndex].Add(videoName);
                        }
                        else
                            throw new FormatException("Error reading ground truth file.");
                    }
                }
            }
            else
            {
                using (StreamReader srGroundTruth = new StreamReader(groundTruthFile))
                {
                    string line = null;
                    bool isFirstLine = true;
                    while ((line = srGroundTruth.ReadLine()) != null)
                    {
                        if (isFirstLine)
                        {
                            isFirstLine = false;
                            continue;
                        }

                        string[] splitLine = line.Trim().Split(new char[] { '.', ',', '\"' });
                        Debug.Assert(splitLine.Length == 7);

                        string videoIDStr = splitLine[1];
                        string videoName = "HVC" + videoIDStr;
                        int eventIndex = int.Parse(splitLine[2].TrimStart('E')) - queryIDOffset - 1;               //!
                        string posNegFlag = splitLine[5];

                        if (posNegFlag == "n")
                        {
                            if (!event_negativeTruthList.ContainsKey(eventIndex))
                                event_negativeTruthList.Add(eventIndex, new HashSet<string>());
                            event_negativeTruthList[eventIndex].Add(videoName);
                        }
                        else if (posNegFlag == "y")
                        {
                            if (!event_positiveTruthList.ContainsKey(eventIndex))
                                event_positiveTruthList.Add(eventIndex, new HashSet<string>());
                            event_positiveTruthList[eventIndex].Add(videoName);
                        }
                        else
                            throw new FormatException("Error reading ground truth file.");
                    }
                }
            }

            Console.WriteLine("Writing ground truth info...");

            string groundTruthInfoFile = mappingResultDir + "GroundTruth_Info" + ".txt";
            using (StreamWriter swInfo = new StreamWriter(groundTruthInfoFile))
            {
                swInfo.WriteLine("<EventIdx>\t<#Positive>\t<#Negative>");
                Debug.Assert(event_positiveTruthList.Count == event_negativeTruthList.Count);
                foreach (int eventIndex in event_positiveTruthList.Keys)
                {
                    swInfo.WriteLine("{0}\t{1}\t{2}", eventIndex, event_positiveTruthList[eventIndex].Count, event_negativeTruthList[eventIndex].Count);
                }
            }

            Console.WriteLine();
        }

        private static void readIrrelevantConceptEnergy(int eventID)
        {
            irrelevantConceptEnergy[eventID] = new Dictionary<string, double>();
            string thisIrrConceptEnergyFile = irrelevantEnergyScoreFile.Replace("%d", eventID.ToString());
            using (StreamReader srIrrEnergy = new StreamReader(thisIrrConceptEnergyFile))
            {
                string line = null;
                while ((line = srIrrEnergy.ReadLine()) != null)
                {
                    string[] splitLine = line.Split(':');
                    Debug.Assert(splitLine.Length == 2);

                    string videoName = splitLine[0].Trim();
                    double score = double.Parse(splitLine[1]);
                    irrelevantConceptEnergy[eventID].Add(videoName, score);
                }
            }
        }


        // Irrelevant concept list format: {concept_id}\t{concept_relevance}\t{concept_name}
        private static void readIrrelevantConceptList(int eventID)
        {
            irrelevantConceptIDList[eventID] = new HashSet<int>();
            string thisIrrConceptListFile = irrelevantConceptTableFile.Replace("%d", eventID.ToString());
            using (StreamReader srIrrConcepts = new StreamReader(thisIrrConceptListFile))
            {
                string line = null;
                while ((line = srIrrConcepts.ReadLine()) != null)
                {
                    string[] splitLine = line.Split('\t');
                    Debug.Assert(splitLine.Length == 3);

                    double relevance = double.Parse(splitLine[1]);
                    if (relevance < thresh_irrelevantConcepts && relevance > 0 - 1e-7)
                        irrelevantConceptIDList[eventID].Add(int.Parse(splitLine[0]));
                }
            }

            Console.WriteLine("      - got {0} irrelevant concepts for Query {1}", irrelevantConceptIDList[eventID].Count, eventID);
        }
        
        private static Dictionary<int, double> makeSparseConceptSimilarityList(Dictionary<string, double> conceptSimilarityList)
        {
            Dictionary<int, double> conceptSimilarityList_sparse = new Dictionary<int, double>();
            Debug.Assert(conceptSimilarityList.Count == conceptBank.Count);

            int index = 0;
            foreach (double weight in conceptSimilarityList.Values)          // assume that concepts in conceptSimilarityList are in the original order; do not care about concept names
            {
                if (weight != 0.0)
                    conceptSimilarityList_sparse.Add(index, weight);
                index++;
            }
            return conceptSimilarityList_sparse;
        }

        private static void handleParallelVideoRanking(int eventID, int testID_k, string rankedVideosDir, ref ConcurrentBag<bool> returnOfThis)
        {
            Dictionary<string, double> conceptSimilarityList = conceptSimilarityList_allEvents[eventID];
            Dictionary<int, double> conceptSimilarityList_sparse = makeSparseConceptSimilarityList(conceptSimilarityList);
#if DOONEEVENT
	                if (eventID != 1)
	                    continue;
#endif

            int eventIndex = eventID - 1;
            string eventName = eventkitList[eventIndex].EventNameString;
            Console.WriteLine("   - at Query {0}: {1}", eventID, eventName);

            // Read score table if per-event score is used
            int scoreTableID;
            if (isUsePerEventConceptResponse)
            {
                scoreTableID = eventID;
                if (!ReadVideoConceptScoreTable(eventID, testID_k))
                    returnOfThis.Add(false);               // most possible case: testID_k does not exist
            }
            else
                scoreTableID = 0;       // common score table is used for all the events

            // Read irrelevant concepts
            if (thresh_irrelevantConcepts > 0)
                readIrrelevantConceptList(eventID);

            // Read irrelevant concept energy
            if (isOffsetByIrrelevConceptEnergy)
                readIrrelevantConceptEnergy(eventID);

            // For hand-picked concepts
            Dictionary<int, double[]> handPickedConceptIDGroupList = null;
            bool isHandPickGrouped = false;
            HashSet<int> groupIDSet = new HashSet<int>();
            if (isUseHandPickedConcepts)
            {
                handPickedConceptIDGroupList = readHandPickedConceptIDs(eventID);
                foreach (double[] tuple in handPickedConceptIDGroupList.Values)
                {
                    int groupID = (int)tuple[0];
                    if (groupID != -1)
                        isHandPickGrouped = true;
                    groupIDSet.Add(groupID);
                }
            }

            Dictionary<string, double> video_rankScore = new Dictionary<string, double>();
            foreach (KeyValuePair<string, Dictionary<int, float>> kvp in video_conceptScoreList[scoreTableID])
            {
                // For each video:

                string thisVideoName = kvp.Key;
                Dictionary<int, float> conceptScoreVector = kvp.Value;
                double videoScore = calcVideoScoreByDotProduct(eventID, conceptSimilarityList_sparse, conceptScoreVector);
                //if (!isHandPickGrouped)
                //    videoScore = calcVideoScoreByDotProduct(eventID, conceptSimilarityList, conceptScoreVector);
                //else
                //    videoScore = calcVideoScoreByIntergroupHarmonicMean(eventID, conceptSimilarityList, conceptScoreVector, handPickedConceptIDGroupList);

                if (isOffsetByIrrelevConceptEnergy)
                    videoScore -= weight_irrelevantEnergy * irrelevantConceptEnergy[eventID][thisVideoName];

                video_rankScore.Add(thisVideoName, videoScore);
            }

            // Rank videos for this event
            IEnumerable<string> rankedVideoList =
                from v in video_rankScore
                orderby v.Value descending
                select v.Key;

            // Re-rank videos according to hand specified groups
            if (isHandPickGrouped && thresh_rerankByGroup > 0)
            {
                int videoRank = 0;
                foreach (string topVideoName in rankedVideoList)
                {
                    videoRank++;
                    if (videoRank > thresh_rerankByGroup)           // re-rank top k videos only
                        break;

                    Dictionary<int, float> conceptScoreVector = video_conceptScoreList[scoreTableID][topVideoName];
                    foreach (int groupID in groupIDSet)
                    {
                        if (groupID <= 0)
                            continue;

                        double videoScore_add = calcVideoScoreByDotProductOnGroupedConcepts(eventID, conceptSimilarityList_sparse, conceptScoreVector, handPickedConceptIDGroupList, groupID);
                        video_rankScore[topVideoName] += 100.0 * videoScore_add;            //! adjust re-rank weight here
                    }
                }

                // Re-ranking
                rankedVideoList =
                    from v in video_rankScore
                    orderby v.Value descending
                    select v.Key;
            }

            videoRankingList_allEvents[eventID] = new List<string>(rankedVideoList);

            // Write video ranking list to file
            string videoRankingListFile = rankedVideosDir + String.Format("Q-{0}.txt", eventID);
            using (StreamWriter swRankList = new StreamWriter(videoRankingListFile))
            {
                foreach (string videoName in rankedVideoList)
                {
                    double score = video_rankScore[videoName];
                    swRankList.WriteLine(videoName + "\t" + score);
                }
            }

            returnOfThis.Add(true);
        }

        private static double calcVideoScoreByDotProductOnGroupedConcepts(int eventID, Dictionary<int, double> conceptSimilarityList_sparse, Dictionary<int, float> conceptScoreVector, Dictionary<int, double[]> handPickedConceptIDGroupList, int groupID)
        {
            double videoScore = 0.0;
            //Debug.Assert(conceptSimilarityList.Count == conceptBank.Count);
            foreach (KeyValuePair<int, double> kvp in conceptSimilarityList_sparse)
            {
                int dimensionIndex = kvp.Key;
                double thisConceptWeight = kvp.Value;

                if (handPickedConceptIDGroupList.ContainsKey(dimensionIndex + 1) && handPickedConceptIDGroupList[dimensionIndex + 1][0] >= groupID && conceptScoreVector.ContainsKey(dimensionIndex))
                {
                    Debug.Assert(thisConceptWeight > 0.0);
                    videoScore += thisConceptWeight * conceptScoreVector[dimensionIndex];           // dot product measurement
                }
            }
            //Debug.Assert(dimensionIndex == conceptScoreVector.Count);
            return videoScore;
        }

        private static double calcVideoScoreByDotProduct(int eventID, Dictionary<int, double> conceptSimilarityList_sparse, Dictionary<int, float> conceptScoreVector)
        {
            double videoScore = 0.0;
            //Debug.Assert(conceptSimilarityList.Count == conceptBank.Count);
            foreach (KeyValuePair<int, double> kvp in conceptSimilarityList_sparse)
            {
                int dimensionIndex = kvp.Key;
                double thisConceptWeight = kvp.Value;

                if (!conceptScoreVector.ContainsKey(dimensionIndex))
                    continue;

                videoScore += thisConceptWeight * conceptScoreVector[dimensionIndex];           // dot product measurement
                if (thresh_irrelevantConcepts > 0 && irrelevantConceptIDList[eventID].Contains(dimensionIndex + 1))              // this is an irrelevant concept
                    videoScore -= 1.0 * conceptScoreVector[dimensionIndex];                     // punish the score by 1.0 x {concept_response} (as a way of reranking)
            }
            //Debug.Assert(dimensionIndex == conceptScoreVector.Count);
            return videoScore;
        }

        private static double calcVideoScoreByIntergroupHarmonicMean(int eventID, Dictionary<string, double> conceptSimilarityList, List<float> conceptScoreVector, Dictionary<int, int> handPickedConceptIDGroupList)
        {
            int dimensionIndex = 0;
            Debug.Assert(conceptSimilarityList.Count == conceptBank.Count);
            Dictionary<int, double> groupedSum = new Dictionary<int, double>();
            //Dictionary<int, int> groupedCount = new Dictionary<int, int>();
            foreach (double thisConceptWeight in conceptSimilarityList.Values)          // assume that concepts in conceptSimilarityList are in the original order; do not care about concept names
            {
                if (handPickedConceptIDGroupList.ContainsKey(dimensionIndex + 1))
                {
                    Debug.Assert(thisConceptWeight > 0.0);
                    int groupID = handPickedConceptIDGroupList[dimensionIndex + 1];
                    if (!groupedSum.ContainsKey(groupID))
                    {
                        groupedSum.Add(groupID, 0.0);
                        //groupedCount.Add(groupID, 0);
                    }
                    groupedSum[groupID] += thisConceptWeight * conceptScoreVector[dimensionIndex];          // dot product measurement
                    //groupedCount[groupID]++;
                    if (thresh_irrelevantConcepts > 0 && irrelevantConceptIDList[eventID].Contains(dimensionIndex + 1))              // this is an irrelevant concept
                        groupedSum[groupID] -= 1.0 * conceptScoreVector[dimensionIndex];                    // punish the score by 1.0 x {concept_response} (as a way of reranking)
                }
                else
                    Debug.Assert(thisConceptWeight == 0.0);         // require every concept to be grouped

                dimensionIndex++;
            }
            Debug.Assert(dimensionIndex == conceptScoreVector.Count);

            List<double> scoreList = new List<double>();
            foreach (KeyValuePair<int, double> kvp in groupedSum)
            {
                //int groupID = kvp.Key;
                double score = kvp.Value;

                scoreList.Add(score);
            }

            return geomatricMean(scoreList);
        }

        private static double harmonicMean(IEnumerable<double> n)
        {
            return n.Count() / n.Sum(i => 1 / i);
        }

        private static double geomatricMean(IEnumerable<double> n)
        {
            return Math.Pow(n.Aggregate((s, i) => s * i), 1.0 / n.Count());
        }

        public static bool RankVideosForAllEvents(int testID_k)
        {
            Console.WriteLine("Ranking videos for all queries...");
            videoRankingList_allEvents = new ConcurrentDictionary<int, List<string>>();
            string rankedVideosDir = mappingResultDir + "RankedVideos" + dirSeperator;
            Directory.CreateDirectory(rankedVideosDir);

            ParallelOptions parallelOptions = new ParallelOptions();
            parallelOptions.MaxDegreeOfParallelism = 4;
            ConcurrentBag<bool> returnOfVideoRankingHandler = new ConcurrentBag<bool>();
            List<int> eventIDList = new List<int>();
            for (int eventID = 1; eventID <= conceptSimilarityList_allEvents.Count; eventID++)
                eventIDList.Add(eventID);
            if (isUsePerEventConceptResponse)
                video_conceptScoreList = new ConcurrentDictionary<int, Dictionary<string, Dictionary<int, float>>>();          // initialize
            irrelevantConceptIDList = new ConcurrentDictionary<int, HashSet<int>>();
            irrelevantConceptEnergy = new ConcurrentDictionary<int, Dictionary<string, double>>();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Parallel.ForEach<int>(eventIDList, parallelOptions, (eventID) =>
            {
                handleParallelVideoRanking(eventID, testID_k, rankedVideosDir, ref returnOfVideoRankingHandler);
            });

            foreach (bool returnValue in returnOfVideoRankingHandler)
            {
                if (returnValue == false)
                    return false;
            }

            stopwatch.Stop();

            Console.WriteLine("Done.");
            Console.WriteLine("Video ranking time spent: {0}.", stopwatch.Elapsed);
            Console.WriteLine();
            return true;
        }

        private static Dictionary<string, double> getNormalizedRankingList(Dictionary<string, double> videoRankList)
        {
            double maxScore = double.MinValue;
            double minScore = double.MaxValue;

            foreach (double score in videoRankList.Values)
            {
                if (score > maxScore)
                    maxScore = score;
                if (score < minScore)
                    minScore = score;
            }

            double range = maxScore - minScore;

            Dictionary<string, double> normRankList = new Dictionary<string, double>();

            foreach (KeyValuePair<string, double> kvp in videoRankList)
            {
                string videoName = kvp.Key;
                double score = kvp.Value;

                double normScore = 0.0;

                if (range != 0.0)
                {
                    normScore = (score - minScore) / range;
                }
                else
                    normScore = 0.0;
                Debug.Assert(normScore >= 0.0 && normScore <= 1.0);                 //! comment this line to speed up

                normRankList.Add(videoName, normScore);
            }

            return normRankList;
        }

        public static bool ReadVideoConceptScoreTable(int eventID, int testID_k)
        {
            Console.WriteLine("      - loading per-query score table for Query {0}...", eventID);
            video_conceptScoreList[eventID] = new Dictionary<string, Dictionary<int, float>>();
            //conceptName_dimensionIndex = new Dictionary<string, int>();

            string conceptResp_TableFile_thisEvent;
            if (testID_k != 0)
                conceptResp_TableFile_thisEvent = conceptResp_TableFile.Replace("%EID", String.Format("E{0:D3}", eventID + queryIDOffset)).Replace("%d", testID_k.ToString());
            else
                conceptResp_TableFile_thisEvent = conceptResp_TableFile.Replace("%EID", String.Format("E{0:D3}", eventID + queryIDOffset));

            string conceptResp_VideoListFile_thisEvent;
            if (testID_k != 0)
                conceptResp_VideoListFile_thisEvent = conceptResp_VideoListFile.Replace("%EID", String.Format("E{0:D3}", eventID + queryIDOffset)).Replace("%d", testID_k.ToString());
            else
                conceptResp_VideoListFile_thisEvent = conceptResp_VideoListFile.Replace("%EID", String.Format("E{0:D3}", eventID + queryIDOffset));

            if (!File.Exists(conceptResp_TableFile_thisEvent))
                return false;

            using (StreamReader srVideoName = new StreamReader(conceptResp_VideoListFile_thisEvent))
            {
                using (StreamReader srScoreTable = new StreamReader(conceptResp_TableFile_thisEvent))
                {
                    string line_videoList = null;
                    while ((line_videoList = srVideoName.ReadLine()) != null)
                    {
                        string thisVideoName = line_videoList.Trim();

                        string thisVideoScoreVectorStr = srScoreTable.ReadLine().Trim();
                        string[] splitScoreVectorStr = thisVideoScoreVectorStr.Split(',');

                        Dictionary<int, float> scoreVector = new Dictionary<int, float>(splitScoreVectorStr.Length);
                        foreach (string scoreTuple in splitScoreVectorStr)
                        {
                            string[] splitScoreTuple = scoreTuple.Split(':');
                            Debug.Assert(splitScoreTuple.Length == 2);
                            int index = int.Parse(splitScoreTuple[0]);
                            float score = float.Parse(splitScoreTuple[1]);
                            scoreVector.Add(index, score);
                        }
                        video_conceptScoreList[eventID].Add(thisVideoName, scoreVector);
                    }
                    Debug.Assert(srScoreTable.ReadLine() == null);
                }
            }

            Console.WriteLine("      - done loading of score table for Query {0}", eventID);
            return true;
        }

        // Single score table for all events
        public static void ReadVideoConceptScoreTable(string conceptResp_TableFile, string conceptResp_VideoListFile)
        {
            Console.WriteLine("Reading video concept score table...");
            video_conceptScoreList = new ConcurrentDictionary<int, Dictionary<string, Dictionary<int, float>>>();
            video_conceptScoreList[0] = new Dictionary<string, Dictionary<int, float>>(340000);          //! eventID = 0, which means a common score table is used for each event (default); default size assumed
            //conceptName_dimensionIndex = new Dictionary<string, int>();

            using (StreamReader srVideoName = new StreamReader(conceptResp_VideoListFile))
            {
                using (StreamReader srScoreTable = new StreamReader(conceptResp_TableFile))
                {
                    string line_videoList = null;
                    while ((line_videoList = srVideoName.ReadLine()) != null)
                    {
                        string thisVideoName = line_videoList.Trim();

                        string thisVideoScoreVectorStr = srScoreTable.ReadLine().Trim();
                        string[] splitScoreVectorStr = thisVideoScoreVectorStr.Split(',');

                        Dictionary<int, float> scoreVector = new Dictionary<int, float>(splitScoreVectorStr.Length);
                        foreach (string scoreTuple in splitScoreVectorStr)
                        {
                            string[] splitScoreTuple = scoreTuple.Split(':');
                            Debug.Assert(splitScoreTuple.Length == 2);
                            int index = int.Parse(splitScoreTuple[0]);
                            float score = float.Parse(splitScoreTuple[1]);
                            scoreVector.Add(index, score);
                        }
                        video_conceptScoreList[0].Add(thisVideoName, scoreVector);
                    }
                    Debug.Assert(srScoreTable.ReadLine() == null);
                }
            }

            Console.WriteLine();
        }

        private static Dictionary<int, double[]> readHandPickedConceptIDs(int eventID)
        {
            Dictionary<int, double[]> conceptIDList = new Dictionary<int, double[]>();            // <conceptID, groupID>
            string handPickedConceptIDListFile = conceptHandPickedPerQueryFile.Replace("%d", eventID.ToString());

            if (File.Exists(handPickedConceptIDListFile))
            {
                using (StreamReader srHandPicked = new StreamReader(handPickedConceptIDListFile))
                {
                    string line = null;
                    while ((line = srHandPicked.ReadLine()) != null)
                    {
                        if (line.StartsWith(";"))
                            continue;

                        string[] splitLine = line.Trim().Split('\t');
                        if (splitLine.Length == 1)
                            conceptIDList.Add(int.Parse(splitLine[0]), new double[] { -1.0, -1.0 });              // groupID is default to -1 if not provided
                        else if (splitLine.Length == 2)
                            conceptIDList.Add(int.Parse(splitLine[0]), new double[] { double.Parse(splitLine[1]), -1.0 });
                        else if (splitLine.Length == 3)
                            conceptIDList.Add(int.Parse(splitLine[0]), new double[] { double.Parse(splitLine[1]), double.Parse(splitLine[2]) });
                        else
                            throw new FormatException("Wrong format in hand-picked concept list.");
                    }
                }
            }

            if (conceptIDList.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("      - found {0} hand-picked concepts for Query {1}", conceptIDList.Count, eventID);
                Console.ResetColor();
            }

            return conceptIDList;
        }

        private static void setSimilarityToUserDefined(Dictionary<int, double[]> reservedConceptIDList, ref Dictionary<string, double> conceptSimilarityList)
        {
            if (reservedConceptIDList.Count == 0)           // do nothing if no hand-picked concepts found
                return;

            Dictionary<string, double> weightResetConcepts = new Dictionary<string, double>();

            int conceptID = 0;
            foreach (string conceptNameStr in conceptSimilarityList.Keys)
            {
                conceptID++;
                if (!reservedConceptIDList.ContainsKey(conceptID))
                    continue;
                if (reservedConceptIDList[conceptID][1] == -1.0)        // no user defined weight for this concept
                    continue;

                weightResetConcepts.Add(conceptNameStr, reservedConceptIDList[conceptID][1]);
            }

            foreach (KeyValuePair<string, double> kvp in weightResetConcepts)
            {
                conceptSimilarityList[kvp.Key] = kvp.Value;
            }
        }

        private static void setSimilarityToZero(Dictionary<int, double[]> reservedConceptIDList, ref Dictionary<string, double> conceptSimilarityList)
        {
            if (reservedConceptIDList.Count == 0)           // do nothing if no hand-picked concepts found
                return;

            HashSet<string> maskedConceptNames = new HashSet<string>();

            int conceptID = 1;
            foreach (string conceptNameStr in conceptSimilarityList.Keys)
            {
                if (!reservedConceptIDList.ContainsKey(conceptID))
                    maskedConceptNames.Add(conceptNameStr);
                conceptID++;
            }

            foreach (string maskedName in maskedConceptNames)
            {
                conceptSimilarityList[maskedName] = 0.0;
            }
        }

        private static void handleParallelConceptSimilarityRanking(EventKitDescription eventKit, StopWord stopword, ref ConcurrentDictionary<int, Dictionary<string, double>> conceptSimilarityList_allEvents_noTopSelection, ref ConcurrentDictionary<int, int> eventID_matchedConceptCount, int nTopConcepts, string rankedConceptsDir)
        {
            // ------------------------------------------------------------------------------------------------
            //  Notes for parallelization:
            //     - conceptSimilarityList_allEvents_noTopSelection assumes that events are put in order
            //     - swMatchedConceptCount assumes that count for each event is written in order
            //     - (global) conceptSimilarityList_allEvents assumes that events are put in order
            // ------------------------------------------------------------------------------------------------

            int eventID = eventkitList.IndexOf(eventKit) + 1;            // this is the internal eventID, not real eventID
#if DOONEEVENT
	                if (eventID != 1)
	                    continue;
#endif

            Console.WriteLine("   - at Query {0}: {1}", eventID, eventKit.EventNameString);
            readConceptBlacklistForEvent(eventID, stopword);

            if (isWordNetSynsets)
            {
                Dictionary<string, string> debug_maxConceptListInEvent = null;
                Dictionary<string, double> conceptSimilarityList;
                if (conceptSimilarityList_allEvents_noTopSelection.Count != eventkitList.Count)
                {
                    if (switch_similarity == 1)
                        conceptSimilarityList = maxSimilarity(eventKit.WordList_WithoutExplication, conceptBank.WordList_ImageNet, eventKit.NormTfTable, eventKit.EventNameTermSet, eventKit.EventDefinitionTermSet, out debug_maxConceptListInEvent);        // for WordNet synsets
                    else if (switch_similarity == 2)
                        conceptSimilarityList = meanSimilarity(eventKit.WordList_WithoutExplication, conceptBank.WordList_ImageNet, eventKit.NormTfTable, eventKit.EventNameTermSet, eventKit.EventDefinitionTermSet);      // for WordNet synsets
                    else if (switch_similarity == 3)
                        conceptSimilarityList = maxTopSimilarity(eventKit.WordList_WithoutExplication, conceptBank.WordList_ImageNet, eventKit.NormTfTable, eventKit.EventNameTermSet, eventKit.EventDefinitionTermSet, out debug_maxConceptListInEvent);     // for WordNet synsets
                    else if (switch_similarity == 4 || switch_similarity == 5 || switch_similarity == 6)
                        conceptSimilarityList = poolingConcepts(eventKit.WordList_WithoutExplication, conceptBank.WordList_ImageNet, eventKit.NormTfTable, eventKit.EventNameTermSet, eventKit.EventDefinitionTermSet);     // for WordNet synsets
                    else
                        conceptSimilarityList = null;
                    Debug.Assert(conceptSimilarityList.Count == conceptBank.Count);

                    conceptSimilarityList_allEvents_noTopSelection[eventID] = new Dictionary<string, double>(conceptSimilarityList);
                }
                else
                {
                    conceptSimilarityList = new Dictionary<string, double>(conceptSimilarityList_allEvents_noTopSelection[eventID]);            // already calculated for this event
                }

                // Index concept ID
                Dictionary<string, int> conceptNameStr_conceptID = new Dictionary<string, int>();
                int conceptID = 1;
                foreach (string conceptNameStr in conceptSimilarityList.Keys)
                {
                    conceptNameStr_conceptID.Add(conceptNameStr, conceptID);
                    conceptID++;
                }

                // For hand-picked concepts
                if (isUseHandPickedConcepts)
                {
                    Dictionary<int, double[]> handPickedConceptIDList = readHandPickedConceptIDs(eventID);
                    setSimilarityToZero(handPickedConceptIDList, ref conceptSimilarityList);
                    setSimilarityToUserDefined(handPickedConceptIDList, ref conceptSimilarityList);
                }

                // Rank concepts according to similarities
                IEnumerable<string> rankedConceptNames_a =
                    from c in conceptSimilarityList
                    orderby c.Value descending
                    select c.Key;
                List<string> rankedConceptNames = new List<string>(rankedConceptNames_a);

                eventID_matchedConceptCount[eventID] = countMatchedConcepts(conceptSimilarityList, rankedConceptNames);

                if (nTopConcepts > 0)
                {
                    int countSkipped = 0;
                    int thisConceptIndex = 0;
                    bool flag_beginToSet0 = false;
                    foreach (string thisConceptName in rankedConceptNames)
                    {
                        countSkipped++;
                        if (countSkipped > nTopConcepts)
                        {
                            double thisConceptSimilr = conceptSimilarityList[thisConceptName];
                            Debug.Assert(rankedConceptNames[thisConceptIndex] == thisConceptName);
                            double lastConceptSimilr;
                            if (thisConceptIndex != 0)
                                lastConceptSimilr = conceptSimilarityList[rankedConceptNames[thisConceptIndex - 1]];
                            else        // the first one
                                lastConceptSimilr = -1.0;

                            if (isExtendTopConceptsByConceptSimilarity)
                            {
                                if (lastConceptSimilr != thisConceptSimilr || flag_beginToSet0)
                                {
                                    conceptSimilarityList[thisConceptName] = 0.0;
                                    flag_beginToSet0 = true;
                                }
                            }
                            else
                                conceptSimilarityList[thisConceptName] = 0.0;
                        }

                        thisConceptIndex++;
                    }
                }
                conceptSimilarityList_allEvents[eventID] = conceptSimilarityList;

                if (processingStep == 2 || processingStep == 4)
                {
                    string rankedConceptListFile = rankedConceptsDir + String.Format("Q-{0}.txt", eventID);
                    using (StreamWriter swRankedConceptList = new StreamWriter(rankedConceptListFile))
                    {
                        foreach (string thisConceptName in rankedConceptNames)
                        {
                            double score = conceptSimilarityList[thisConceptName];
                            int thisConceptID = conceptNameStr_conceptID[thisConceptName];

                            if (switch_similarity == 1 || switch_similarity == 3)
                            {
                                string maxConceptInEvent = debug_maxConceptListInEvent[thisConceptName];
                                swRankedConceptList.WriteLine(thisConceptName + "\t" + thisConceptID + "\t" + score + "\t(" + maxConceptInEvent + ")");
                            }
                            else
                            {
                                swRankedConceptList.WriteLine(thisConceptName + "\t" + thisConceptID + "\t" + score);
                            }
                        }
                    }
                }
            }
            else        // not WordNet synsets
            {
                Dictionary<string, string> debug_maxConceptListInEvent = null;
                Dictionary<string, double> conceptSimilarityList;
                if (conceptSimilarityList_allEvents_noTopSelection.Count != eventkitList.Count)
                {
                    if (switch_similarity == 1)
                        conceptSimilarityList = maxSimilarity(eventKit.WordList_WithoutExplication, conceptBank.ConceptList, eventKit.NormTfTable, eventKit.VerbList, eventKit.EventNameTermSet, eventKit.EventDefinitionTermSet, stopword, out debug_maxConceptListInEvent, conceptBlacklist_perEvent[eventID]);
                    else if (switch_similarity == 2)
                        conceptSimilarityList = meanSimilarity(eventKit.WordList_WithoutExplication, conceptBank.ConceptList, eventKit.NormTfTable, eventKit.VerbList, eventKit.EventNameTermSet, eventKit.EventDefinitionTermSet, stopword, conceptBlacklist_perEvent[eventID]);
                    else if (switch_similarity == 3)
                        conceptSimilarityList = maxTopSimilarity(eventKit.WordList_WithoutExplication, conceptBank.ConceptList, eventKit.NormTfTable, eventKit.VerbList, eventKit.EventNameTermSet, eventKit.EventDefinitionTermSet, stopword, out debug_maxConceptListInEvent, conceptBlacklist_perEvent[eventID]);
                    else if (switch_similarity == 4 || switch_similarity == 5 || switch_similarity == 6)
                    {
                        if (!isMergedConceptDataset)
                            conceptSimilarityList = poolingConcepts(eventKit.WordList_WithoutExplication, conceptBank.ConceptList, eventKit.NormTfTable, eventKit.VerbList, eventKit.EventNameTermSet, eventKit.EventDefinitionTermSet, stopword, conceptBlacklist_perEvent[eventID]);
                        else
                            conceptSimilarityList = poolingConcepts_mergedDataset(eventKit.WordList_WithoutExplication, conceptBank.ConceptList, eventKit.NormTfTable, eventKit.VerbList, eventKit.EventNameTermSet, eventKit.EventDefinitionTermSet, stopword, conceptBlacklist_perEvent[eventID]);
                    }
                    else
                        conceptSimilarityList = null;
                    Debug.Assert(conceptSimilarityList.Count == conceptBank.Count);

                    conceptSimilarityList_allEvents_noTopSelection[eventID] = new Dictionary<string, double>(conceptSimilarityList);
                }
                else
                {
                    conceptSimilarityList = new Dictionary<string, double>(conceptSimilarityList_allEvents_noTopSelection[eventID]);            // already calculated for this event
                }

                // Index concept ID
                Dictionary<string, int> conceptNameStr_conceptID = new Dictionary<string, int>();
                int conceptID = 1;
                foreach (string conceptNameStr in conceptSimilarityList.Keys)
                {
                    conceptNameStr_conceptID.Add(conceptNameStr, conceptID);
                    conceptID++;
                }

                // For hand-picked concepts
                if (isUseHandPickedConcepts)
                {
                    Dictionary<int, double[]> handPickedConceptIDList = readHandPickedConceptIDs(eventID);
                    setSimilarityToZero(handPickedConceptIDList, ref conceptSimilarityList);
                    setSimilarityToUserDefined(handPickedConceptIDList, ref conceptSimilarityList);
                }

                // Rank concepts according to similarities
                IEnumerable<string> rankedConceptNames_a =
                    from c in conceptSimilarityList
                    orderby c.Value descending
                    select c.Key;
                List<string> rankedConceptNames = new List<string>(rankedConceptNames_a);

                eventID_matchedConceptCount[eventID] = countMatchedConcepts(conceptSimilarityList, rankedConceptNames);

                if (nTopConcepts > 0)
                {
                    int countSkipped = 0;
                    int thisConceptIndex = 0;
                    bool flag_beginToSet0 = false;
                    foreach (string thisConceptName in rankedConceptNames)
                    {
                        countSkipped++;
                        if (countSkipped > nTopConcepts)
                        {
                            double thisConceptSimilr = conceptSimilarityList[thisConceptName];
                            Debug.Assert(rankedConceptNames[thisConceptIndex] == thisConceptName);
                            double lastConceptSimilr;
                            if (thisConceptIndex != 0)
                                lastConceptSimilr = conceptSimilarityList[rankedConceptNames[thisConceptIndex - 1]];
                            else        // the first one
                                lastConceptSimilr = -1.0;

                            if (isExtendTopConceptsByConceptSimilarity)
                            {
                                if (lastConceptSimilr != thisConceptSimilr || flag_beginToSet0)
                                {
                                    conceptSimilarityList[thisConceptName] = 0.0;
                                    flag_beginToSet0 = true;
                                }
                            }
                            else
                                conceptSimilarityList[thisConceptName] = 0.0;
                        }

                        thisConceptIndex++;
                    }
                }
                conceptSimilarityList_allEvents[eventID] = conceptSimilarityList;

                if (processingStep == 2 || processingStep == 4)
                {
                    string rankedConceptListFile = rankedConceptsDir + String.Format("Q-{0}.txt", eventID);
                    using (StreamWriter swRankedConceptList = new StreamWriter(rankedConceptListFile))
                    {
                        foreach (string thisConceptName in rankedConceptNames)
                        {
                            double score = conceptSimilarityList[thisConceptName];
                            int thisConceptID = conceptNameStr_conceptID[thisConceptName];

                            if (switch_similarity == 1 || switch_similarity == 3)
                            {
                                string maxConceptInEvent = debug_maxConceptListInEvent[thisConceptName];
                                swRankedConceptList.WriteLine(thisConceptName + "\t" + thisConceptID + '\t' + score + "\t(" + maxConceptInEvent + ")");
                            }
                            else
                            {
                                swRankedConceptList.WriteLine(thisConceptName + "\t" + thisConceptID + '\t' + score);
                            }
                        }
                    }
                }
            }
        }

        public static void RankConceptsForAllEvents(int nTopConcepts, ConcurrentDictionary<int, Dictionary<string, double>> conceptSimilarityList_allEvents_noTopSelection)
        {
            string rankedConceptsDir = mappingResultDir + "RankedConcepts" + dirSeperator;
            Directory.CreateDirectory(rankedConceptsDir);
            conceptSimilarityList_allEvents = new ConcurrentDictionary<int, Dictionary<string, double>>();
            if (conceptSimilarityList_allEvents_noTopSelection == null)
                conceptSimilarityList_allEvents_noTopSelection = new ConcurrentDictionary<int, Dictionary<string, double>>();

            Stopwatch stopwatch = new Stopwatch();
            StopWord stopword = new StopWord(stopWordFile);

            stopwatch.Start();
            Console.WriteLine("Ranking concepts for each query...");

            Console.ForegroundColor = ConsoleColor.Blue;
            if (switch_similarity == 1)
                Console.WriteLine("Similarity measurement = MAX of query terms");
            else if (switch_similarity == 2)
                Console.WriteLine("Similarity measurement = MEAN of query terms");
            else if (switch_similarity == 3)
                Console.WriteLine("Similarity measurement = MAX + TOP of query terms");
            else if (switch_similarity == 4)
                Console.WriteLine("Similarity measurement = Selected concepts: SUM UP");
            else if (switch_similarity == 5)
                Console.WriteLine("Similarity measurement = Selected concepts: AVERAGE");
            else if (switch_similarity == 6)
                Console.WriteLine("Similarity measurement = Selected concepts: MAX");
            if (switch_tfidf == 0)
                Console.WriteLine("No TFIDF");
            else if (switch_tfidf == 1)
                Console.WriteLine("TF only");
            else if (switch_tfidf == 2)
                Console.WriteLine("TF+IDF");
            Console.ResetColor();


            ParallelOptions parallelOptions = new ParallelOptions();
            parallelOptions.MaxDegreeOfParallelism = nThreadsForSimilarityRanking;
            ConcurrentDictionary<int, int> eventID_matchedConceptCount = new ConcurrentDictionary<int, int>();
            conceptBlacklist_perEvent = new ConcurrentDictionary<int, ConceptBank>();
            Parallel.ForEach<EventKitDescription>(eventkitList, parallelOptions, (eventKit) => handleParallelConceptSimilarityRanking(eventKit, stopword, ref conceptSimilarityList_allEvents_noTopSelection, ref eventID_matchedConceptCount, nTopConcepts, rankedConceptsDir));
            writeMatchedConceptCountsForAllEvents(eventID_matchedConceptCount, mappingResultDir);

            stopwatch.Stop();

            Console.WriteLine("Concept ranking time spent: {0}.", stopwatch.Elapsed);
            Console.WriteLine();
        }

        private static void writeMatchedConceptCountsForAllEvents(ConcurrentDictionary<int, int> eventID_matchedConceptCount, string mappingResultDir)
        {
            string matchedConceptCountFile = mappingResultDir + "Matched_Concept_Count" + ".txt";
            using (StreamWriter swMatchedConceptCount = new StreamWriter(matchedConceptCountFile))
            {
                for (int eventID = 1; eventID <= eventID_matchedConceptCount.Count; eventID++)
                {
                    swMatchedConceptCount.WriteLine(eventID_matchedConceptCount[eventID]);
                }
            }
        }

        private static int countMatchedConcepts(Dictionary<string, double> conceptSimilarityList, IEnumerable<string> rankedConceptNames)
        {
            int count = 0;
            foreach (string conceptName in rankedConceptNames)
            {
                if (conceptSimilarityList[conceptName] != 0.0)
                    count++;
                else
                    break;
            }
            return count;
        }

        #region Similarity Measurement
        private static double getSimilarityForTermPair(EventConceptTermPair termPair, Dictionary<string, double> normTfTable, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet)
        {
            // similarity x [TFIDF] x [event_section_weight]

            double similarity;

            // TFIDF
            if (switch_tfidf == 0)
                similarity = event_concept_termTable[termPair.EventTerm + "\t" + termPair.ConceptTerm];
            else if (switch_tfidf == 1)
                similarity = event_concept_termTable[termPair.EventTerm + "\t" + termPair.ConceptTerm] * normTfTable[termPair.EventTerm];
            else if (switch_tfidf == 2)
            {
                if (idfTable.ContainsKey(termPair.EventTerm))
                    similarity = event_concept_termTable[termPair.EventTerm + "\t" + termPair.ConceptTerm] * normTfTable[termPair.EventTerm] * idfTable[termPair.EventTerm];
                else
                    similarity = event_concept_termTable[termPair.EventTerm + "\t" + termPair.ConceptTerm] * normTfTable[termPair.EventTerm] * Math.Log((double)(idf_nTotalDocs + 1) / (double)(0 + 1), idf_logBase);            //! constant here should be matched to IDF calculation
            }
            else
                throw new ArgumentOutOfRangeException("Wrong TFIDF parameter.");

            // Event section weight
            if (weightMultiplier_queryTitleTerms > 0.0)
            {
                if (eventNameTermSet.Contains(termPair.EventTerm))
                    similarity *= weightMultiplier_queryTitleTerms;
            }
            if (weightMultiplier_queryDefinitionTerms > 0.0)
            {
                if (eventDefTermSet.Contains(termPair.EventTerm))
                    similarity *= weightMultiplier_queryDefinitionTerms;
            }
            if (weightMultiplier_highWeightConcepts > 0.0)
            {
                if (highWeightConceptSet.Contains(termPair.ConceptTerm))
                    similarity *= weightMultiplier_highWeightConcepts;
            }
            if (weightMultiplier_lowWeightConcepts > 0.0)
            {
                if (lowWeightConceptSet.Contains(termPair.ConceptTerm))
                    similarity *= weightMultiplier_lowWeightConcepts;
            }

            return similarity;
        }

        private static Dictionary<string, double> poolingConcepts(HashSet<string> concept_eventKit, HashSet<string> concept_conceptBank, Dictionary<string, double> normTFTable, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet)
        {
            // Used for WordNet synsets (concept names match exactly)

            Dictionary<string, double> similarityList = new Dictionary<string, double>();

            Dictionary<string, List<double>> concept_similarityTable = new Dictionary<string, List<double>>();
            foreach (string thisConcept_eventKit in concept_eventKit)
            {
                double maxSimilr = 0.0;
                List<string> maxConceptForThisQuery = new List<string>();
                foreach (string thisConcept_conceptBank in concept_conceptBank)
                {
                    EventConceptTermPair termPair = new EventConceptTermPair(thisConcept_eventKit, thisConcept_conceptBank);

                    double similarity = getSimilarityForTermPair(termPair, normTFTable, eventNameTermSet, eventDefTermSet);
                    if (similarity > maxSimilr)
                    {
                        maxSimilr = similarity;
                    }
                }

                if (maxSimilr == 0.0)       // no concept was selected for this query term
                    continue;

                // Rescan concepts and collect all max
                foreach (string thisConcept_conceptBank in concept_conceptBank)
                {
                    EventConceptTermPair termPair = new EventConceptTermPair(thisConcept_eventKit, thisConcept_conceptBank);
                    double similarity = getSimilarityForTermPair(termPair, normTFTable, eventNameTermSet, eventDefTermSet);
                    if (similarity == maxSimilr)
                        maxConceptForThisQuery.Add(thisConcept_conceptBank);
                }
                Debug.Assert(maxConceptForThisQuery.Count >= 1);

                foreach (string maxConcept in maxConceptForThisQuery)
                {
                    if (!concept_similarityTable.ContainsKey(maxConcept))
                        concept_similarityTable.Add(maxConcept, new List<double>());
                    concept_similarityTable[maxConcept].Add(maxSimilr);
                }
            }

            foreach (string thisConcept in concept_conceptBank)
            {
                if (!concept_similarityTable.ContainsKey(thisConcept))
                    similarityList.Add(thisConcept, 0.0);
                else
                {
                    if (switch_similarity == 4)         // sum up
                    {
                        double similr_sumUp = 0.0;
                        foreach (double score in concept_similarityTable[thisConcept])
                        {
                            similr_sumUp += score;
                        }
                        similarityList.Add(thisConcept, similr_sumUp);
                    }
                    else if (switch_similarity == 5)    // average
                    {
                        double similr_average = 0.0;
                        foreach (double score in concept_similarityTable[thisConcept])
                        {
                            similr_average += score;
                        }
                        similr_average /= concept_similarityTable[thisConcept].Count;
                        similarityList.Add(thisConcept, similr_average);
                    }
                    else if (switch_similarity == 6)    // max
                    {
                        double maxSimilr = 0.0;
                        foreach (double score in concept_similarityTable[thisConcept])
                        {
                            if (score > maxSimilr)
                                maxSimilr = score;
                        }
                        similarityList.Add(thisConcept, maxSimilr);
                    }
                }
            }

            return similarityList;
        }

        private static Dictionary<string, double> maxSimilarity(HashSet<string> concept_eventKit, HashSet<string> concept_conceptBank, Dictionary<string, double> normTfTable, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet, out Dictionary<string, string> debug_maxConceptListInEvent)
        {
            // Used for WordNet synsets (concept names match exactly)

            Dictionary<string, double> similarityList = new Dictionary<string, double>();
            debug_maxConceptListInEvent = new Dictionary<string, string>();

            foreach (string thisConcept_conceptBank in concept_conceptBank)
            {
                double maxSimilr = 0.0;
                string maxConceptInEvent = null;
                foreach (string thisConcept_eventKit in concept_eventKit)
                {
                    EventConceptTermPair termPair = new EventConceptTermPair(thisConcept_eventKit, thisConcept_conceptBank);
                    double similarity = getSimilarityForTermPair(termPair, normTfTable, eventNameTermSet, eventDefTermSet);
                    if (similarity > maxSimilr)
                    {
                        maxSimilr = similarity;
                        maxConceptInEvent = thisConcept_eventKit;
                    }
                    // For debug only
                    //if (similarity > 1.0)
                    //{
                    //    Console.ForegroundColor = ConsoleColor.Yellow;
                    //    Console.WriteLine(thisConcept_eventKit + " " + thisConcept_conceptBank + "\t" + similarity);
                    //    Console.ResetColor();
                    //}
                }
                string thisConcept_conceptBank_reshaped = thisConcept_conceptBank.Replace('_', ' ');
                similarityList.Add(thisConcept_conceptBank_reshaped, maxSimilr);       //! assume no overlapped concept name
                debug_maxConceptListInEvent.Add(thisConcept_conceptBank_reshaped, maxConceptInEvent);
            }

            return similarityList;
        }

        private static Dictionary<string, double> maxTopSimilarity(HashSet<string> concept_eventKit, HashSet<string> concept_conceptBank, Dictionary<string, double> normTfTable, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet, out Dictionary<string, string> debug_maxConceptListInEvent)
        {
            // Used for WordNet synsets (concept names match exactly)

            Dictionary<string, double> similarityList = new Dictionary<string, double>();
            debug_maxConceptListInEvent = new Dictionary<string, string>();

            foreach (string thisConcept_conceptBank in concept_conceptBank)
            {
                string maxConceptInEvent;
                double maxSimilr = calcEventTopSimilarityForConcept(thisConcept_conceptBank, concept_eventKit, normTfTable, eventNameTermSet, eventDefTermSet, out maxConceptInEvent);

                string thisConcept_conceptBank_reshaped = thisConcept_conceptBank.Replace('_', ' ');
                similarityList.Add(thisConcept_conceptBank_reshaped, maxSimilr);       //! assume no overlapped concept name
                debug_maxConceptListInEvent.Add(thisConcept_conceptBank_reshaped, maxConceptInEvent);
            }

            return similarityList;
        }

        private static double calcEventTopSimilarityForConcept(string term_conceptBank, HashSet<string> conceptList_eventKit, Dictionary<string, double> normTfTable, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet, out string maxConceptInEvent)
        {
            // Referred by WordNet synsets (concept names match exactly)

            // For max + top similarity method, average the impact of the top k selected terms in event description
            int topK = 5;              //! parameter here, can be determined automatically by maximum entropy method

            Dictionary<string, double> eventConcept_similarity = new Dictionary<string, double>();
            foreach (string thisConcept_eventKit in conceptList_eventKit)
            {
                EventConceptTermPair termPair = new EventConceptTermPair(thisConcept_eventKit, term_conceptBank);
                double similarity = getSimilarityForTermPair(termPair, normTfTable, eventNameTermSet, eventDefTermSet);
                eventConcept_similarity.Add(thisConcept_eventKit, similarity);
            }

            IEnumerable<string> rankedConceptsInEvent =
                from e in eventConcept_similarity
                orderby e.Value descending
                select e.Key;

            int count_top = 0;
            double averageSimilarity = 0.0;
            maxConceptInEvent = "";
            foreach (string thisTopConcept_eventKit in rankedConceptsInEvent)
            {
                if (count_top >= topK)
                    break;

                if (count_top < 3)
                    maxConceptInEvent += thisTopConcept_eventKit + ", ";

                double similarity = eventConcept_similarity[thisTopConcept_eventKit];
                averageSimilarity += similarity;

                count_top++;
            }
            maxConceptInEvent = maxConceptInEvent.Trim(new char[] { ' ', ',' });
            averageSimilarity /= count_top;

            return averageSimilarity;
        }

        private static Dictionary<string, double> meanSimilarity(HashSet<string> concept_eventKit, HashSet<string> concept_conceptBank, Dictionary<string, double> normTfTable, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet)
        {
            // Used for WordNet synsets (concept names match exactly)

            Dictionary<string, double> similarityList = new Dictionary<string, double>();

            foreach (string thisConcept_conceptBank in concept_conceptBank)
            {
                double meanSimilr = 0.0;
                int count = 0;
                foreach (string thisConcept_eventKit in concept_eventKit)
                {
                    EventConceptTermPair termPair = new EventConceptTermPair(thisConcept_eventKit, thisConcept_conceptBank);
                    double similarity = getSimilarityForTermPair(termPair, normTfTable, eventNameTermSet, eventDefTermSet);
                    meanSimilr += similarity;
                    count++;
                }
                meanSimilr /= count;
                string thisConcept_conceptBank_reshaped = thisConcept_conceptBank.Replace('_', ' ');
                similarityList.Add(thisConcept_conceptBank_reshaped, meanSimilr);       //! assume no overlapped concept name
            }

            return similarityList;
        }

        /////////////////////////////////////////////////////////////////////////////

        private static double getSimilarityWithEventTitle(HashSet<string> conceptName, HashSet<string> eventTitle, StopWord stopword)
        {
            HashSet<string> conceptName_b = new HashSet<string>(conceptName);

            conceptName_b.IntersectWith(eventTitle);

            int nCrossTerms_concept = 0;
            int nStopWords_eventTitle = 0;
            foreach (string term in conceptName_b)
            {
                if (!stopword.IsStopword(term))
                    nCrossTerms_concept++;
                else
                    nStopWords_eventTitle++;
            }

            int nValidTerms_eventTitle = eventTitle.Count - nStopWords_eventTitle;
            double rate = (double)nCrossTerms_concept / (double)nValidTerms_eventTitle;
            if (rate < 1.0 / weightMultiplier_queryTitleConcept)
                rate = 1.0 / weightMultiplier_queryTitleConcept;
            else
            {
                if (!weightMultiplier_queryTitleTerms_isWeightedByQueryTitleConcept)
                {
                    if (nCrossTerms_concept != nValidTerms_eventTitle)
                        rate = 1.0 / weightMultiplier_queryTitleConcept;
                }
            }

            return rate;
        }

        // For concept names formatted by comma-separated alias
        private static Dictionary<string, double> poolingConcepts(HashSet<string> concept_eventKit, List<List<HashSet<string>>> concept_conceptBank, Dictionary<string, double> normTfTable, HashSet<string> verbList, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet, StopWord stopword, ConceptBank conceptBlacklist_thisEvent)
        {
            Dictionary<string, double> similarityList = new Dictionary<string, double>();

            Dictionary<string, List<double>> concept_similarityTable = new Dictionary<string, List<double>>();
            List<string> conceptNameList_conceptBank = new List<string>();          // is able to handle duplicate concept names

            foreach (string thisConcept_eventKit in concept_eventKit)
            {
                double maxSimilr = 0.0;
                List<string> maxConceptForThisQuery = new List<string>();

                // Formalize the concept name list
                conceptNameList_conceptBank.Clear();
                foreach (List<HashSet<string>> thisConcept_conceptBank in concept_conceptBank)      // per concept
                {
                    string conceptNameStr = "";

                    foreach (HashSet<string> phrase_conceptBank in thisConcept_conceptBank)         // phrase originally separated by ","
                    {
                        string phraseStr = "\"";

                        // Verbs in event description must be constrained with context
                        bool isCancelThisPhrase = false;
                        if (isVerbFix && verbList.Contains(thisConcept_eventKit) && phrase_conceptBank.Count > 1)        // if this is a verb and the candidate concept phrase contains multiple terms
                        {
                            int countFind = 0;
                            foreach (string term_conceptBank in phrase_conceptBank)
                            {
                                if (term_conceptBank != thisConcept_eventKit)               // term exactly matching the query doesn't count
                                    if (concept_eventKit.Contains(term_conceptBank) || stopword.IsStopword(term_conceptBank))        // among terms other than query term, at least one should match the context of event
                                        countFind++;
                            }

                            if (countFind == 0)
                                isCancelThisPhrase = true;
                        }

                        foreach (string term_conceptBank in phrase_conceptBank)
                        {
                            phraseStr += term_conceptBank + " ";
                            EventConceptTermPair termPair = new EventConceptTermPair(thisConcept_eventKit, term_conceptBank);

                            double similarity;
                            if (conceptBlacklist_global.IsInConceptBank(thisConcept_conceptBank) || conceptBlacklist_thisEvent.IsInConceptBank(thisConcept_conceptBank))
                                similarity = 0.0;
                            else
                                similarity = getWeightedSimilarity(termPair, normTfTable, eventNameTermSet, eventDefTermSet, phrase_conceptBank, stopword);

                            if (similarity > maxSimilr && !isCancelThisPhrase)
                            {
                                maxSimilr = similarity;
                            }
                        }
                        phraseStr = phraseStr.Trim() + "\"";
                        conceptNameStr += phraseStr + "+";
                    }
                    conceptNameStr = conceptNameStr.TrimEnd('+');
                    conceptNameList_conceptBank.Add(conceptNameStr);
                }
                Debug.Assert(conceptNameList_conceptBank.Count == conceptBank.Count);

                if (maxSimilr == 0.0)
                {
                    continue;
                }

                // Rescan concepts and collect all max
                bool flag_maxSimilrHit = false;
                foreach (List<HashSet<string>> thisConcept_conceptBank in concept_conceptBank)      // per concept
                {
                    string conceptNameStr = "";

                    foreach (HashSet<string> phrase_conceptBank in thisConcept_conceptBank)         // phrase originally separated by ","
                    {
                        string phraseStr = "\"";

                        // Verbs in event description must be constrained with context
                        bool isCancelThisPhrase = false;
                        if (isVerbFix && verbList.Contains(thisConcept_eventKit) && phrase_conceptBank.Count > 1)        // if this is a verb and the candidate concept phrase contains multiple terms
                        {
                            int countFind = 0;
                            foreach (string term_conceptBank in phrase_conceptBank)
                            {
                                if (term_conceptBank != thisConcept_eventKit)               // term exactly matching the query doesn't count
                                    if (concept_eventKit.Contains(term_conceptBank) || stopword.IsStopword(term_conceptBank))        // among terms other than query term, at least one should match the context of event
                                        countFind++;
                            }

                            if (countFind == 0)
                                isCancelThisPhrase = true;
                        }

                        foreach (string term_conceptBank in phrase_conceptBank)
                        {
                            phraseStr += term_conceptBank + " ";
                            EventConceptTermPair termPair = new EventConceptTermPair(thisConcept_eventKit, term_conceptBank);

                            double similarity;
                            if (conceptBlacklist_global.IsInConceptBank(thisConcept_conceptBank) || conceptBlacklist_thisEvent.IsInConceptBank(thisConcept_conceptBank))
                                similarity = 0.0;
                            else
                                similarity = getWeightedSimilarity(termPair, normTfTable, eventNameTermSet, eventDefTermSet, phrase_conceptBank, stopword);

                            if (similarity == maxSimilr && !isCancelThisPhrase)
                                flag_maxSimilrHit = true;
                        }
                        phraseStr = phraseStr.Trim() + "\"";
                        conceptNameStr += phraseStr + "+";
                    }
                    conceptNameStr = conceptNameStr.TrimEnd('+');
                    if (flag_maxSimilrHit == true)
                    {
                        maxConceptForThisQuery.Add(conceptNameStr);
                        flag_maxSimilrHit = false;
                    }
                }
                Debug.Assert(maxConceptForThisQuery.Count >= 1);

                foreach (string maxConcept in maxConceptForThisQuery)
                {
                    if (!concept_similarityTable.ContainsKey(maxConcept))
                        concept_similarityTable.Add(maxConcept, new List<double>());
                    concept_similarityTable[maxConcept].Add(maxSimilr);
                }
            }

            foreach (string thisConcept in conceptNameList_conceptBank)
            {
                if (!concept_similarityTable.ContainsKey(thisConcept))
                    similarityList.Add(thisConcept, 0.0);
                else
                {
                    if (switch_similarity == 4)         // sum up
                    {
                        double similr_sumUp = 0.0;
                        foreach (double score in concept_similarityTable[thisConcept])
                        {
                            similr_sumUp += score;
                        }
                        similarityList.Add(thisConcept, similr_sumUp);
                    }
                    else if (switch_similarity == 5)    // average
                    {
                        double similr_average = 0.0;
                        foreach (double score in concept_similarityTable[thisConcept])
                        {
                            similr_average += score;
                        }
                        similr_average /= concept_similarityTable[thisConcept].Count;
                        similarityList.Add(thisConcept, similr_average);
                    }
                    else if (switch_similarity == 6)    // max
                    {
                        double maxSimilr = 0.0;
                        foreach (double score in concept_similarityTable[thisConcept])
                        {
                            if (score > maxSimilr)
                                maxSimilr = score;
                        }
                        similarityList.Add(thisConcept, maxSimilr);
                    }
                }
            }

            return similarityList;
        }

        private static double getWeightedSimilarity(EventConceptTermPair termPair, Dictionary<string, double> normTfTable, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet, HashSet<string> phrase_conceptBank, StopWord stopword)
        {
            double similarity = getSimilarityForTermPair(termPair, normTfTable, eventNameTermSet, eventDefTermSet);
            if (weightMultiplier_queryTitleConcept > 0.0)
            {
                similarity *= weightMultiplier_queryTitleConcept * getSimilarityWithEventTitle(phrase_conceptBank, eventNameTermSet, stopword);
            }

            return similarity;
        }

        // For concept names formatted by comma-separated alias
        private static Dictionary<string, double> poolingConcepts_mergedDataset(HashSet<string> concept_eventKit, List<List<HashSet<string>>> concept_conceptBank, Dictionary<string, double> normTfTable, HashSet<string> verbList, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet, StopWord stopword, ConceptBank conceptBlacklist_thisEvent)
        {
            Dictionary<string, double> similarityList = new Dictionary<string, double>();

            Dictionary<string, List<double>> concept_similarityTable = new Dictionary<string, List<double>>();          // only contains the selected concepts, not all concepts; for whole concept name list
            List<string> conceptNameList_conceptBank = new List<string>();          // is able to handle duplicate concept names

            foreach (string thisConcept_eventKit in concept_eventKit)
            {
                List<double> maxSimilrList = new List<double>();
                List<string> maxConceptForThisQuery = new List<string>();

                conceptNameList_conceptBank.Clear();        // used to generate the whole concept name list

                // For merged concept dataset, split concept_conceptBank into separate sets
                List<List<List<HashSet<string>>>> splitConcepts_conceptBank = new List<List<List<HashSet<string>>>>();
                int datasetIndex = 0;
                int conceptID_inDataset = 0;
                splitConcepts_conceptBank.Add(new List<List<HashSet<string>>>());
                foreach (List<HashSet<string>> thisConcept_conceptBank in concept_conceptBank)
                {
                    conceptID_inDataset++;

                    if (conceptID_inDataset <= mergedInfo_nConceptsInOrigin[datasetIndex])
                    {
                        splitConcepts_conceptBank[datasetIndex].Add(thisConcept_conceptBank);
                    }
                    else        // new dataset
                    {
                        datasetIndex++;
                        splitConcepts_conceptBank.Add(new List<List<HashSet<string>>>());
                        conceptID_inDataset = 1;
                        splitConcepts_conceptBank[datasetIndex].Add(thisConcept_conceptBank);
                    }
                }
                Debug.Assert(splitConcepts_conceptBank.Count == mergedInfo_nConceptsInOrigin.Count);


                // Find max similarity in every batch of concepts
                int batchID = 0;
                foreach (List<List<HashSet<string>>> thisBatch_conceptBank in splitConcepts_conceptBank)     // per batch of concepts
                {
                    batchID++;

                    double maxSimilr = 0.0;             // per batch max
                    foreach (List<HashSet<string>> thisConcept_conceptBank in thisBatch_conceptBank)         // per concept
                    {
                        string conceptNameStr = "";

                        foreach (HashSet<string> phrase_conceptBank in thisConcept_conceptBank)         // phrase originally separated by ","
                        {
                            string phraseStr = "\"";

                            // Verbs in event description must be constrained with context
                            bool isCancelThisPhrase = false;
                            if (isVerbFix && verbList.Contains(thisConcept_eventKit) && phrase_conceptBank.Count > 1)        // if this is a verb and the candidate concept phrase contains multiple terms
                            {
                                int countFind = 0;
                                foreach (string term_conceptBank in phrase_conceptBank)
                                {
                                    if (term_conceptBank != thisConcept_eventKit)               // term exactly matching the query doesn't count
                                        if (concept_eventKit.Contains(term_conceptBank) || stopword.IsStopword(term_conceptBank))        // among terms other than query term, at least one should match the context of event
                                            countFind++;
                                }

                                if (countFind == 0)
                                    isCancelThisPhrase = true;
                            }

                            foreach (string term_conceptBank in phrase_conceptBank)
                            {
                                phraseStr += term_conceptBank + " ";
                                EventConceptTermPair termPair = new EventConceptTermPair(thisConcept_eventKit, term_conceptBank);

                                double similarity;
                                if (conceptBlacklist_global.IsInConceptBank(thisConcept_conceptBank) || conceptBlacklist_thisEvent.IsInConceptBank(thisConcept_conceptBank))
                                    similarity = 0.0;
                                else
                                    similarity = getWeightedSimilarity(termPair, normTfTable, eventNameTermSet, eventDefTermSet, phrase_conceptBank, stopword);

                                if (similarity > maxSimilr && !isCancelThisPhrase)
                                {
                                    maxSimilr = similarity;
                                }
                            }
                            phraseStr = phraseStr.Trim() + "\"";
                            conceptNameStr += phraseStr + "+";
                        }
                        conceptNameStr = conceptNameStr.TrimEnd('+');
                        conceptNameList_conceptBank.Add("(#" + batchID + ")" + conceptNameStr);
                    }
                    maxSimilrList.Add(maxSimilr);
                }
                Debug.Assert(conceptNameList_conceptBank.Count == conceptBank.Count);           // whole concept name list
                Debug.Assert(maxSimilrList.Count == mergedInfo_nConceptsInOrigin.Count);

                double maxSimilrOverall = 0.0;
                foreach (double maxSimilr in maxSimilrList)
                {
                    if (maxSimilr > maxSimilrOverall)
                        maxSimilrOverall = maxSimilr;
                }
                if (maxSimilrOverall == 0.0)
                {
                    continue;
                }


                // Rescan concepts and collect all max
                batchID = 0;
                foreach (List<List<HashSet<string>>> thisBatch_conceptBank in splitConcepts_conceptBank)     // per batch of concepts
                {
                    batchID++;
                    bool flag_maxSimilrHit = false;
                    foreach (List<HashSet<string>> thisConcept_conceptBank in thisBatch_conceptBank)         // per concept
                    {
                        string conceptNameStr = "";

                        foreach (HashSet<string> phrase_conceptBank in thisConcept_conceptBank)         // phrase originally separated by ","
                        {
                            string phraseStr = "\"";

                            // Verbs in event description must be constrained with context
                            bool isCancelThisPhrase = false;
                            if (isVerbFix && verbList.Contains(thisConcept_eventKit) && phrase_conceptBank.Count > 1)        // if this is a verb and the candidate concept phrase contains multiple terms
                            {
                                int countFind = 0;
                                foreach (string term_conceptBank in phrase_conceptBank)
                                {
                                    if (term_conceptBank != thisConcept_eventKit)               // term exactly matching the query doesn't count
                                        if (concept_eventKit.Contains(term_conceptBank) || stopword.IsStopword(term_conceptBank))        // among terms other than query term, at least one should match the context of event
                                            countFind++;
                                }

                                if (countFind == 0)
                                    isCancelThisPhrase = true;
                            }

                            foreach (string term_conceptBank in phrase_conceptBank)
                            {
                                phraseStr += term_conceptBank + " ";
                                EventConceptTermPair termPair = new EventConceptTermPair(thisConcept_eventKit, term_conceptBank);

                                double similarity;
                                if (conceptBlacklist_global.IsInConceptBank(thisConcept_conceptBank) || conceptBlacklist_thisEvent.IsInConceptBank(thisConcept_conceptBank))
                                    similarity = 0.0;
                                else
                                    similarity = getWeightedSimilarity(termPair, normTfTable, eventNameTermSet, eventDefTermSet, phrase_conceptBank, stopword);

                                if (similarity == maxSimilrList[batchID - 1] && !isCancelThisPhrase)
                                    flag_maxSimilrHit = true;
                            }
                            phraseStr = phraseStr.Trim() + "\"";
                            conceptNameStr += phraseStr + "+";
                        }
                        conceptNameStr = conceptNameStr.TrimEnd('+');
                        if (flag_maxSimilrHit == true)
                        {
                            maxConceptForThisQuery.Add("(#" + batchID + ")" + conceptNameStr);             // conceptNameStr should be identical to that in conceptNameList_conceptBank
                            flag_maxSimilrHit = false;
                        }
                    }
                }
                Debug.Assert(maxConceptForThisQuery.Count >= 1);


                // Fill in the similarity table
                foreach (string maxConcept in maxConceptForThisQuery)
                {
                    string batchIDStr = maxConcept.Replace("(#", "").Replace(")", " ");
                    batchIDStr = batchIDStr.Substring(0, batchIDStr.IndexOf(" "));

                    int batchID_b = int.Parse(batchIDStr);

                    if (!concept_similarityTable.ContainsKey(maxConcept))
                        concept_similarityTable.Add(maxConcept, new List<double>());
                    concept_similarityTable[maxConcept].Add(maxSimilrList[batchID_b - 1]);
                }
            }


            // Do for the whole concept list
            foreach (string thisConcept in conceptNameList_conceptBank)
            {
                if (!concept_similarityTable.ContainsKey(thisConcept))
                    similarityList.Add(thisConcept, 0.0);
                else
                {
                    if (switch_similarity == 4)         // sum up
                    {
                        double similr_sumUp = 0.0;
                        foreach (double score in concept_similarityTable[thisConcept])
                        {
                            similr_sumUp += score;
                        }
                        similarityList.Add(thisConcept, similr_sumUp);
                    }
                    else if (switch_similarity == 5)    // average
                    {
                        double similr_average = 0.0;
                        foreach (double score in concept_similarityTable[thisConcept])
                        {
                            similr_average += score;
                        }
                        similr_average /= concept_similarityTable[thisConcept].Count;
                        similarityList.Add(thisConcept, similr_average);
                    }
                    else if (switch_similarity == 6)    // max
                    {
                        double maxSimilr = 0.0;
                        foreach (double score in concept_similarityTable[thisConcept])
                        {
                            if (score > maxSimilr)
                                maxSimilr = score;
                        }
                        similarityList.Add(thisConcept, maxSimilr);
                    }
                }
            }

            return similarityList;
        }

        private static Dictionary<string, double> maxSimilarity(HashSet<string> concept_eventKit, List<List<HashSet<string>>> concept_conceptBank, Dictionary<string, double> normTfTable, HashSet<string> verbList, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet, StopWord stopword, out Dictionary<string, string> debug_maxConceptListInEvent, ConceptBank conceptBlacklist_thisEvent)
        {
            Dictionary<string, double> similarityList = new Dictionary<string, double>();
            debug_maxConceptListInEvent = new Dictionary<string, string>();

            //HashSet<string> usedConcept_eventKit = new HashSet<string>();           // for debug only, to constrain that each query term is used only once, preventing it dominating the concept selection

            foreach (List<HashSet<string>> thisConcept_conceptBank in concept_conceptBank)
            {
                double maxSimilr = 0.0;
                string maxConceptInEvent = null;
                string conceptNameStr = "";

                foreach (HashSet<string> phrase_conceptBank in thisConcept_conceptBank)         // phrase originally separated by ","
                {
                    string phraseStr = "\"";
                    foreach (string term_conceptBank in phrase_conceptBank)                     // each single term in phrase
                    {
                        phraseStr += term_conceptBank + " ";
                        foreach (string thisConcept_eventKit in concept_eventKit)
                        {
                            // Verbs in event description must be constrained with context
                            bool isCancelThisPhrase = false;
                            if (isVerbFix && verbList.Contains(thisConcept_eventKit) && phrase_conceptBank.Count > 1)        // if this is a verb and the candidate concept phrase contains multiple terms
                            {
                                int countFind = 0;
                                foreach (string term_conceptBank_B in phrase_conceptBank)
                                {
                                    if (term_conceptBank_B != thisConcept_eventKit)               // term exactly matching the query doesn't count
                                        if (concept_eventKit.Contains(term_conceptBank_B) || stopword.IsStopword(term_conceptBank_B))        // among terms other than query term, at least one should match the context of event
                                            countFind++;
                                }

                                if (countFind == 0)
                                    isCancelThisPhrase = true;
                            }

                            EventConceptTermPair termPair = new EventConceptTermPair(thisConcept_eventKit, term_conceptBank);

                            double similarity;
                            if (conceptBlacklist_global.IsInConceptBank(thisConcept_conceptBank) || conceptBlacklist_thisEvent.IsInConceptBank(thisConcept_conceptBank))
                                similarity = 0.0;
                            else
                                similarity = getWeightedSimilarity(termPair, normTfTable, eventNameTermSet, eventDefTermSet, phrase_conceptBank, stopword);

                            if (similarity > maxSimilr && !isCancelThisPhrase)
                            {
                                maxSimilr = similarity;
                                maxConceptInEvent = thisConcept_eventKit;
                            }
                        }
                    }
                    phraseStr = phraseStr.Trim() + "\"";
                    conceptNameStr += phraseStr + "+";
                }
                conceptNameStr = conceptNameStr.TrimEnd('+');

                similarityList.Add(conceptNameStr, maxSimilr);
                debug_maxConceptListInEvent.Add(conceptNameStr, maxConceptInEvent);
                //usedConcept_eventKit.Add(maxConceptInEvent);
            }
            return similarityList;
        }

        private static Dictionary<string, double> maxTopSimilarity(HashSet<string> concept_eventKit, List<List<HashSet<string>>> concept_conceptBank, Dictionary<string, double> normTfTable, HashSet<string> verbList, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet, StopWord stopword, out Dictionary<string, string> debug_maxConceptListInEvent, ConceptBank conceptBlacklist_thisEvent)
        {
            Dictionary<string, double> similarityList = new Dictionary<string, double>();
            debug_maxConceptListInEvent = new Dictionary<string, string>();

            foreach (List<HashSet<string>> thisConcept_conceptBank in concept_conceptBank)
            {
                double maxSimilr = 0.0;
                string maxConceptInEvent = null;
                string conceptNameStr = "";

                foreach (HashSet<string> phrase_conceptBank in thisConcept_conceptBank)         // phrase originally separated by ","
                {
                    string phraseStr = "\"";
                    foreach (string term_conceptBank in phrase_conceptBank)                     // each single term in phrase in concept bank
                    {
                        phraseStr += term_conceptBank + " ";

                        string maxConceptForThisTerm;
                        double similarity = calcEventTopSimilarityForConcept(term_conceptBank, phrase_conceptBank, thisConcept_conceptBank, concept_eventKit, normTfTable, verbList, eventNameTermSet, eventDefTermSet, stopword, out maxConceptForThisTerm, conceptBlacklist_thisEvent);

                        if (similarity > maxSimilr)
                        {
                            maxSimilr = similarity;
                            maxConceptInEvent = maxConceptForThisTerm;
                        }
                    }
                    phraseStr = phraseStr.Trim() + "\"";
                    conceptNameStr += phraseStr + "+";
                }
                conceptNameStr = conceptNameStr.TrimEnd('+');

                similarityList.Add(conceptNameStr, maxSimilr);
                debug_maxConceptListInEvent.Add(conceptNameStr, maxConceptInEvent);
            }
            return similarityList;
        }

        private static double calcEventTopSimilarityForConcept(string term_conceptBank, HashSet<string> phrase_conceptBank, List<HashSet<string>> thisConcept_conceptBank, HashSet<string> conceptList_eventKit, Dictionary<string, double> normTfTable, HashSet<string> verbList, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet, StopWord stopword, out string maxConceptInEvent, ConceptBank conceptBlacklist_thisEvent)
        {
            // For max + top similarity method, average the impact of the top k selected terms in event description
            int topK = 5;              //! parameter here, can be determined automatically by maximum entropy method

            Dictionary<string, double> eventConcept_similarity = new Dictionary<string, double>();
            foreach (string thisConcept_eventKit in conceptList_eventKit)
            {
                EventConceptTermPair termPair = new EventConceptTermPair(thisConcept_eventKit, term_conceptBank);

                double similarity;
                if (conceptBlacklist_global.IsInConceptBank(thisConcept_conceptBank) || conceptBlacklist_thisEvent.IsInConceptBank(thisConcept_conceptBank))
                    similarity = 0.0;
                else
                    similarity = getWeightedSimilarity(termPair, normTfTable, eventNameTermSet, eventDefTermSet, phrase_conceptBank, stopword);

                eventConcept_similarity.Add(thisConcept_eventKit, similarity);
            }

            IEnumerable<string> rankedConceptsInEvent =
                from e in eventConcept_similarity
                orderby e.Value descending
                select e.Key;

            int count_top = 0;
            double averageSimilarity = 0.0;
            maxConceptInEvent = "";
            foreach (string thisTopConcept_eventKit in rankedConceptsInEvent)
            {
                if (count_top >= topK)
                    break;

                // Verbs in event description must be constrained with context
                bool isCancelThisPhrase = false;
                if (verbList.Contains(thisTopConcept_eventKit) && phrase_conceptBank.Count > 1)        // if this is a verb and the candidate concept phrase contains multiple terms
                {
                    int countFind = 0;
                    foreach (string term_conceptBank_B in phrase_conceptBank)
                    {
                        if (term_conceptBank_B != thisTopConcept_eventKit)                // term exactly matching the query doesn't count
                            if (conceptList_eventKit.Contains(term_conceptBank_B) || stopword.IsStopword(term_conceptBank_B))        // among terms other than query term, at least one should match the context of event
                                countFind++;
                    }

                    if (countFind == 0)
                        isCancelThisPhrase = true;
                }

                if (isCancelThisPhrase)
                    continue;

                if (count_top < 3)
                    maxConceptInEvent += thisTopConcept_eventKit + ", ";

                double similarity = eventConcept_similarity[thisTopConcept_eventKit];
                averageSimilarity += similarity;

                count_top++;
            }
            maxConceptInEvent = maxConceptInEvent.Trim(new char[] { ' ', ',' });
            averageSimilarity /= count_top;

            return averageSimilarity;
        }

        private static Dictionary<string, double> meanSimilarity(HashSet<string> concept_eventKit, List<List<HashSet<string>>> concept_conceptBank, Dictionary<string, double> normTfTable, HashSet<string> verbList, HashSet<string> eventNameTermSet, HashSet<string> eventDefTermSet, StopWord stopword, ConceptBank conceptBlacklist_thisEvent)
        {
            Dictionary<string, double> similarityList = new Dictionary<string, double>();

            foreach (List<HashSet<string>> thisConcept_conceptBank in concept_conceptBank)
            {
                double maxSimilr_termInConceptBank = 0.0;
                string conceptNameStr = "";

                foreach (HashSet<string> phrase_conceptBank in thisConcept_conceptBank)         // phrase originally separated by ","
                {
                    string phraseStr = "\"";
                    foreach (string term_conceptBank in phrase_conceptBank)                     // each single term in phrase
                    {
                        phraseStr += term_conceptBank + " ";

                        double meanSimilr = 0.0;
                        int count = 0;
                        foreach (string thisConcept_eventKit in concept_eventKit)
                        {
                            // Verbs in event description must be constrained with context
                            bool isCancelThisPhrase = false;
                            if (isVerbFix && verbList.Contains(thisConcept_eventKit) && phrase_conceptBank.Count > 1)        // if this is a verb and the candidate concept phrase contains multiple terms
                            {
                                int countFind = 0;
                                foreach (string term_conceptBank_B in phrase_conceptBank)
                                {
                                    if (term_conceptBank_B != thisConcept_eventKit)               // term exactly matching the query doesn't count
                                        if (concept_eventKit.Contains(term_conceptBank_B) || stopword.IsStopword(term_conceptBank_B))        // among terms other than query term, at least one should match the context of event
                                            countFind++;
                                }

                                if (countFind == 0)
                                    isCancelThisPhrase = true;
                            }

                            if (isCancelThisPhrase)
                                continue;

                            EventConceptTermPair termPair = new EventConceptTermPair(thisConcept_eventKit, term_conceptBank);

                            double similarity;
                            if (conceptBlacklist_global.IsInConceptBank(thisConcept_conceptBank) || conceptBlacklist_thisEvent.IsInConceptBank(thisConcept_conceptBank))
                                similarity = 0.0;
                            else
                                similarity = getWeightedSimilarity(termPair, normTfTable, eventNameTermSet, eventDefTermSet, phrase_conceptBank, stopword);

                            meanSimilr += similarity;
                            count++;
                        }
                        meanSimilr /= count;

                        if (meanSimilr > maxSimilr_termInConceptBank)
                        {
                            maxSimilr_termInConceptBank = meanSimilr;
                        }
                    }   // end of term in concept bank
                    phraseStr = phraseStr.Trim() + "\"";
                    conceptNameStr += phraseStr + "+";
                }   // end of phrase in concept bank
                conceptNameStr = conceptNameStr.TrimEnd('+');

                similarityList.Add(conceptNameStr, maxSimilr_termInConceptBank);
            }

            return similarityList;
        }
        #endregion

        public static void ReadEventConceptSimilarityTable()
        {
            event_concept_termTable = new Dictionary<string, float>();

            Console.WriteLine("Reading query-concept similarity table...");
            Console.WriteLine("   - size = {0}x{1}", eventWordList.Count, conceptWordList.Count);
            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();
            using (StreamReader srSimilarityTable = new StreamReader(similarityTableFile))
            {
                string line = null;
                while ((line = srSimilarityTable.ReadLine()) != null)
                {
                    string[] splitLine = line.Split(new char[] { ' ', '\t' });
                    Debug.Assert(splitLine.Length == 3);
                    //EventConceptTermPair termPair = new EventConceptTermPair(splitLine[0], splitLine[1]);
                    float score = float.Parse(splitLine[2]);
                    event_concept_termTable.Add(splitLine[0] + "\t" + splitLine[1], score);
                }
            }
            // The following assertion ensures the similarity table matches the query-concept terms
            Debug.Assert(event_concept_termTable.Count == eventWordList.Count * conceptWordList.Count);         // generate the similarity table again if failed
            stopwatch.Stop();
            TimeSpan ts = stopwatch.Elapsed;
            Console.WriteLine("Acquired {0} term pairs. Time spent: {1}.", event_concept_termTable.Count, ts);

            Console.WriteLine();
        }

        public static void CreateEventConceptTermTable()
        {
            List<string> wordList_event = new List<string>(eventWordList);
            List<string> wordList_concept = new List<string>(conceptWordList);

            Console.WriteLine("Generating query-concept term table...");
            Console.WriteLine("   - size = {0}x{1}", wordList_event.Count, wordList_concept.Count);
            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();
            event_concept_termTable = new Dictionary<string, float>();
            for (int i = 0; i < wordList_event.Count; i++)
            {
                for (int j = 0; j < wordList_concept.Count; j++)
                {
                    string eventTerm = wordList_event[i];
                    string conceptTerm = wordList_concept[j];
                    //EventConceptTermPair termPair = new EventConceptTermPair(eventTerm, conceptTerm);
                    event_concept_termTable.Add(eventTerm + "\t" + conceptTerm, -19870121.0f);
                }
            }
            stopwatch.Stop();
            TimeSpan ts = stopwatch.Elapsed;
            Console.WriteLine("{0} term pairs were created. Time spent: {1}.", event_concept_termTable.Count, ts);

            // Print term table
            if (processingStep == 1)
            {
                string termTableFile = mappingResultDir + "Event_Concept_TermTable.txt";
                using (StreamWriter swTermTable = new StreamWriter(termTableFile))
                {
                    int printCounter = 0;
                    foreach (string termPairStr in event_concept_termTable.Keys)
                    {
                        string[] termPair = termPairStr.Split('\t');
                        swTermTable.WriteLine(termPair[0] + "\t" + termPair[1]);
                        printCounter++;
                    }
                    Debug.Assert(printCounter == event_concept_termTable.Count);
                }
            }

            Console.WriteLine();
        }

        public static void CreateWordList()
        {
            eventWordList = new HashSet<string>();
            foreach (EventKitDescription eventKit in eventkitList)
            {
                foreach (string term in eventKit.WordList_WithoutExplication)
                    eventWordList.Add(term);
            }

            if (isWordNetSynsets)
                conceptWordList = conceptBank.WordList_ImageNet;
            else
                conceptWordList = conceptBank.WordList;
        }

        public static void ReadConceptBlacklist()
        {
            conceptBlacklist_global = new ConceptBank();

            if (!isUseConceptBlacklist)
                return;

            StopWord stopword = new StopWord(stopWordFile);
            conceptBlacklist_global.ReadConceptList(conceptGlobalBanFile, isWordNetSynsets, stopword);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("{0} global banned concepts.", conceptBlacklist_global.Count);
            Console.ResetColor();

            // For debug, print all concepts
#if DEBUG
            string conceptBlackListFile_reshaped = mappingResultDir + "ConceptBlacklist_Reshaped.txt";
            conceptBlacklist_global.PrintConceptList(conceptBlackListFile_reshaped);
#endif
            Console.WriteLine();
        }

        public static void readConceptBlacklistForEvent(int eventID, StopWord stopword)
        {
            ConceptBank conceptBlacklist_thisEvent = conceptBlacklist_perEvent[eventID] = new ConceptBank();

            if (!isUseConceptBlacklist)
                return;

            string conceptBlacklistFile_thisEvent = conceptBanPerQueryFile.Replace("%d", eventID.ToString());
            if (File.Exists(conceptBlacklistFile_thisEvent))
                conceptBlacklist_thisEvent.ReadConceptList_Silent(conceptBlacklistFile_thisEvent, isWordNetSynsets, stopword);

            if (conceptBlacklist_thisEvent.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("      - found {0} blacklisted concepts for Query {1}", conceptBlacklist_thisEvent.Count, eventID);
                Console.ResetColor();
            }
        }

        public static void ReadConceptList()
        {
            conceptBank = new ConceptBank();

            StopWord stopword = new StopWord(stopWordFile);
            conceptBank.ReadConceptList(conceptListFile, isWordNetSynsets, stopword);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("{0} concepts were found.", conceptBank.Count);
            Console.ResetColor();

            // For debug, print all concepts
#if DEBUG
            string conceptListFile_reshaped = mappingResultDir + "Concept_Reshaped.txt";
            conceptBank.PrintConceptList(conceptListFile_reshaped);
#endif

            Console.WriteLine();
        }

        public static void ParseEventNLPFile()
        {
            eventkitList = new List<EventKitDescription>();

            string description_allevents = null;
            using (StreamReader srEventDescription = new StreamReader(queryDescriptionFile))
            {
                description_allevents = srEventDescription.ReadToEnd();
            }

            List<int> eventHeadIndex = new List<int>();
            int startingIndex = 0;
            int curEventHeadIndex = 0;
            while ((curEventHeadIndex = description_allevents.IndexOf("Query title:", startingIndex)) != -1 || (curEventHeadIndex = description_allevents.IndexOf("Query Title:", startingIndex)) != -1)
            {
                eventHeadIndex.Add(curEventHeadIndex);
                startingIndex = curEventHeadIndex + 1;
                if (startingIndex >= description_allevents.Length)
                    break;
            }

            Console.WriteLine("{0} queries were found in the NLP file.", eventHeadIndex.Count);

            // Parse each event
            StopWord stopword = new StopWord(stopWordFile);

            for (int i = 0; i < eventHeadIndex.Count; i++)
            {
                int curHead = eventHeadIndex[i];
                int nextHead = 0;
                if (i + 1 < eventHeadIndex.Count)
                    nextHead = eventHeadIndex[i + 1];
                else
                    nextHead = description_allevents.Length;

                int curDescriptionLength = nextHead - curHead;
                string curEventDescription = description_allevents.Substring(curHead, curDescriptionLength).Trim();

                EventKitDescription curEventKit = new EventKitDescription();
                if (curEventKit.ParseDescription(curEventDescription, stopword, isMEDQuery))
                    eventkitList.Add(curEventKit);
            }

            Console.WriteLine("Finish parsing query file, {0} queries were added.", eventkitList.Count);

            // For debug, print NLP description for all events
#if DEBUG
            string descriptionFile_allEvents = mappingResultDir + "Event_NLP_Description_Reshaped.txt";
            using (StreamWriter swNLP = new StreamWriter(descriptionFile_allEvents))
            {
                foreach (EventKitDescription eventKit in eventkitList)
                {
                    swNLP.WriteLine(eventKit.PrintEventDescription());
                    swNLP.WriteLine();
                }
            }
#endif

            Console.WriteLine();
        }

        private static void FormatPaths()
        {
            dirSeperator = Path.DirectorySeparatorChar.ToString();

            if (!mappingResultDir.EndsWith(dirSeperator))
                mappingResultDir += dirSeperator;

            Directory.CreateDirectory(mappingResultDir);
        }

        private static void LoadConfigFile(string configFile)
        {
            Dictionary<string, string> configParams = parseConfigFile(configFile);

            // Paths
            queryDescriptionFile = configParams["queryDescriptionFile"].Trim(new char[] { '\"' });
            conceptListFile = configParams["conceptListFile"].Trim(new char[] { '\"' });
            conceptGlobalBanFile = configParams["conceptGlobalBanFile"].Trim(new char[] { '\"' });
            conceptBanPerQueryFile = configParams["conceptBanPerQueryFile"].Trim(new char[] { '\"' });
            conceptHandPickedPerQueryFile = configParams["conceptHandPickedPerQueryFile"].Trim(new char[] { '\"' });
            idfTableFile = configParams["idfTableFile"].Trim(new char[] { '\"' });

            similarityTableFile = configParams["similarityTableFile"].Trim(new char[] { '\"' });
            mergedInfo_nConceptsFile = configParams["mergedInfo_nConceptsFile"].Trim(new char[] { '\"' });

            conceptResp_TableFile = configParams["conceptResp_TableFile"].Trim(new char[] { '\"' });
            conceptResp_VideoListFile = configParams["conceptResp_VideoListFile"].Trim(new char[] { '\"' });
            groundTruthFile = configParams["groundTruthFile"].Trim(new char[] { '\"' });

            mappingResultDir = configParams["mappingResultDir"].Trim(new char[] { '\"' });

            // Controllers
            switch_tfidf = int.Parse(configParams["switch_tfidf"]);
            switch_similarity = int.Parse(configParams["switch_similarity"]);

            // Parameters
            isMEDQuery = bool.Parse(configParams["isMEDQuery"]);
            isUseConceptBlacklist = bool.Parse(configParams["isUseConceptBlacklist"]);
            isUseHandPickedConcepts = bool.Parse(configParams["isUseHandPickedConcepts"]);
            thresh_rerankByGroup = int.Parse(configParams["thresh_rerankByGroup"]);
            isVerbFix = bool.Parse(configParams["isVerbFix"]);
            isMergedConceptDataset = bool.Parse(configParams["isMergedConceptDataset"]);
            isCalcMAP = bool.Parse(configParams["isCalcMAP"]);
            isCalcInfMAP = bool.Parse(configParams["isCalcInfMAP"]);
            isTestRandomResult = bool.Parse(configParams["isTestRandomResult"]);
            nThreadsForSimilarityRanking = int.Parse(configParams["nThreadsForSimilarityRanking"]);
            nTopConceptsOnly = int.Parse(configParams["nTopConceptsOnly"]);
            isExtendTopConceptsByConceptSimilarity = bool.Parse(configParams["isExtendTopConceptsByConceptSimilarity"]);
            isDoTopConceptSelection = bool.Parse(configParams["isDoTopConceptSelection"]);
            weightMultiplier_queryDefinitionTerms = double.Parse(configParams["weightMultiplier_queryDefinitionTerms"]);
            weightMultiplier_queryTitleTerms = double.Parse(configParams["weightMultiplier_queryTitleTerms"]);
            weightMultiplier_highWeightConcepts = double.Parse(configParams["weightMultiplier_highWeightConcepts"]);
            weightMultiplier_lowWeightConcepts = double.Parse(configParams["weightMultiplier_lowWeightConcepts"]);

            highWeightConceptSet = new HashSet<string>();
            foreach (string conceptNameA in configParams["highWeightConceptSet"].Split(','))
            {
                string conceptName = conceptNameA.Trim().Trim(new char[] { '\"' });
                highWeightConceptSet.Add(conceptName);
            }

            lowWeightConceptSet = new HashSet<string>();
            foreach (string conceptNameA in configParams["lowWeightConceptSet"].Split(','))
            {
                string conceptName = conceptNameA.Trim().Trim(new char[] { '\"' });
                lowWeightConceptSet.Add(conceptName);
            }

            queryIDOffset = int.Parse(configParams["queryIDOffset"]);
            stopWordFile = configParams["stopWordFile"].Trim(new char[] { '\"' });
        }

        private static Dictionary<string, string> parseConfigFile(string configFile)
        {
            Dictionary<string, string> configParams = new Dictionary<string, string>();
            using (StreamReader srConfig = new StreamReader(configFile))
            {
                string line = null;
                while ((line = srConfig.ReadLine()) != null)
                {
                    int posComment = line.IndexOf(';');
                    if (posComment >= 0)
                        line = line.Remove(posComment).Trim();
                    if (line == "")
                        continue;

                    string[] splitLine = line.Split('=');
                    Debug.Assert(splitLine.Length == 2);
                    configParams.Add(splitLine[0].Trim(), splitLine[1].Trim());
                }
            }

            return configParams;
        }



        // Data
        private static string dirSeperator = null;
        private static ConceptBank conceptBank = null;
        private static ConceptBank conceptBlacklist_global = null;
        private static ConcurrentDictionary<int, ConceptBank> conceptBlacklist_perEvent = null;
        private static List<EventKitDescription> eventkitList = null;
        private static HashSet<string> eventWordList = null;
        private static HashSet<string> conceptWordList = null;
        private static Dictionary<string, float> event_concept_termTable = null;
        private static ConcurrentDictionary<int, Dictionary<string, double>> conceptSimilarityList_allEvents = null;             //! concept name string changes from original
        private static Dictionary<string, double> idfTable = null;
        private static List<int> mergedInfo_nConceptsInOrigin = null;
        private static int idf_nTotalDocs = 0;
        private static double idf_logBase = 0.0;
        private static Random randgen = new Random();

        private static ConcurrentDictionary<int, HashSet<int>> irrelevantConceptIDList = null;
        private static ConcurrentDictionary<int, Dictionary<string, double>> irrelevantConceptEnergy = null;
        private static ConcurrentDictionary<int, Dictionary<string, Dictionary<int, float>>> video_conceptScoreList = null;
        private static ConcurrentDictionary<int, List<string>> videoRankingList_allEvents = null;
        private static Dictionary<int, HashSet<string>> event_positiveTruthList = null;
        private static Dictionary<int, HashSet<string>> event_negativeTruthList = null;
        private static Dictionary<int, HashSet<string>> event_unjudgedTruthList = null;
        //private static Dictionary<string, int> conceptName_dimensionIndex = null;
    }

    struct EventConceptTermPair
    {
        public string EventTerm;
        public string ConceptTerm;

        public EventConceptTermPair(string eventTerm, string conceptTerm)
        {
            EventTerm = eventTerm;
            ConceptTerm = conceptTerm;
        }
    }
}
