using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FileSystemCrawler
{
    public class WindowsCrawlerAssistant : ICrawlerAssistant
    {
        public bool IsHidden(string directoryPath)
        {
            var directory = new DirectoryInfo(directoryPath);
            return directory.Attributes.HasFlag(FileAttributes.Hidden);
        }

        public CrawlerFileInfo GetFileInfo(string fileName)
        {
            var fileInfo = new FileInfo(fileName);

            var crawlerInfo = new CrawlerFileInfo()
            {
                FullName = fileInfo.FullName,
                Name = fileInfo.Name,
                CreationTime = fileInfo.CreationTime,
                LastWriteTime = fileInfo.LastWriteTime,
                Length = fileInfo.Length,
                Extension = fileInfo.Extension
            };

            return crawlerInfo;
        }

        public IEnumerable<string> GetDirectories(string startPath)
        {
            var directory = new DirectoryInfo(startPath);
            return directory.GetDirectories().Select(d => d.FullName).ToList();
        }

        public IEnumerable<string> GetFiles(string startPath, int yearMonthFilter)
        {
            var directory = new DirectoryInfo(startPath);
            //return directory.GetFiles($"{yearMonthFilter}*.*").Select(f => f.FullName).ToList();
            //Sorry, Liskov
            return directory.GetFiles("*.*").Select(f => f.FullName).ToList();
        }
    }
}
