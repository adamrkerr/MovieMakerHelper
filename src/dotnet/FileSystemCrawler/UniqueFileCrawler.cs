using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FileSystemCrawler
{

    public class UniqueFileCrawler : CrawlerBase
    {
        public UniqueFileCrawler(IEnumerable<string> namesToIgnore, IEnumerable<string> extensionsToSearch) : base(namesToIgnore, extensionsToSearch) { }
        
        /// <summary>
        /// In this implementation, we filter out files that are duplicates.
        /// Duplicate files are detected based on name and size, 
        /// it seems unlikely that two files with same name and same exact file size would not be the duplicates
        /// </summary>
        /// <param name="foundFiles"></param>
        /// <param name="childFiles"></param>
        protected override void ProcessChildFiles(Dictionary<DateTime, List<VideoDetails>> foundFiles, Dictionary<DateTime, List<VideoDetails>> childFiles)
        {
            foreach (var date in childFiles.Keys)
            {
                if (!foundFiles.ContainsKey(date))
                {
                    foundFiles.Add(date, new List<VideoDetails>());
                }

                foreach (var newFile in childFiles[date])
                {
                    //unlikely that two files with same name, same date, and same exact file size would not be the duplicates
                    var existing = foundFiles[date].FirstOrDefault(f => f.FileInfo.Name == newFile.FileInfo.Name && f.FileInfo.Length == newFile.FileInfo.Length);
                    if (existing == null)
                    {
                        foundFiles[date].Add(newFile);
                    }
                    else
                    {
                        existing.PossibleDuplicates++;
                        Console.WriteLine($"Found possible duplicate file: {existing.FileInfo.FullName} -- {newFile.FileInfo.FullName}");
                    }
                }

            }
        }
    }
}
