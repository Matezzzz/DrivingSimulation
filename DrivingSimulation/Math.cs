using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace DrivingSimulation
{


    //A 2D vectori
    [JsonObject(MemberSerialization.OptIn)]
    struct Vector2
    {
        [JsonProperty]
        public float X;
        [JsonProperty]
        public float Y;

        public static readonly Vector2 Zero = new ();
        public static readonly Vector2 UnitX = new (1, 0);
        public static readonly Vector2 UnitY = new(0, 1);
        //both components equal to 1
        public static readonly Vector2 OneXY = new(1);
        //both components equal to float.MinValue
        public static readonly Vector2 MinValue = new(float.MinValue);
        //both components equal to float.MaxValue
        public static readonly Vector2 MaxValue = new(float.MaxValue);

        //get X cast to int
        public int Xi { get => (int)X; }
        //get Y cast to int
        public int Yi { get => (int)Y; }
        //a null vector
        public Vector2() : this(0) { }
        //a vector with both components equal to x
        public Vector2(float x) : this(x, x) { }
        public Vector2(float x, float y)
        {
            X = x; Y = y;
        }
        public Vector2(double x, double y) : this((float) x, (float) y)
        { }

        //convert rotation in degrees to vector
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
        //multiply elements together and return
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
        //divide elements together and return
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
        //return rotation in degrees
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
        //return the larger component
        public float Max()
        {
            return Math.Max(X, Y);
        }
        //return the smaller component
        public float Min()
        {
            return Math.Min(X, Y);
        }
        //sum components together
        public float Sum()
        {
            return X + Y;
        }
        //take the absolute value of each component and return that
        public Vector2 Abs()
        {
            return new Vector2(MathF.Abs(X), MathF.Abs(Y));
        }
        //call MathF.Min on each component from a and b, then return the resulting vector
        public static Vector2 Min(Vector2 a, Vector2 b)
        {
            return new Vector2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y));
        }
        //call MathF.Max on each component from a and b, then return the resulting vector
        public static Vector2 Max(Vector2 a, Vector2 b)
        {
            return new Vector2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }
        //refer to the == operator. Only here so IDE doesn't complain
        public override bool Equals([NotNullWhen(true)] object obj)
        {
            if (obj is Vector2 v) return v == this;
            return base.Equals(obj);
        }
        //only here so IDE doesn't complain
        public override int GetHashCode() => base.GetHashCode();

        //clamp both coordinates between their respective min and max
        public Vector2 Clamp(Vector2 min, Vector2 max)
        {
            return new Vector2(Math.Clamp(X, min.X, max.X), Math.Clamp(Y, min.Y, max.Y));
        }
        //take e to the power of X, then Y. Return the result
        public Vector2 Exp()
        {
            return new Vector2(MathF.Exp(X), MathF.Exp(Y));
        }
        //take the log of X and Y, then return
        public Vector2 Log()
        {
            return new Vector2(MathF.Log(X), MathF.Log(Y));
        }


        const float tolerance = 1e-6f;
        //true if difference between both coordinates is smaller than tolerance
        public static bool operator ==(Vector2 a, Vector2 b)
        {
            return MathF.Abs(a.X - b.X) < tolerance && MathF.Abs(a.Y - b.Y) < tolerance;
        }
        //just inverse the == operator
        public static bool operator !=(Vector2 a, Vector2 b)
        {
            return !(a == b);
        }
        //convert to float - used e.g. when we know vector represents a uniform scale, and want to convert it back to float scale
        public float AsFloat()
        {
            //if (Math.Abs(X - Y) > tolerance) throw new ArgumentException("Converting vector with large x/y difference to float");
            return X;
        }
        //convert vector to string - E.g. 'Vec2(0.1245, 3.6211)'
        public override string ToString()
        {
            return $"Vec2({X:F4}, {Y:F4})";
        }
    }







    //Holds one bezier curve
    [JsonObject(MemberSerialization.OptIn)]
    abstract class BezierCurve : SimulationObject
    {
        

        //abstract methods to get each individual point - now, trajectory can override it to be equal to a position of road connection vector, which can change each frame, when user moves it
        protected abstract Vector2 P0 { get; }
        protected abstract Vector2 P1 { get; }
        protected abstract Vector2 P2 { get; }
        protected abstract Vector2 P3 { get; }

        //points after the curve is finished
        List<Vector2> points;

        const float segment_length = Constant.bezier_curve_segment_length;
        const int bezier_curve_finish_resolution = Constant.bezier_curve_finish_resolution;

        //how many segments is this curve composed of
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
                //compute the defined number of points on the bezier curve. These are computed from the cubic bezier curve equation, using parameter values evenly distributed between 0 and 1
                List<Vector2> init_points = new();
                //approximate curve length
                double curve_length = 0;
                //go through all parameter values
                for (int i = 0; i <= bezier_curve_finish_resolution; i++)
                {
                    Vector2 p = ExactPos(i, bezier_curve_finish_resolution);
                    //if there already was a point computed before, compute segment length and add it
                    if (i != 0) curve_length += (p - init_points.Last()).Length();
                    init_points.Add(p);
                }
                //how many segments will this curve have (+1 to always have at least one segment)
                int curve_segments = (int)(curve_length / segment_length) + 1;
                //compute segment length for this curve (as close to desired length selected in constants as possible)
                double actual_segment_length = curve_length / curve_segments;
                //length until next point should be added
                double remaining_L = actual_segment_length;
                //initiate final curve points - start with just the beginning one
                points = new() { init_points[0] };
                //last point is at the threshold of being added - do not iterate until the last segment - double rounding might either add or not add the last point. We add it manually below
                for (int i = 0; i < bezier_curve_finish_resolution - 1; i++)
                {
                    //compute current section length
                    double L = (init_points[i + 1] - init_points[i]).Length();
                    //if the next point should be on this segment, compute the exact position using linear interpolation
                    if (L > remaining_L)
                    {
                        float k = (float)(remaining_L / L);
                        points.Add((1 - k) * init_points[i] + k * init_points[i + 1]);
                        remaining_L += actual_segment_length;
                    }
                    //subtract the length from remaining one
                    remaining_L -= L;
                }
                //add the last point
                points.Add(init_points.Last());
            }
        }
        protected override void UnfinishI()
        {
            base.UnfinishI();
            //set points to null (they will be initialized again next finish, perhaps differently if point is moved)
            points = null;
        }
        //get a position in simulation mode (points must be initialized)
        public Vector2 GetPos(float segment_i)
        {
            //subtract a small tolerance (otherwise, if we were getting the exact last segment, this code would not work)
            int i = (int)(segment_i - 1e-5);
            //linear interpolation between two nearest points
            float k = segment_i - i;
            return (points[i] * (1 - k) + points[i + 1] * k);
        }
        //get derivative in simulation mode (points must be initialized)
        public Vector2 GetDerivative(float segment_i)
        {
            //pretty much, linear interpolation between the derivatives of two nearest lines
            segment_i -= 1e-5f;
            //if there are no two nearest lines, because we are at the border, just return the derivative of current line
            if (segment_i <= 0.5 || segment_i >= SegmentCount - 0.5)
            {
                int i = (int)(segment_i - 1e-5);
                return points[i + 1] - points[i];
            }
            //if there are two nearest lines
            float di = segment_i - 0.5f;
            int i0 = (int)di;
            //compute their derivatives
            Vector2 d1 = points[i0 + 1] - points[i0];
            Vector2 d2 = points[i0 + 2] - points[i0 + 1];
            //then do the interpolation
            float k = di - i0;
            return d1 * (1 - k) + d2 * k;
        }

        protected override void DrawI(SDLApp app, Transform camera)
        {
            DrawCurve(app, camera, Color.Black, 0);
        }

        //Draw this curve. from = from which segment to draw
        public void DrawCurve(SDLApp app, Transform camera, Color color, float from)
        {
            //if finished, render the computed trajectory (the ones with segments of equal length, on which vehicles move)
            //otherwise, use the ExactPos method to get points straight from bezier curve equation
            Func<float, Vector2> f = Finished ? x => parent.ParentWorld.LocalToWorldPos(GetPos(x)) : i => ExactPos(i, Constant.bezier_curve_unfinished_resolution - 1);
            int fromi = (int)from;
            //if we are rendering an incomplete segment (e.g. rendering a vehicle path, so we render from the vehicle to its' segment end), Draw just that one line
            if (fromi != from)
            {
                app.DrawLine(color, f(from), f(fromi+1), camera);
                fromi++;
            }
            //draw the rest of the segments
            app.DrawLines(color, f, Finished ? PointCount : Constant.bezier_curve_unfinished_resolution, fromi, camera);
        }

        //compute intersection of two curves
        public void Intersect(BezierCurve b, out float i1, out float i2)
        {
            i1 = -1;
            i2 = -1;
            //just iterate over all segments of both curves
            for (int i = 0; i < SegmentCount; i++)
            {
                for (int j = 0; j < b.SegmentCount; j++)
                {
                    //if the two segments intersect - line intersect returns values between based on where on a line the intersection is - for line segments, must be between 0 and 1
                    LineIntersect(points[i], points[i + 1], b.points[j], b.points[j + 1], out float ii, out float ij);
                    if (0 <= ii && ii <= 1 && 0 <= ij && ij <= 1)
                    {
                        //to get the segment index of the intersection, add the segment index to the i value from the function above
                        i1 = i + ii;
                        i2 = j + ij;
                        return;
                    }
                }
            }
        }


        //compute exact position from the cubic bezier curve equation (link: https://en.wikipedia.org/wiki/Bezier_curve, cubic curves, explicit form)
        public Vector2 ExactPos(float k, int part_count)
        {
            float t = k / part_count;
            float t_ = 1 - t;
            return t_ * t_ * t_ * P0 + 3 * t_ * t_ * t * P1 + 3 * t_ * t * t * P2 + t * t * t * P3;
        }

        //compute exact derivative from the cubic bezier curve equation (link: [https://en.wikipedia.org/wiki/Bezier_curve], cubic curves, first derivative with respect to t)
        public Vector2 ExactDerivative(float k, int part_count)
        {
            float t = k / part_count;
            float t_ = 1 - t;
            return 3 * t_ * t_ * (P1 - P0) + 6 * t_ * t * (P2 - P1) + 3 * t * t * (P3 - P2);
        }

        //way too complex intersection of two lines. Equation taken from wikipedia, I was too lazy to derive it myself. Link: [https://en.wikipedia.org/wiki/Line–line_intersection], section Formulas > given two points on each line segment
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
        //convert bezier curve segments to world distance
        public static float SegmentsToDist(this float segments) => segments * Constant.bezier_curve_segment_length;

        //convert world distance to bezier curve segments
        public static float DistToSegments(this float dist) => dist / Constant.bezier_curve_segment_length;

        //functions for conversions from degrees to radians and vice versa

        const float rad_k = MathF.PI / 180;
        public static float Radians(this float deg) => deg * rad_k;
        const float deg_k = 180 / MathF.PI;
        public static float Degrees(this float rad) => rad * deg_k;
    }
}
