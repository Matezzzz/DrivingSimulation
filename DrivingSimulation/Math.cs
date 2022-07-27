using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrivingSimulation
{
    struct Vector2
    {
        public float X;
        public float Y;

        public static Vector2 Zero = new (0, 0);

        public int Xi { get => (int)X; }
        public int Yi { get => (int)Y; }
        public Vector2(float x) : this(x, x) { }
        public Vector2(float x, float y)
        {
            X = x; Y = y;
        }
        public Vector2(double x, double y) : this((float) x, (float) y)
        { }
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
            return MathF.Atan2(Y, X) / MathF.PI * 180;
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
        public static Vector2 Min(Vector2 a, Vector2 b)
        {
            return new Vector2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y));
        }
        public static Vector2 Max(Vector2 a, Vector2 b)
        {
            return new Vector2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
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
        public override string ToString()
        {
            return $"Vec2({X:F4}, {Y:F4})";
        }
    }






    class BezierCurve : DrivingSimulationObject
    {
        public const float segment_length = 0.2f;
        const int bezier_curve_init_segments = 1000;



        readonly Vector2 p0, p1, p2, p3;

        readonly List<Vector2> points;

        public int SegmentCount { get => points.Count - 1; }
        public float PointCount { get => points.Count; }
        public float Length { get => SegmentCount * segment_length; }

        public override int DrawLayer => 0;
        protected override bool PreDraw => true;
        public BezierCurve(RoadWorld world, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Transform transform = null) : base(world)
        {
            this.p0 = p0; this.p1 = p1; this.p2 = p2; this.p3 = p3;
            if (transform == null) transform = Transform.Identity;
            List<Vector2> init_points = new();
            double curve_length = 0;
            for (int i = 0; i <= bezier_curve_init_segments; i++)
            {
                float t = 1.0f * i / bezier_curve_init_segments;
                Vector2 p = transform.Apply(ExactPos(t));
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
            this.p0 = transform.Apply(p0); this.p1 = transform.Apply(p1); this.p2 = transform.Apply(p2); this.p3 = transform.Apply(p3);
            points.Add(init_points.Last());
        }
        public Vector2 GetPos(float segment_i)
        {
            int i = (int)(segment_i - 1e-5);
            float k = segment_i - i;
            return (points[i] * (1 - k) + points[i + 1] * k);
        }
        public Vector2 GetDerivative(float segment_i)
        {
            int i = (int)(segment_i - 1e-5);
            return points[i + 1] - points[i];
        }

        protected override void Draw(SDLApp app, Transform transform)
        {
            DrawCurve(app, transform, Color.Black, 0);
        }

        public void DrawCurve(SDLApp app, Transform transform, Color color, float from)
        {
            int fromi = (int)from;
            if (fromi != from)
            {
                app.DrawLine(color, transform.Apply(GetPos(from)), transform.Apply(GetPos(fromi+1)));
                fromi++;
            }
            app.DrawLines(color, points, transform, fromi);
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


        private Vector2 ExactPos(float t)
        {
            float t_ = 1 - t;
            return t_ * t_ * t_ * p0 + 3 * t_ * t_ * t * p1 + 3 * t_ * t * t * p2 + t * t * t * p3;
        }
        public Vector2 ExactPosSeg(float seg)
        {
            return ExactPos(seg / SegmentCount);
        }


        public Vector2 ExactDerivativeSeg(float seg)
        {
            float t = seg / SegmentCount;
            float t_ = 1 - t;
            return 3 * t_ * t_ * (p1 - p0) + 6 * t_ * t * (p2 - p1) + 3 * t * t * (p3 - p2);
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
            return $"Curve: {p0} -> {p3}";
        }
    }
    static class BezierExtensions
    {
        public static float SegmentsToDist(this float segments)
        {
            return segments * BezierCurve.segment_length;
        }
        public static float DistToSegments(this float dist)
        {
            return dist / BezierCurve.segment_length;
        }
    }
}
