﻿using FileSystemCrawler;
using Microsoft.DirectX.AudioVideoPlayback;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileCataloger
{
    class Program
    {
        private static DateTime _startDate = new DateTime(2013, 1, 1);
        private static DateTime _endDate = new DateTime(2014, 1, 1);
        private const string _searchDirectory = "F:\\";
        private const string _outputDirectoryFormat = "C:\\Users\\Adam\\Videos\\Reports\\Old Drive {0}.csv";
        static readonly string[] _movieExtensions = { ".mp4", ".mov", ".mts", ".avi", ".mpg", ".mpeg", ".asf", ".3gp" };
        static readonly string[] _ignoreNames = { "itunes", "top gear", "valve", "xgames", "top.gear", "pocket_lint", "fireproof",
            "\\zip disks\\", "\\videos\\", "\\local disk (g)\\movies\\", "\\my movies\\", "\\my documents\\my videos\\",
            "video0054.mp4", "video0056.mp4", "video0058.mp4", "video0061.mp4", "video0062.mp4", "video0063.mp4", "video0068.mp4",
            "video0072.mp4", "video0070.mp4", "video0071.mp4" };

        [STAThread]
        static void Main(string[] args)
        {
            var uniqueFileCrawler = new UniqueFileCrawler(new WindowsCrawlerAssistant(), _ignoreNames, _movieExtensions);

            var files = uniqueFileCrawler.CrawlFileSystem(_searchDirectory, _startDate, _endDate);

            var reportString = GenerateFileReport(files);
            var reportFileName = string.Format(_outputDirectoryFormat, $"{_startDate:yyyyMMdd} to {_endDate:yyyyMMdd}");
            using(var writer = new StreamWriter(reportFileName))
            {
                writer.Write(reportString);
            }

            Console.ReadKey();
        }


        [STAThread]
        private static string GenerateFileReport(Dictionary<DateTime, List<VideoDetails>> files)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Date,Path,Size,Year,Month,Day,Height,Width,Duration,Duplicates");
            foreach (var dateKey in files.Keys.OrderBy(k => k))
            {
                Console.WriteLine($"{dateKey}: {files[dateKey].Count} files");

                var filesForDate = files[dateKey].OrderBy(f => f.ActualFileDateTime);

                foreach (var file in filesForDate)
                {
                    var videoDetails = CompleteVideoDetails(file);

                    sb.AppendLine($"\"{videoDetails.ActualFileDateTime:MM/dd/yyyy HH:mm:ss}\",\"{videoDetails.FileInfo.FullName}\",\"{videoDetails.FileInfo.Length}\",\"{videoDetails.ActualFileDateTime.Year}\",\"{videoDetails.ActualFileDateTime.Month}\",\"{videoDetails.ActualFileDateTime.Day}\",\"{videoDetails.Height}\",\"{videoDetails.Width}\",\"{videoDetails.Duration}\",\"{videoDetails.PossibleDuplicates}\"");
                }
            }

            return sb.ToString();

        }

        [STAThread]
        private static VideoDetails CompleteVideoDetails(VideoDetails file)
        {
            try
            {
                using (
                    var video = new Video(file.FileInfo.FullName, false))
                {
                    file.Duration = video.Duration;
                    file.Height = video.DefaultSize.Height;
                    file.Width = video.DefaultSize.Width;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Unable to get video details for file {file.FileInfo.FullName}, error: {ex.Message}");
                file.Duration = -1;
                file.Height = -1;
                file.Width = -1;
            }

            return file;
        }



    }
}
