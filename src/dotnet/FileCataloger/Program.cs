using FileSystemCrawler;
using Microsoft.DirectX.AudioVideoPlayback;
using Newtonsoft.Json;
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

        [STAThread]
        static void Main(string[] args)
        {
            var configuration = LoadConfiguration();

            var uniqueFileCrawler = new UniqueFileCrawler(new WindowsCrawlerAssistant(), configuration.IgnoreNames, configuration.MovieExtensions);

            var files = uniqueFileCrawler.CrawlFileSystem(configuration.SearchDirectory, configuration.StartDate, configuration.EndDate);

            var reportString = GenerateFileReport(files);
            var reportFileName = string.Format(configuration.OutputDirectoryFormat, $"{configuration.StartDate:yyyyMMdd} to {configuration.EndDate:yyyyMMdd}");
            using(var writer = new StreamWriter(reportFileName))
            {
                writer.Write(reportString);
            }

            Console.ReadKey();
        }

        private static CatalogerConfig LoadConfiguration()
        {
            var availableConfigs = Directory.GetFiles("Configurations", "*.json", SearchOption.AllDirectories);

            Console.WriteLine("Please select a configuration:");

            var counter = 1;
            foreach (var availableConfig in availableConfigs)
            {
                Console.WriteLine($"{counter}: {availableConfigs[counter - 1]}");
                counter++;
            }

            var selection = 0;
            while (selection < 1 || selection > availableConfigs.Length)
            {
                var s = Console.ReadLine();

                int.TryParse(s, out selection);
            }

            var content = File.ReadAllText(availableConfigs[selection - 1]);
            JsonSerializerSettings formatSettings = new JsonSerializerSettings
            {
                DateFormatString = "yyyyMMdd"
            };


            return JsonConvert.DeserializeObject<CatalogerConfig>(content, formatSettings);
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
