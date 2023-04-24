﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Molten.Graphics.Vulkan
{
    /// <summary>
    /// Generates a <see cref="ShaderReflection"/> object from a compiled SPIR-V shader, by parsing its bytecode.
    /// </summary>
    /// <remarks>For SPIR-V specificational information see the following:
    /// <para>Main specification: https://registry.khronos.org/SPIR-V/specs/unified1/SPIRV.html#_magic_number</para>
    /// <para>Physical/Data layout: https://registry.khronos.org/SPIR-V/specs/unified1/SPIRV.html#PhysicalLayout</para>
    /// </remarks>
    internal unsafe class SpirvReflector
    {
        const uint MAGIC_NUMBER = 0x07230203;

        uint* _ptrStart;
        uint* _ptrEnd;
        uint* _ptr;
        ulong _numInstructions;
        List<SpirvInstruction> _instructions;

        internal SpirvReflector(void* byteCode, nuint numBytes)
        {
            if (numBytes % 4 != 0)
                throw new ArgumentException("Bytecode size must be a multiple of 4.", nameof(numBytes));

            _ptrEnd = (uint*)((byte*)byteCode + numBytes);
            _ptrStart = (uint*)byteCode;
            _ptr = _ptrStart;
            _instructions = new List<SpirvInstruction>();
            _numInstructions = numBytes / 4U;

            // First op is always the magic number.
            if (ReadWord() != MAGIC_NUMBER)
                throw new ArgumentException("Invalid SPIR-V bytecode.", nameof(byteCode));

            // Next op is the version number.
            SpirvVersion version = (SpirvVersion)ReadWord();

            // Next op is the generator number.
            uint generator = ReadWord();

            // Next op is the bound number.
            uint bound = ReadWord();

            // Next op is the schema number.
            uint schema = ReadWord();

            uint instID = 0;
            while(_ptr < _ptrEnd)
            {
                SpirvInstruction inst = new SpirvInstruction(_ptr);
                _instructions.Add(inst);

                Debug.WriteLine($"Instruction {instID++}: {(Enum.IsDefined(inst.OpCode) ? inst.OpCode : $"Unknown Opcode ({inst.OpCode})")}");
                _ptr += inst.WordCount;
            }
        }

        private uint ReadWord()
        {
            uint val = *_ptr;
            _ptr++;
            return val;
        }

        public ulong NumInstructions => _numInstructions;
    }
}
