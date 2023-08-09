using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FileSystemCrawler
{
    public class VideoDetails
    {
        public VideoDetails()
        {

        }

        public VideoDetails(CrawlerFileInfo fileInfo)
        {
            FileInfo = fileInfo;
            ActualFileDateTime = fileInfo.GetActualFileDateTime();
        }

        public VideoDetails(DateTime actualTime)
        {
            ActualFileDateTime = actualTime;
        }

        public double Duration { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public CrawlerFileInfo FileInfo { get; set; }
        public DateTime ActualFileDateTime { get; private set; }
        public int PossibleDuplicates { get; set; }
                
    }
}
