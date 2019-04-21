using FileSystemCrawler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader
{
    partial class Program
    {
        static bool _stopFlag = false;
        static StreamWriter _progressStream = null;
        static UploaderConfiguration _uploaderConfiguration = GetUploaderConfiguration();

        static void Main(string[] args)
        {
            try
            {
                var uploader = UploadFiles();
                var waiter = WaitForInput();

                Task.WaitAll(uploader, waiter);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                _progressStream?.Dispose();
            }
        }

        static async Task UploadFiles()
        {
            //create the filestream to record the uploads
            _progressStream = new StreamWriter(Path.Combine(_uploaderConfiguration.UploadRecordPath, $"Uploads_{DateTime.Now:yyyyMMddhhmmss}.csv"));

            var filesToUpload = await LoadVideoDetails();

            //TODO: filter the files by date?

            Console.WriteLine($"Loaded {filesToUpload.Count} records for upload");

            //put in a queue to make this easy
            var fileQueue = new Queue<VideoDetails>(filesToUpload);

            while (!_stopFlag && fileQueue.Any())
            {
                var detail = fileQueue.Dequeue();
                Console.WriteLine($"Uploading file {detail.FileInfo.FullName}");
                await _progressStream.WriteLineAsync($"\"{detail.ActualFileDateTime}\",\"{detail.FileInfo.FullName}\"");
                await _progressStream.FlushAsync();
                await Task.Delay(5000);
            }

            Console.WriteLine("Completed uploading, press any key to exit.");
            Console.ReadLine();
        }

        static async Task WaitForInput()
        {
            Console.ReadLine();
            _stopFlag = true;
        }

        static async Task<List<VideoDetails>> LoadVideoDetails()
        {
            var details = new List<VideoDetails>();

            using(var reader = new StreamReader(_uploaderConfiguration.FileCatalogPath))
            {
                //skip title line
                await reader.ReadLineAsync();
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();

                    var cells = line.Split(new[] { "," }, StringSplitOptions.None)
                        .Select(s => s.Trim('"'))
                        .ToArray();

                    var detail = new VideoDetails(DateTime.Parse(cells[0]))
                    {
                        FileInfo = new FileInfo(cells[1]),
                        Duration = double.Parse(cells[8]),
                        Height = int.Parse(cells[6]),
                        Width = int.Parse(cells[7])
                    };

                    details.Add(detail);
                }
            }

            return details;
        }
    }
}
