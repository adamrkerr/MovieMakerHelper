using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TranscoderEnqueuer
{
    internal enum TranscoderFunctions
    {
        Subtitle = 1,
        Compile = 2,
        SubtitleAndCompile = 3,
        Test = 4
    }

    internal class TranscoderConfiguration
    {
        public string RegionName { get; internal set; }
        public string ArchiveBucketRoot { get; internal set; }
        public string JobRoleName { get; internal set; }
        public string DestinationBucketRoot { get; internal set; }
    }
}
