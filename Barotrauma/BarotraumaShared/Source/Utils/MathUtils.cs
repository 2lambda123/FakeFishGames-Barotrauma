﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Barotrauma.Extensions;

namespace Barotrauma
{
    //TODO: Currently this is only used for text positioning? -> move there?
    [Flags]
    public enum Alignment
    {
        CenterX = 1, Left = 2, Right = 4, CenterY = 8, Top = 16, Bottom = 32,
        TopLeft = (Top | Left), TopCenter = (CenterX | Top), TopRight = (Top | Right),
        CenterLeft = (Left | CenterY), Center = (CenterX | CenterY), CenterRight = (Right | CenterY),
        BottomLeft = (Bottom | Left), BottomCenter = (CenterX | Bottom), BottomRight = (Bottom | Right),
    }

    static class MathUtils
    {
        public static int PositiveModulo(int i, int n)
        {
            return (i % n + n) % n;
        }

        public static Vector2 SmoothStep(Vector2 v1, Vector2 v2, float amount)
        {
            return new Vector2(
                 MathHelper.SmoothStep(v1.X, v2.X, amount),
                 MathHelper.SmoothStep(v1.Y, v2.Y, amount));
        }

        public static Vector2 ClampLength(this Vector2 v, float length)
        {
            float currLength = v.Length();
            if (currLength > length)
            {
                return v / currLength * length;
            }
            return v;
        }

        public static float Round(float value, float div)
        {
            return (value < 0.0f) ? 
                (float)Math.Ceiling(value / div) * div : 
                (float)Math.Floor(value / div) * div;
        }

        public static float RoundTowardsClosest(float value, float div)
        {
            return (float)Math.Round(value / div) * div;
        }

        public static float VectorToAngle(Vector2 vector)
        {
            return (float)Math.Atan2(vector.Y, vector.X);
        }

        public static Point ToPoint(Vector2 vector)
        {
            return new Point((int)vector.X,(int)vector.Y);
        }

        public static bool IsValid(float value)
        {
            return (!float.IsInfinity(value) && !float.IsNaN(value));
        }

        public static bool IsValid(Vector2 vector)
        {
            return (IsValid(vector.X) && IsValid(vector.Y));
        }

        public static Rectangle ExpandRect(Rectangle rect, int amount)
        {
            return new Rectangle(rect.X - amount, rect.Y + amount, rect.Width + amount * 2, rect.Height + amount * 2);
        }

        public static int VectorOrientation(Vector2 p1, Vector2 p2, Vector2 p)
        {
            // Determinant
            float Orin = (p2.X - p1.X) * (p.Y - p1.Y) - (p.X - p1.X) * (p2.Y - p1.Y);

            if (Orin > 0)
                return -1; //          (* Orientation is to the left-hand side  *)
            if (Orin < 0)
                return 1; // (* Orientation is to the right-hand side *)

            return 0; //  (* Orientation is neutral aka collinear  *)
        }

        
        public static float CurveAngle(float from, float to, float step)
        {

            from = WrapAngleTwoPi(from);
            to = WrapAngleTwoPi(to);

            if (Math.Abs(from - to) < MathHelper.Pi)
            {
                // The simple case - a straight lerp will do. 
                return MathHelper.Lerp(from, to, step);
            }

            // If we get here we have the more complex case. 
            // First, increment the lesser value to be greater. 
            if (from < to)
                from += MathHelper.TwoPi;
            else
                to += MathHelper.TwoPi;

            float retVal = MathHelper.Lerp(from, to, step);

            // Now ensure the return value is between 0 and 2pi 
            if (retVal >= MathHelper.TwoPi)
                retVal -= MathHelper.TwoPi;
            return retVal;
        }

        /// <summary>
        /// wrap the angle between 0.0f and 2pi
        /// </summary>
        public static float WrapAngleTwoPi(float angle)
        {
            if (float.IsInfinity(angle) || float.IsNegativeInfinity(angle) || float.IsNaN(angle))
            {
                return 0.0f;
            }

            while (angle < 0)
                angle += MathHelper.TwoPi;
            while (angle >= MathHelper.TwoPi)
                angle -= MathHelper.TwoPi;

            return angle;
        }

        /// <summary>
        /// wrap the angle between -pi and pi
        /// </summary>
        public static float WrapAnglePi(float angle)
        {
            if (float.IsInfinity(angle) || float.IsNegativeInfinity(angle) || float.IsNaN(angle))
            {
                return 0.0f;
            }
            // Ensure that -pi <= angle < pi for both "from" and "to" 
            while (angle < -MathHelper.Pi)
                angle += MathHelper.TwoPi;
            while (angle >= MathHelper.Pi)
                angle -= MathHelper.TwoPi;

            return angle;
        }

        public static float GetShortestAngle(float from, float to)
        {
            // Ensure that 0 <= angle < 2pi for both "from" and "to" 
            from = WrapAngleTwoPi(from);
            to = WrapAngleTwoPi(to);

            if (Math.Abs(from - to) < MathHelper.Pi)
            {
                return to - from;
            }

            // If we get here we have the more complex case. 
            // First, increment the lesser value to be greater. 
            if (from < to)
                from += MathHelper.TwoPi;
            else
                to += MathHelper.TwoPi;

            return to - from;
        }

        /// <summary>
        /// solves the angle opposite to side a (parameters: lengths of each side)
        /// </summary>
        public static float SolveTriangleSSS(float a, float b, float c)
        {
            float A = (float)Math.Acos((b * b + c * c - a * a) / (2 * b * c));

            if (float.IsNaN(A)) A = 1.0f;

            return A;
        }

        public static byte AngleToByte(float angle)
        {
            angle = WrapAngleTwoPi(angle);
            angle = angle * (255.0f / MathHelper.TwoPi);
            return Convert.ToByte(angle);
        }

        public static float ByteToAngle(byte b)
        {
            float angle = (float)b;
            angle = angle * (MathHelper.TwoPi / 255.0f);
            return angle;
        }

        /// <summary>
        /// check whether line from a to b is intersecting with line from c to b
        /// </summary>
        public static bool LinesIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float denominator = ((b.X - a.X) * (d.Y - c.Y)) - ((b.Y - a.Y) * (d.X - c.X));
            float numerator1 = ((a.Y - c.Y) * (d.X - c.X)) - ((a.X - c.X) * (d.Y - c.Y));
            float numerator2 = ((a.Y - c.Y) * (b.X - a.X)) - ((a.X - c.X) * (b.Y - a.Y));

            if (denominator == 0) return numerator1 == 0 && numerator2 == 0;

            float r = numerator1 / denominator;
            float s = numerator2 / denominator;

            return (r >= 0 && r <= 1) && (s >= 0 && s <= 1);
        }

        // a1 is line1 start, a2 is line1 end, b1 is line2 start, b2 is line2 end
        public static Vector2? GetLineIntersection(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            Vector2 b = a2 - a1;
            Vector2 d = b2 - b1;
            float bDotDPerp = b.X * d.Y - b.Y * d.X;

            // if b dot d == 0, it means the lines are parallel so have infinite intersection points
            if (bDotDPerp == 0) return null;

            Vector2 c = b1 - a1;
            float t = (c.X * d.Y - c.Y * d.X) / bDotDPerp;
            if (t < 0 || t > 1) return null;

            float u = (c.X * b.Y - c.Y * b.X) / bDotDPerp;
            if (u < 0 || u > 1) return null;

            return a1 + t * b;
        }
        
        public static Vector2? GetAxisAlignedLineIntersection(Vector2 a1, Vector2 a2, Vector2 axisAligned1, Vector2 axisAligned2, bool isHorizontal)
        {
            if (!isHorizontal)
            {
                if (Math.Sign(a1.X - axisAligned1.X) == Math.Sign(a2.X - axisAligned1.X))                
                    return null;
                
                float s = (a2.Y - a1.Y) / (a2.X - a1.X);
                float y = a1.Y + (axisAligned1.X - a1.X) * s;

                if (axisAligned1.Y < axisAligned2.Y)
                {
                    if (y < axisAligned1.Y) return null;
                    if (y > axisAligned2.Y) return null;
                }
                else
                {
                    if (y > axisAligned1.Y) return null;
                    if (y < axisAligned2.Y) return null;
                }
                
                return new Vector2(axisAligned1.X, y);
            }
            else //horizontal line
            {
                if (Math.Sign(a1.Y - axisAligned1.Y) == Math.Sign(a2.Y - axisAligned1.Y))                
                    return null;

                float s = (a2.X - a1.X) / (a2.Y - a1.Y);
                float x = a1.X + (axisAligned1.Y - a1.Y) * s;

                if (axisAligned1.X < axisAligned2.X)
                {
                    if (x < axisAligned1.X) return null;
                    if (x > axisAligned2.X) return null;
                }
                else
                {
                    if (x > axisAligned1.X) return null;
                    if (x < axisAligned2.X) return null;
                }
                
                return new Vector2(x, axisAligned1.Y);
            }
        }

        public static Vector2? GetLineRectangleIntersection(Vector2 a1, Vector2 a2, Rectangle rect)
        {
            Vector2? intersection = GetAxisAlignedLineIntersection(a1, a2, 
                new Vector2(rect.X, rect.Y),
                new Vector2(rect.Right, rect.Y),
                true);

            if (intersection != null) return intersection;

            intersection = GetAxisAlignedLineIntersection(a1, a2,
                new Vector2(rect.X, rect.Y-rect.Height),
                new Vector2(rect.Right, rect.Y-rect.Height),
                true);

            if (intersection != null) return intersection;

            intersection = GetAxisAlignedLineIntersection(a1, a2,
                new Vector2(rect.X, rect.Y),
                new Vector2(rect.X, rect.Y - rect.Height),
                false);

            if (intersection != null) return intersection;

            return GetAxisAlignedLineIntersection(a1, a2,
                new Vector2(rect.Right, rect.Y),
                new Vector2(rect.Right, rect.Y - rect.Height),
                false);
        }

        public static List<Vector2> GetLineRectangleIntersections(Vector2 a1, Vector2 a2, Rectangle rect)
        {
            List<Vector2> intersections = new List<Vector2>();

            Vector2? intersection = GetAxisAlignedLineIntersection(a1, a2,
                new Vector2(rect.X, rect.Y),
                new Vector2(rect.Right, rect.Y),
                true);

            if (intersection != null) intersections.Add((Vector2)intersection);

            intersection = GetAxisAlignedLineIntersection(a1, a2,
                new Vector2(rect.X, rect.Y - rect.Height),
                new Vector2(rect.Right, rect.Y - rect.Height),
                true);

            if (intersection != null) intersections.Add((Vector2)intersection);

            intersection = GetAxisAlignedLineIntersection(a1, a2,
                new Vector2(rect.X, rect.Y),
                new Vector2(rect.X, rect.Y - rect.Height),
                false);

            if (intersection != null) intersections.Add((Vector2)intersection);

            intersection = GetAxisAlignedLineIntersection(a1, a2,
                new Vector2(rect.Right, rect.Y),
                new Vector2(rect.Right, rect.Y - rect.Height),
                false);

            if (intersection != null) intersections.Add((Vector2)intersection);

            return intersections;
        }

        public static float LineToPointDistance(Vector2 lineA, Vector2 lineB, Vector2 point)
        {
            float xDiff = lineB.X - lineA.X;
            float yDiff = lineB.Y - lineA.Y;

            if (xDiff == 0 && yDiff == 0)
            {
                return Vector2.Distance(lineA, point);
            }

            return (float)(Math.Abs(xDiff * (lineA.Y - point.Y) - yDiff * (lineA.X - point.X)) /
                Math.Sqrt(xDiff * xDiff + yDiff * yDiff));
        } 

        public static bool CircleIntersectsRectangle(Vector2 circlePos, float radius, Rectangle rect)
        {
            float xDist = Math.Abs(circlePos.X - rect.Center.X);
            float yDist = Math.Abs(circlePos.Y - rect.Center.Y);

            int halfWidth = rect.Width / 2;
            int halfHeight = rect.Height / 2;
            
            if (xDist > (halfWidth + radius))   { return false; }
            if (yDist > (halfHeight + radius))  { return false; }   


            if (xDist <= (halfWidth))           { return true; }         
            if (yDist <= (halfHeight))          { return true; }

            float distSqX = xDist - halfWidth;
            float distSqY = yDist - halfHeight;

            return (distSqX * distSqX + distSqY * distSqY <= (radius * radius));
        }
        
        /// <summary>
        /// divide a convex hull into triangles
        /// </summary>
        /// <returns>List of triangle vertices (sorted counter-clockwise)</returns>
        public static List<Vector2[]> TriangulateConvexHull(List<Vector2> vertices, Vector2 center)
        {
            List<Vector2[]> triangles = new List<Vector2[]>();

            int triangleCount = vertices.Count - 2;

            vertices.Sort(new CompareCCW(center));

            int lastIndex = 1;
            for (int i = 0; i < triangleCount; i++)
            {
                Vector2[] triangleVertices = new Vector2[3];
                triangleVertices[0] = vertices[0];
                int k = 1;
                for (int j = lastIndex; j <= lastIndex + 1; j++)
                {
                    triangleVertices[k] = vertices[j];
                    k++;
                }
                lastIndex += 1;

                triangles.Add(triangleVertices);
            }

            return triangles;
        }

        public static List<Vector2> GiftWrap(List<Vector2> points)
        {
            if (points.Count == 0) return points;

            Vector2 leftMost = points[0];
            foreach (Vector2 point in points)
            {
                if (point.X < leftMost.X) leftMost = point;
            }

            List<Vector2> wrappedPoints = new List<Vector2>();

            Vector2 currPoint = leftMost;
            Vector2 endPoint;
            do
            {
                wrappedPoints.Add(currPoint);
                endPoint = points[0];

                for (int i = 1; i < points.Count; i++)
                {
                    if (points[i] == currPoint) continue;
                    if (currPoint == endPoint ||
                        MathUtils.VectorOrientation(currPoint, endPoint, points[i]) == -1)
                    {
                        endPoint = points[i];
                    }
                }
                
                currPoint = endPoint;

            }
            while (endPoint != leftMost);

            return wrappedPoints;
        }

        public static List<Vector2[]> GenerateJaggedLine(Vector2 start, Vector2 end, int iterations, float offsetAmount)
        {
            List<Vector2[]> segments = new List<Vector2[]>();

            segments.Add(new Vector2[] { start, end });
            
            for (int n = 0; n < iterations; n++)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    Vector2 startSegment = segments[i][0];
                    Vector2 endSegment = segments[i][1];

                    segments.RemoveAt(i);

                    Vector2 midPoint = (startSegment + endSegment) / 2.0f;

                    Vector2 normal = Vector2.Normalize(endSegment - startSegment);
                    normal = new Vector2(-normal.Y, normal.X);
                    midPoint += normal * Rand.Range(-offsetAmount, offsetAmount, Rand.RandSync.Server);

                    segments.Insert(i, new Vector2[] { startSegment, midPoint });
                    segments.Insert(i + 1, new Vector2[] { midPoint, endSegment });

                    i++;
                }

                offsetAmount *= 0.5f;
            }

            return segments;
        }

        // Returns the human-readable file size for an arbitrary, 64-bit file size 
        // The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB"
        public static string GetBytesReadable(long i)
        {
            // Get absolute value
            long absolute_i = (i < 0 ? -i : i);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (i >> 50);
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (i >> 40);
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (i >> 30);
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (i >> 20);
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (i >> 10);
            }
            else if (absolute_i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);
            // Return formatted number with suffix
            return readable.ToString("0.# ") + suffix;
        }

        public static void SplitRectanglesHorizontal(List<Rectangle> rects, Vector2 point)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                if (point.Y > rects[i].Y && point.Y < rects[i].Y + rects[i].Height)
                {
                    Rectangle rect1 = rects[i];
                    Rectangle rect2 = rects[i];

                    rect1.Height = (int)(point.Y - rects[i].Y);

                    rect2.Height = rects[i].Height - rect1.Height;
                    rect2.Y = rect1.Y + rect1.Height;
                    rects[i] = rect1;
                    rects.Insert(i + 1, rect2); i++;
                }
            }
        }

        public static void SplitRectanglesVertical(List<Rectangle> rects, Vector2 point)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                if (point.X>rects[i].X && point.X<rects[i].X+rects[i].Width)
                {
                    Rectangle rect1 = rects[i];
                    Rectangle rect2 = rects[i];
                    
                    rect1.Width = (int)(point.X-rects[i].X);

                    rect2.Width = rects[i].Width - rect1.Width;
                    rect2.X = rect1.X + rect1.Width;
                    rects[i] = rect1;
                    rects.Insert(i + 1, rect2); i++;
                }
            }

            /*for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i].Width <= 0 || rects[i].Height <= 0)
                {
                    rects.RemoveAt(i); i--;
                }
            }*/
        }

        /// <summary>
        /// Float comparison. Note that may still fail in some cases.
        /// </summary>
        // References: 
        // http://floating-point-gui.de/errors/comparison/
        // https://stackoverflow.com/questions/3874627/floating-point-comparison-functions-for-c-sharp
        public static bool NearlyEqual(float a, float b, float epsilon = 0.0001f)
        {
            float diff = Math.Abs(a - b);
            if (a == b)
            {
                // shortcut, handles infinities
                return true;
            }
            else if (a == 0 || b == 0 || diff < float.Epsilon)
            {
                // a or b is zero or both are extremely close to it
                // relative error is less meaningful here
                return diff < epsilon;
            }
            else
            {
                // use relative error
                return diff / (Math.Abs(a) + Math.Abs(b)) < epsilon;
            }
        }

        /// Returns a position in a curve.
        /// </summary>
        public static Vector2 Bezier(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            return Pow(1 - t, 2) * start + 2 * t * (1 - t) * control + Pow(t, 2) * end;
        }

        public static float Pow(float f, float p)
        {
            return (float)Math.Pow(f, p);
        }

        /// <summary>
        /// Rotates a point in 2d space around another point.
        /// Modified from:
        /// http://www.gamefromscratch.com/post/2012/11/24/GameDev-math-recipes-Rotating-one-point-around-another-point.aspx
        /// </summary>
        public static Vector2 RotatePointAroundTarget(Vector2 point, Vector2 target, float degrees, bool clockWise = true)
        {
            // (Math.PI / 180) * degrees
            var angle = MathHelper.ToRadians(degrees);
            var sin = Math.Sin(angle);
            var cos = Math.Cos(angle);
            if (!clockWise)
            {
                sin = -sin;
            }
            Vector2 dir = point - target;
            var x = (cos * dir.X) - (sin * dir.Y) + target.X;
            var y = (sin * dir.X) + (cos * dir.Y) + target.Y;
            return new Vector2((float)x, (float)y);
        }

        /// <summary>
        /// Returns the corners of an imaginary rectangle.
        /// Unlike the XNA rectangle, this can be rotated with the up parameter.
        /// </summary>
        public static Vector2[] GetImaginaryRect(Vector2 up, Vector2 center, Vector2 size)
        {
            return GetImaginaryRect(new Vector2[4], up, center, size);
        }

        /// <summary>
        /// Returns the corners of an imaginary rectangle.
        /// Unlike the XNA Rectangle, this can be rotated with the up parameter.
        /// </summary>
        public static Vector2[] GetImaginaryRect(Vector2[] corners, Vector2 up, Vector2 center, Vector2 size)
        {
            if (corners.Length != 4)
            {
                throw new Exception("Invalid length for the corners array. Must be 4.");
            }
            Vector2 halfSize = size / 2;
            Vector2 left = up.Right();
            corners[0] = center + up * halfSize.Y + left * halfSize.X;
            corners[1] = center + up * halfSize.Y - left * halfSize.X;
            corners[2] = center - up * halfSize.Y - left * halfSize.X;
            corners[3] = center - up * halfSize.Y + left * halfSize.X;
            return corners;
        }

        /// <summary>
        /// Check if a point is inside a rectangle.
        /// Unlike the XNA Rectangle, this rectangle might have been rotated.
        /// For XNA Rectangles, use the Contains instance method.
        /// </summary>
        public static bool RectangleContainsPoint(Vector2[] corners, Vector2 point)
        {
            if (corners.Length != 4)
            {
                throw new Exception("Invalid length of the corners array! Must be 4");
            }
            return RectangleContainsPoint(corners[0], corners[1], corners[2], corners[3], point);
        }

        /// <summary>
        /// Check if a point is inside a rectangle.
        /// Unlike the XNA Rectangle, this rectangle might have been rotated.
        /// For XNA Rectangles, use the Contains instance method.
        /// </summary>
        public static bool RectangleContainsPoint(Vector2 c1, Vector2 c2, Vector2 c3, Vector2 c4, Vector2 point)
        {
            return TriangleContainsPoint(c1, c2, c3, point) || TriangleContainsPoint(c1, c3, c4, point);
        }

        /// <summary>
        /// Slightly modified from https://gamedev.stackexchange.com/questions/110229/how-do-i-efficiently-check-if-a-point-is-inside-a-rotated-rectangle
        /// </summary>
        public static bool TriangleContainsPoint(Vector2 c1, Vector2 c2, Vector2 c3, Vector2 point)
        {
            // Compute vectors        
            Vector2 v0 = c3 - c1;
            Vector2 v1 = c2 - c1;
            Vector2 v2 = point - c1;

            // Compute dot products
            float dot00 = Vector2.Dot(v0, v0);
            float dot01 = Vector2.Dot(v0, v1);
            float dot02 = Vector2.Dot(v0, v2);
            float dot11 = Vector2.Dot(v1, v1);
            float dot12 = Vector2.Dot(v1, v2);

            // Compute barycentric coordinates
            float invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
            float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            // Check if the point is in triangle
            return u >= 0 && v >= 0 && (u + v) < 1;
        }
    }

    class CompareCCW : IComparer<Vector2>
    {
        private Vector2 center;

        public CompareCCW(Vector2 center)
        {
            this.center = center;
        }
        public int Compare(Vector2 a, Vector2 b)
        {
            return Compare(a, b, center);
        }

        public static int Compare(Vector2 a, Vector2 b, Vector2 center)
        {
            if (a == b) return 0;
            if (a.X - center.X >= 0 && b.X - center.X < 0) return -1;
            if (a.X - center.X < 0 && b.X - center.X >= 0) return 1;
            if (a.X - center.X == 0 && b.X - center.X == 0)
            {
                if (a.Y - center.Y >= 0 || b.Y - center.Y >= 0) return Math.Sign(b.Y - a.Y);
                return Math.Sign(a.Y - b.Y);
            }

            // compute the cross product of vectors (center -> a) x (center -> b)
            float det = (a.X - center.X) * (b.Y - center.Y) - (b.X - center.X) * (a.Y - center.Y);
            if (det < 0) return -1;
            if (det > 0) return 1;

            // points a and b are on the same line from the center
            // check which point is closer to the center
            float d1 = (a.X - center.X) * (a.X - center.X) + (a.Y - center.Y) * (a.Y - center.Y);
            float d2 = (b.X - center.X) * (b.X - center.X) + (b.Y - center.Y) * (b.Y - center.Y);
            return Math.Sign(d2 - d1);
        }
    }
}
