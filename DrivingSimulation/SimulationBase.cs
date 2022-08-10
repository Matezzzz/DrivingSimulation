using System.Collections.Generic;
using System;
using System.Threading;
using System.Linq;
using Newtonsoft.Json;

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
        public static void DisableSafeSpots(this List<Trajectory> trajectories)
        {
            foreach (var t in trajectories) t.SafePointsEnabled = false;
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


    abstract class VehicleCounterDrivingSimulationObject : SimulationObject
    {
        public CumulativeBuffered<int> VehiclesInside;
        public VehicleCounterDrivingSimulationObject(SimulationObjectCollection w) : base(w)
        {
            VehiclesInside = new(0);
        }

        
        public void VehicleEnters() { Interlocked.Increment(ref VehiclesInside.NextRef); }
        public void VehicleLeaves() { Interlocked.Decrement(ref VehiclesInside.NextRef); }

        protected override void PostUpdateI()
        {
            base.PostUpdateI();
            VehiclesInside.PostUpdate();
        }
    }



    class SafeSpot : VehicleCounterDrivingSimulationObject
    {
        public float from;
        public float to;
        public int Capacity { get; private set; }
        
        public SafeSpot(SimulationObjectCollection w, float from, float to, int capacity) : base(w)
        {
            this.from = from;
            this.to = to;
            Capacity = capacity;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    class TrajectoryPart
    {
        [JsonProperty]
        public SafeSpot safe_spot;
        [JsonProperty]
        public CrysisPoint crysis_point;

        [JsonConstructor]
        private TrajectoryPart() { }

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


    [JsonObject(MemberSerialization.OptIn)]
    class Trajectory : BezierCurve
    {
        [JsonProperty]
        public List<TrajectoryPart> Parts;

        //we assume normal-speed cars - max 1 can leave/enter the trajectory every frame
        public FrameList<Vehicle> VehicleList = new(); //every trajectory is a curve - first one to enter is the first one to leave

        public int VehicleCount => VehicleList.Count;

        public float Capacity => Length / Vehicle.preferred_spacing;
        public float Occupancy => VehicleCount / Capacity;

        [JsonProperty]
        readonly RoadConnectionVectorView from;
        [JsonProperty]
        readonly RoadConnectionVectorView to;


       
        protected override Vector2 P0 => from.From;
        protected override Vector2 P1 => from.To;
        protected override Vector2 P2 => to.To;
        protected override Vector2 P3 => to.From;

        [JsonProperty]
        public int MergePriority;
        [JsonProperty]
        public bool SafePointsEnabled;
        [JsonProperty]
        public int Id;

        
        [JsonProperty]
        public float MaxSpeed = 1;

        [JsonProperty]
        static int global_id_counter = 0;

        [JsonConstructor]
        private Trajectory() {}
        public Trajectory(SimulationObjectCollection world, RoadConnectionVectorView from, RoadConnectionVectorView to) : base(world)
        {
            Parts = new();
            this.from = from;
            this.to = to;
            MergePriority = 0;
            SafePointsEnabled = true;
            Id = global_id_counter++;
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
        protected override void PostUpdateI()
        {
            VehicleList.PostUpdate();
        }
        public void AddCrysisPoint(CrysisPoint p)
        {
            Parts.Add(new TrajectoryPart(p));
        }

        void AddSafeSpot(SimulationObjectCollection world, float from, float to)
        {
            float dist_without_first = (to - from).SegmentsToDist() - Vehicle.min_vehicle_distance;
            if (dist_without_first > 0)
            {
                Parts.Add(new TrajectoryPart(new SafeSpot(world, from, to, 1 + (int)(dist_without_first / Vehicle.preferred_spacing))));
            }
        }
        protected override void FinishI(FinishPhase phase)
        {
            base.FinishI(phase);
            if (phase == FinishPhase.CREATE_TRAJECTORY_SEGMENTS)
            {
                var ordered_cryses = Parts.OrderBy(pt => pt.crysis_point.GetBranchInfo(this).from).ToList();
                Parts = new();
                float start = 0;
                int crysis_i = 0;
                bool first = true;
                foreach (var part in ordered_cryses)
                {
                    var info = part.crysis_point.GetBranchInfo(this);
                    info.on_trajectory_index = crysis_i++;
                    if (first || SafePointsEnabled) AddSafeSpot(parent.GetParentWorld(), start, info.from);
                    Parts.Add(part);
                    start = info.to;
                    first = false;
                }
                AddSafeSpot(parent.GetParentWorld(), start, SegmentCount);
            }
        }
        protected override void UnfinishI()
        {
            base.UnfinishI();
            Parts.RemoveAll(x => x.safe_spot != null || !x.crysis_point.IsCross);
        }
        public void DrawDirectionArrows(SDLApp app, Transform camera, bool end)
        {
            if (!Finished) return;
            for (float i = 0; i < MaxSpeed; i++)
            {
                float seg = end ? SegmentCount - i : i;
                app.DrawArrowTop(Color.DarkGray, GetPos(seg), GetDerivative(seg).Normalized(), camera);
            }
        }
        public override string ToString()
        {
            return $"{base.ToString()}, Id:{Id}";
        }
        protected override void DestroyI()
        {
            base.DestroyI();
            var e1 = from.RemoveForwardEdge(this);
            var e2 = to.RemoveBackwardEdge(this);
            if (e1 != e2) throw new Exception("Destroying, but removed edges do not match");
            foreach (TrajectoryPart p in Parts)
            {
                if (p.crysis_point != null)
                {
                    p.crysis_point.OnDeleteTrajectory(this);
                }
            }
            e1.Destroy();
        }
    }




    [JsonObject(MemberSerialization.OptIn)]
    class CrysisPointBranchInfo
    {
        [JsonProperty]
        public Trajectory trajectory;
        [JsonProperty]
        public int priority;

        public float from = 0;
        public float to = 0;

        public int on_trajectory_index;

        public readonly ResetBuffered<float> entry_time = new(float.PositiveInfinity);
        public readonly ResetBuffered<float> exit_time = new(0);

        public float MinTimeToTravelThrough { get => (to - from).SegmentsToDist() / trajectory.MaxSpeed; }

        [JsonConstructor]
        private CrysisPointBranchInfo() {
        }
        public CrysisPointBranchInfo(Trajectory t_, int prio)
        {
            trajectory = t_;
            priority = prio;
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
        public void Draw(SDLApp app, Transform camera)
        {
            float marker_step = marker_step_dist.DistToSegments();
            float normal_k = (on_trajectory_index % 2 == 0) ? -1 : 1;
            for (float x = from; x < to - marker_step; x += 2 * marker_step)
            {
                Vector2 shift = (trajectory.GetDerivative(x).Normalized() * normal_k).Rotate90CW() * alternating_shift;
                app.DrawLine(priority_colors[priority], trajectory.GetPos(x)+shift, trajectory.GetPos(x + marker_step)+shift, camera);
            }
        }
    }







    [JsonObject(MemberSerialization.OptIn)]
    class CrysisPoint : VehicleCounterDrivingSimulationObject
    {
        [JsonProperty]
        public List<CrysisPointBranchInfo> blocks;
        public ResetBuffered<int> trajectory_inside = new(-1);
        bool active_now = false;
        int inactive_time = 0;

        enum Type
        {
            SPLIT, MERGE, CROSS
        }

        public bool IsCross => type == Type.CROSS;


        [JsonProperty]
        readonly Type type;

        const int inactive_threshold = 200;
        public bool Inactive => inactive_time > inactive_threshold;

        public override DrawLayer DrawZ => DrawLayer.CRYSIS_POINTS;

        [JsonConstructor]
        private CrysisPoint() : base(null) {
        }
        private CrysisPoint(SimulationObjectCollection world, List<Trajectory> trajectories, List<int> priorities, Type type) : base(world.GetParentWorld())
        {
            blocks = new();
            for (int i = 0; i < trajectories.Count; i++)
            {
                blocks.Add(new CrysisPointBranchInfo(trajectories[i], priorities[i]));
            }
            foreach (var t in trajectories) t.AddCrysisPoint(this);
            this.type = type;
        }

        public static void CreateCrossCrysisPoint(SimulationObjectCollection world, Trajectory t1, Trajectory t2, int prio1, int prio2)
        {
            _ = new CrysisPoint(world, new List<Trajectory>() { t1, t2 }, prio1 == prio2 ? ListUtils.Range(1, -1, -1) : new List<int>() { prio1, prio2 }, Type.CROSS);
        }
        public static void CreateMergeCrysisPoint(SimulationObjectCollection world, List<Trajectory> trajectories)
        {
            _ = new CrysisPoint(world, trajectories, trajectories.ConvertAll(x => x.MergePriority), Type.MERGE);
        }
        public static void CreateSplitCrysisPoint(SimulationObjectCollection world, List<Trajectory> trajectories)
        {
            _ = new CrysisPoint(world, trajectories, ListUtils.Constant(trajectories.Count, 0), Type.SPLIT);
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
                Vector2 dif = b2.ExactPos(t2, b2.SegmentCount) - check_pos;
                if (dif.Length() < Vehicle.min_vehicle_distance) return true;
                float step = (2 * dif * b2.ExactDerivative(t2, b2.SegmentCount)).Sum();
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

        protected override void FinishI(FinishPhase phase)
        {
            base.FinishI(phase);
            if (phase == FinishPhase.COMPUTE_CRYSIS_POINTS)
            {
                if (type == Type.CROSS)
                {
                    var t1 = blocks[0].trajectory;
                    var t2 = blocks[1].trajectory;
                    t1.Intersect(t2, out float i1, out float i2);
                    if (i1 < 0 || i2 < 0)
                    {
                        Console.WriteLine("Crysis point is being created but has no intersection");
                        return;
                    }
                    blocks[0].from = FindCrysisPointEnd(t1, i1, t2, i2, -1);
                    blocks[0].to = FindCrysisPointEnd(t1, i1, t2, i2, 1);
                    blocks[1].from = FindCrysisPointEnd(t2, i2, t1, i1, -1);
                    blocks[1].to = FindCrysisPointEnd(t2, i2, t1, i1, 1);
                }
                else
                {
                    bool merge = type == Type.MERGE;
                    for (int t1 = 0; t1 < blocks.Count; t1++)
                    {
                        var trajectory_1 = blocks[t1].trajectory;
                        if (merge)
                        {
                            blocks[t1].to = trajectory_1.SegmentCount;
                            blocks[t1].from = trajectory_1.SegmentCount;
                        }
                        else
                        {
                            blocks[t1].from = 0;
                            blocks[t1].to = 0;
                        }
                        for (int t2 = 0; t2 < blocks.Count; t2++)
                        {
                            if (t1 == t2) continue;
                            var trajectory_2 = blocks[t2].trajectory;
                            if (merge)
                            {
                                blocks[t1].from = Math.Min(blocks[t1].from, FindCrysisPointEnd(trajectory_1, trajectory_1.SegmentCount, trajectory_2, trajectory_2.SegmentCount, -1));
                            }
                            else //split
                            {
                                blocks[t1].to = Math.Max(blocks[t1].to, FindCrysisPointEnd(trajectory_1, 0, trajectory_2, 0, 1));
                            }
                        }
                    }
                }
            }
        }
        protected override void UnfinishI()
        {
            base.UnfinishI();
            if (!IsCross) Destroy();
        }



        protected override void PostUpdateI()
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
            base.PostUpdateI();
        }


        protected override void DrawI(SDLApp app, Transform camera)
        {
            if (!Finished) return;
            foreach (var b in blocks)
            {
                b.Draw(app, camera);
                if (Inactive) app.DrawCircle(Color.Black, b.trajectory.GetPos((b.to + b.from) / 2), .05f, camera);
            }
        }

        public float WaitTimeUntilFree(float time_from, float time_to, Trajectory t)
        {
            CrysisPointBranchInfo info = GetBranchInfo(t);
            if (Inactive && inactive_time % 3 == info.priority) return 0; //no vehicle passed through this crysis point for a long time - to avoid deadlocks, just go without letting anyone else go first
            //% 3 is a hack to allow only one branch to enter each frame - only one priority may enter this step
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
        public void OnDeleteTrajectory(Trajectory t)
        {
            if (blocks.Count == 2) Destroy();
            else
            {
                blocks.RemoveAll(b => b.trajectory == t);
            }
        }
    }
}
