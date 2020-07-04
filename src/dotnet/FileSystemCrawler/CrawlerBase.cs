using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FileSystemCrawler
{
    public abstract class CrawlerBase
    {
        public CrawlerBase(IEnumerable<string> namesToIgnore, IEnumerable<string> extensionsToSearch)
        {
            IgnoredNames = namesToIgnore.ToList();
            IncludedExtensions = extensionsToSearch.ToList();
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

            var directory = new DirectoryInfo(startPath);

            var files = directory.GetFiles();

            foreach (var file in files)
            {
                var fileExtension = Path.GetExtension(file.Name).ToLower();

                if (!IncludedExtensions.Contains(fileExtension))
                {
                    continue;
                }

                //filter stuff we know we don't want
                if (IgnoredNames.Any(ig => file.FullName.ToLower().Contains(ig)))
                {
                    continue;
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

            var directories = directory.GetDirectories();

            foreach (var childDirectory in directories)
            {

                if (childDirectory.Attributes.HasFlag(FileAttributes.Hidden))
                    continue;

                var childFiles = CrawlFileSystem(childDirectory.FullName, minDate, maxDate);

                ProcessChildFiles(foundFiles, childFiles);
            }

            return foundFiles;
        }

        protected abstract void ProcessChildFiles(Dictionary<DateTime, List<VideoDetails>> foundFiles, Dictionary<DateTime, List<VideoDetails>> childFiles);

    }

}
