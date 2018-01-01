using Microsoft.DirectX.AudioVideoPlayback;
using Shell32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MovieMakerHelper
{
 
    class Program
    {

        [STAThread]
        static void Main(string[] args)
        {

            //var project = new Project() { name = "Test", themeId = "0", version = "65540", templateID = "SimpleProjectTemplate" };
            var minDate = new DateTime(2005, 1, 1);
            var maxDate = new DateTime(2006, 1, 1);
            var files = CrawlFileSystem("G:\\", minDate, maxDate);

            var project = GenerateDefaultProject($"{minDate:yyyyMMdd} to {maxDate:yyyyMMdd}");

            AddFilesToProject(project, files);

            var deserializer = new XmlSerializer(typeof(Project));

            using (var stream = new FileStream($"C:\\Users\\Adam\\Videos\\{project.name}.wlmp", FileMode.CreateNew))
            {
                deserializer.Serialize(stream, project);
            }
        }


        [STAThread]
        private static void AddFilesToProject(Project project, Dictionary<DateTime, List<FileInfo>> files)
        {
            var mediaItems = new List<ProjectMediaItem>();
            var mediaItemCounter = 1;
            var extentCounter = 5;
            var videoClips = new List<ProjectExtentsVideoClip>();
            var titleClips = new List<ProjectExtentsVideoClip>();
            var videoExtentRefs = new List<ProjectExtentsExtentSelectorExtentRef>();
            var titleExtentRefs = new List<ProjectExtentsExtentSelectorExtentRef>();
            var extents = new ProjectExtents();

            foreach (var dateKey in files.Keys.OrderBy(k => k))
            {
                var filesForDate = files[dateKey].OrderBy(f => GetActualFileDateTime(f));

                var isFirst = true;

                foreach (var file in filesForDate)
                {
                    var videoDetails = GetVideoDetails(file);

                    var mediaItem = new ProjectMediaItem
                    {
                        id = $"{mediaItemCounter}",
                        filePath = file.FullName,
                        arWidth = videoDetails.Width.ToString(), //TODO
                        arHeight = videoDetails.Height.ToString(), //TODO
                        duration = videoDetails.Duration.ToString(), //TODO
                        stabilizationMode = "0",
                        mediaItemType = "1",
                        songTitle = string.Empty,
                        songAlbum = string.Empty,
                        songArtist = string.Empty,
                        songArtistUrl = string.Empty,
                        songAudioFileUrl = string.Empty,
                        songCopyrightUrl = string.Empty
                    };

                    mediaItems.Add(mediaItem);

                    mediaItemCounter++;

                    var videoClip = new ProjectExtentsVideoClip
                    {
                        extentID = $"{extentCounter}",
                        gapBefore = "0",
                        mediaItemID = mediaItem.id,
                        inTime = "0",
                        outTime = "0",
                        speed = "1",
                        stabilizationMode = "0",
                        BoundProperties = GenerateDefaultVideoBoundProperties(),
                        Effects = string.Empty,
                        Transitions = new Transitions()
                    };

                    if (isFirst)
                    {
                        //add fade in
                        //add date heading?
                        isFirst = false;
                    }
                    else
                    {
                        //add diagonal transition
                    }

                    videoClips.Add(videoClip);

                    videoExtentRefs.Add(new ProjectExtentsExtentSelectorExtentRef
                    {
                        id = videoClip.extentID
                    });

                    extentCounter++;
                }

            }

            extents.VideoClip = videoClips.ToArray();

            extents.ExtentSelector = new[] {
                new ProjectExtentsExtentSelector
                {
                    extentID = "1",
                    gapBefore = "0",
                    primaryTrack = "true",
                    Effects = String.Empty,
                    BoundProperties = string.Empty,
                    Transitions = string.Empty,
                    ExtentRefs = videoExtentRefs.ToArray()
                },
                new ProjectExtentsExtentSelector
                {
                    extentID = "2",
                    gapBefore = "0",
                    primaryTrack = "false",
                    Effects = String.Empty,
                    BoundProperties = string.Empty,
                    Transitions = string.Empty,
                    ExtentRefs = new ProjectExtentsExtentSelectorExtentRef[0]
                },
                new ProjectExtentsExtentSelector
                {
                    extentID = "3",
                    gapBefore = "0",
                    primaryTrack = "false",
                    Effects = String.Empty,
                    BoundProperties = string.Empty,
                    Transitions = string.Empty,
                    ExtentRefs = new ProjectExtentsExtentSelectorExtentRef[0]
                },
                new ProjectExtentsExtentSelector
                {
                    extentID = "4",
                    gapBefore = "0",
                    primaryTrack = "false",
                    Effects = String.Empty,
                    BoundProperties = string.Empty,
                    Transitions = string.Empty,
                    ExtentRefs = titleExtentRefs.ToArray()
                }
            };

            project.MediaItems = mediaItems.ToArray();

            project.Extents = extents;
        }

        [STAThread]
        private static VideoDetails GetVideoDetails(FileInfo file)
        {
            using(var video = new Video(file.FullName, false)){

                return new VideoDetails
                {
                    Duration = video.Duration,
                    Height = video.Size.Height,
                    Width = video.Size.Width
                };
            }
        }

        private static BoundProperties GenerateDefaultVideoBoundProperties()
        {
            var boundProperties = new BoundProperties
            {
                BoundPropertyBool = new[] { new BoundPropertiesBoundPropertyBool {
                Name = "Mute",
                Value = "false"
                }
                },
                BoundPropertyInt = new[] { new BoundPropertiesBoundPropertyInt {
                    Name="rotateStepNinety",
                    Value = "0"
                } },
                BoundPropertyFloat = new[] { new BoundPropertiesBoundPropertyFloat
                {
                    Name = "Volume",
                    Value = "1"
                }
                }
            };

            return boundProperties;
        }

        //private static Transitions GenerateFadeBlackTransition()
        //{
        //    var transitions = new Transitions()
        //    {
        //        ShaderEffect = new[] { }
        //    };

        //    return transitions;
        //}

        static readonly string[] movieExtensions = { ".mp4", ".mov", ".mts", ".avi", ".mpg", ".mpeg", ".asf" };
        static readonly string[] ignoreNames = { "itunes", "top gear" };
        static Dictionary<DateTime, List<FileInfo>> CrawlFileSystem(string startPath, DateTime minDate, DateTime maxDate)
        {
            var foundFiles = new Dictionary<DateTime, List<FileInfo>>();

            var directory = new DirectoryInfo(startPath);

            var files = directory.GetFiles();

            foreach (var file in files)
            {
                var fileExtension = Path.GetExtension(file.Name).ToLower();

                if (!movieExtensions.Contains(fileExtension))
                {
                    continue;
                }

                var date = GetActualFileDateTime(file);

                if (date < minDate || date >= maxDate)
                {
                    continue;
                }

                //filter stuff we know we don't want
                if(ignoreNames.Any(ig => file.FullName.ToLower().Contains(ig)))
                {
                    continue;
                }

                if (!foundFiles.ContainsKey(date))
                {
                    foundFiles.Add(date, new List<FileInfo>());
                }

                foundFiles[date].Add(file);
            }

            var directories = directory.GetDirectories();

            foreach (var childDirectory in directories)
            {

                if (childDirectory.Attributes.HasFlag(FileAttributes.Hidden))
                    continue;

                var childFiles = CrawlFileSystem(childDirectory.FullName, minDate, maxDate);

                foreach (var date in childFiles.Keys)
                {
                    if (!foundFiles.ContainsKey(date))
                    {
                        foundFiles.Add(date, new List<FileInfo>());
                    }

                    foreach (var newFile in childFiles[date])
                    {
                        //unlikely that two files with same name and same exact file size would not be the duplicates
                        var existing = foundFiles[date].FirstOrDefault(f => f.Name == newFile.Name && f.Length == newFile.Length);
                        if (existing == null)
                        {
                            foundFiles[date].Add(newFile);
                        }
                        else
                        {
                            Console.WriteLine($"Found possible duplicate file: {existing.FullName} -- {newFile.FullName}");
                        }
                    }

                }
            }

            return foundFiles;
        }

        static DateTime GetActualFileDateTime(FileInfo file)
        {
            var fileExtension = Path.GetExtension(file.Name);

            var date = file.LastWriteTime <= file.CreationTime ? file.LastWriteTime : file.CreationTime;

            var name = file.Name.Remove(file.Name.Length - fileExtension.Length);

            if (name.Length >= 14)
            {
                if (DateTime.TryParseExact(name.Substring(0, 8), "yyyyMMdd_HHmmss", CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime parsedDate)
                    || DateTime.TryParseExact(name.Substring(0, 8), "yyyyMMddHHmmss", CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedDate))
                {
                    if (date > parsedDate)
                    {
                        date = parsedDate;
                    }
                }
            }

            return date;
        }

        static Project GenerateDefaultProject(string projectName)
        {
            var project = new Project()
            {
                name = projectName,
                themeId = "0",
                version = "65540",
                templateID = "SimpleProjectTemplate"
            };

            var boundProperties = new BoundProperties();

            project.BoundProperties = boundProperties;

            var floatSet = new BoundPropertiesBoundPropertyFloatSet() { Name = "AspectRatio" };
            floatSet.BoundPropertyFloatElement = new[] { new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement {
                Value = "1.7777776718139648"
            }
            };

            boundProperties.BoundPropertyFloatSet = new[] { floatSet };

            boundProperties.BoundPropertyFloat = new[]
            {
                new BoundPropertiesBoundPropertyFloat
                {
                    Name="DuckedNarrationAndSoundTrackMix", Value="0.5"
                },
                new BoundPropertiesBoundPropertyFloat
                {
                    Name="DuckedVideoAndNarrationMix", Value="0"
                },
                new BoundPropertiesBoundPropertyFloat
                {
                    Name="DuckedVideoAndSoundTrackMix", Value="0"
                },
                new BoundPropertiesBoundPropertyFloat
                {
                    Name="SoundTrackMix", Value="0"
                },
            };

            project.ThemeOperationLog = new ProjectThemeOperationLog
            {
                MonolithicThemeOperations = string.Empty,
                themeID = "0"
            };

            project.AudioDuckingProperties = new ProjectAudioDuckingProperties
            {
                emphasisPlaceholderID = "Narration"
            };

            project.BoundPlaceholders = new[] {
                new ProjectBoundPlaceholder
                {
                    placeholderID="SingleExtentView", extentID="0"
                },
                new ProjectBoundPlaceholder
                {
                    placeholderID="Main", extentID="1"
                },
                new ProjectBoundPlaceholder
                {
                    placeholderID="SoundTrack", extentID="2"
                },
                new ProjectBoundPlaceholder
                {
                    placeholderID="Text", extentID="4"
                },
                new ProjectBoundPlaceholder
                {
                    placeholderID="Narration", extentID="3"
                }
            };

            return project;
        }
    }

    class VideoDetails
    {
        public double Duration { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
    }
}
