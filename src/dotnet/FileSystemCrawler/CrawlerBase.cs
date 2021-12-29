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

        private IEnumerable<int> GetYearMonthCollection(DateTime minDate, DateTime maxDate)
        {
            var dates = new List<DateTime>();

            var currentDate = new DateTime(minDate.Year, minDate.Month, 1);

            while(currentDate < maxDate)
            {
                dates.Add(currentDate);

                currentDate = currentDate.AddMonths(1);
            }

            return dates.Select(d => (d.Year * 100) + d.Month);
        }
        
        public Dictionary<DateTime, List<VideoDetails>> CrawlFileSystem(string startPath, DateTime minDate, DateTime maxDate)
        {
            var foundFiles = new Dictionary<DateTime, List<VideoDetails>>();

            //ignore whole directories
            if (IgnoredNames.Any(ig => startPath.ToLower().Contains(ig)))
            {
                return foundFiles;
            }

            Console.WriteLine($"{minDate:MM/dd/yyyy} {maxDate:MM/dd/yyy} Directory: {startPath}");

            var yearMonths = GetYearMonthCollection(minDate, maxDate);
            
            var fileNames = yearMonths.SelectMany(y => _assistant.GetFiles(startPath, y)).Distinct().ToList();

            var counter = 1;

            foreach (var fileName in fileNames)
            {
                Console.WriteLine($"Checking file {counter} of {fileNames.Count()}: {fileName}");
                counter++;

                var fileExtension = Path.GetExtension(fileName).ToLower();

                if (string.IsNullOrEmpty(fileExtension))
                {
                    continue;
                }

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

                Console.WriteLine($"File {fileName} is {file.Length / 1024} KB");

                if(file.Length < 1)
                {
                    Console.WriteLine("Zero length file detected, this may indicate a copy error.");
                }

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
