using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ad_hoc_video_search_console
{
    public class EventEvidence
    {
        List<HashSet<string>> scene_nouns = null;
        HashSet<string> scene_verbs = null;
        List<HashSet<string>> objects_nouns = null;
        HashSet<string> objects_verbs = null;
        List<HashSet<string>> activities_nouns = null;
        HashSet<string> activities_verbs = null;
        List<HashSet<string>> audio_nouns = null;
        HashSet<string> audio_verbs = null;
        HashSet<string> wordList_withoutAudio;
        HashSet<string> verbList;
        HashSet<string> wordList;

        public EventEvidence()
        {
        }

        public bool ParseDescription(string description, StopWord stopword)
        {
            description = description.Trim();
            if (!description.StartsWith("scene:"))
                throw new FormatException("Event evidence string does not start with formal head.");

            // -------------- Retrieve substring of each type -------------- //
            int pos_scene = 0;
            int pos_objects = description.IndexOf("objects/people:");
            int pos_activities = description.IndexOf("activities:");
            int pos_audio = description.IndexOf("audio:");
            if (pos_objects < 1 || pos_activities < 1 || pos_audio < 1)
                throw new FormatException("One or more types of evidence are missing.");
            if (!(pos_scene < pos_objects && pos_objects < pos_activities && pos_activities < pos_audio))
                throw new FormatException("Types of evidence are in wrong order.");

            int pos_scene_string = pos_scene + 6;
            int pos_objects_string = pos_objects + 15;
            int pos_activities_string = pos_activities + 11;
            int pos_audio_string = pos_audio + 6;

            int length_scene_string = pos_objects - pos_scene_string;
            int length_objects_string = pos_activities - pos_objects_string;
            int length_activities_string = pos_audio - pos_activities_string;
            int length_audio_string = description.Length - pos_audio_string;
            if (length_scene_string < 0 || length_objects_string < 0 || length_activities_string < 0 || length_audio_string < 0)
                throw new FormatException("Types of evidence are in wrong order.");

            string description_scene = description.Substring(pos_scene_string, length_scene_string).Trim();
            string description_objects = description.Substring(pos_objects_string, length_objects_string).Trim();
            string description_activities = description.Substring(pos_activities_string, length_activities_string).Trim();
            string description_audio = description.Substring(pos_audio_string, length_audio_string).Trim();

            // ---------------------- Parse ------------------------- //
            NLPSentenceParser nlpParser = new NLPSentenceParser();

            int countEmpty = 0;

            if (nlpParser.ParseSentence(description_scene, stopword))
            {
                scene_nouns = new List<HashSet<string>>(nlpParser.NounPhraseList);
                scene_verbs = new HashSet<string>(nlpParser.VerbList);
            }
            else
            {
                countEmpty++;
                scene_nouns = new List<HashSet<string>>();
                scene_verbs = new HashSet<string>();
            }

            if (nlpParser.ParseSentence(description_objects, stopword))
            {
                objects_nouns = new List<HashSet<string>>(nlpParser.NounPhraseList);
                objects_verbs = new HashSet<string>(nlpParser.VerbList);
            }
            else
            {
                countEmpty++;
                objects_nouns = new List<HashSet<string>>();
                objects_verbs = new HashSet<string>();
            }

            if (nlpParser.ParseSentence(description_activities, stopword))
            {
                activities_nouns = new List<HashSet<string>>(nlpParser.NounPhraseList);
                activities_verbs = new HashSet<string>(nlpParser.VerbList);
            }
            else
            {
                countEmpty++;
                activities_nouns = new List<HashSet<string>>();
                activities_verbs = new HashSet<string>();
            }

            if (nlpParser.ParseSentence(description_audio, stopword))
            {
                audio_nouns = new List<HashSet<string>>(nlpParser.NounPhraseList);
                audio_verbs = new HashSet<string>(nlpParser.VerbList);
            }
            else
            {
                countEmpty++;
                audio_nouns = new List<HashSet<string>>();
                audio_verbs = new HashSet<string>();
            }

            MakeWordList();

            if (countEmpty == 4)
                return false;
            else
                return true;            // missing type is allowed
        }

        public void MakeWordList()
        {
            if (scene_nouns == null || audio_verbs == null)
                throw new TypeInitializationException("EventEvidence", null);

            wordList_withoutAudio = new HashSet<string>();
            verbList = new HashSet<string>();
            wordList = new HashSet<string>();

            foreach (HashSet<string> termList in scene_nouns)
            {
                foreach (string term in termList)
                {
                    wordList_withoutAudio.Add(term);
                    wordList.Add(term);
                }
            }
            foreach (string verb in scene_verbs)
            {
                wordList_withoutAudio.Add(verb);
                verbList.Add(verb);
                wordList.Add(verb);
            }

            foreach (HashSet<string> termList in objects_nouns)
            {
                foreach (string term in termList)
                {
                    wordList_withoutAudio.Add(term);
                    wordList.Add(term);
                }
            }
            foreach (string verb in objects_verbs)
            {
                wordList_withoutAudio.Add(verb);
                verbList.Add(verb);
                wordList.Add(verb);
            }

            foreach (HashSet<string> termList in activities_nouns)
            {
                foreach (string term in termList)
                {
                    wordList_withoutAudio.Add(term);
                    wordList.Add(term);
                }
            }
            foreach (string verb in activities_verbs)
            {
                wordList_withoutAudio.Add(verb);
                verbList.Add(verb);
                wordList.Add(verb);
            }

            // audio evidence
            foreach (HashSet<string> termList in audio_nouns)
            {
                foreach (string term in termList)
                {
                    wordList.Add(term);
                }
            }
            foreach (string verb in audio_verbs)
            {
                verbList.Add(verb);
                wordList.Add(verb);
            }
        }

        public string PrintEvidence()
        {
            string description = "Evidential Description: ";

            description += "scene: " + NLPSentenceParser.MakeNLPSentence(scene_nouns, scene_verbs) + "\n";
            description += new String(' ', 24) + "objects/people: " + NLPSentenceParser.MakeNLPSentence(objects_nouns, objects_verbs) + "\n";
            description += new String(' ', 24) + "activities: " + NLPSentenceParser.MakeNLPSentence(activities_nouns, activities_verbs) + "\n";
            description += new String(' ', 24) + "audio: " + NLPSentenceParser.MakeNLPSentence(audio_nouns, audio_verbs);

            return description;
        }


        ////////////////////////////////////////////////////////////////////////////////
        public List<HashSet<string>> Scene_Nouns
        {
            get { return this.scene_nouns; }
        }
        public HashSet<string> Scene_NounTerms
        {
            get
            {
                HashSet<String> nounTerms = new HashSet<String>();
                foreach (HashSet<string> phrase in this.scene_nouns)
                {
                    foreach (string noun in phrase)
                    {
                        nounTerms.Add(noun);
                    }
                }
                return nounTerms;
            }
        }
        public HashSet<string> Scene_Verbs
        {
            get { return this.scene_verbs; }
        }
        public List<HashSet<string>> Objects_Nouns
        {
            get { return this.objects_nouns; }
        }
        public HashSet<string> Objects_Verbs
        {
            get { return this.objects_verbs; }
        }
        public List<HashSet<string>> Activities_Nouns
        {
            get { return this.activities_nouns; }
        }
        public HashSet<string> Activities_Verbs
        {
            get { return this.activities_verbs; }
        }
        public List<HashSet<string>> Audio_Nouns
        {
            get { return this.audio_nouns; }
        }
        public HashSet<string> Audio_Verbs
        {
            get { return this.audio_verbs; }
        }

        public HashSet<string> WordList_WithoutAudio
        {
            get { return this.wordList_withoutAudio; }
        }

        public HashSet<string> VerbList
        {
            get { return this.verbList; }
        }

        public HashSet<string> WordList
        {
            get { return this.wordList; }
        }
    }
}
