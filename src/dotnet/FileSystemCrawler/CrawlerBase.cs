using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FileSystemCrawler
{
    public abstract class CrawlerBase
    {
        private readonly ICrawlerAssistant _assistant;

        public CrawlerBase(ICrawlerAssistant assistant, IEnumerable<string> namesToIgnore, IEnumerable<string> extensionsToSearch)
        {
            IgnoredNames = namesToIgnore.ToList();
            IncludedExtensions = extensionsToSearch.ToList();
            this._assistant = assistant;
        }

        public List<string> IgnoredNames { get; set; }

        public List<string> IncludedExtensions { get; set; }
        
        public Dictionary<DateTime, List<VideoDetails>> CrawlFileSystem(string startPath, DateTime minDate, DateTime maxDate)
        {
            var foundFiles = new Dictionary<DateTime, List<VideoDetails>>();

            //ignore whole directories
            if (IgnoredNames.Any(ig => startPath.ToLower().Contains(ig)))
            {
                return foundFiles;
            }

            Console.WriteLine($"{minDate:MM/dd/yyyy} {maxDate:MM/dd/yyy} Directory: {startPath}");
            
            var fileNames = _assistant.GetFiles(startPath);

            var counter = 1;

            foreach (var fileName in fileNames)
            {
                Console.WriteLine($"Checking file {counter} of {fileNames.Count()}: {fileName}");
                counter++;

                var fileExtension = Path.GetExtension(fileName).ToLower();

                //Allow all extensions if empty
                if (IncludedExtensions.Any() && !IncludedExtensions.Contains(fileExtension))
                {
                    continue;
                }

                //filter stuff we know we don't want
                if (IgnoredNames.Any(ig => fileName.ToLower().Contains(ig)))
                {
                    continue;
                }

                var file = _assistant.GetFileInfo(fileName);

                var date = VideoDetails.GetActualFileDateTime(file).Date;

                if (date < minDate || date >= maxDate)
                {
                    continue;
                }

                if (!foundFiles.ContainsKey(date))
                {
                    foundFiles.Add(date, new List<VideoDetails>());
                }

                foundFiles[date].Add(new VideoDetails(file));
            }

            var directories = _assistant.GetDirectories(startPath);

            foreach (var childDirectory in directories)
            {

                if (_assistant.IsHidden(childDirectory))
                    continue;

                var childFiles = CrawlFileSystem(childDirectory, minDate, maxDate);

                ProcessChildFiles(foundFiles, childFiles);
            }

            return foundFiles;
        }

        

        protected abstract void ProcessChildFiles(Dictionary<DateTime, List<VideoDetails>> foundFiles, Dictionary<DateTime, List<VideoDetails>> childFiles);

    }

}
