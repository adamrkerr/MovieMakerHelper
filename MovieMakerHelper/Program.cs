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
        static void Main(string[] args)
        {

            //var project = new Project() { name = "Test", themeId = "0", version = "65540", templateID = "SimpleProjectTemplate" };

            var files = CrawlFileSystem("G:\\");

            var deserializer = new XmlSerializer(typeof(Project));

            using (var stream = new FileStream("C:\\Users\\Adam\\Videos\\2017\\test.wlmp", FileMode.Open))
            {
                var objResult = deserializer.Deserialize(stream);

                var project = objResult as Project;
            }
        }

        static readonly string[] movieExtensions = { ".mp4", ".mov", ".mts", ".avi", ".mpg", ".mpeg" };
        static Dictionary<DateTime, List<FileInfo>> CrawlFileSystem(string startPath)
        {
            var foundFiles = new Dictionary<DateTime, List<FileInfo>>();
                        
            var directory = new DirectoryInfo(startPath);

            var files = directory.GetFiles();

            foreach (var file in files)
            {
                var fileExtension = Path.GetExtension(file.Name).ToLower();
                if (movieExtensions.Contains(fileExtension))
                {
                    var date = file.LastWriteTime <= file.CreationTime ? file.LastWriteTime : file.CreationTime;

                    var name = file.Name.Remove(file.Name.Length - fileExtension.Length);

                    if (name.Length >= 8)
                    {
                        if (DateTime.TryParseExact(name.Substring(0, 8), "yyyyMMdd", CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime parsedDate))
                        {
                            if (date > parsedDate)
                            {
                                date = parsedDate;
                            }
                        }
                    }

                    date = date.Date;

                    if (!foundFiles.ContainsKey(date))
                    {
                        foundFiles.Add(date, new List<FileInfo>());
                    }

                    foundFiles[date].Add(file);
                }
            }

            var directories = directory.GetDirectories();

            foreach(var childDirectory in directories)
            {

                if (childDirectory.Attributes.HasFlag(FileAttributes.Hidden))
                    continue;

                var childFiles = CrawlFileSystem(childDirectory.FullName);

                foreach(var date in childFiles.Keys)
                {
                    if (!foundFiles.ContainsKey(date))
                    {
                        foundFiles.Add(date, new List<FileInfo>());
                    }

                    foreach(var newFile in childFiles[date])
                    {
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
                MonolithicThemeOperations = null,
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
}
