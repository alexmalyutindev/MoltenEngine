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
///   Polygon constructors sprused up, checks for 3+ polys
///   Naming of everything
///   getTriangulationMode() -> TriangulationMode { get; }
///   Exceptions replaced
/// Future possibilities
///   We have a lot of Add/Clear methods -- we may prefer to just expose the container
///   Some self-explanitory methods may deserve commenting anyways

using System;
using System.Collections.Generic;
using System.Linq;

namespace Molten
{
    public class Shape
    {
        List<ShapePoint> _points = new List<ShapePoint>();
        List<ShapePoint> _steinerPoints;
        List<Shape> _holes;
        List<ShapeTriangle> _triangles;

        public IList<Shape> Holes => _holes;

        /// <summary>
        /// Create a polygon from a list of at least 3 points with no duplicates.
        /// </summary>
        /// <param name="points">A list of unique points</param>
        public Shape(IList<ShapePoint> points)
        {
            if (points.Count < 3)
                throw new ArgumentException("List has fewer than 3 points", "points");

            // Lets sanity check that first and last point haven't got the same position
            // Its something that often happens when importing polygon data from other formats
            if (points[0].Equals(points[points.Count - 1]))
                points.RemoveAt(points.Count - 1);

            _points.AddRange(points);
        }

        /// <summary>
        /// Create a polygon from a list of at least 3 points with no duplicates.
        /// </summary>
        /// <param name="points">A list of unique points.</param>
        public Shape(IEnumerable<ShapePoint> points) : this((points as IList<ShapePoint>) ?? points.ToArray()) { }

        /// <summary>
        /// Create a polygon from a list of at least 3 points with no duplicates.
        /// </summary>
        /// <param name="points">A list of unique points.</param>
        public Shape(params ShapePoint[] points) : this((IList<ShapePoint>)points) { }

        /// <summary>
        /// Creates a polygon from a list of at least 3 Vector3 points, with no duplicates.
        /// </summary>
        /// <param name="points">The input points.</param>
        /// <param name="offset">An offset to apply to all of the provided points.</param>
        /// <param name="scale">The scale of the provided points. 0.5f is half size. 2.0f is 2x the normal size.</param>
        public Shape(IList<Vector2> points, Vector2 offset, float scale)
        {
            if (points.Count < 3)
                throw new ArgumentException("List has fewer than 3 points", "points");

            // Lets sanity check that first and last point haven't got the same position
            // Its something that often happens when importing polygon data from other formats
            if (points[0].Equals(points[points.Count - 1]))
                points.RemoveAt(points.Count - 1);

            for (int i = 0; i < points.Count; i++)
                _points.Add(new ShapePoint(offset + (points[i] * scale)));
        }

        /// <summary>
        /// Creates a polygon from a list of at least 3 Vector3 points, with no duplicates.
        /// </summary>
        /// <param name="points">The input points.</param>
        public Shape(IList<Vector2> points) : this(points, Vector2.Zero, 1.0f) { }

        /// <summary>
        /// Triangulates the shape and adds all of the points (in triangle list layout) to the provided output.
        /// </summary>
        /// <param name="output">The output list.</param>
        public void Triangulate(IList<Vector2> output)
        {
            Triangulation.Triangulate(this);

            foreach (ShapeTriangle tri in _triangles)
            {
                tri.ReversePointFlow();
                output.Add(TriToVector2(tri.Points[0]));
                output.Add(TriToVector2(tri.Points[1]));
                output.Add(TriToVector2(tri.Points[2]));
            }
        }

        /// <summary>
        /// Triangulates the shape and adds all of the triangles to the provided output.
        /// </summary>
        /// <param name="output">The output list.</param>
        public void Triangulate(IList<ShapeTriangle> output)
        {
            Triangulation.Triangulate(this);
            for (int i = 0; i < _triangles.Count; i++)
                output.Add(_triangles[i]);
        }

        private Vector2 TriToVector2(ShapePoint p)
        {
            return new Vector2()
            {
                X = (float)p.X,
                Y = (float)p.Y,
            };
        }

        public void AddSteinerPoint(ShapePoint point)
        {
            if (_steinerPoints == null) _steinerPoints = new List<ShapePoint>();
            _steinerPoints.Add(point);
        }

        public void AddSteinerPoints(List<ShapePoint> points)
        {
            if (_steinerPoints == null) _steinerPoints = new List<ShapePoint>();
            _steinerPoints.AddRange(points);
        }

        public void ClearSteinerPoints()
        {
            if (_steinerPoints != null) _steinerPoints.Clear();
        }

        /// <summary>
        /// Add a hole to the polygon.
        /// </summary>
        /// <param name="poly">A subtraction polygon fully contained inside this polygon.</param>
        public void AddHole(Shape poly)
        {
            if (_holes == null)
                _holes = new List<Shape>();

            _holes.Add(poly);
            // XXX: tests could be made here to be sure it is fully inside
            //        addSubtraction( poly.getPoints() );
        }

        /// <summary>
        /// Inserts newPoint after point.
        /// </summary>
        /// <param name="point">The point to insert after in the polygon</param>
        /// <param name="newPoint">The point to insert into the polygon</param>
        public void InsertPointAfter(ShapePoint point, ShapePoint newPoint)
        {
            // Validate that 
            int index = _points.IndexOf(point);
            if (index == -1) throw new ArgumentException("Tried to insert a point into a Polygon after a point not belonging to the Polygon", "point");
            _points.Insert(index + 1, newPoint);
        }

        /// <summary>
        /// Inserts list (after last point in polygon?)
        /// </summary>
        /// <param name="list"></param>
        public void AddPoints(IEnumerable<ShapePoint> list)
        {
            _points.AddRange(list);
        }

        /// <summary>
        /// Adds a point after the last in the polygon.
        /// </summary>
        /// <param name="p">The point to add</param>
        public void AddPoint(ShapePoint p)
        {
            _points.Add(p);
        }

        /// <summary>
        /// Removes a point from the polygon.
        /// </summary>
        /// <param name="p"></param>
        public void RemovePoint(ShapePoint p)
        {
            _points.Remove(p);
        }

        public void AddTriangle(ShapeTriangle t)
        {
            _triangles.Add(t);
        }

        public void AddTriangles(IEnumerable<ShapeTriangle> list)
        {
            _triangles.AddRange(list);
        }

        public void ClearTriangles()
        {
            if (_triangles != null)
                _triangles.Clear();
        }

        /// <summary>
        /// Creates constraints and populates the context with points
        /// </summary>
        /// <param name="tcx">The context</param>
        internal void Prepare(TriangulationContext tcx)
        {
            if (_triangles == null)
                _triangles = new List<ShapeTriangle>(_points.Count);
            else
                _triangles.Clear();

            // Outer constraints
            for (int i = 0; i < _points.Count - 1; i++)
                tcx.NewConstraint(_points[i], _points[i + 1]);

            tcx.NewConstraint(_points[0], _points[_points.Count - 1]);
            tcx.Points.AddRange(_points);

            // Hole constraints
            if (_holes != null)
            {
                foreach (Shape p in _holes)
                {
                    for (int i = 0; i < p._points.Count - 1; i++)
                        tcx.NewConstraint(p._points[i], p._points[i + 1]);

                    tcx.NewConstraint(p._points[0], p._points[p._points.Count - 1]);
                    tcx.Points.AddRange(p._points);
                }
            }

            if (_steinerPoints != null)
                tcx.Points.AddRange(_steinerPoints);
        }
    }
}
