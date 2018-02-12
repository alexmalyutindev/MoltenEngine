﻿//MIT, 2016, Viktor Chlumsky, Multi-channel signed distance field generator, from https://github.com/Chlumsky/msdfgen
//MIT, 2017, WinterDev (C# port)
//MIT, 2018, James Yarwood (Adapted for Molten Engine)
//MIT, 2017 Applied Ckohnert's changes from https://github.com/ckohnert/msdfgen
//MIT, 2018, James Yarwood (Adapted for Molten Engine)

using Molten;
using Molten.Graphics;
using System;
using System.Collections.Generic;

namespace Msdfgen
{
    public struct FloatRGB
    {
        public float r, g, b;
        public FloatRGB(float r, float g, float b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }
#if DEBUG
        public override string ToString()
        {
            return r + "," + g + "," + b;
        }
#endif
    }

    public class FloatBmp
    {
        float[] buffer;
        public FloatBmp(int w, int h)
        {
            this.Width = w;
            this.Height = h;
            buffer = new float[w * h];
        }
        public int Width { get; set; }
        public int Height { get; set; }

        public float this[int x, int y]
        {
            get => buffer[x + (y * Width)];
            set => buffer[x + (y * Width)] = value;
        }
    }

    public class FloatRGBBmp
    {
        FloatRGB[] buffer;
        public FloatRGBBmp(int w, int h)
        {
            this.Width = w;
            this.Height = h;
            buffer = new FloatRGB[w * h];
        }
        public int Width { get; set; }
        public int Height { get; set; }

        public FloatRGB this[int x, int y]
        {
            get => buffer[x + (y * Width)];
            set => buffer[x + (y * Width)] = value;
        }
    }

    public static class SdfGenerator
    {
        //siged distance field generator

        public static void GenerateSdf(FloatBmp output,
          Shape shape,
          double range,
          Vector2 scale,
          Vector2 translate)
        {

            List<Contour> contours = shape.contours;
            int contourCount = contours.Count;
            int w = output.Width, h = output.Height;
            List<int> windings = new List<int>(contourCount);
            for (int i = 0; i < contourCount; ++i)
            {
                windings.Add(contours[i].Winding());
            }

            double[] contourSD = new double[contourCount];
            for (int y = 0; y < h; ++y)
            {
                int row = shape.InverseYAxis ? h - y - 1 : y;
                for (int x = 0; x < w; ++x)
                {
                    double dummy = 0;
                    Vector2 p = (new Vector2(x + .5, y + .5) / scale) - translate;
                    double negDist = -SignedDistance.INFINITE.distance;
                    double posDist = SignedDistance.INFINITE.distance;
                    int winding = 0;


                    for (int i = 0; i < contourCount; ++i)
                    {
                        Contour contour = contours[i];
                        SignedDistance minDistance = SignedDistance.INFINITE;
                        List<EdgeHolder> edges = contour.Edges;
                        int edgeCount = edges.Count;
                        for (int ee = 0; ee < edgeCount; ++ee)
                        {
                            EdgeHolder edge = edges[ee];
                            SignedDistance distance = edge.edgeSegment.signedDistance(p, out dummy);
                            if (distance < minDistance)
                                minDistance = distance;
                        }

                        contourSD[i] = minDistance.distance;
                        if (windings[i] > 0 && minDistance.distance >= 0 && Math.Abs(minDistance.distance) < Math.Abs(posDist))
                            posDist = minDistance.distance;
                        if (windings[i] < 0 && minDistance.distance <= 0 && Math.Abs(minDistance.distance) < Math.Abs(negDist))
                            negDist = minDistance.distance;
                    }

                    double sd = SignedDistance.INFINITE.distance;
                    if (posDist >= 0 && Math.Abs(posDist) <= Math.Abs(negDist))
                    {
                        sd = posDist;
                        winding = 1;
                        for (int i = 0; i < contourCount; ++i)
                            if (windings[i] > 0 && contourSD[i] > sd && Math.Abs(contourSD[i]) < Math.Abs(negDist))
                                sd = contourSD[i];
                    }
                    else if (negDist <= 0 && Math.Abs(negDist) <= Math.Abs(posDist))
                    {
                        sd = negDist;
                        winding = -1;
                        for (int i = 0; i < contourCount; ++i)
                            if (windings[i] < 0 && contourSD[i] < sd && Math.Abs(contourSD[i]) < Math.Abs(posDist))
                                sd = contourSD[i];
                    }
                    for (int i = 0; i < contourCount; ++i)
                        if (windings[i] != winding && Math.Abs(contourSD[i]) < Math.Abs(sd))
                            sd = contourSD[i];

                    output[x, row] = (float)(sd / range + .5);
                }
            }
        }

    }

    struct MultiDistance
    {
        public double r, g, b;
        public double med;
    }

    public static class MsdfGenerator
    {
        static float median(float a, float b, float c)
        {
            return Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
        }

        static double median(double a, double b, double c)
        {
            return Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
        }

        static bool pixelClash(FloatRGB a, FloatRGB b, double threshold)
        {
            // Only consider pair where both are on the inside or both are on the outside
            bool aIn = ((a.r > 0.5f) ? 1 : 0) + ((a.g > .5f) ? 1 : 0) + ((a.b > .5f) ? 1 : 0) >= 2;
            bool bIn = ((b.r > 0.5f) ? 1 : 0) + ((b.g > .5f) ? 1 : 0) + ((b.b > .5f) ? 1 : 0) >= 2;

            if (aIn != bIn) return false;
            // If the change is 0 <-> 1 or 2 <-> 3 channels and not 1 <-> 1 or 2 <-> 2, it is not a clash
            if ((a.r > .5f && a.g > .5f && a.b > .5f) || (a.r < .5f && a.g < .5f && a.b < .5f)
                || (b.r > .5f && b.g > .5f && b.b > .5f) || (b.r < .5f && b.g < .5f && b.b < .5f))
                return false;
            // Find which color is which: _a, _b = the changing channels, _c = the remaining one
            float aa, ab, ba, bb, ac, bc;
            if ((a.r > .5f) != (b.r > .5f) && (a.r < .5f) != (b.r < .5f))
            {
                aa = a.r; ba = b.r;
                if ((a.g > .5f) != (b.g > .5f) && (a.g < .5f) != (b.g < .5f))
                {
                    ab = a.g; bb = b.g;
                    ac = a.b; bc = b.b;
                }
                else if ((a.b > .5f) != (b.b > .5f) && (a.b < .5f) != (b.b < .5f))
                {
                    ab = a.b; bb = b.b;
                    ac = a.g; bc = b.g;
                }
                else
                    return false; // this should never happen
            }
            else if ((a.g > .5f) != (b.g > .5f) && (a.g < .5f) != (b.g < .5f)
              && (a.b > .5f) != (b.b > .5f) && (a.b < .5f) != (b.b < .5f))
            {
                aa = a.g; ba = b.g;
                ab = a.b; bb = b.b;
                ac = a.r; bc = b.r;
            }
            else
                return false;
            // Find if the channels are in fact discontinuous
            return (Math.Abs(aa - ba) >= threshold)
                && (Math.Abs(ab - bb) >= threshold)
                && Math.Abs(ac - .5f) >= Math.Abs(bc - .5f); // Out of the pair, only flag the pixel farther from a shape edge
        }

        static void msdfErrorCorrection(FloatRGBBmp output, Vector2 threshold)
        {
            List<ValueTuple<int, int>> clashes = new List<ValueTuple<int, int>>();
            int w = output.Width, h = output.Height;
            for (int y = 0; y < h; ++y)
            {
                for (int x = 0; x < w; ++x)
                {
                    if ((x > 0 && pixelClash(output[x, y], output[x - 1, y], threshold.X))
                        || (x < w - 1 && pixelClash(output[x, y], output[x + 1, y], threshold.X))
                        || (y > 0 && pixelClash(output[x, y], output[x, y - 1], threshold.Y))
                        || (y < h - 1 && pixelClash(output[x, y], output[x, y + 1], threshold.Y)))
                    {
                        clashes.Add(new ValueTuple<int, int>(x, y));
                    }
                }
            }
            int clash_count = clashes.Count;
            for (int i = 0; i < clash_count; ++i)
            {
                ValueTuple<int, int> clash = clashes[i];
                FloatRGB pixel = output[clash.Item1, clash.Item2];
                float med = median(pixel.r, pixel.g, pixel.b);
                pixel.r = med; pixel.g = med; pixel.b = med;
            }
            //for (std::vector<std::pair<int, int>>::const_iterator clash = clashes.begin(); clash != clashes.end(); ++clash)
            //{
            //    FloatRGB & pixel = output(clash->first, clash->second);
            //    float med = median(pixel.r, pixel.g, pixel.b);
            //    pixel.r = med, pixel.g = med, pixel.b = med;
            //}
        }

        //multi-channel signed distance field generator
        struct EdgePoint
        {
            public SignedDistance minDistance;
            public EdgeHolder nearEdge;
            public double nearParam;
        }

        public static Color[] ConvertToR8G8B8A8(FloatRGBBmp input)
        {
            int height = input.Height;
            int width = input.Width;
            Color[] output = new Color[input.Width * input.Height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; ++x)
                {
                    //a b g r
                    //----------------------------------
                    FloatRGB pixel = input[x, y];
                    output[(y * width) + x] = new Color(pixel.r, pixel.g,  pixel.b, 255);
                }
            }

            return output;
        }

        public static Color[] ConvertToR8G8B8A8(FloatBmp input)
        {
            int height = input.Height;
            int width = input.Width;
            Color[] output = new Color[input.Width * input.Height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; ++x)
                {
                    //a b g r
                    //----------------------------------
                    float pixel = input[x, y];
                    output[(y * width) + x] = new Color(pixel, pixel, pixel, 255);
                }
            }

            return output;
        }

        public static void generateMSDF_v3(FloatRGBBmp output, Shape shape, double range, Vector2 scale, Vector2 translate, double edgeThreshold)
        {
            List<Contour> contours = shape.contours;
            int contourCount = contours.Count;
            int w = output.Width;
            int h = output.Height;

            WindingSpanner spanner = new WindingSpanner();
            double bound_l, bound_t, bound_b, bound_r;
            shape.FindBounds(out bound_l, out bound_b, out bound_r, out bound_t);

            var contourSD = new MultiDistance[contourCount];
            for(int y = 0; y < h; ++y)
            {
                int row = shape.InverseYAxis ? h - y - 1 : y;

                // Start slightly off the -X edge so we ensure we find all spans.
                spanner.Collect(shape, new Vector2(bound_l - 0.5, (y + 0.5) / scale.Y - translate.Y));

                for(int x = 0; x < w; ++x)
                {
                    Vector2 p = new Vector2(x + .5, y + .5) / scale - translate;

                    EdgePoint sr = new EdgePoint() { minDistance = SignedDistance.INFINITE },
                        sg = new EdgePoint() { minDistance = SignedDistance.INFINITE },
                        sb = new EdgePoint() { minDistance = SignedDistance.INFINITE };

                    sr.nearEdge = sg.nearEdge = sb.nearEdge = null;
                    sr.nearParam = sg.nearParam = sb.nearParam = 0;
                    int realSign = spanner.AdvanceTo(p.X);

                    for(int i = 0; i < contourCount; ++i)
                    {
                        Contour contour = contours[i];
                        EdgePoint r = new EdgePoint() { minDistance = SignedDistance.INFINITE }, 
                            g = new EdgePoint() { minDistance = SignedDistance.INFINITE }, 
                            b = new EdgePoint() { minDistance = SignedDistance.INFINITE };

                        r.nearEdge = g.nearEdge = b.nearEdge = null;
                        r.nearParam = g.nearParam = b.nearParam = 0;

                        for (int e = 0; e < contour.Edges.Count; e++)
                        {
                            double param;
                            EdgeHolder holder = contour.Edges[e];
                            EdgeSegment edge = holder.edgeSegment;
                            SignedDistance distance = edge.signedDistance(p, out param);
                            if(holder.HasComponent(EdgeColor.RED) && distance < r.minDistance)
                            {
                                r.minDistance = distance;
                                r.nearEdge = holder;
                                r.nearParam = param;
                            }
                            if (holder.HasComponent(EdgeColor.GREEN) && distance < g.minDistance)
                            {
                                g.minDistance = distance;
                                g.nearEdge = holder;
                                g.nearParam = param;
                            }
                            if (holder.HasComponent(EdgeColor.BLUE) && distance < b.minDistance)
                            {
                                b.minDistance = distance;
                                b.nearEdge = holder;
                                b.nearParam = param;
                            }
                        }

                        if (r.minDistance < sr.minDistance)
                        {
                            sr = r;
                        }
                        if (g.minDistance < sg.minDistance)
                        {
                            sg = g;
                        }
                        if (b.minDistance < sb.minDistance)
                        {
                            sb = b;
                        }
                    }

                    sb.nearEdge?.edgeSegment.distanceToPseudoDistance(ref sr.minDistance, p, sr.nearParam);
                    sb.nearEdge?.edgeSegment.distanceToPseudoDistance(ref sg.minDistance, p, sg.nearParam);
                    sb.nearEdge?.edgeSegment.distanceToPseudoDistance(ref sb.minDistance, p, sb.nearParam);

                    double dr = sr.minDistance.distance;
                    double dg = sg.minDistance.distance;
                    double db = sb.minDistance.distance;

                    double med = median(dr, dg, db);
                    // Note: Use signbit() not sign() here because we need to know -0 case.
                    int medSign = SdfMath.signbit(med) ? -1 : 1;

                    if (medSign != realSign)
                    {
                        dr = -dr;
                        dg = -dg;
                        db = -db;
                    }

                    output[x, row] = new FloatRGB()
                    {
                        r = (float)(dr / range + .5),
                        g = (float)(dg / range + .5),
                        b = (float)(db / range + .5)
                    };
                }
            }

            if (edgeThreshold > 0)
                msdfErrorCorrection(output, edgeThreshold / (scale * range));
        }

        public static void generateMSDF(FloatRGBBmp output, Shape shape, double range, Vector2 scale, Vector2 translate, double edgeThreshold)
        {
            List<Contour> contours = shape.contours;
            int contourCount = contours.Count;
            int w = output.Width;
            int h = output.Height;
            List<int> windings = new List<int>(contourCount);
            for (int i = 0; i < contourCount; ++i)
            {
                windings.Add(contours[i].Winding());
            }

            var contourSD = new MultiDistance[contourCount];

            for (int y = 0; y < h; ++y)
            {
                int row = shape.InverseYAxis ? h - y - 1 : y;
                for (int x = 0; x < w; ++x)
                {
                    Vector2 p = (new Vector2(x + .5, y + .5) / scale) - translate;
                    EdgePoint sr = new EdgePoint { minDistance = SignedDistance.INFINITE },
                        sg = new EdgePoint { minDistance = SignedDistance.INFINITE },
                        sb = new EdgePoint { minDistance = SignedDistance.INFINITE };
                    double d = Math.Abs(SignedDistance.INFINITE.distance);
                    double negDist = -Math.Abs(SignedDistance.INFINITE.distance);
                    double posDist = Math.Abs(SignedDistance.INFINITE.distance);
                    int winding = 0;

                    for (int n = 0; n < contourCount; ++n)
                    {
                        //for-each contour
                        Contour contour = contours[n];
                        List<EdgeHolder> edges = contour.Edges;
                        int edgeCount = edges.Count;
                        EdgePoint r = new EdgePoint { minDistance = SignedDistance.INFINITE },
                        g = new EdgePoint { minDistance = SignedDistance.INFINITE },
                        b = new EdgePoint { minDistance = SignedDistance.INFINITE };
                        for (int ee = 0; ee < edgeCount; ++ee)
                        {
                            EdgeHolder edge = edges[ee];
                            double param;
                            SignedDistance distance = edge.edgeSegment.signedDistance(p, out param);
                            if (edge.HasComponent(EdgeColor.RED) && distance < r.minDistance)
                            {
                                r.minDistance = distance;
                                r.nearEdge = edge;
                                r.nearParam = param;
                            }
                            if (edge.HasComponent(EdgeColor.GREEN) && distance < g.minDistance)
                            {
                                g.minDistance = distance;
                                g.nearEdge = edge;
                                g.nearParam = param;
                            }
                            if (edge.HasComponent(EdgeColor.BLUE) && distance < b.minDistance)
                            {
                                b.minDistance = distance;
                                b.nearEdge = edge;
                                b.nearParam = param;
                            }
                        }
                        //----------------
                        if (r.minDistance < sr.minDistance)
                            sr = r;
                        if (g.minDistance < sg.minDistance)
                            sg = g;
                        if (b.minDistance < sb.minDistance)
                            sb = b;
                        //----------------
                        double medMinDistance = Math.Abs(median(r.minDistance.distance, g.minDistance.distance, b.minDistance.distance));
                        if (medMinDistance < d)
                        {
                            d = medMinDistance;
                            winding = -windings[n];
                        }

                        if (r.nearEdge != null)
                            r.nearEdge.edgeSegment.distanceToPseudoDistance(ref r.minDistance, p, r.nearParam);
                        if (g.nearEdge != null)
                            g.nearEdge.edgeSegment.distanceToPseudoDistance(ref g.minDistance, p, g.nearParam);
                        if (b.nearEdge != null)
                            b.nearEdge.edgeSegment.distanceToPseudoDistance(ref b.minDistance, p, b.nearParam);

                        //--------------
                        medMinDistance = median(r.minDistance.distance, g.minDistance.distance, b.minDistance.distance);
                        contourSD[n].r = r.minDistance.distance;
                        contourSD[n].g = g.minDistance.distance;
                        contourSD[n].b = b.minDistance.distance;
                        contourSD[n].med = medMinDistance;

                        if (windings[n] > 0 && medMinDistance >= 0 && Math.Abs(medMinDistance) < Math.Abs(posDist))
                            posDist = medMinDistance;
                        if (windings[n] < 0 && medMinDistance <= 0 && Math.Abs(medMinDistance) < Math.Abs(negDist))
                            negDist = medMinDistance;
                    }
                    if (sr.nearEdge != null)
                        sr.nearEdge.edgeSegment.distanceToPseudoDistance(ref sr.minDistance, p, sr.nearParam);
                    if (sg.nearEdge != null)
                        sg.nearEdge.edgeSegment.distanceToPseudoDistance(ref sg.minDistance, p, sg.nearParam);
                    if (sb.nearEdge != null)
                        sb.nearEdge.edgeSegment.distanceToPseudoDistance(ref sb.minDistance, p, sb.nearParam);

                    MultiDistance msd;
                    msd.r = msd.g = msd.b = msd.med = SignedDistance.INFINITE.distance;
                    if (posDist >= 0 && Math.Abs(posDist) <= Math.Abs(negDist))
                    {
                        msd.med = SignedDistance.INFINITE.distance;
                        winding = 1;
                        for (int i = 0; i < contourCount; ++i)
                            if (windings[i] > 0 && contourSD[i].med > msd.med && Math.Abs(contourSD[i].med) < Math.Abs(negDist))
                                msd = contourSD[i];
                    }
                    else if (negDist <= 0 && Math.Abs(negDist) <= Math.Abs(posDist))
                    {
                        msd.med = -SignedDistance.INFINITE.distance;
                        winding = -1;
                        for (int i = 0; i < contourCount; ++i)
                            if (windings[i] < 0 && contourSD[i].med < msd.med && Math.Abs(contourSD[i].med) < Math.Abs(posDist))
                                msd = contourSD[i];
                    }
                    for (int i = 0; i < contourCount; ++i)
                        if (windings[i] != winding && Math.Abs(contourSD[i].med) < Math.Abs(msd.med))
                            msd = contourSD[i];
                    if (median(sr.minDistance.distance, sg.minDistance.distance, sb.minDistance.distance) == msd.med)
                    {
                        msd.r = sr.minDistance.distance;
                        msd.g = sg.minDistance.distance;
                        msd.b = sb.minDistance.distance;
                    }

                    output[x, row] = new FloatRGB(
                                (float)(msd.r / range + .5),
                                (float)(msd.g / range + .5),
                                (float)(msd.b / range + .5)
                            );
                }
            }

            if (edgeThreshold > 0)
                msdfErrorCorrection(output, edgeThreshold / (scale * range));
        }

        public static void GenerateSdf(FloatBmp output,
          Shape shape,
          double range,
          Vector2 scale,
          Vector2 translate)
        {

            List<Contour> contours = shape.contours;
            int contourCount = contours.Count;
            int w = output.Width, h = output.Height;
            List<int> windings = new List<int>(contourCount);
            for (int i = 0; i < contourCount; ++i)
            {
                windings.Add(contours[i].Winding());
            }

            //# ifdef MSDFGEN_USE_OPENMP
            //#pragma omp parallel
            //#endif
            {

                //# ifdef MSDFGEN_USE_OPENMP
                //#pragma omp for
                //#endif
                double[] contourSD = new double[contourCount];
                for (int y = 0; y < h; ++y)
                {
                    int row = shape.InverseYAxis ? h - y - 1 : y;
                    for (int x = 0; x < w; ++x)
                    {
                        double dummy = 0;
                        Vector2 p = (new Vector2(x + .5, y + .5) / scale) - translate;
                        double negDist = -SignedDistance.INFINITE.distance;
                        double posDist = SignedDistance.INFINITE.distance;
                        int winding = 0;


                        for (int i = 0; i < contourCount; ++i)
                        {
                            Contour contour = contours[i];
                            SignedDistance minDistance = SignedDistance.INFINITE;
                            List<EdgeHolder> edges = contour.Edges;
                            int edgeCount = edges.Count;
                            for (int ee = 0; ee < edgeCount; ++ee)
                            {
                                EdgeHolder edge = edges[ee];
                                SignedDistance distance = edge.edgeSegment.signedDistance(p, out dummy);
                                if (distance < minDistance)
                                    minDistance = distance;
                            }

                            contourSD[i] = minDistance.distance;
                            if (windings[i] > 0 && minDistance.distance >= 0 && Math.Abs(minDistance.distance) < Math.Abs(posDist))
                                posDist = minDistance.distance;
                            if (windings[i] < 0 && minDistance.distance <= 0 && Math.Abs(minDistance.distance) < Math.Abs(negDist))
                                negDist = minDistance.distance;
                        }

                        double sd = SignedDistance.INFINITE.distance;
                        if (posDist >= 0 && Math.Abs(posDist) <= Math.Abs(negDist))
                        {
                            sd = posDist;
                            winding = 1;
                            for (int i = 0; i < contourCount; ++i)
                                if (windings[i] > 0 && contourSD[i] > sd && Math.Abs(contourSD[i]) < Math.Abs(negDist))
                                    sd = contourSD[i];
                        }
                        else if (negDist <= 0 && Math.Abs(negDist) <= Math.Abs(posDist))
                        {
                            sd = negDist;
                            winding = -1;
                            for (int i = 0; i < contourCount; ++i)
                                if (windings[i] < 0 && contourSD[i] < sd && Math.Abs(contourSD[i]) < Math.Abs(posDist))
                                    sd = contourSD[i];
                        }
                        for (int i = 0; i < contourCount; ++i)
                            if (windings[i] != winding && Math.Abs(contourSD[i]) < Math.Abs(sd))
                                sd = contourSD[i];

                        output[x, row] = (float)(sd / range + .5);
                    }
                }
            }
        }
    }
}