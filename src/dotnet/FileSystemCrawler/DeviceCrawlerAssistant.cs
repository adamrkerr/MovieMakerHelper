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
            var fileInfo = _device.GetFileInfo(fileName);

            var crawlerInfo = new CrawlerFileInfo
            {
                CreationTime = fileInfo.CreationTime ?? DateTime.MinValue,
                Extension = Path.GetExtension(fileName),
                FullName = fileInfo.FullName,
                LastWriteTime = fileInfo.LastWriteTime ?? DateTime.MinValue,
                Length = (long)fileInfo.Length,
                Name = fileInfo.Name
            };

            return crawlerInfo;
        }

        public IEnumerable<string> GetFiles(string startPath)
        {
            return _device.GetFiles(startPath).ToList();
        }

        public bool IsHidden(string directoryPath)
        {
            return _device.GetDirectoryInfo(directoryPath).Attributes.HasFlag(MediaFileAttributes.Hidden);
        }
    }
}
