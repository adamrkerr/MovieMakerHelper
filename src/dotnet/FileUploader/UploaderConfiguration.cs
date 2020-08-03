using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;

namespace FileUploader
{
    class UploaderConfiguration
    {
        public string UploadRecordPath { get; set; }
        public string FileCatalogPath { get; set; }
        public string RegionName { get; internal set; }
        public string ArchiveBucketRoot { get; internal set; }
        public S3StorageClass ArchiveStorageClass { get; internal set; }
        public DateTime NewestFileDate { get; internal set; }
        public DateTime OldestFileDate { get; internal set; }
    }
}
