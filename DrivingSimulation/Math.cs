using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace DrivingSimulation
{
    [JsonObject(MemberSerialization.OptIn)]
    struct Vector2
    {
        [JsonProperty]
        public float X;
        [JsonProperty]
        public float Y;

        public static readonly Vector2 Zero = new (0);
        public static readonly Vector2 UnitX = new (1, 0);
        public static readonly Vector2 UnitY = new(0, 1);
        public static readonly Vector2 OneXY = new(1, 1);
        public static readonly Vector2 MinValue = new(float.MinValue);
        public static readonly Vector2 MaxValue = new(float.MaxValue);

        public int Xi { get => (int)X; }
        public int Yi { get => (int)Y; }
        public Vector2() : this(0) { }
        public Vector2(float x) : this(x, x) { }
        public Vector2(float x, float y)
        {
            X = x; Y = y;
        }
        public Vector2(double x, double y) : this((float) x, (float) y)
        { }
        public static Vector2 FromRotation(float rot)
        {
            return new Vector2(MathF.Cos(rot.Radians()), MathF.Sin(rot.Radians()));
        }

        public static Vector2 operator -(Vector2 v)
        {
            return new Vector2(-v.X, -v.Y);
        }
        public static Vector2 operator +(Vector2 x, Vector2 y)
        {
            return new Vector2(x.X + y.X, x.Y + y.Y);
        }
        public static Vector2 operator -(Vector2 x, Vector2 y)
        {
            return new Vector2(x.X - y.X, x.Y - y.Y);
        }
        public static Vector2 operator -(Vector2 x, float y)
        {
            return new Vector2(x.X - y, x.Y - y);
        }
        public static Vector2 operator *(Vector2 v, float k)
        {
            return new Vector2(v.X * k, v.Y * k);
        }
        public static Vector2 operator *(Vector2 x, Vector2 y)
        {
            return new Vector2(x.X * y.X, x.Y * y.Y);
        }
        public static Vector2 operator *(float k, Vector2 v)
        {
            return v * k;
        }
        public static Vector2 operator /(Vector2 v, float k)
        {
            return new Vector2(v.X / k, v.Y / k);
        }
        public static Vector2 operator /(Vector2 x, Vector2 y)
        {
            return new Vector2(x.X / y.X, x.Y / y.Y);
        }
        public Vector2 Normalized()
        {
            return this / Length();
        }
        public float Length()
        {
            return MathF.Sqrt(X * X + Y * Y);
        }
        public float Rotation()
        {
            return MathF.Atan2(Y, X).Degrees();
        }
        public Vector2 Rotate90CW()
        {
            return new Vector2(Y, -X);
        }
        public Vector2 Rotate270CW()
        {
            return -Rotate90CW();
        }
        public float Max()
        {
            return Math.Max(X, Y);
        }
        public float Min()
        {
            return Math.Min(X, Y);
        }
        public float Sum()
        {
            return X + Y;
        }
        public Vector2 Abs()
        {
            return new Vector2(MathF.Abs(X), MathF.Abs(Y));
        }
        public static Vector2 Min(Vector2 a, Vector2 b)
        {
            return new Vector2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y));
        }
        public static Vector2 Max(Vector2 a, Vector2 b)
        {
            return new Vector2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }
        public override bool Equals([NotNullWhen(true)] object obj)
        {
            if (obj is Vector2 v) return v == this;
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static Vector2 Mix(Vector2 a, Vector2 b, float k)
        {
            return (1 - k) * a + k * b;
        }
        public Vector2 Clamp(Vector2 min, Vector2 max)
        {
            Math.Clamp(X, min.X, max.X);
            Math.Clamp(Y, min.Y, max.Y);
            return this;
        }
        public static Vector2 Pow(float x, Vector2 y)
        {
            return new Vector2(MathF.Pow(x, y.X), MathF.Pow(x, y.Y));
        }
        public Vector2 Exp()
        {
            return new Vector2(MathF.Exp(X), MathF.Exp(Y));
        }
        public Vector2 Log()
        {
            return new Vector2(MathF.Log(X), MathF.Log(Y));
        }

        const float tolerance = 1e-6f;
        public static bool operator ==(Vector2 a, Vector2 b)
        {
            return MathF.Abs(a.X - b.X) < tolerance && MathF.Abs(a.Y - b.Y) < tolerance;
        }
        public static bool operator !=(Vector2 a, Vector2 b)
        {
            return !(a == b);
        }
        public float AsFloat()
        {
            if (Math.Abs(X - Y) > 1e-4) throw new ArgumentException("Converting vector with large x/y difference to float");
            return X;
        }
        public override string ToString()
        {
            return $"Vec2({X:F4}, {Y:F4})";
        }
    }








    [JsonObject(MemberSerialization.OptIn)]
    abstract class BezierCurve : SimulationObject
    {
        public const float segment_length = 0.2f;
        const int bezier_curve_init_segments = 1000;
        const int unfinished_resolution = 10;


        protected abstract Vector2 P0 { get; }
        protected abstract Vector2 P1 { get; }
        protected abstract Vector2 P2 { get; }
        protected abstract Vector2 P3 { get; }

        List<Vector2> points;

        public int SegmentCount { get => points.Count - 1; }
        public int PointCount { get => points.Count; }
        public float Length { get => SegmentCount * segment_length; }

        public override DrawLayer DrawZ => DrawLayer.TRAJECTORIES;

        [JsonConstructor]
        protected BezierCurve() : base(null) { }
        public BezierCurve(SimulationObjectCollection world) : base(world)
        {}
        protected override void FinishI(FinishPhase phase)
        {
            base.FinishI(phase);
            if (phase == FinishPhase.COMPUTE_TRAJECTORIES)
            {
                List<Vector2> init_points = new();
                double curve_length = 0;
                for (int i = 0; i <= bezier_curve_init_segments; i++)
                {
                    float t = 1.0f * i;
                    Vector2 p = ExactPos(t, bezier_curve_init_segments);
                    if (i != 0)
                    {
                        curve_length += (p - init_points.Last()).Length();
                    }
                    init_points.Add(p);
                }
                int curve_segments = (int)(curve_length / segment_length);
                double actual_segment_length = curve_length / curve_segments;
                double remaining_L = actual_segment_length;
                points = new() { init_points[0] };
                //do not iterate until the last segment - double rounding might either add or not add the last point. We add it manually below
                for (int i = 0; i < bezier_curve_init_segments - 1; i++)
                {
                    double L = (init_points[i + 1] - init_points[i]).Length();
                    if (L > remaining_L)
                    {
                        float k = (float)(remaining_L / L);
                        points.Add((1 - k) * init_points[i] + k * init_points[i + 1]);
                        remaining_L += actual_segment_length;
                    }
                    remaining_L -= L;
                }
                points.Add(init_points.Last());
            }
        }
        protected override void UnfinishI()
        {
            base.UnfinishI();
            points = null;
        }
        public Vector2 GetPos(float segment_i)
        {
            int i = (int)(segment_i - 1e-5);
            float k = segment_i - i;
            return (points[i] * (1 - k) + points[i + 1] * k);
        }
        public Vector2 GetDerivative(float segment_i)
        {
            segment_i -= 1e-5f;
            if (segment_i <= 0.5 || segment_i >= SegmentCount - 0.5)
            {
                int i = (int)(segment_i - 1e-5);
                return points[i + 1] - points[i];
            }
            float di = segment_i - 0.5f;
            int i0 = (int)di;
            Vector2 d1 = points[i0 + 1] - points[i0];
            Vector2 d2 = points[i0 + 2] - points[i0 + 1];
            float k = di - i0;
            return d1 * (1 - k) + d2 * k;
        }

        protected override void DrawI(SDLApp app, Transform camera)
        {
            DrawCurve(app, camera, Color.Black, 0);
        }

        public void DrawCurve(SDLApp app, Transform camera, Color color, float from)
        {
            Func<float, Vector2> f = Finished ? x => parent.GetParentWorld().LocalToWorldPos(GetPos(x)) : i => ExactPos(i, unfinished_resolution-1);
            int fromi = (int)from;
            if (fromi != from)
            {
                app.DrawLine(color, f(from), f(fromi+1), camera);
                fromi++;
            }
            app.DrawLines(color, f, Finished ? PointCount : unfinished_resolution, fromi, camera);
        }

        public void Intersect(BezierCurve b, out float i1, out float i2)
        {
            i1 = -1;
            i2 = -1;
            for (int i = 0; i < SegmentCount; i++)
            {
                for (int j = 0; j < b.SegmentCount; j++)
                {
                    LineIntersect(points[i], points[i + 1], b.points[j], b.points[j + 1], out float ii, out float ij);
                    if (0 <= ii && ii <= 1 && 0 <= ij && ij <= 1)
                    {
                        i1 = i + ii;
                        i2 = j + ij;
                        return;
                    }
                }
            }
        }


        public Vector2 ExactPos(float k, int part_count)
        {
            float t = k / part_count;
            float t_ = 1 - t;
            return t_ * t_ * t_ * P0 + 3 * t_ * t_ * t * P1 + 3 * t_ * t * t * P2 + t * t * t * P3;
        }


        public Vector2 ExactDerivative(float k, int part_count)
        {
            float t = k / part_count;
            float t_ = 1 - t;
            return 3 * t_ * t_ * (P1 - P0) + 6 * t_ * t * (P2 - P1) + 3 * t * t * (P3 - P2);
        }


        static void LineIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out float i1, out float i2)
        {
            float x13 = p1.X - p3.X, x12 = p1.X - p2.X, x34 = p3.X - p4.X;
            float y12 = p1.Y - p2.Y, y13 = p1.Y - p3.Y, y34 = p3.Y - p4.Y;
            float top1 = x13 * y34 - y13 * x34;
            float bot1 = x12 * y34 - y12 * x34;
            float top2 = x13 * y12 - y13 * x12;
            float bot2 = x12 * y34 - y12 * x34;
            i1 = top1 / bot1;
            i2 = top2 / bot2;
        }
        public override string ToString()
        {
            return $"Curve: {P0} -> {P3}";
        }
    }
    static class ConversionsExt
    {
        public static float SegmentsToDist(this float segments)
        {
            return segments * BezierCurve.segment_length;
        }
        public static float DistToSegments(this float dist)
        {
            return dist / BezierCurve.segment_length;
        }
        const float rad_k = MathF.PI / 180;
        public static float Radians(this float deg) => deg * rad_k;
        const float deg_k = 180 / MathF.PI;
        public static float Degrees(this float rad) => rad * deg_k;
    }
}
