using MediaDevices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FileSystemCrawler
{
    public class DeviceCrawlerAssistant : ICrawlerAssistant, IDisposable
    {
        private readonly MediaDevice _device;

        private readonly List<MediaFileInfo> knownFiles = new List<MediaFileInfo>();

        public DeviceCrawlerAssistant(MediaDevice device)
        {
            _device = device;

            if (!_device.IsConnected)
            {
                _device.Connect();
            }
        }

        public DeviceCrawlerAssistant(string deviceName)
        {
            var devices = MediaDevice.GetDevices();

            _device = devices.Single(s => s.FriendlyName.Trim() == deviceName);

            _device.Connect();
        }

        public void Dispose()
        {
            _device?.Disconnect();
        }

        public IEnumerable<string> GetDirectories(string startPath)
        {
            return _device.GetDirectories(startPath).ToList();
        }

        public CrawlerFileInfo GetFileInfo(string fileName)
        {
            var knownFile = knownFiles.SingleOrDefault(f => f.FullName == fileName);

            if(knownFile != null)
            {
                return CrawlerFileInfo.FromFileInfo(knownFile);
            }

            var fileInfo = _device.GetFileInfo(fileName);

            return CrawlerFileInfo.FromFileInfo(fileInfo);
        }

        public IEnumerable<string> GetFiles(string startPath, int yearMonthFilter)
        {
            Console.WriteLine($"Loading files for month {yearMonthFilter}");
            var filesInDirectory = _device.GetDirectoryInfo(startPath)
                .EnumerateFiles($"{yearMonthFilter}*.*").ToList();

            knownFiles.AddRange(filesInDirectory);

            return filesInDirectory.Select(f => f.FullName);
        }

        public bool IsHidden(string directoryPath)
        {
            return _device.GetDirectoryInfo(directoryPath).Attributes.HasFlag(MediaFileAttributes.Hidden);
        }
    }
}
