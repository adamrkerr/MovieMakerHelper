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

            var files = LoadFilesForMonth(2016, 1);

            switch (_transcoderConfiguration.CurrentFunction)
            {
                case TranscoderFunctions.Subtitle:
                    GenerateSubtitleFiles(files);
                    break;
                case TranscoderFunctions.Rotate:
                    EnqueueRotation(files);
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

        private static void EnqueueRotation(List<VideoSummary> files)
        {
            foreach(var file in files)
            {
                //we only care about "portrait" videos
                if(file.Height < file.Width)
                {
                    continue;
                }

                Console.WriteLine($"{file.ARN} {file.Width} x {file.Height}");
            }

            Console.WriteLine("Video Rotation Enqueue Completed...");
        }

        private static void GenerateSubtitleFiles(List<VideoSummary> files)
        {
            var distinctDates = files.Select(f => f.ActualFileDateTime.Date).Distinct();

            foreach(var date in distinctDates)
            {
                Console.WriteLine();
                Console.WriteLine("1");
                Console.WriteLine("00:00:00,500 --> 00:00:02,500");
                Console.WriteLine($"{date:MM/dd/yyyy}");
                Console.WriteLine();
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

                ListObjectsV2Request listRequest = new ListObjectsV2Request
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
