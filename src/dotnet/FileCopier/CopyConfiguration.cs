using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileCopier
{
    internal class CopyConfiguration
    {
        public String SearchDirectory { get; set; }
        public String DeviceName { get; set; }
        public String TargetDirectory { get; set; }
        public String[] IgnoreNames { get; set; }
    }
}
