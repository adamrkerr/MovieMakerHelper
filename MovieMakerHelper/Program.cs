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
        private static DateTime _startDate = new DateTime(2009, 1, 1);
        private static DateTime _endDate = new DateTime(2010, 1, 1);
        private const string _searchDirectory = "G:\\";
        private const string _outputDirectoryFormat = "C:\\Users\\Adam\\Videos\\generated\\{0}.wlmp";
        private const string _dividerPath = "C:\\Users\\Adam\\Videos\\Pure Black.png";
        static readonly string[] _movieExtensions = { ".mp4", ".mov", ".mts", ".avi", ".mpg", ".mpeg", ".asf" };
        static readonly string[] _ignoreNames = { "itunes", "top gear", "valve", "xgames", "top.gear", "pocket_lint", "fireproof", "\\videos\\", "\\local disk (g)\\movies\\", "\\my movies\\", "\\my documents\\my videos\\" };

        [STAThread]
        static void Main(string[] args)
        {
            var currentStartTime = _startDate;
            var currentEndTime = _startDate.AddYears(1);

            while (currentEndTime <= _endDate)
            {
                var files = CrawlFileSystem(_searchDirectory, currentStartTime, currentEndTime);

                var project = GenerateDefaultProject($"{currentStartTime:yyyyMMdd} to {currentEndTime:yyyyMMdd}");

                AddFilesToProject(project, files);

                var deserializer = new XmlSerializer(typeof(Project));

                using (var stream = new FileStream(string.Format(_outputDirectoryFormat, project.name), FileMode.OpenOrCreate))
                {
                    deserializer.Serialize(stream, project);
                }

                currentStartTime = currentEndTime;
                currentEndTime = currentEndTime.AddYears(1);
            }
        }


        [STAThread]
        private static void AddFilesToProject(Project project, Dictionary<DateTime, List<FileInfo>> files)
        {
            var mediaItems = new List<ProjectMediaItem>();
            var mediaItemCounter = 1;
            var extentCounter = 5;
            var videoClips = new List<ProjectExtentsVideoClip>();
            var imageClips = new List<ProjectExtentsImageClip>();
            var titleClips = new List<ProjectExtentsTitleClip>();
            var videoExtentRefs = new List<ProjectExtentsExtentSelectorExtentRef>();
            var titleExtentRefs = new List<ProjectExtentsExtentSelectorExtentRef>();
            var extents = new ProjectExtents();
            double totalLength = 0;
            double lastTitlePosition = 0;

            var blackScreenMediaItem = GenerateDividerItem(mediaItemCounter);

            mediaItems.Add(blackScreenMediaItem);

            mediaItemCounter++;

            foreach (var dateKey in files.Keys.OrderBy(k => k))
            {
                var filesForDate = files[dateKey].OrderBy(f => GetActualFileDateTime(f));

                var isFirst = true;

                double dateClipLength = 0;

                var previousLength = totalLength;

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

                    extentCounter++;

                    if (isFirst)
                    {
                        //add fade in
                        videoClip.Transitions = GenerateFadeThroughBlackTransition();
                        isFirst = false;
                    }
                    else
                    {
                        //add diagonal transition
                        videoClip.Transitions = GenerateDiagonalTransition();
                    }

                    videoClips.Add(videoClip);

                    videoExtentRefs.Add(new ProjectExtentsExtentSelectorExtentRef
                    {
                        id = videoClip.extentID
                    });

                    totalLength += (videoDetails.Duration - 1.5); //1.5 adjusts for transition
                    dateClipLength += (videoDetails.Duration - 1.5); //1.5 adjusts for transition

                }

                //add date heading?
                var titleClip = GenerateTitleClip(dateKey, extentCounter, previousLength, dateClipLength, lastTitlePosition);

                if (double.Parse(titleClip.duration) >= 1) //ignore very small ones?
                {
                    extentCounter++;
                    titleClips.Add(titleClip);
                    titleExtentRefs.Add(new ProjectExtentsExtentSelectorExtentRef
                    {
                        id = titleClip.extentID
                    });

                    lastTitlePosition += double.Parse(titleClip.gapBefore) + double.Parse(titleClip.duration);
                }

                //add black placehold, fade out transition on last one
                //videoClips.Last().Transitions = GenerateFadeThroughBlackTransition();

                var blackImageClip = new ProjectExtentsImageClip
                {
                    Effects = string.Empty,
                    extentID = $"{extentCounter}",
                    gapBefore = "0",
                    mediaItemID = $"{blackScreenMediaItem.id}",
                    duration = "3",
                    BoundProperties = new BoundProperties
                    {
                        BoundPropertyInt = new[] {
                            new BoundPropertiesBoundPropertyInt
                            {
                                Name="rotateStepNinety",
                                Value = "0"
                            }
                        }
                    },
                    Transitions = GenerateFadeThroughBlackTransition()
                };

                imageClips.Add(blackImageClip);

                videoExtentRefs.Add(new ProjectExtentsExtentSelectorExtentRef
                {
                    id = blackImageClip.extentID
                });

                extentCounter++;

                totalLength += 3;

            }

            extents.VideoClip = videoClips.ToArray();

            extents.ImageClip = imageClips.ToArray();

            extents.TitleClip = titleClips.ToArray();

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

        private static ProjectMediaItem GenerateDividerItem(int mediaItemCounter)
        {
            //add black screen to media items
            return new ProjectMediaItem
            {
                id = $"{mediaItemCounter}",
                filePath = _dividerPath,
                arWidth = "882",
                arHeight = "446",
                duration = "0",
                stabilizationMode = "0",
                mediaItemType = "2",
                songTitle = string.Empty,
                songAlbum = string.Empty,
                songArtist = string.Empty,
                songArtistUrl = string.Empty,
                songAudioFileUrl = string.Empty,
                songCopyrightUrl = string.Empty
            };
        }

        private static ProjectExtentsTitleClip GenerateTitleClip(DateTime dateTime, int extentCounter, double totalLength, double videoDuration, double lastTitlePosition)
        {
            double standardDuration = 7;

            var gapBefore = (totalLength - lastTitlePosition) + 3;

            var titleDuration = 0.0;

            if (videoDuration < standardDuration)
            {
                titleDuration = videoDuration;
                gapBefore -= 3; //remove lead time
            }
            else
            {
                titleDuration = standardDuration;
            }

            var titleClip = new ProjectExtentsTitleClip()
            {
                extentID = $"{extentCounter}",
                gapBefore = $"{gapBefore}",
                duration = $"{titleDuration}"
            };

            titleClip.Transitions = string.Empty;

            titleClip.BoundProperties = new BoundProperties
            {
                BoundPropertyFloatSet = new[]
                {
                    new BoundPropertiesBoundPropertyFloatSet
                    {
                        Name = "diffuseColor",
                        BoundPropertyFloatElement = new []{
                            new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement{
                                Value = "0.75"
                            },
                            new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement{
                                Value = "1"
                            },
                            new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement{
                                Value = "0"
                            }
                        }
                    }
                },
                BoundPropertyFloat = new[]
                {
                    new BoundPropertiesBoundPropertyFloat
                    {
                        Name="transparency",
                        Value="1"
                    }
                }
            };

            titleClip.Effects = new[]
            {
                new ProjectExtentsTitleClipTextEffect
                {
                    effectTemplateID = "TextEffectFadeTemplate",
                    TextScriptId = "1",
                    BoundProperties = new BoundProperties
                    {
                        BoundPropertyBool = new[]
                        {
                            new BoundPropertiesBoundPropertyBool
                            {
                                Name="automatic",
                                Value = "false"
                            },
                            new BoundPropertiesBoundPropertyBool
                            {
                                Name="horizontal",
                                Value = "true"
                            },
                            new BoundPropertiesBoundPropertyBool
                            {
                                Name="leftToRight",
                                Value = "true"
                            }
                        },
                        BoundPropertyFloatSet = new[]
                        {
                            new BoundPropertiesBoundPropertyFloatSet
                            {
                                Name = "color",
                                BoundPropertyFloatElement = new []{
                                    new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement{
                                        Value = "1"
                                    },
                                    new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement{
                                        Value = "1"
                                    },
                                    new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement{
                                        Value = "1"
                                    }
                                }
                            },
                            new BoundPropertiesBoundPropertyFloatSet
                            {
                                Name = "length"
                            },
                            new BoundPropertiesBoundPropertyFloatSet
                            {
                                Name = "outlineColor",
                                BoundPropertyFloatElement = new []{
                                    new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement{
                                        Value = "0"
                                    },
                                    new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement{
                                        Value = "0"
                                    },
                                    new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement{
                                        Value = "0"
                                    }
                                }
                            },
                            new BoundPropertiesBoundPropertyFloatSet
                            {
                                Name = "position",
                                BoundPropertyFloatElement = new []{
                                    new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement{
                                        Value = "3.4281764030456543"
                                    },
                                    new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement{
                                        Value = "-1.7933578491210937"
                                    },
                                    new BoundPropertiesBoundPropertyFloatSetBoundPropertyFloatElement{
                                        Value = "0.02500000037252903"
                                    }
                                }
                            }
                        },
                        BoundPropertyStringSet = new[]
                        {
                            new BoundPropertiesBoundPropertyStringSet
                            {
                                Name = "family",
                               BoundPropertyStringElement = new[]
                               {
                                   new BoundPropertiesBoundPropertyStringSetBoundPropertyStringElement
                                   {
                                       Value = "Segoe UI"
                                   }
                               }
                            },
                            new BoundPropertiesBoundPropertyStringSet
                            {
                                Name = "justify",
                               BoundPropertyStringElement = new[]
                               {
                                   new BoundPropertiesBoundPropertyStringSetBoundPropertyStringElement
                                   {
                                       Value = "END"
                                   }
                               }
                            },
                            new BoundPropertiesBoundPropertyStringSet
                            {
                                Name = "string",
                               BoundPropertyStringElement = new[]
                               {
                                   new BoundPropertiesBoundPropertyStringSetBoundPropertyStringElement
                                   {
                                       Value = $"{dateTime:MM/dd/yyyy}"
                                   }
                               }
                            }
                        },
                        BoundPropertyFloat = new[]
                        {
                            new BoundPropertiesBoundPropertyFloat
                            {
                                Name="maxExtent",
                                Value="0"
                            },
                            new BoundPropertiesBoundPropertyFloat
                            {
                                Name="size",
                                Value="0.40000000596046448" //TODO: understand this?
                            },
                            new BoundPropertiesBoundPropertyFloat
                            {
                                Name="transparency",
                                Value="0"
                            }
                        },
                        BoundPropertyInt = new[]
                        {
                            new BoundPropertiesBoundPropertyInt
                            {
                                Name="outlineSizeIndex",
                                Value = "1"
                            }
                        },
                        BoundPropertyString = new[]
                        {
                            new BoundPropertiesBoundPropertyString
                            {
                                Name="style",
                                Value = "Plain"
                            }
                        }
                    }
                }
            };

            return titleClip;
        }

        private static Transitions GenerateFadeThroughBlackTransition()
        {
            var transition = new Transitions()
            {
                ShapeEffect = new[]
                {
                    new TransitionsShapeEffect()
                    {
                        BoundProperties = String.Empty,
                        duration = "1.5",
                        effectTemplateID = "BlurThroughBlackTransitionTemplate"
                    }
                }
            };

            return transition;
        }

        private static Transitions GenerateDiagonalTransition()
        {
            var transition = new Transitions()
            {
                ShaderEffect = new[]
                {
                    new TransitionsShaderEffect()
                    {
                        BoundProperties = String.Empty,
                        duration = "1.5",
                        effectTemplateID = "DiagonalDownRightTransitionTemplate"
                    }
                }
            };

            return transition;
        }

        [STAThread]
        private static VideoDetails GetVideoDetails(FileInfo file)
        {
            using (var video = new Video(file.FullName, false))
            {
                return new VideoDetails
                {
                    Duration = video.Duration,
                    Height = video.DefaultSize.Height,
                    Width = video.DefaultSize.Width
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
                
        static Dictionary<DateTime, List<FileInfo>> CrawlFileSystem(string startPath, DateTime minDate, DateTime maxDate)
        {
            var foundFiles = new Dictionary<DateTime, List<FileInfo>>();

            var directory = new DirectoryInfo(startPath);

            var files = directory.GetFiles();

            foreach (var file in files)
            {
                var fileExtension = Path.GetExtension(file.Name).ToLower();

                if (!_movieExtensions.Contains(fileExtension))
                {
                    continue;
                }

                //filter stuff we know we don't want
                if (_ignoreNames.Any(ig => file.FullName.ToLower().Contains(ig)))
                {
                    continue;
                }

                var date = GetActualFileDateTime(file).Date;

                if (date < minDate || date >= maxDate)
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

            if (name.Length >= 15)
            {
                if (DateTime.TryParseExact(name.Substring(0, 15), "yyyyMMdd_HHmmss", CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    if (date != parsedDate)
                    {
                        date = parsedDate;
                    }
                }
            }
            else if (name.Length == 14)
            {
                if (DateTime.TryParseExact(name.Substring(0, 14), "yyyyMMddHHmmss", CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    if (date != parsedDate)
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
