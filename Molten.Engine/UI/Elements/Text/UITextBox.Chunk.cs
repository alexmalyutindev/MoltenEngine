﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Molten.Collections;
using Molten.Graphics;

namespace Molten.UI
{
    public partial class UITextBox
    {
        const int CHUNK_CAPACITY = 128;

        internal struct ChunkPickResult
        {
            internal UITextLine Line;

            internal UITextSegment Segment;
        }

        internal class Chunk
        {
            int _width;
            int _height;
            int _startLineNumber;

            public Chunk(int firstLineNumber)
            {
                _startLineNumber = firstLineNumber;
            }

            private void FastAppendLine(UITextLine line)
            {
                LastLine.Next = line;
                line.Previous = LastLine;
                LastLine = line;

                LineCount++;
                _width = Math.Max(_width, (int)Math.Ceiling(line.Width));
                _height += line.Height; 

                if (Next != null)
                    Next.StartLineNumber++;
            }

            private void FastInsertLine(UITextLine line, UITextLine lineBefore)
            {
                if (lineBefore != null)
                {
                    lineBefore.Next = line;
                    line.Previous = lineBefore;

                    if (lineBefore == LastLine)
                        LastLine = line;
                    else if(lineBefore == FirstLine)
                        FirstLine = line;
                }

                LineCount++;
                _width = Math.Max(_width, (int)Math.Ceiling(line.Width));
                _height += line.Height;

                if (Next != null)
                    Next.StartLineNumber++;
            }

            internal Chunk AppendLine(UITextLine line)
            {
                if(LineCount < CHUNK_CAPACITY)
                {
                    FastAppendLine(line);
                }
                else
                {
                    if (Next == null || Next.Capacity == 0)
                        NewNext();

                    Next.FastInsertLine(line, Next.FirstLine);
                    return Next;
                }

                return this;
            }

            internal Chunk InsertLine(UITextLine line, UITextLine lineBefore)
            {
                if (LineCount < CHUNK_CAPACITY)
                {
                    FastInsertLine(line, lineBefore);
                }
                else
                {
                    if (lineBefore == FirstLine)
                    {
                        if (Previous == null || Previous.Capacity == 0)
                            NewPrevious();

                        Previous.FastAppendLine(line);
                        return Previous;
                    }
                    else if (lineBefore == LastLine)
                    {
                        if (Next == null || Next.Capacity == 0)
                            NewNext();

                        // Directly insert line to avoid duplicated checks
                        Next.FastInsertLine(line, 0);
                        return Next;
                    }
                    else
                    {
                        Split(lineBefore);
                        FastAppendLine(line);
                    }
                }

                return this;
            }

            /// <summary>
            /// Splits the current <see cref="Chunk"/>, moving all items from at and beyond the given index, into a new <see cref="Chunk"/>.
            /// </summary>
            /// <param name="splitAt">All lines at and beyond the given <see cref="UITextLine"/> are cut off into a new chunk, added after the current one.</param>
            private void Split(UITextLine splitAt)
            {
                UITextLine line = splitAt;
                UITextLine last = splitAt;

                int moveCount = 0;
                while(line != null)
                {
                    moveCount++;
                    last = line;
                    line = line.Next;
                }

                if (Next == null || Next.Capacity < moveCount)
                    NewNext();

                splitAt.Previous = null;
                Next.LineCount += moveCount;
                Next.LastLine = last;

                CalculateSize();
                Next.CalculateSize();
            }

            internal void CalculateSize()
            {
                _width = 0;
                _height = 0;
                UITextLine line = FirstLine;

                while(line != null)
                {
                    _width = Math.Max(_width, (int)Math.Ceiling(line.Width));
                    _height += line.Height;
                    line = line.Next;
                }
            }

            private void NewPrevious()
            {
                Chunk prev = new Chunk(StartLineNumber-1);

                if (Previous != null)
                {
                    Previous.Next = prev;
                    prev.Previous = Previous;
                }

                Previous = prev;
                Previous.Next = this;
            }

            private void NewNext()
            {
                Chunk next = new Chunk(StartLineNumber + LineCount);

                // Update the current "Next".
                if (Next != null)
                {
                    Next.Previous = next;
                    next.Next = Next;
                }

                // Update the new "Next".
                Next = next;
                Next.Previous = this;
            }

            internal void Pick(Vector2I pos, ref Rectangle bounds, out ChunkPickResult result)
            {
                Rectangle lBounds = bounds;

                if (bounds.Contains(pos))
                {
                    UITextLine line = FirstLine;
                    while(line != null)
                    {
                        lBounds.Height = line.Height;

                        if (lBounds.Contains(pos))
                        {
                            UITextSegment seg = line.First;
                            RectangleF segBounds = lBounds;

                            while(seg != null)
                            {
                                segBounds.Width = seg.Size.X;
                                if (segBounds.Contains(pos))
                                    break;

                                segBounds.X += seg.Size.X;
                                seg = seg.Next;
                            }

                            result = new ChunkPickResult()
                            {
                                Line = line,
                                Segment = seg
                            };

                            return;
                        }

                        lBounds.Y += line.Height;
                        line = line.Next;
                    }
                }

                result = new ChunkPickResult();
            }

            internal Chunk Previous { get; set; }
            
            internal Chunk Next { get; set; }

            internal UITextLine FirstLine { get; private set; }

            internal UITextLine LastLine { get; private set; }

            public int LineCount { get; private set; }

            internal int StartLineNumber
            {
                get => _startLineNumber;
                set
                {
                    if(_startLineNumber != value)
                    {
                        _startLineNumber = value;
                        if (Next != null)
                            Next.StartLineNumber = StartLineNumber + LineCount;
                    }
                }
            }

            public int Capacity => CHUNK_CAPACITY - LineCount;

            public int Width => _width;

            public int Height => _height;
        }
    }
}
