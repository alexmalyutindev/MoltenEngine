﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Molten.Graphics.MSDF
{
    public class GeneratorConfig
    {
        public bool OverlapSupport { get; set; }

        public GeneratorConfig(bool overlapSupport = true)
        {
            OverlapSupport = overlapSupport;
        }
    }
}
