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

            foreach(var file in files.OrderBy(f => f.ActualFileDateTime))
            {
                Console.WriteLine($"{file.ActualFileDateTime} - {file.ARN} {file.RunTime}");
            }

            var totalRunTimeSeconds = files.Sum(t => t.RunTime.TotalSeconds);

            var totalRunTime = TimeSpan.FromSeconds(totalRunTimeSeconds);

            Console.WriteLine($"Total run time would be {totalRunTime}");

            EnqueueConversion(files);

            Console.ReadKey();
        }

        private static void EnqueueConversion(List<VideoSummary> files)
        {
            using (var mediaClient = new AmazonMediaConvertClient(RegionEndpoint.GetBySystemName(_transcoderConfiguration.RegionName)))
            {
                //mediaClient.
            }
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

                var summaries = objects.S3Objects.Select(v => ConvertToVideoSummary(v, awsClient)).ToList();
                
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

            var dateTimeString = tags.Single(t => t.Key == "ActualFileDateTime").Value;
            var durationString = tags.Single(t => t.Key == "Duration").Value;
            var fileName = tags.Single(t => t.Key == "OriginalPath").Value;

            var dateTime = DateTime.Parse(dateTimeString);

            var totalSeconds = double.Parse(durationString);

            return new VideoSummary()
            {
                ARN = $"{arg.BucketName}/{arg.Key}",
                FileName = fileName,
                ActualFileDateTime = dateTime,
                RunTime = TimeSpan.FromSeconds(totalSeconds)
            };
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
    }
}
