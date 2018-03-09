﻿// MIT - 2018 - James Yarwood - Modified for Molten Engine - https://github.com/Syncaidius/MoltenEngine

/* Poly2Tri
 * Copyright (c) 2009-2010, Poly2Tri Contributors
 * http://code.google.com/p/poly2tri/
 *
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without modification,
 * are permitted provided that the following conditions are met:
 *
 * * Redistributions of source code must retain the above copyright notice,
 *   this list of conditions and the following disclaimer.
 * * Redistributions in binary form must reproduce the above copyright notice,
 *   this list of conditions and the following disclaimer in the documentation
 *   and/or other materials provided with the distribution.
 * * Neither the name of Poly2Tri nor the names of its contributors may be
 *   used to endorse or promote products derived from this software without specific
 *   prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

/// Changes from the Java version
///   attributification
/// Future possibilities
///   Flattening out the number of indirections
///     Replacing arrays of 3 with fixed-length arrays?
///     Replacing bool[3] with a bit array of some sort?
///     Bundling everything into an AoS mess?
///     Hardcode them all as ABC ?

using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Molten
{
    public class ShapeTriangle
    {
        public ShapePoint[] Points;
        public ShapeTriangle[] Neighbors;
        public bool[] EdgeIsConstrained, EdgeIsDelaunay;
        public bool IsInterior { get; set; }

        public ShapeTriangle(ShapePoint p1, ShapePoint p2, ShapePoint p3)
        {
            Points = new ShapePoint[3] { p1, p2, p3 };
            Neighbors = new ShapeTriangle[3];
            EdgeIsConstrained = new bool[3];
            EdgeIsDelaunay = new bool[3];
        }

        public void ReversePointFlow()
        {
            ShapePoint temp = Points[1];
            Points[1] = Points[2];
            Points[2] = temp;
        }

        public int IndexOf(ShapePoint p)
        {
            int i = Triangulation.IndexOf(Points, p);
            if (i == -1)
                throw new Exception("Calling IndexOf with a point that doesn't exist in triangle");
            return i;
        }

        public int IndexCWFrom(ShapePoint p) { return (IndexOf(p) + 2) % 3; }
        public int IndexCCWFrom(ShapePoint p) { return (IndexOf(p) + 1) % 3; }

        public bool Contains(ShapePoint p) { return Triangulation.Contains(Points, p); }


        public bool Contains(ShapePoint p, ShapePoint q) { return Triangulation.Contains(Points, p) && Triangulation.Contains(Points, q); }
        /// <summary>
        /// Update neighbor pointers
        /// </summary>
        /// <param name="p1">Point 1 of the shared edge</param>
        /// <param name="p2">Point 2 of the shared edge</param>
        /// <param name="t">This triangle's new neighbor</param>
        private void MarkNeighbor(ShapePoint p1, ShapePoint p2, ShapeTriangle t)
        {
            int i = EdgeIndex(p1, p2);
            if (i == -1)
                throw new Exception("Error marking neighbors -- t doesn't contain edge p1-p2!");
            Neighbors[i] = t;
        }

        /// <summary>
        /// Exhaustive search to update neighbor pointers
        /// </summary>
        public void MarkNeighbor(ShapeTriangle t)
        {
            // Points of this triangle also belonging to t
            bool a = t.Contains(Points[0]);
            bool b = t.Contains(Points[1]);
            bool c = t.Contains(Points[2]);

            if (b && c) { Neighbors[0] = t; t.MarkNeighbor(Points[1], Points[2], this); }
            else if (a && c) { Neighbors[1] = t; t.MarkNeighbor(Points[0], Points[2], this); }
            else if (a && b) { Neighbors[2] = t; t.MarkNeighbor(Points[0], Points[1], this); }
            else throw new Exception("Failed to mark neighbor, doesn't share an edge!");
        }

        /// <param name="t">Opposite triangle</param>
        /// <param name="p">The point in t that isn't shared between the triangles</param>
        public ShapePoint OppositePoint(ShapeTriangle t, ShapePoint p)
        {
            Debug.Assert(t != this, "self-pointer error");
            return PointCWFrom(t.PointCWFrom(p));
        }

        public ShapeTriangle NeighborCWFrom(ShapePoint point) { return Neighbors[(Triangulation.IndexOf(Points, point) + 1) % 3]; }
        public ShapeTriangle NeighborCCWFrom(ShapePoint point) { return Neighbors[(Triangulation.IndexOf(Points, point) + 2) % 3]; }
        public ShapeTriangle NeighborAcrossFrom(ShapePoint point) { return Neighbors[Triangulation.IndexOf(Points, point)]; }

        public ShapePoint PointCCWFrom(ShapePoint point) { return Points[(IndexOf(point) + 1) % 3]; }
        public ShapePoint PointCWFrom(ShapePoint point) { return Points[(IndexOf(point) + 2) % 3]; }

        private void RotateCW()
        {
            var t = Points[2];
            Points[2] = Points[1];
            Points[1] = Points[0];
            Points[0] = t;
        }

        /// <summary>
        /// Legalize triangle by rotating clockwise around oPoint
        /// </summary>
        /// <param name="oPoint">The origin point to rotate around</param>
        /// <param name="nPoint">???</param>
        public void Legalize(ShapePoint oPoint, ShapePoint nPoint)
        {
            RotateCW();
            Points[IndexCCWFrom(oPoint)] = nPoint;
        }

        public override string ToString()
        {
            return Points[0] + "," + Points[1] + "," + Points[2];
        }

        /// <summary>
        /// Finalize edge marking
        /// </summary>
        public void MarkNeighborEdges()
        {
            for (int i = 0; i < 3; i++) if (EdgeIsConstrained[i] && Neighbors[i] != null)
                {
                    Neighbors[i].MarkConstrainedEdge(Points[(i + 1) % 3], Points[(i + 2) % 3]);
                }
        }

        public void MarkEdge(ShapeTriangle triangle)
        {
            for (int i = 0; i < 3; i++)
            {
                if (EdgeIsConstrained[i])
                    triangle.MarkConstrainedEdge(Points[(i + 1) % 3], Points[(i + 2) % 3]);
            }
        }

        public void MarkEdge(List<ShapeTriangle> tList)
        {
            foreach (ShapeTriangle t in tList)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (t.EdgeIsConstrained[i])
                        MarkConstrainedEdge(t.Points[(i + 1) % 3], t.Points[(i + 2) % 3]);
                }
            }
        }

        public void MarkConstrainedEdge(int index)
        {
            EdgeIsConstrained[index] = true;
        }

        public void MarkConstrainedEdge(TriangulationConstraint edge)
        {
            MarkConstrainedEdge(edge.P, edge.Q);
        }

        /// <summary>
        /// Mark edge as constrained
        /// </summary>
        internal void MarkConstrainedEdge(ShapePoint p, ShapePoint q)
        {
            int i = EdgeIndex(p, q);

            if (i != -1)
                EdgeIsConstrained[i] = true;
        }

        /// <summary>
        /// Returns the area of the triangle.
        /// </summary>
        /// <returns></returns>
        public double Area()
        {
            double b = Points[0].X - Points[1].X;
            double h = Points[2].Y - Points[1].Y;

            return Math.Abs((b * h * 0.5f));
        }

        /// <summary>
        /// Returns the median center-point of the triangle, based on the position of the 3 corner points.
        /// </summary>
        /// <returns></returns>
        public ShapePoint Centroid()
        {
            float cx = (Points[0].X + Points[1].X + Points[2].X) / 3f;
            float cy = (Points[0].Y + Points[1].Y + Points[2].Y) / 3f;
            return new ShapePoint(cx, cy);
        }

        /// <summary>
        /// Get the index of the neighbor that shares this edge (or -1 if it isn't shared)
        /// </summary>
        /// <returns>index of the shared edge or -1 if edge isn't shared</returns>
        public int EdgeIndex(ShapePoint p1, ShapePoint p2)
        {
            int i1 = Triangulation.IndexOf(Points, p1);
            int i2 = Triangulation.IndexOf(Points, p2);

            // Points of this triangle in the edge p1-p2
            bool a = (i1 == 0 || i2 == 0);
            bool b = (i1 == 1 || i2 == 1);
            bool c = (i1 == 2 || i2 == 2);

            if (b && c) return 0;
            if (a && c) return 1;
            if (a && b) return 2;
            return -1;
        }

        public bool GetConstrainedEdgeCCW(ShapePoint p) { return EdgeIsConstrained[(IndexOf(p) + 2) % 3]; }
        public bool GetConstrainedEdgeCW(ShapePoint p) { return EdgeIsConstrained[(IndexOf(p) + 1) % 3]; }
        public bool GetConstrainedEdgeAcross(ShapePoint p) { return EdgeIsConstrained[IndexOf(p)]; }
        public void SetConstrainedEdgeCCW(ShapePoint p, bool ce) { EdgeIsConstrained[(IndexOf(p) + 2) % 3] = ce; }
        public void SetConstrainedEdgeCW(ShapePoint p, bool ce) { EdgeIsConstrained[(IndexOf(p) + 1) % 3] = ce; }
        public void SetConstrainedEdgeAcross(ShapePoint p, bool ce) { EdgeIsConstrained[IndexOf(p)] = ce; }

        public bool GetDelaunayEdgeCCW(ShapePoint p) { return EdgeIsDelaunay[(IndexOf(p) + 2) % 3]; }
        public bool GetDelaunayEdgeCW(ShapePoint p) { return EdgeIsDelaunay[(IndexOf(p) + 1) % 3]; }
        public bool GetDelaunayEdgeAcross(ShapePoint p) { return EdgeIsDelaunay[IndexOf(p)]; }
        public void SetDelaunayEdgeCCW(ShapePoint p, bool ce) { EdgeIsDelaunay[(IndexOf(p) + 2) % 3] = ce; }
        public void SetDelaunayEdgeCW(ShapePoint p, bool ce) { EdgeIsDelaunay[(IndexOf(p) + 1) % 3] = ce; }
        public void SetDelaunayEdgeAcross(ShapePoint p, bool ce) { EdgeIsDelaunay[IndexOf(p)] = ce; }
    }
}
