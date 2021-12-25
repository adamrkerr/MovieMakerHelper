using FileSystemCrawler;
using MediaDevices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileCopier
{
    class Program
    {
        private static DateTime _startDate = new DateTime(2020, 1, 1);
        private static DateTime _endDate = new DateTime(2020, 8, 1);
        private const string _deviceName = "phone";
        private const string _searchDirectory = "\\Phone\\DCIM\\Camera";
        private const string _targetDirectory = "F:\\Phone 2020\\";
        static readonly string[] _ignoreNames = {"\\2015", "\\2016", "\\2017", "\\2018", "\\2019",
            "\\2020", 
            "\\202101", 
            "\\202102", 
            "\\202103",
            "\\202104",
            "\\202105",
            "\\202106",
            "\\202107",
            "\\202108",
            "\\202109",
            "\\202110"
        };

        [STAThread]
        static void Main(string[] args)
        {

            var devices = MediaDevice.GetDevices();

            var device = devices.Single(s => s.FriendlyName.Trim() == _deviceName);

            device.Connect();

            using(device)
            using (var assistant = new DeviceCrawlerAssistant(device))
            {
                var uniqueFileCrawler = new UniqueFileCrawler(assistant, _ignoreNames, new List<string>());

                var files = uniqueFileCrawler.CrawlFileSystem(_searchDirectory, _startDate, _endDate);

                CopyAllFiles(device, files, _targetDirectory);

                Console.WriteLine("Finished copying files.");

                Console.ReadKey();
            }
        }

        private static void CopyAllFiles(MediaDevice device, Dictionary<DateTime, List<VideoDetails>> files, string targetDirectory)
        {
            var totalFiles = files.Values.SelectMany(v => v);
            int counter = 1;
            int totalCount = totalFiles.Count();

            foreach (var file in totalFiles)
            {
                Console.WriteLine($"{counter} of {totalCount} : Copying file {file.FileInfo.FullName} to {targetDirectory}\\{file.ActualFileDateTime.Month:00}");

                var targetPath = Path.Combine(targetDirectory, $"{file.ActualFileDateTime.Month:00}");

                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                targetPath = Path.Combine(targetPath, file.FileInfo.Name);

                if (File.Exists(targetPath))
                {
                    var breaker = "we have a problem"; //figure out what to do about this...
                }
                else
                {
                    try
                    {
                        //    File.Copy(file.FileInfo.FullName, targetPath);
                        using (MemoryStream memoryStream = new System.IO.MemoryStream())
                        {
                            device.DownloadFile(file.FileInfo.FullName, memoryStream);
                            memoryStream.Position = 0;
                            WriteSreamToDisk(targetPath, memoryStream);
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"File {file.FileInfo.FullName} could not be copied {ex.Message}");
                    }
                }

                counter++;
            }
        }

        static void WriteSreamToDisk(string filePath, MemoryStream memoryStream)
        {
            using (FileStream file = new FileStream(filePath, FileMode.Create, System.IO.FileAccess.Write))
            {
                byte[] bytes = new byte[memoryStream.Length];
                memoryStream.Read(bytes, 0, (int)memoryStream.Length);
                file.Write(bytes, 0, bytes.Length);
                memoryStream.Close();
            }
        }
    }
}
