using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileCataloger
{
    internal class CatalogerConfig
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public String SearchDirectory { get; set; }
        public String OutputDirectoryFormat { get; set; }
        public String[] MovieExtensions { get; set; }
        public String[] IgnoreNames { get; set; }
    }
}
