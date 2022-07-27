using System.Collections.Generic;
using System;
using System.Threading;
using System.Linq;

namespace DrivingSimulation
{
    static class DrivingSimulationExtensions
    {
        public static T Last<T>(this List<T> l)
        {
            return l[^1];
        }
        public static Vector2 Sum<T>(this List<T> data, Func<T, Vector2> conv)
        {
            Vector2 result = new();
            foreach (T item in data) result += conv(item);
            return result;
        }
        public static Vector2 Average<T>(this List<T> data, Func<T, Vector2> conv)
        {
            return data.Sum(conv) / data.Count;
        }
        public static List<T> Merge<T>(this List<T> a, List<T> b)
        {
            a.AddRange(b);
            return a;
        }
        public static List<Tdst> ConvertTo<Tsrc, Tdst>(List<Tsrc> l, Func<Tsrc, Tdst> convert)
        {
            List<Tdst> newl = new();
            foreach (Tsrc a in l) newl.Add(convert(a));
            return newl;
        }
        public static void SetMaxSpeed(this List<Trajectory> trajectories, float max_speed)
        {
            foreach (var t in trajectories) t.MaxSpeed = max_speed;
        }
        public static void SetMergePriority(this List<Trajectory> trajectories, int prio)
        {
            foreach (var t in trajectories) t.MergePriority = prio;
        }
    }


    class ListUtils
    {
        public static List<T> Constant<T>(int count, T val = default)
        {
            List<T> list = new ();
            for (int i = 0; i < count; i++) list.Add(val);
            return list;
        }
        public static List<int> Range(int from, int to, int step)
        {
            List<int> list = new();
            for (int i = from; step > 0 ? i < to : i > to; i += step) list.Add(i);
            return list;
        }
    }


    class FrameList<T> : LinkedList<T>
    {
        int remove;
        int add;
        T add_val;
        T add_val_2;

        public void FrameAdd(T t)
        {
            int i = Interlocked.Add(ref add, 1);
            if (i == 1)
            {
                add_val = t;
            }else if (i == 2)
            {
                add_val_2 = t;
                Console.WriteLine("Error!");
                if (add_val_2 is Vehicle vehicle) vehicle.selected = true;
                if (add_val is Vehicle v2) v2.selected = true;
            }
            else
            {
                Console.WriteLine("Error^2!");
            }
           
        }
        public void FrameRemoveLast()
        {
            int i = Interlocked.Add(ref remove, 1);
            if (i > 1) {
                Console.WriteLine($"Remove error:{i}");
            }
        }
        
        public void PostUpdate()
        {
            if (add >= 1) AddFirst(add_val);
            if (add >= 2) AddFirst(add_val_2);
            if (remove >= 1) RemoveLast();
            if (remove >= 2) RemoveLast();
            add = 0;
            remove = 0;
        }
        
    }


    class SafeSpot
    {
        public float from;
        public float to;
        public SafeSpot(float from, float to)
        {
            this.from = from;
            this.to = to;
        }
    }


    class TrajectoryPart
    {
        public SafeSpot safe_spot;
        public CrysisPoint crysis_point;

        public TrajectoryPart(SafeSpot safe_spot, CrysisPoint crysis_point)
        {
            this.safe_spot = safe_spot;
            this.crysis_point = crysis_point;
        }
        public TrajectoryPart(SafeSpot spot) : this(spot, null)
        { }
        public TrajectoryPart(CrysisPoint pt) : this(null, pt)
        { }
    }


    class Trajectory : BezierCurve
    {
        public List<TrajectoryPart> Parts;

        //we assume normal-speed cars - max 1 can leave/enter the trajectory every frame
        public FrameList<Vehicle> VehicleList; //every trajectory is a curve - first one to enter is the first one to leave

        public int VehicleCount => VehicleList.Count;

        public float Capacity => Length / Vehicle.min_vehicle_distance;
        public float Occupancy => VehicleCount / Capacity;

        public int MergePriority;

        public int Id;

        static int global_id_counter = 0;

        public float MaxSpeed = 1;

        public Trajectory(RoadWorld world, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Transform transform, int merge_priority = 0) : base(world, p0, p1, p2, p3, transform)
        {
            Parts = new();
            VehicleList = new();
            MergePriority = merge_priority;
            Id = global_id_counter++;
        }

        public static Trajectory FromDir(RoadWorld world, Vector2 p0, Vector2 dir0, Vector2 p1, Vector2 dir1, Transform transform, int merge_priority = 0)
        {
            return new Trajectory(world, p0, p0 + dir0, p1 + dir1, p1, transform, merge_priority);
        }

        public void AddVehicle(Vehicle vehicle)
        {
            VehicleList.FrameAdd(vehicle);
        }
        public void RemoveLastVehicle()
        {
            VehicleList.FrameRemoveLast();
        }
        public void RemoveVehicle(Vehicle vehicle)
        {
            VehicleList.Remove(vehicle);
        }
        public override void PostUpdate()
        {
            VehicleList.PostUpdate();
        }
        public void AddCrysisPoint(CrysisPoint p)
        {
            Parts.Add(new TrajectoryPart(p));
        }

        void AddSafeSpot(float from, float to)
        {
            if ((to - from).SegmentsToDist() > 2 * Vehicle.vehicle_radius)
            {
                Parts.Add(new TrajectoryPart(new SafeSpot(from, to)));
            }
        }
        int finish_count = 0;
        public override void Finish()
        {
            if (Interlocked.Increment(ref finish_count) > 1) Console.WriteLine("Finishing the same trajectory twice, broken");
            var ordered_cryses = Parts.OrderBy(pt => pt.crysis_point.GetBranchInfo(this).from).ToList();
            Parts = new();
            float start = 0;
            int crysis_i = 0;
            foreach (var part in ordered_cryses)
            {
                var info = part.crysis_point.GetBranchInfo(this);
                info.on_trajectory_index = crysis_i++;
                AddSafeSpot(start, info.from);
                Parts.Add(part);
                start = info.to;
            }
            AddSafeSpot(start, SegmentCount);
        }
        public void DrawDirectionArrows(SDLApp app, Transform transform, bool end)
        {
            for (float i = 0; i < MaxSpeed; i++)
            {
                float seg = end ? SegmentCount - i : i;
                app.DrawArrowTop(Color.DarkGray, transform.Apply(GetPos(seg)), transform.ApplyDirection(GetDerivative(seg).Normalized()));
            }
        }
    }

    class Buffered<T>
    {
        T value;
        T next_value;
        readonly T default_value;

        public Buffered(T default_val) : this(default_val, default_val, default_val)
        { }
        private Buffered(T val, T next_val, T default_val)
        {
            value = val;
            next_value = next_val;
            default_value = default_val;
        }
        public void PostUpdate()
        {
            value = next_value;
            next_value = default_value;
        }
        public T Value { get => value; }
        public T NextValue { get => next_value; }
        public ref T Ref { get => ref value; }
        public ref T NextRef { get => ref next_value; }

        public static implicit operator T(Buffered<T> b)
        {
            return b.value;
        }

        public void Set(T val)
        {
            next_value = val;
        }
        public override string ToString()
        {
            return $"{value}";
        }
    }



    class CrysisPointBranchInfo
    {
        public Trajectory trajectory;
        public float from;
        public float to;
        public int priority;

        public int on_trajectory_index;

        public readonly Buffered<float> entry_time;
        public readonly Buffered<float> exit_time;

        public float MinTimeToTravelThrough { get => (to - from).SegmentsToDist() / trajectory.MaxSpeed; }


        public CrysisPointBranchInfo(Trajectory t_, int prio)
        {
            trajectory = t_;
            from = trajectory.SegmentCount;
            to = 0;
            priority = prio;
            entry_time = new(float.PositiveInfinity);
            exit_time = new(0);
        }
        public void SetEntryTime(float entry_time_)
        {
            InterlockedMin(ref entry_time.NextRef, entry_time_);
        }
        public void SetExitTime(float exit_time_)
        {
            InterlockedMax(ref exit_time.NextRef, exit_time_);
        }
        public void PostUpdate()
        {
            entry_time.PostUpdate();
            exit_time.PostUpdate();
        }
        static void InterlockedMin(ref float location, float new_value)
        {
            InterlockedExchange(ref location, new_value, (new_x, old_x) => new_x >= old_x);
        }
        static void InterlockedMax(ref float location, float new_value)
        {
            InterlockedExchange(ref location, new_value, (new_x, old_x) => new_x <= old_x);
        }
        static void InterlockedExchange(ref float location, float new_value, Func<float, float, bool> when_to_abort)
        {
            float initialValue;
            do
            {
                //current value in location
                initialValue = location;
                if (when_to_abort(new_value, initialValue)) return;
            }
            //if location hasn't changed, overwrite it with new_value (which is smaller). Else try the whole thing again
            while (Interlocked.CompareExchange(ref location, new_value, initialValue) != initialValue);
        }



        const float marker_step_dist = 0.05f;
        const float alternating_shift = .025f;
        static readonly Color[] priority_colors = new Color[] { Color.Green, Color.Yellow, Color.Red, Color.Black};
        public void Draw(SDLApp app, Transform transform)
        {
            float marker_step = marker_step_dist.DistToSegments();
            float normal_k = (on_trajectory_index % 2 == 0) ? -1 : 1;
            for (float x = from; x < to - marker_step; x += 2 * marker_step)
            {
                Vector2 shift = transform.ApplyDirection(trajectory.GetDerivative(x) * normal_k).Rotate90CW() * alternating_shift;
                app.DrawLine(priority_colors[priority], transform.Apply(trajectory.GetPos(x))+shift, transform.Apply(trajectory.GetPos(x + marker_step))+shift);
            }
        }
    }





    class CrysisPoint : DrivingSimulationObject
    {
        public CrysisPointBranchInfo[] blocks;
        public Buffered<int> trajectory_inside;
        bool active_now = false;
        int inactive_time = 0;

        const int inactive_threshold = 200;
        public bool Inactive => inactive_time > inactive_threshold;

        public override int DrawLayer => 2;
        protected override bool PreDraw => true;
        private CrysisPoint(RoadWorld world, CrysisPointBranchInfo[] blocks) : base(world)
        {
            trajectory_inside = new(-1);
            this.blocks = blocks;
        }

        public static void TryCreatingCrysisPoint(RoadWorld world, Trajectory t1, Trajectory t2, int prio1, int prio2)
        {
            t1.Intersect(t2, out float i1, out float i2);
            if (i1 < 0 || i2 < 0) return;
            CrysisPoint p = CreateEmptyCrysisPoint(world, 0, 0, new List<Trajectory>() { t1, t2 }, prio1 == prio2 ? ListUtils.Range(1, -1, -1) : new List<int>() { prio1, prio2 });
            p.blocks[0].from = FindCrysisPointEnd(t1, i1, t2, i2, -1);
            p.blocks[0].to   = FindCrysisPointEnd(t1, i1, t2, i2, 1);
            p.blocks[1].from = FindCrysisPointEnd(t2, i2, t1, i1, -1);
            p.blocks[1].to   = FindCrysisPointEnd(t2, i2, t1, i1, 1);
        }
        public static void CreateMergeCrysisPoint(RoadWorld world, List<Trajectory> trajectories)
        {
            CrysisPoint p = CreateEmptyCrysisPoint(world, float.PositiveInfinity, float.NegativeInfinity, trajectories, trajectories.ConvertAll(x => x.MergePriority));
            for (int t1 = 0; t1 < trajectories.Count; t1++)
            {
                p.blocks[t1].to = p.blocks[t1].trajectory.SegmentCount;
                for (int t2 = 0; t2 < trajectories.Count; t2++)
                {
                    if (t1 == t2) continue;
                    p.blocks[t1].from = Math.Min(p.blocks[t1].from, FindCrysisPointEnd(trajectories[t1], trajectories[t1].SegmentCount, trajectories[t2], trajectories[t2].SegmentCount, -1));
                }
            }
        }
        public static void CreateSplitCrysisPoint(RoadWorld world, List<Trajectory> trajectories)
        {
            CrysisPoint p = CreateEmptyCrysisPoint(world, 0, 0, trajectories, ListUtils.Constant<int>(trajectories.Count, 0));
            for (int t1 = 0; t1 < trajectories.Count; t1++)
            {
                for (int t2 = 0; t2 < trajectories.Count; t2++)
                {
                    if (t1 == t2) continue;
                    p.blocks[t1].to = Math.Max(p.blocks[t1].to, FindCrysisPointEnd(trajectories[t1], 0, trajectories[t2], 0, 1));
                }
            }
        }
        private static CrysisPoint CreateEmptyCrysisPoint(RoadWorld world, float from, float to, List<Trajectory> trajectories, List<int> priorities)
        {
            CrysisPointBranchInfo[] blocks = new CrysisPointBranchInfo[trajectories.Count];
            for (int i = 0; i < trajectories.Count; i++)
            {
                blocks[i] = new CrysisPointBranchInfo(trajectories[i], priorities[i])
                {
                    from = from,
                    to = to
                };
            }
            var cp = new CrysisPoint(world, blocks);
            foreach (var t in trajectories) t.AddCrysisPoint(cp);
            return cp;
        }


        static float FindCrysisPointEnd(BezierCurve get_end_on, float intersection_1, BezierCurve other, float intersection_2, float step_k)
        {
            float crysis_end = intersection_1;
            float step = Vehicle.min_vehicle_distance.DistToSegments() * step_k;
            bool first = true;
            while (Math.Abs(step) > 0.01)
            {
                float pos = crysis_end + step;
                pos = Math.Clamp(pos, 0, get_end_on.SegmentCount); 
                if (IsCrysis(get_end_on, pos, other, intersection_2))
                {
                    crysis_end = pos;
                    if (pos == get_end_on.SegmentCount || pos == 0) return crysis_end;
                    if (!first) step /= 2;
                }
                else
                {
                    step /= 2;
                    first = false;
                }
            }
            return crysis_end;
        }

        public void MovingInside()
        {
            active_now = true;
        }



        const float search_speed = 0.1f;
        static bool IsCrysis(BezierCurve b1, float t, BezierCurve b2, float b2_intersection_pos)
        {
            Vector2 check_pos = b1.GetPos(t);
            float t2 = b2_intersection_pos;
            while (true)
            {
                Vector2 dif = b2.ExactPosSeg(t2) - check_pos;
                if (dif.Length() < Vehicle.min_vehicle_distance) return true;
                float step = (2 * dif * b2.ExactDerivativeSeg(t2)).Sum();
                t2 -= step * search_speed;
                if (Math.Abs(step) < 0.01) break;
            }
            return false;
        }
        
        public CrysisPointBranchInfo GetBranchInfo(Trajectory t)
        {
            foreach(CrysisPointBranchInfo br in blocks)
            {
                if (br.trajectory == t) return br;
            }
            throw new ApplicationException("Failed to find a trajectory in crysis point");
        }

        public bool FreeForTrajectory(int trajectory_id)
        {
            return trajectory_inside == -1 || trajectory_inside == trajectory_id;
        }
        


        public override void PostUpdate()
        {
            if (active_now)
            {
                inactive_time = 0;
                active_now = false;
            }
            else
            {
                inactive_time++;
            }
            trajectory_inside.PostUpdate();
            
            foreach (CrysisPointBranchInfo br in blocks)
            {
                br.PostUpdate();
            }
        }


        protected override void Draw(SDLApp app, Transform transform)
        {
            foreach (CrysisPointBranchInfo b in blocks) b.Draw(app, transform);
            if (Inactive)
            {
                foreach (var b in blocks)
                {
                    app.DrawCircle(Color.Black, transform.Apply(b.trajectory.GetPos((b.to + b.from) / 2)), transform.ApplySize(.05f));
                }
            }
        }

        public float WaitTimeUntilFree(float time_from, float time_to, Trajectory t)
        {
            if (Inactive) return 0; //no vehicle passed through this crysis point for a long time - to avoid deadlocks, just go without letting anyone else go first
            CrysisPointBranchInfo info = GetBranchInfo(t);
            float wait_time = 0;
            foreach (CrysisPointBranchInfo b in blocks)
            {
                //if the other lane has priority
                if (b.priority > info.priority)
                {
                    //we don't have enough time to get out before the other lane - we have to wait for it to exit
                    if (!(b.exit_time <= time_from && time_to <= b.entry_time))
                    {
                        wait_time = Math.Max(wait_time, b.entry_time - time_from + b.MinTimeToTravelThrough);
                    }
                }
            }
            //immediately allocate the time
            info.SetEntryTime(wait_time + time_from);
            return wait_time;
        }
    }






    class Vehicle : DrivingSimulationObject
    {

        readonly Queue<Trajectory> m_planned_path;

        Trajectory CurTrajectory { get => m_planned_path.Peek(); }

        Vector2 Position => CurTrajectory.GetPos(position);

        public readonly Buffered<float> position;
        readonly Buffered<float> speed;

        const float max_acceleration = 1;
        const float braking_force = 2;
        public const float vehicle_radius = 0.2f;
        public const float min_vehicle_distance = 2 * vehicle_radius;
        const float preferred_spacing = 0.6f;

        public override bool Disabled => disabled;

        public bool selected;

        bool disabled = false;

        Color color;

        public int Id;
        static int global_id_counter = 0;

        public override int DrawLayer => 3;
        protected override bool PreDraw => false;

        public Vehicle(RoadWorld world, Queue<Trajectory> path, Color color) : base(world)
        {
            position = new(0);
            speed = new (0.5f);
            m_planned_path = path;
            CurTrajectory.AddVehicle(this);
            this.color = color;
            Id = global_id_counter++;
        }

        static void SolveQuadratic(float a, float b, float c, out float x1, out float x2)
        {
            float D_ = b * b - 4 * a * c;
            if (D_ < 0) throw new ArithmeticException("Solving equation failed: negative discriminant");
            D_ = MathF.Sqrt(D_);
            x1 = (-b - D_) / 2 / a;
            x2 = (-b + D_) / 2 / a;
        }

        float MinTimeToTravel(float distance)
        {
            if (distance < 0) return 0;
            SolveQuadratic(max_acceleration / 2, speed, -distance, out float _, out float time);
            float overspeed_time = time - (CurTrajectory.MaxSpeed - speed) / max_acceleration;
            if (overspeed_time < 0) return time;
            return time + overspeed_time * overspeed_time * max_acceleration / 2 / CurTrajectory.MaxSpeed;
        }



        public bool Overlaps(Vector2 pos)
        {
            return (Position - pos).Length() < vehicle_radius;
        }


        public void Disable()
        {
            disabled = true;
            CurTrajectory.RemoveVehicle(this);
        }
        public void Select()
        {
            selected = true;
        }



        public static float DistToBreak(float cur_speed, float target_speed)
        {
            float t = (cur_speed - target_speed) / braking_force;
            return cur_speed * t - braking_force * t * t / 2;
        }



        public override void Update(float dt)
        {
            if (disabled) return;
            float stop_distance = float.PositiveInfinity;
            float cur_acceleration = max_acceleration;


            //do not go faster than the vehicle ahead
            var vehicle_ahead = Search.VehicleAhead(this, m_planned_path);
            if (vehicle_ahead.Exists())
            {
                float brake_dist = DistToBreak(1.2f*speed, vehicle_ahead.vehicle.speed);
                float reserve_dist = vehicle_ahead.distance - preferred_spacing;
                if (reserve_dist < brake_dist) cur_acceleration = -braking_force;
                if (vehicle_ahead.vehicle.speed == 0) stop_distance = reserve_dist;
            }

            bool inside_safe_point = false;
            float safe_spot_remaining_distance = 0;

            bool first_safe_spot = true;
            float max_wait_time_until_safe = 0;
            foreach (var ev in Search.Path(position, m_planned_path))
            {
                if (ev.IsCrysisPoint())
                {
                    float time_from = MinTimeToTravel(ev.from);
                    float time_to = MinTimeToTravel(ev.to);
                    float wait_time = 0;
                    //all further events will be too far in the future for current observations to mean anything. We will figure it out when we get there
                    if (time_to > 5) break;
                    if (ev.from < 0)
                    {
                        ev.crysis_point.trajectory_inside.Set(CurTrajectory.Id);
                        ev.crysis_point.GetBranchInfo(CurTrajectory).SetExitTime(time_to);
                        ev.crysis_point.MovingInside();
                    }
                    else
                    {
                        wait_time = ev.crysis_point.WaitTimeUntilFree(time_from, time_to, ev.trajectory);
                        max_wait_time_until_safe = Math.Max(wait_time, max_wait_time_until_safe);
                        if (wait_time != 0 || !ev.crysis_point.FreeForTrajectory(ev.trajectory.Id)) stop_distance = Math.Min(stop_distance, ev.from);
                    }
                }
                else
                {
                    if (first_safe_spot)
                    {
                        if (ev.from < 0) inside_safe_point = true;
                        safe_spot_remaining_distance = ev.to;
                    }
                    first_safe_spot = false;
                }
            }
            if (inside_safe_point && max_wait_time_until_safe != 0)
            {
                stop_distance = Math.Min(stop_distance, safe_spot_remaining_distance);
            }

            if (stop_distance < DistToBreak(speed, 0))
            {
                float required_brake_to_stop = speed * speed / stop_distance;
                cur_acceleration = Math.Min(cur_acceleration, -required_brake_to_stop);
            }
            

            speed.Set(Math.Clamp(speed + dt * cur_acceleration, 0, CurTrajectory.MaxSpeed));
            //problem - same frame entry + failing to leave in correct order :(.... 
            float dpos = (dt * speed).DistToSegments();
            if (dpos > stop_distance) dpos = stop_distance - 1e-4f;


            position.Set(Math.Max(0, position + dpos));
            int segments;
            while (position.NextValue > (segments = CurTrajectory.SegmentCount))
            {
                position.NextRef -= segments;
                m_planned_path.Dequeue().RemoveLastVehicle();
                if (m_planned_path.Count == 0)
                {
                    disabled = true;
                    break;
                }
                CurTrajectory.AddVehicle(this);
            }
        }
        public override void PostUpdate()
        {
            position.PostUpdate();
            speed.PostUpdate();
        }
        protected override void Draw(SDLApp app, Transform transform)
        {
            if (!disabled)
            {
                app.DrawTriangle(color, transform.Apply(CurTrajectory.GetPos(position)), transform.ApplySize(vehicle_radius), transform.ApplyDirection(CurTrajectory.GetDerivative(position)).Rotation()+90);
                if (selected)
                {
                    float from = position;
                    foreach (Trajectory t in m_planned_path)
                    {
                        t.DrawCurve(app, transform, Color.Magenta, from);
                        from = 0;
                    }
                }
            }
        }

        public override string ToString()
        {
            return $"Vehicle(Position:{CurTrajectory.GetPos(position)}, Color:{color})";
        }
    }
}
