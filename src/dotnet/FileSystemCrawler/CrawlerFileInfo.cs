using System;
using System.Collections.Generic;
using System.Text;

namespace FileSystemCrawler
{
    public class CrawlerFileInfo
    {
        public string FullName { get; set; }
        public string Name{ get; set; }

        public DateTime LastWriteTime { get; set; }
        public DateTime CreationTime { get; set; }
        public long Length { get; internal set; }

        public string Extension { get; set; }
    }
}
