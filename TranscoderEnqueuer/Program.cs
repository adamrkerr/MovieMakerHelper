using Amazon;
using Amazon.MediaConvert;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TranscoderEnqueuer
{
    partial class Program
    {
        private static TranscoderConfiguration _transcoderConfiguration;

        static void Main(string[] args)
        {
            _transcoderConfiguration = GetTranscoderConfiguration();

            int processYear = 0;

            while (processYear == 0)
            {
                Console.WriteLine("For what year do you want to process videos?");

                var yearString = Console.ReadLine();

                int.TryParse(yearString, out processYear);
            }

            int processMonth = 0;

            while (processMonth == 0)
            {
                Console.WriteLine("For what month do you want to process videos? (1-12)");

                var monthString = Console.ReadLine();

                int.TryParse(monthString, out processMonth);
            }

            var files = LoadFilesForMonth(processYear, processMonth);

            switch (_transcoderConfiguration.CurrentFunction)
            {
                case TranscoderFunctions.Subtitle:
                    GenerateSubtitleFiles(files);
                    break;
                case TranscoderFunctions.Compile:
                    EnqueueConversion(files);
                    break;
                case TranscoderFunctions.Test:
                    ListFiles(files);
                    break;
            }

            Console.ReadKey();
        }

        private static void ListFiles(List<VideoSummary> files)
        {
            foreach (var file in files.OrderBy(f => f.ActualFileDateTime))
            {
                Console.WriteLine($"{file.ActualFileDateTime} - {file.ARN} {file.RunTime}");
            }

            var totalRunTimeSeconds = files.Sum(t => t.RunTime.TotalSeconds);

            var totalRunTime = TimeSpan.FromSeconds(totalRunTimeSeconds);

            Console.WriteLine($"Total run time would be {totalRunTime}");
        }

        private static void GenerateSubtitleFiles(List<VideoSummary> files)
        {
            Console.WriteLine("Generating subtitles...");

            var distinctDates = files.Select(f => f.ActualFileDateTime.Date).Distinct();

            var textDictionary = new Dictionary<DateTime, string>();

            foreach (var date in distinctDates)
            {
                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("1");
                sb.AppendLine("00:00:00,500 --> 00:00:02,500");
                sb.AppendLine($"{date:MM/dd/yyyy}");
                sb.AppendLine();

                textDictionary.Add(date, sb.ToString());

                Console.WriteLine(textDictionary[date]);
            }

            Console.WriteLine("Enter \"Y\" to generate the above subtitle files. Enter any other key to discard.");

            var confirm = Console.ReadLine();

            if (!confirm.Equals("Y", StringComparison.CurrentCultureIgnoreCase))
            {
                Console.WriteLine("Generation cancelled.");
                return;
            }

            Console.WriteLine("Beginning subtitle generation.");

            WriteSubtitleFilesToS3(textDictionary);

            Console.WriteLine("Subtitle generation complete.");
        }

        private static void WriteSubtitleFilesToS3(Dictionary<DateTime, string> textDictionary)
        {
            using (var awsClient = new AmazonS3Client(GetConfig()))
            {
                foreach(var date in textDictionary)
                {
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = $"{_transcoderConfiguration.ArchiveBucketRoot}/{date.Key.Year}/{date.Key.Month}",
                        Key = $"{date.Key:yyyyMMdd}.srt",
                        ContentBody = date.Value,                        
                        StorageClass = S3StorageClass.ReducedRedundancy //this is easily regenerated
                    };

                    var result = awsClient.PutObject(putRequest);

                    if(result.HttpStatusCode == System.Net.HttpStatusCode.OK)
                    {
                        Console.WriteLine($"Created subtitle file \"{putRequest.BucketName}/{putRequest.Key}\"");
                    }
                    else
                    {

                        Console.WriteLine($"Failed to create subtitle file \"{putRequest.BucketName}/{putRequest.Key}\", error {result.HttpStatusCode}");
                    }
                }
            }
        }

        private static void EnqueueConversion(List<VideoSummary> files)
        {
            foreach (var file in files.OrderBy(f => f.ActualFileDateTime))
            {
                Console.WriteLine($"{file.ActualFileDateTime} - {file.ARN} {file.RunTime}");
            }

            var totalRunTimeSeconds = files.Sum(t => t.RunTime.TotalSeconds);

            var totalRunTime = TimeSpan.FromSeconds(totalRunTimeSeconds);

            Console.WriteLine($"Total run time would be {totalRunTime}");

            //using (var mediaClient = new AmazonMediaConvertClient(RegionEndpoint.GetBySystemName(_transcoderConfiguration.RegionName)))
            //{
            //    //mediaClient.
            //}
        }

        static List<VideoSummary> LoadFilesForMonth(int year, int month)
        {
            using (var awsClient = new AmazonS3Client(GetConfig()))
            {

                var listRequest = new ListObjectsV2Request
                {
                    BucketName = _transcoderConfiguration.ArchiveBucketRoot,
                    Prefix = $"{year}/{month}/"
                };

                var objects = awsClient.ListObjectsV2(listRequest);

                var summaries = objects.S3Objects
                                .Select(v => ConvertToVideoSummary(v, awsClient))
                                .Where(v => v != null) //filter out other files
                                .ToList();

                return summaries;
            }
        }

        private static VideoSummary ConvertToVideoSummary(S3Object arg, AmazonS3Client client)
        {
            var tagReq = new GetObjectTaggingRequest()
            {
                BucketName = arg.BucketName,
                Key = arg.Key
            };

            var tagResp = client.GetObjectTagging(tagReq);

            var tags = tagResp.Tagging;

            var fileName = tags.SingleOrDefault(t => t.Key == "OriginalPath")?.Value;

            if (string.IsNullOrEmpty(fileName))
            {
                return null; //this is not the file we are looking for
            }

            var dateTimeString = tags.SingleOrDefault(t => t.Key == "ActualFileDateTime")?.Value;
            var durationString = tags.SingleOrDefault(t => t.Key == "Duration")?.Value;
            var heightString = tags.SingleOrDefault(t => t.Key == "Height")?.Value;
            var widthString = tags.SingleOrDefault(t => t.Key == "Width")?.Value;

            var dateTime = DateTime.Parse(dateTimeString);
            var totalSeconds = double.Parse(durationString);

            var summary = new VideoSummary()
            {
                ARN = $"{arg.BucketName}/{arg.Key}",
                FileName = fileName,
                ActualFileDateTime = dateTime,
                RunTime = TimeSpan.FromSeconds(totalSeconds),
                Height = int.Parse(heightString),
                Width = int.Parse(widthString)
            };

            return summary;
        }

        static AmazonS3Config GetConfig()
        {
            return new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(_transcoderConfiguration.RegionName)
            };
        }
    }

    class VideoSummary
    {
        public string ARN { get; set; }

        public TimeSpan RunTime { get; set; }

        public DateTime ActualFileDateTime { get; set; }

        public string FileName { get; set; }

        public int Height { get; set; }

        public int Width { get; set; }
    }
}
