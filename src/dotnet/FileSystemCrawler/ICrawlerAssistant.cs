using System.Collections.Generic;

namespace FileSystemCrawler
{
    public interface ICrawlerAssistant
    {
        IEnumerable<string> GetDirectories(string startPath);
        CrawlerFileInfo GetFileInfo(string fileName);
        IEnumerable<string> GetFiles(string startPath, int yearMonthFilter);
        bool IsHidden(string directoryPath);
    }
}