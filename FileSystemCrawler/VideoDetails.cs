using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FileSystemCrawler
{
    public class VideoDetails
    {
        public VideoDetails(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            ActualFileDateTime = GetActualFileDateTime(fileInfo);
        }

        public VideoDetails(DateTime actualTime)
        {
            ActualFileDateTime = actualTime;
        }

        public double Duration { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public FileInfo FileInfo { get; set; }
        public DateTime ActualFileDateTime { get; private set; }
        public int PossibleDuplicates { get; set; }

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
            else if (name.Length == 12)
            {
                if (DateTime.TryParseExact(name.Substring(0, 12), "yyyyMMddHHmm", CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime parsedDate))
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
}
