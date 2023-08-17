using FileSystemCrawler;
using MediaDevices;
using Newtonsoft.Json;
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

        [STAThread]
        static void Main(string[] args)
        {
            CopyConfiguration configuration = LoadCopyConfiguration();

            var devices = MediaDevice.GetDevices();

            var device = devices.Single(s => s.FriendlyName.Trim() == configuration.DeviceName);

            device.Connect();

            using(device)
            using (var assistant = new DeviceCrawlerAssistant(device))
            {
                var uniqueFileCrawler = new UniqueFileCrawler(assistant, configuration.IgnoreNames, new List<string>());

                DateTime _startDate = GetStartDate(configuration.TargetDirectory);
                DateTime _endDate = GetEndDate();

                var tempFileName = GetTempFileName(configuration.DeviceName);
                Dictionary<DateTime, List<VideoDetails>> files = null;

                //check if temp json exists
                if (File.Exists(tempFileName))
                {
                    //ask if should load from file
                    Console.WriteLine($"Found an existing file list. Enter Y to use the existing list.");

                    var choice = Console.ReadLine();

                    if (choice.Equals("Y", StringComparison.CurrentCultureIgnoreCase))
                    {
                        files = LoadTempFileList(tempFileName, _startDate, _endDate);
                    }
                    else
                    {
                        files = uniqueFileCrawler.CrawlFileSystem(configuration.SearchDirectory, _startDate, _endDate);
                    }
                }
                else
                {
                    files = uniqueFileCrawler.CrawlFileSystem(configuration.SearchDirectory, _startDate, _endDate);
                }

                //dump files to temp json
                SaveTempFileList(tempFileName, files);

                CopyAllFiles(device, files, configuration.TargetDirectory);

                Console.WriteLine("Finished copying files.");

                //delete temp file
                File.Delete(tempFileName);

                Console.ReadKey();
            }
        }

        private static CopyConfiguration LoadCopyConfiguration()
        {
            var availableConfigs = Directory.GetFiles("Configurations", "*.json", SearchOption.AllDirectories);

            Console.WriteLine("Please select a configuration:");

            var counter = 1;
            foreach (var availableConfig in availableConfigs)
            {
                Console.WriteLine($"{counter}: {availableConfigs[counter-1]}");
                counter++;
            }

            var selection = 0;
            while(selection < 1 || selection > availableConfigs.Length)
            {
                var s = Console.ReadLine();

                int.TryParse(s, out selection);
            }

            var content = File.ReadAllText(availableConfigs[selection - 1]);
            return JsonConvert.DeserializeObject<CopyConfiguration>(content);
        }

        private static Dictionary<DateTime, List<VideoDetails>> LoadTempFileList(string tempFileName, DateTime startDate, DateTime endDate)
        {
            var json = File.ReadAllText(tempFileName);

            var files = JsonConvert.DeserializeObject<Dictionary<DateTime, List<VideoDetails>>>(json);

            List<DateTime> datesToRemove = new List<DateTime>();

            //remove old files
            foreach(var pair in files)
            {
                var newList = pair.Value.Where(file => {
                    if (file.ActualFileDateTime < startDate || file.ActualFileDateTime >= endDate)
                    {
                        return false;
                    }

                    return true;
                }).ToList();

                pair.Value.Clear();
                pair.Value.AddRange(newList);

                if (pair.Value.Count == 0)
                {
                    datesToRemove.Add(pair.Key);
                }
            }

            foreach(var date in datesToRemove)
            {
                files.Remove(date);
            }

            return files;
        }

        private static void SaveTempFileList(string tempFileName, Dictionary<DateTime, List<VideoDetails>> files)
        {
            var json = JsonConvert.SerializeObject(files);

            File.WriteAllText(tempFileName, json);
        }

        private static String GetTempFileName(string deviceName)
        {
            return String.Format("{0}_TEMP.json", deviceName);
        }

        private static DateTime GetEndDate()
        {
            Console.WriteLine($"Enter Y to use current datetime as the end date");

            var choice = Console.ReadLine();

            if (choice.Equals("Y", StringComparison.CurrentCultureIgnoreCase))
            {
                return DateTime.Now;
            }

            String dateChoice = String.Empty;
            DateTime dateResult;

            while (!DateTime.TryParse(dateChoice, out dateResult))
            {
                Console.WriteLine($"Please enter an end date: ");
                dateChoice = Console.ReadLine();
            }

            return dateResult;
        }

        private static DateTime GetStartDate(string targetDirectory)
        {
            if (Directory.Exists(targetDirectory))
            {
                var allFiles = Directory.EnumerateFiles(targetDirectory, "*.*", SearchOption.AllDirectories);

                if (allFiles.Any())
                {
                    var maxDate = allFiles.Select(s => CrawlerFileInfo.FromFileInfo(new FileInfo(s)).GetActualFileDateTime()).Max();

                    maxDate = maxDate.Date;

                    Console.WriteLine($"Latest date in target directory is {maxDate}, enter Y to use as the start date");

                    var choice = Console.ReadLine();

                    if(choice.Equals("Y", StringComparison.CurrentCultureIgnoreCase))
                    {
                        return maxDate;
                    }
                }
            }

            String dateChoice = String.Empty;
            DateTime dateResult;

            while (!DateTime.TryParse(dateChoice, out dateResult))
            {
                Console.WriteLine($"Please enter a start date: ");
                dateChoice = Console.ReadLine();
            }

            return dateResult;
        }

        private static void CopyAllFiles(MediaDevice device, Dictionary<DateTime, List<VideoDetails>> files, string targetDirectory)
        {
            var allFiles = files.Values.SelectMany(v => v);
            var totalFileCount = allFiles.Count();
            var fileCounter = 1;
            foreach (var file in allFiles)
            {
                //TODO: add file count here
                Console.WriteLine($"{fileCounter} / {totalFileCount} : Copying file {file.FileInfo.FullName} to {targetDirectory}\\{file.ActualFileDateTime.Month:00}");

                var targetPath = Path.Combine(targetDirectory, $"{file.ActualFileDateTime.Month:00}");

                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                targetPath = Path.Combine(targetPath, file.FileInfo.Name);

                if (File.Exists(targetPath))
                {
                    Console.WriteLine($"Skipping duplicate file {targetPath}");
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

                fileCounter++;
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
