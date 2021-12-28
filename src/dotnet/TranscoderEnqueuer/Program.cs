using Amazon;
using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            int processMonth = 0;
            int processDay = 0;
            DateTime? processDate = null;

            int endYear = 0;
            int endMonth = 0;
            int endDay = 0;
            DateTime? endDate = null;

            while (processDate == null)
            {
                Console.WriteLine("Enter a year and month (YYYYMM) or a date range (YYYYMMDD YYYYMMDD) to process videos");

                var yearString = Console.ReadLine();

                if(yearString.Length == 6)
                {
                    int.TryParse(yearString.Substring(0, 4), out processYear);
                    int.TryParse(yearString.Substring(4, 2), out processMonth);
                    processDate = new DateTime(processYear, processMonth, 1);
                }
                else if (yearString.Length == 17)
                {
                    int.TryParse(yearString.Substring(0, 4), out processYear);
                    int.TryParse(yearString.Substring(4, 2), out processMonth);
                    int.TryParse(yearString.Substring(6, 2), out processDay);
                    processDate = new DateTime(processYear, processMonth, processDay);

                    int.TryParse(yearString.Substring(9, 4), out endYear);
                    int.TryParse(yearString.Substring(13, 2), out endMonth);
                    int.TryParse(yearString.Substring(15, 2), out endDay);
                    endDate = new DateTime(endYear, endMonth, endDay);
                }
                else
                {
                    Console.WriteLine("Invalid date range, please try again.");
                }

            }
            List<VideoSummary> files = new List<VideoSummary>();

            if (endDate == null)
            {
                files = LoadFilesForMonth(processDate.Value);
            }
            else
            {
                files = LoadFilesForRange(processDate.Value, endDate.Value);
            }

            Console.WriteLine("What do you want to do?");
            Console.WriteLine("1) Subtitle");
            Console.WriteLine("2) Compile");
            Console.WriteLine("3) Subtitle & Compile");
            Console.WriteLine("4) Glacierize");
            Console.WriteLine("5) Test");
            Console.WriteLine("6) Exit");

            int command = 0;

            while (command == 0)
            {
                var commandString = Console.ReadLine();

                int.TryParse(commandString, out command);

                if(command < 1 || command > 6)
                {
                    command = 0;
                }
            }
            
            switch ((TranscoderFunctions)command)
            {
                case TranscoderFunctions.Subtitle:
                    GenerateSubtitleFiles(files);
                    break;
                case TranscoderFunctions.Compile:
                    EnqueueConversion(files, processDate.Value, endDate);
                    break;
                case TranscoderFunctions.SubtitleAndCompile:
                    if (GenerateSubtitleFiles(files))
                    {
                        //only do this if above was ok
                        EnqueueConversion(files, processDate.Value, endDate);
                    }
                    break;
                case TranscoderFunctions.Glacierize:
                    Glacierize(files);
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
        
        private static void Glacierize(List<VideoSummary> files)
        {
            Console.WriteLine($"Enter \"Y\" to glacierize {files.Count} files. Enter any other key to return.");

            var confirm = Console.ReadLine();

            if (!confirm.Equals("Y", StringComparison.CurrentCultureIgnoreCase))
            {
                Console.WriteLine("Glacierization cancelled.");
                return;
            }

            using(var client = new AmazonS3Client(GetCredentials(), GetConfig()))
            {
                foreach(var file in files)
                {
                    //3gp seems to have audio codec problems
                    if (file.FileName.EndsWith(".3gp", true, CultureInfo.CurrentCulture))
                    {
                        Console.WriteLine($"UNSUPPORTED: {file.FileName}");
                        continue;
                    }

                    var request = new CopyObjectRequest()
                    {
                        DestinationBucket = _transcoderConfiguration.ArchiveBucketRoot,
                        DestinationKey = file.ARN.Replace(_transcoderConfiguration.ArchiveBucketRoot, string.Empty),
                        SourceBucket = _transcoderConfiguration.ArchiveBucketRoot,
                        SourceKey = file.ARN.Replace(_transcoderConfiguration.ArchiveBucketRoot, string.Empty),
                        CannedACL = S3CannedACL.Private,
                        StorageClass = S3StorageClass.Glacier
                    };

                    var result = client.CopyObject(request);

                    if ((int)result.HttpStatusCode < 200 || (int)result.HttpStatusCode >= 300)
                    {
                        Console.WriteLine($"Failed glacierize file {file.ARN}, {result.HttpStatusCode}");
                    }
                    else
                    {
                        Console.WriteLine($"Glacierized file {file.ARN}");
                    }
                }
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

        private static bool GenerateSubtitleFiles(List<VideoSummary> files)
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
                return false;
            }

            Console.WriteLine("Beginning subtitle generation.");

            WriteSubtitleFilesToS3(textDictionary);

            Console.WriteLine("Subtitle generation complete.");

            return true;
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

        const string CAPTIONS_SELECTOR = "Captions Selector 1";

        private static void EnqueueConversion(List<VideoSummary> files, DateTime processDate, DateTime? endDate)
        {
            var jobRequest = GenerateJobRequest(processDate, endDate);

            var previousDate = DateTime.MinValue;

            foreach (var file in files.OrderBy(f => f.ActualFileDateTime))
            {

                //3gp seems to have audio codec problems
                if (file.FileName.EndsWith(".3gp", true, CultureInfo.CurrentCulture)){
                    Console.WriteLine($"UNSUPPORTED: {file.FileName}");
                    continue;
                }

                var input = GetDefaultInput();

                input.FileInput = $"s3://{file.ARN}";

                //if this is a new date, add the caption
                if (file.ActualFileDateTime.Date != previousDate)
                {
                    Console.WriteLine($"{file.ActualFileDateTime.Date}");

                    input.CaptionSelectors.Add(CAPTIONS_SELECTOR, new CaptionSelector
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
                else
                {
                    //have to add a blank caption when we don't want one to show
                    input.CaptionSelectors.Add(CAPTIONS_SELECTOR, new CaptionSelector
                    {
                        LanguageCode = LanguageCode.ENG,
                        SourceSettings = new CaptionSourceSettings
                        {
                            SourceType = CaptionSourceType.SRT,
                            FileSourceSettings = new FileSourceSettings
                            {
                                SourceFile = $"s3://{_transcoderConfiguration.ArchiveBucketRoot}/blank.srt"
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

        private static CreateJobRequest GenerateJobRequest(DateTime minDate, DateTime? maxDate)
        {
            var destination = $"s3://{_transcoderConfiguration.DestinationBucketRoot}/{minDate.Year}/{minDate:yyyyMM}";

            if(maxDate != null)
            {
                destination = $"s3://{_transcoderConfiguration.DestinationBucketRoot}/{minDate.Year}/{minDate:yyyyMMdd}_{maxDate:yyyyMMdd}";
            }

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
                                    Destination = destination
                                }
                            },
                            Outputs = new List<Output>
                            {
                                new Output
                                {
                                    Preset = "System-Generic_Hd_Mp4_Avc_Aac_16x9_1920x1080p_60Hz_9Mbps", //may need to adjust this
                                    CaptionDescriptions = new List<CaptionDescription>
                                    {
                                        new CaptionDescription
                                        {
                                            CaptionSelectorName = CAPTIONS_SELECTOR,
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
            return jobRequest;
        }

        private static Input GetDefaultInput()
        {
            var input = new Input();

            input.AudioSelectors.Add("Audio Selector 1", new AudioSelector
            {
                Offset = 0,
                DefaultSelection = AudioDefaultSelection.DEFAULT,
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


        private static List<VideoSummary> LoadFilesForRange(DateTime processDate, DateTime endDate)
        {
            if(processDate.Year == endDate.Year && processDate.Month == endDate.Month)
            {
                var summaries = LoadFilesForMonth(processDate)
                                .Where(v => v.ActualFileDateTime >= processDate
                                    && v.ActualFileDateTime < endDate.AddDays(1)) //filter out other dates
                                .ToList();

                return summaries;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        static List<VideoSummary> LoadFilesForMonth(DateTime processDate)
        {
            using (var awsClient = new AmazonS3Client(GetCredentials(), GetConfig()))
            {

                var listRequest = new ListObjectsV2Request
                {
                    BucketName = _transcoderConfiguration.ArchiveBucketRoot,
                    Prefix = $"{processDate.Year}/{processDate.Month}/"
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
