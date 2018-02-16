﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Molten.Font
{
    /// <summary>A grid-fitting and scan-conversion procedure table (gasp).<para/>
    /// See: https://docs.microsoft.com/en-us/typography/opentype/spec/gasp </summary>
    public class Gasp : FontTable
    {
        public ushort Version { get; private set; }

        /// <summary>Gets an array of <see cref="GaspRange"/> instances, sorted by ppem.</summary>
        public GaspRange[] Ranges { get; private set; }

        internal class Parser : FontTableParser
        {
            public override string TableTag => "gasp";

            internal override FontTable Parse(BinaryEndianAgnosticReader reader, TableHeader header, Logger log, DependencyList dependencies)
            {
                Gasp table = new Gasp()
                {
                    Version = reader.ReadUInt16(),
                };

                ushort numRanges = reader.ReadUInt16();
                table.Ranges = new GaspRange[numRanges];
                for(int i = 0; i < numRanges; i++)
                {
                    table.Ranges[i] = new GaspRange()
                    {
                        MaxPPEM = reader.ReadUInt16(),
                        BehaviourFlags = (GaspBehaviorFlags)reader.ReadUInt16(),
                    };
                }

                return table;
            }
        }
    }

    /// <summary>Represents recommended behaviors for ppem sizes.</summary>
    public class GaspRange
    {
        /// <summary>
        /// Gets the upper limit of range, in PPEM
        /// </summary>
        public ushort MaxPPEM { get; internal set; }

        /// <summary>Gets flags describing desired rasterizer behavior.</summary>
        public GaspBehaviorFlags BehaviourFlags { get; internal set; }
    }

    [Flags]
    public enum GaspBehaviorFlags : ushort
    {
        None = 0,

        /// <summary>
        /// Use gridfitting
        /// </summary>
        GridFit = 0x0001,

        /// <summary>
        /// Use grayscale rendering
        /// </summary>
        DoGray = 0x0002,

        /// <summary>
        /// Use grayscale rendering
        /// </summary>
        DoGrey = 0x0002,

        /// <summary>
        /// Use gridfitting with ClearType symmetric smoothing
        /// </summary>
        SymmetricGridFit = 0x0004,

        /// <summary>
        /// Use smoothing along multiple axes with ClearType®. Only supported in version 1 of gasp.
        /// </summary>
        SymmetricSmoothing = 0x0008,

        /// <summary>
        /// Reserved. Only supported in version 1 of gasp.
        /// </summary>
        Reserved = 0xFFF0,
    }

}
