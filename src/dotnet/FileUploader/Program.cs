using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
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
        static WindowsCrawlerAssistant _windowsCrawlerAssistant = new WindowsCrawlerAssistant();

        static void Main(string[] args)
        {
            try
            {
                var uploader = UploadFiles();
                var waiter = WaitForInput();

                Task.WaitAll(uploader, waiter);
            }
            catch (Exception ex)
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
            var filesToUpload = await LoadVideoDetails();
            
            filesToUpload = filesToUpload.Where(f => f.ActualFileDateTime >= _uploaderConfiguration.OldestFileDate
                                                && f.ActualFileDateTime < _uploaderConfiguration.NewestFileDate)
                                                .OrderBy(f => f.ActualFileDateTime)
                                                .ToList();

            var totalUploadSize = filesToUpload.Sum(f => f.FileInfo.Length);

            Console.WriteLine($"Loaded {filesToUpload.Count} records for upload");

            //put in a queue to make this easy
            var fileQueue = new Queue<VideoDetails>(filesToUpload);

            //create the filestream to record the uploads
            using (_progressStream = new StreamWriter(Path.Combine(_uploaderConfiguration.UploadRecordPath, $"Uploads_{DateTime.Now:yyyyMMddhhmmss}.csv")))
            using (var awsClient = new AmazonS3Client(GetConfig()))
            {
                await _progressStream.WriteLineAsync("Date,Path,Success");

                var fileCounter = 1;
                long totalUploadCounter = 0;
                while (!_stopFlag && fileQueue.Any())
                {
                    var detail = fileQueue.Dequeue();
                                        
                    await UploadFile(awsClient, detail, fileCounter, filesToUpload.Count, totalUploadCounter, totalUploadSize);

                    fileCounter++;
                    totalUploadCounter += detail.FileInfo.Length;
                }
            }

            _progressStream = null;
            Console.WriteLine("Completed uploading, press any key to exit.");
            Console.ReadLine();
        }

        static AmazonS3Config GetConfig()
        {
            return new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(_uploaderConfiguration.RegionName)                
            };
        }

        static async Task UploadFile(AmazonS3Client s3Client, VideoDetails detail, int fileCounter, int totalCount, long totalCurrentUpload, long totalFinalUpload)
        {
            Console.WriteLine($"Uploading file {detail.FileInfo.FullName}");

            try
            {
                if (!File.Exists(detail.FileInfo.FullName))
                {
                    Console.WriteLine($"File {detail.FileInfo.FullName} was not found!");
                    throw new FileNotFoundException($"File {detail.FileInfo.FullName} was not found!");
                }

                var bucketName = $"{_uploaderConfiguration.ArchiveBucketRoot}/{detail.ActualFileDateTime.Year}/{detail.ActualFileDateTime.Month}";
                var fileKey = detail.FileInfo.Name;

                //check for existing file with same name
                fileKey = await GetUniqueFileKey(s3Client, detail, bucketName, fileKey);


                var transferRequest = new TransferUtilityUploadRequest
                {
                    BucketName = bucketName,
                    FilePath = detail.FileInfo.FullName,
                    StorageClass = _uploaderConfiguration.ArchiveStorageClass,
                    CannedACL = S3CannedACL.Private,
                    Key = fileKey
                };

                transferRequest.UploadProgressEvent += new EventHandler<UploadProgressArgs>((sender, e) => WriteProgess(sender, e, fileCounter, totalCount, totalCurrentUpload, totalFinalUpload));
                transferRequest.TagSet = new List<Tag>
                {
                    new Tag(){Key = nameof(VideoDetails.ActualFileDateTime), Value = detail.ActualFileDateTime.ToString()},
                    new Tag(){Key = "Year", Value = detail.ActualFileDateTime.Year.ToString()},
                    new Tag(){Key = "Month", Value = detail.ActualFileDateTime.Month.ToString()},
                    new Tag(){Key = "Day", Value = detail.ActualFileDateTime.Day.ToString()},
                    new Tag(){Key = nameof(VideoDetails.Duration), Value = detail.Duration.ToString()},
                    new Tag(){Key = nameof(VideoDetails.Height), Value = detail.Height.ToString()},
                    new Tag(){Key = nameof(VideoDetails.Width), Value = detail.Width.ToString()},
                    new Tag(){Key = "OriginalPath", Value = detail.FileInfo.FullName.Replace('\\', '/').Replace("'", "")},
                    new Tag(){Key = "Extension", Value = detail.FileInfo.Extension},
                    new Tag(){Key = "UploadedDate", Value = DateTime.Now.ToString()}
                };

                var utility = new TransferUtility(s3Client);

                await utility.UploadAsync(transferRequest);                                
            }
            catch (Exception ex)
            {
                await _progressStream.WriteLineAsync($"\"{detail.ActualFileDateTime}\",\"{detail.FileInfo.FullName}\",false,\"{ex.Message}\"");
                
                await _progressStream.FlushAsync();

                return;
            }

            await _progressStream.WriteLineAsync($"\"{detail.ActualFileDateTime}\",\"{detail.FileInfo.FullName}\",true");

            await _progressStream.FlushAsync();
        }

        private static async Task<string> GetUniqueFileKey(AmazonS3Client s3Client, VideoDetails detail, string bucketName, string fileKey, int increment = 1)
        {
            var existingObjectRequest = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = fileKey                
            };

            try
            {

                var existingResponse = await s3Client.GetObjectMetadataAsync(existingObjectRequest);

                if (existingResponse.HttpStatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    fileKey = (detail.FileInfo.Name.Replace(detail.FileInfo.Extension, string.Empty)) + $"_{increment}" + detail.FileInfo.Extension;

                    //check if the next increment exists
                    return await GetUniqueFileKey(s3Client, detail, bucketName, fileKey, increment++);
                }
            }
            catch(AmazonS3Exception ex)
            {
                if(ex.ErrorCode == "NotFound")
                {
                    return fileKey;
                }
                else
                {
                    throw;
                }
            }

            return fileKey;
        }

        private static void WriteProgess(object sender, UploadProgressArgs e, int fileCounter, int totalCount, long totalCurrentCount, long totalFinalCount)
        {
            Console.WriteLine($"File {fileCounter} / {totalCount} - Uploaded {e.TransferredBytes} / {e.TotalBytes}, {e.PercentDone}%, Overall {((float)(totalCurrentCount + e.TransferredBytes)/(float)totalFinalCount) * 100}%");
        }

        static async Task WaitForInput()
        {
            Console.ReadLine();
            _stopFlag = true;
        }

        static async Task<List<VideoDetails>> LoadVideoDetails()
        {
            var details = new List<VideoDetails>();

            using (var reader = new StreamReader(_uploaderConfiguration.FileCatalogPath))
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
                        FileInfo = _windowsCrawlerAssistant.GetFileInfo(cells[1]),
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
