using MediaDevices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

        public static CrawlerFileInfo FromFileInfo(MediaFileInfo fileInfo)
        {
            return new CrawlerFileInfo
            {
                CreationTime = fileInfo.CreationTime ?? DateTime.MinValue,
                Extension = Path.GetExtension(fileInfo.FullName),
                FullName = fileInfo.FullName,
                LastWriteTime = fileInfo.LastWriteTime ?? DateTime.MinValue,
                Length = (long)fileInfo.Length,
                Name = fileInfo.Name
            };
        }

        public static CrawlerFileInfo FromFileInfo(FileInfo fileInfo)
        {
            return new CrawlerFileInfo
            {
                CreationTime = fileInfo.CreationTime,
                Extension = Path.GetExtension(fileInfo.FullName),
                FullName = fileInfo.FullName,
                LastWriteTime = fileInfo.LastWriteTime,
                Length = (long)fileInfo.Length,
                Name = fileInfo.Name
            };
        }

        public DateTime GetActualFileDateTime()
        {
            var fileExtension = Path.GetExtension(this.Name);

            var date = this.LastWriteTime <= this.CreationTime ? this.LastWriteTime : this.CreationTime;

            var name = this.Name.Remove(this.Name.Length - fileExtension.Length);

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
