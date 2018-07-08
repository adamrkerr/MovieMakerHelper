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

        public Dictionary<DateTime, List<FileInfo>> CrawlFileSystem(string startPath, DateTime minDate, DateTime maxDate)
        {

            var foundFiles = new Dictionary<DateTime, List<FileInfo>>();

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

                var date = GetActualFileDateTime(file).Date;

                if (date < minDate || date >= maxDate)
                {
                    continue;
                }

                if (!foundFiles.ContainsKey(date))
                {
                    foundFiles.Add(date, new List<FileInfo>());
                }

                foundFiles[date].Add(file);
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

        protected abstract void ProcessChildFiles(Dictionary<DateTime, List<FileInfo>> foundFiles, Dictionary<DateTime, List<FileInfo>> childFiles);

        public static DateTime GetActualFileDateTime(FileInfo file)
        {
            var fileExtension = Path.GetExtension(file.Name);

            var date = file.LastWriteTime <= file.CreationTime ? file.LastWriteTime : file.CreationTime;

            var name = file.Name.Remove(file.Name.Length - fileExtension.Length);

            if (name.Length >= 15)
            {
                if (DateTime.TryParseExact(name.Substring(0, 15), "yyyyMMdd_HHmmss", CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    if (date != parsedDate)
                    {
                        date = parsedDate;
                    }
                }
            }
            else if (name.Length == 14)
            {
                if (DateTime.TryParseExact(name.Substring(0, 14), "yyyyMMddHHmmss", CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    if (date != parsedDate)
                    {
                        date = parsedDate;
                    }
                }
            }

            return date;
        }
    }

    public class UniqueFileCrawler : CrawlerBase
    {
        public UniqueFileCrawler(IEnumerable<string> namesToIgnore, IEnumerable<string> extensionsToSearch) :base(namesToIgnore, extensionsToSearch) { }

        /// <summary>
        /// In this implementation, we filter out files that are duplicates.
        /// Duplicate files are detected based on name and size, 
        /// it seems unlikely that two files with same name and same exact file size would not be the duplicates
        /// </summary>
        /// <param name="foundFiles"></param>
        /// <param name="childFiles"></param>
        protected override void ProcessChildFiles(Dictionary<DateTime, List<FileInfo>> foundFiles, Dictionary<DateTime, List<FileInfo>> childFiles)
        {
            foreach (var date in childFiles.Keys)
            {
                if (!foundFiles.ContainsKey(date))
                {
                    foundFiles.Add(date, new List<FileInfo>());
                }

                foreach (var newFile in childFiles[date])
                {
                    //unlikely that two files with same name and same exact file size would not be the duplicates
                    var existing = foundFiles[date].FirstOrDefault(f => f.Name == newFile.Name && f.Length == newFile.Length);
                    if (existing == null)
                    {
                        foundFiles[date].Add(newFile);
                    }
                    else
                    {
                        Console.WriteLine($"Found possible duplicate file: {existing.FullName} -- {newFile.FullName}");
                    }
                }

            }
        }
    }
}
