using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ad_hoc_video_search_console
{
    public class StopWord
    {
        HashSet<string> stopwordList = null;

        public StopWord(string stopwordListFile)
        {
            stopwordList = new HashSet<string>();

            using (StreamReader swStopWord = new StreamReader(stopwordListFile))
            {
                string line = null;
                while ((line = swStopWord.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith(";"))           // skip comments
                        continue;
                    stopwordList.Add(line);
                }
            }
        }

        public bool IsStopword(string word)
        {
            if (stopwordList == null)
                throw new TypeInitializationException("StopWord", null);

            if (stopwordList.Contains(word))
                return true;
            else
                return false;
        }
    }
}
