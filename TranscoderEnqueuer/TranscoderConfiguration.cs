using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TranscoderEnqueuer
{
    internal class TranscoderConfiguration
    {
        public string RegionName { get; internal set; }
        public string ArchiveBucketRoot { get; internal set; }
    }
}
