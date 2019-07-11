using Amazon;
using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
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

            Console.WriteLine("Enter \"X\" to quit, any other key to run again.");

            var cont = Console.ReadLine();

            if (!cont.Equals("X", StringComparison.CurrentCultureIgnoreCase))
            {
                Main(args);
            }
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
            using (var awsClient = new AmazonS3Client(GetCredentials(), GetConfig()))
            {
                foreach(var date in textDictionary)
                {
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = $"{_transcoderConfiguration.ArchiveBucketRoot}/{date.Key.Year}/{date.Key.Month}/subtitles",
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
            var minYear = files.Select(f => f.ActualFileDateTime.Year).Min();

            var jobRequest = new CreateJobRequest()
            {
                //Queue
                Role = _transcoderConfiguration.JobRoleName,
                StatusUpdateInterval = StatusUpdateInterval.SECONDS_60,
                Settings = new JobSettings
                {
                    OutputGroups = new List<OutputGroup>
                    {
                        new OutputGroup
                        {
                            Name = "File Group",
                            OutputGroupSettings = new OutputGroupSettings
                            {
                                Type = OutputGroupType.FILE_GROUP_SETTINGS,
                                FileGroupSettings = new FileGroupSettings
                                {
                                    Destination = $"s3://{_transcoderConfiguration.DestinationBucketRoot}/{minYear}/{files.Select(f => f.ActualFileDateTime).Min():yyyyMM}"
                                }
                            },
                            Outputs = new List<Output>
                            {
                                new Output
                                {
                                    Preset = "System-Generic_Hd_Mp4_Avc_Aac_16x9_1280x720p_24Hz_4.5Mbps", //may need to adjust this
                                    CaptionDescriptions = new List<CaptionDescription>
                                    {
                                        new CaptionDescription
                                        {
                                            CaptionSelectorName = "Captions Selector 1",
                                            LanguageCode = LanguageCode.ENG,
                                            DestinationSettings = new CaptionDestinationSettings
                                            {
                                                DestinationType = CaptionDestinationType.BURN_IN,
                                                BurninDestinationSettings = new BurninDestinationSettings
                                                {
                                                    Alignment = BurninSubtitleAlignment.LEFT,
                                                    TeletextSpacing = BurninSubtitleTeletextSpacing.FIXED_GRID,
                                                    OutlineSize = 2,
                                                    ShadowColor = BurninSubtitleShadowColor.NONE,
                                                    FontOpacity = 255,
                                                    FontSize = 0,
                                                    FontScript = FontScript.AUTOMATIC,
                                                    FontColor = BurninSubtitleFontColor.WHITE,
                                                    BackgroundColor = BurninSubtitleBackgroundColor.NONE,
                                                    FontResolution = 96,
                                                    OutlineColor = BurninSubtitleOutlineColor.BLACK
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    Inputs = new List<Input>()
                }
            };

            var previousDate = DateTime.MinValue;

            foreach (var file in files.OrderBy(f => f.ActualFileDateTime))
            {

                var input = GetDefaultInput();

                input.FileInput = $"s3://{file.ARN}";

                //if this is a new date, add the caption
                if (file.ActualFileDateTime.Date != previousDate)
                {
                    Console.WriteLine($"{file.ActualFileDateTime.Date}");

                    input.CaptionSelectors.Add("Captions Selector 1", new CaptionSelector
                    {
                        LanguageCode = LanguageCode.ENG,
                        SourceSettings = new CaptionSourceSettings
                        {
                            SourceType = CaptionSourceType.SRT,                            
                            FileSourceSettings = new FileSourceSettings
                            {
                                SourceFile = $"s3://{_transcoderConfiguration.ArchiveBucketRoot}/{file.ActualFileDateTime.Year}/{file.ActualFileDateTime.Month}/subtitles/{file.ActualFileDateTime.Date:yyyyMMdd}.srt"
                            }
                        }
                    });
                }

                Console.WriteLine($"{file.ActualFileDateTime} - {file.FileName} {file.RunTime}");

                jobRequest.Settings.Inputs.Add(input);

                previousDate = file.ActualFileDateTime.Date;
            }

            var totalRunTimeSeconds = files.Sum(t => t.RunTime.TotalSeconds);

            var totalRunTime = TimeSpan.FromSeconds(totalRunTimeSeconds);

            Console.WriteLine($"Total run time would be {totalRunTime}");

            Console.WriteLine("Enter \"Y\" to generate the above job. Enter any other key to discard.");

            var confirm = Console.ReadLine();

            if (!confirm.Equals("Y", StringComparison.CurrentCultureIgnoreCase))
            {
                Console.WriteLine("Generation cancelled.");
                return;
            }
            
            Console.WriteLine("Beginning video generation.");

            var config = new AmazonMediaConvertConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(_transcoderConfiguration.RegionName)
            };

            //one call against region to get endpoint
            using (var mediaClient = new AmazonMediaConvertClient(GetCredentials(), config))
            {
                //get the endpoint

                var epResponse = mediaClient.DescribeEndpoints(new DescribeEndpointsRequest());

                if (epResponse.HttpStatusCode != System.Net.HttpStatusCode.OK || epResponse.Endpoints.Count < 1)
                {
                    Console.WriteLine("Unable to get video generation endpoint.");
                    return;
                }

                config.RegionEndpoint = null;
                config.ServiceURL = epResponse.Endpoints[0].Url;
            }

            //now we have the specific endpoint
            using (var mediaClient = new AmazonMediaConvertClient(GetCredentials(), config))
            {
                var result = mediaClient.CreateJob(jobRequest);

                if (result.HttpStatusCode >= System.Net.HttpStatusCode.OK && (int)result.HttpStatusCode < 300)
                {
                    Console.WriteLine($"Created video job {result.Job.Arn}");
                }
                else
                {
                    Console.WriteLine($"Failed to create job, error {result.HttpStatusCode}");
                }
            }

            Console.WriteLine("Video generation complete.");
        }

        private static Input GetDefaultInput()
        {
            var input = new Input();

            input.AudioSelectors.Add("Audio Selector 1", new AudioSelector
            {
                Offset = 0,
                DefaultSelection = "DEFAULT",
                ProgramSelection = 1
            });

            input.VideoSelector = new VideoSelector
            {
                ColorSpace = ColorSpace.FOLLOW,
                Rotate = InputRotate.AUTO
            };

            input.FilterEnable = InputFilterEnable.AUTO;
            input.PsiControl = InputPsiControl.USE_PSI;
            input.FilterStrength = 0;
            input.DeblockFilter = InputDeblockFilter.DISABLED;
            input.DenoiseFilter = InputDenoiseFilter.DISABLED;
            input.TimecodeSource = InputTimecodeSource.EMBEDDED;

            return input;
        }

        static List<VideoSummary> LoadFilesForMonth(int year, int month)
        {
            using (var awsClient = new AmazonS3Client(GetCredentials(), GetConfig()))
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
                RegionEndpoint = RegionEndpoint.GetBySystemName(_transcoderConfiguration.RegionName),                
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
