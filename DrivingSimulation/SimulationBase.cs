using System.Collections.Generic;
using System;
using System.Threading;
using System.Linq;
using Newtonsoft.Json;

namespace DrivingSimulation
{
    static class DrivingSimulationExtensions
    {
        //last element of a list
        public static T Last<T>(this List<T> l)
        {
            return l[^1];
        }
        //convert T to vector2 using the given conversion function, then sum all of them
        public static Vector2 Sum<T>(this List<T> data, Func<T, Vector2> conv)
        {
            Vector2 result = new();
            foreach (T item in data) result += conv(item);
            return result;
        }
        //add b to a, then return a
        public static List<T> Merge<T>(this List<T> a, List<T> b)
        {
            a.AddRange(b);
            return a;
        }
        //set max speed for all trajectories
        public static void SetMaxSpeed(this List<Trajectory> trajectories, float max_speed)
        {
            foreach (var t in trajectories) t.MaxSpeed = max_speed;
        }
        //set merge priority for all trajectories
        public static void SetMergePriority(this List<Trajectory> trajectories, int prio)
        {
            foreach (var t in trajectories) t.MergePriority = prio;
        }
        //disable safe spots for all trajectories
        public static void DisableSafeSpots(this List<Trajectory> trajectories)
        {
            foreach (var t in trajectories) t.SafePointsEnabled = false;
        }
    }


    class ListUtils
    {
        //return a list filled with a constant value
        public static List<T> Constant<T>(int count, T val = default)
        {
            List<T> list = new ();
            for (int i = 0; i < count; i++) list.Add(val);
            return list;
        }
        //return a list with values from from to to with step step :D
        public static List<int> Range(int from, int to, int step)
        {
            List<int> list = new();
            for (int i = from; step > 0 ? i < to : i > to; i += step) list.Add(i);
            return list;
        }
    }


    class TrajectoryVehicleQueue : List<Vehicle>
    {
        int remove_count;
        int add_count;
        //normally, only one vehicle should be added to trajectory each frame
        //however, sometimes, simulation error happens and vehicles overlap for a while
        //so we keep a reserve count here, we can add two per frame at most. Should be enough
        readonly Vehicle[] add_values = new Vehicle[2];

        //approximates how many vehicles pass through this trajectory each second
        public float vehicle_throughput = 0;
        public float average_vehicle_speed = 0;

        //add a vehicle during frame - get index in add values using interlocked.Increment, then save the value to add there
        public void FrameAdd(Vehicle t)
        {
            int i = Interlocked.Increment(ref add_count) - 1;
            add_values[i] = t;
        }
        //remove last element
        public void FrameRemoveLast()
        {
            Interlocked.Increment(ref remove_count);
        }
        
        //we need time step to compute how many vehicles per second are passing through here
        public void PostUpdate()
        {
            //ADD/REMOVE VALUES INTO THE ACTUAL ARRAY

            //normal vehicle position is modified during post update, so we can't use that
            //use NextValue instead, that will for sure contain the next position
            //we can add 0, 1 or 2 vehicles per frame, anything larger will throw an error
            //When adding 2, we try to order them correctly - the one in the front is enqueued first
            //honestly, insert is O(n), and that is sort-of painful, however, I didn't find any .NET collection that supports removing elements in the middle occasionally, adding at the front and removing at the end
            //(and I don't want to write my own)
            switch (add_count)
            {
                case 0:
                    break;
                case 1:
                    Insert(0, add_values[0]);
                    break;
                case 2:
                    if (add_values[0].position.NextValue < add_values[1].position.NextValue)
                    {
                        Insert(0, add_values[1]); Insert(0, add_values[0]);
                    }
                    else
                    {
                        Insert(0, add_values[0]); Insert(0, add_values[1]);
                    }
                    break;
                default:
                    throw new InvalidOperationException("Adding too many vehicles this step. Simulation broke");
            }
            //dequeue doesn't care about ordering, so it is easy to do as many steps as we want. (However, removing more than one vehicle at a time means something is broken)
            for (int i = 0; i < remove_count; i++) RemoveRange(Count - remove_count, remove_count);

            //track the average speed on this trajectory. If no vehicles are present, don't change it - leave the last one we remember
            if (Count != 0) 
            {
                //compute average of all speeds, we use vehicle.nextvalue because current value is changed during the postUpdate step, computation would be unstable due to multithreading
                float cur_avg_speed = 0;
                foreach (var v in this) cur_avg_speed += v.speed.NextValue;
                average_vehicle_speed = cur_avg_speed / Count;
            }
            add_count = 0;
            remove_count = 0;
        }
        
    }


    //a object that counts vehicles inside
    abstract class VehicleCounterDrivingSimulationObject : SimulationObject
    {
        //number of vehicles inside
        public Buffered<int> VehiclesInside;
        public VehicleCounterDrivingSimulationObject(SimulationObjectCollection w) : base(w)
        {
            VehiclesInside = new(0);
        }

        
        public void VehicleEnters() => Interlocked.Increment(ref VehiclesInside.NextValue);
        public void VehicleLeaves() => Interlocked.Decrement(ref VehiclesInside.NextValue);

        //update vehicles inside number
        protected override void PostUpdateI()
        {
            base.PostUpdateI();
            VehiclesInside.PostUpdate();
        }
    }


    //represents a safe spot on a trajectory, where vehicles can stop
    class SafeSpot : VehicleCounterDrivingSimulationObject
    {
        public float from;
        public float to;

        //(to - from).SegmentsToDist() -> safe spot length. First vehicle only takes up to Vehicle.min_vehicle_distance, so subtract that, and later add 1 to the total count
        //every additional vehicle must also have enough space after the previous one -> Vehicle.preferred_spacing
        int CapacityF => (int) (1 + ((to - from).SegmentsToDist() - Vehicle.min_vehicle_distance) / Vehicle.preferred_surround_spacing);
        //precomputed capacity on creation, accessible from other objects to use
        public int Capacity;

        public SafeSpot(SimulationObjectCollection w, float from, float to) : base(w)
        {
            this.from = from;
            this.to = to;
            //set state to finished so object can be destroyed correctly when unfinish is called
            state = ObjectState.FINISHED;
            Capacity = CapacityF;
        }
        protected override void UnfinishI()
        {
            base.UnfinishI();
            Destroy();
        }
    }



    //trajectory part - either a safe spot or a crysis point
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
        //list of all crysis points and safe spots
        [JsonProperty]
        public List<TrajectoryPart> Parts;

        //we assume normal-speed cars - max 1 can leave/enter the trajectory every frame
        public TrajectoryVehicleQueue VehicleList = new(); //every trajectory is a curve - first one to enter is the first one to leave

        //vehicles on this trajectory, how many vehicles it can fit, and how much is occupied, as a percentage
        public int VehicleCount => VehicleList.Count;
        public float Capacity => Length / Vehicle.preferred_surround_spacing;
        public float Occupancy => VehicleCount / Capacity;

        //from which roadconnectionvector to which one this leads
        [JsonProperty]
        readonly RoadConnectionVectorView from;
        [JsonProperty]
        readonly RoadConnectionVectorView to;

        //approximate how many vehicles are passing through here each second - ( AverageVehicleSpeed / Length) = percentage of trajectory travelled per second, * Vehicle count - per each vehicle
        public float AverageThroughput => AverageVehicleSpeed / Length * VehicleCount;
        public float AverageVehicleSpeed => VehicleList.average_vehicle_speed;
       

        //override bezier curve control point get methods
        protected override Vector2 P0 => from.From;
        protected override Vector2 P1 => from.To;
        protected override Vector2 P2 => to.To;
        protected override Vector2 P3 => to.From;

        //determines which lane has priority when merging
        [JsonProperty]
        public int MergePriority;
        //safe points can be disabled on main roads - vehicles can go forward, if space behind the crossroads is free, no other conditions
        [JsonProperty]
        public bool SafePointsEnabled;
        [JsonProperty]
        public int Id;

        //max speed of vehicles here
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
        
        protected override void PostUpdateI() => VehicleList.PostUpdate();
        
        protected override void FinishI(FinishPhase phase)
        {
            base.FinishI(phase);
            if (phase == FinishPhase.CREATE_TRAJECTORY_SEGMENTS)
            {
                //order added crysis points
                var ordered_cryses = Parts.OrderBy(pt => pt.crysis_point.GetBranchInfo(this).from).ToList();
                Parts = new();
                float start = 0;
                int crysis_i = 0;
                bool first = true;
                //create a safe spot between each two crysis points, if there is enough space
                foreach (var part in ordered_cryses)
                {
                    var info = part.crysis_point.GetBranchInfo(this);
                    info.on_trajectory_index = crysis_i++;
                    if (first || SafePointsEnabled) AddSafeSpot(parent.ParentWorld, start, info.from);
                    Parts.Add(part);
                    start = info.to;
                    first = false;
                }
                //safe spot from last crysis point to end
                AddSafeSpot(parent.ParentWorld, start, SegmentCount);
            }
        }
        //filter all safe spots - they do not exist in edit mode
        protected override void UnfinishI()
        {
            base.UnfinishI();
            Parts.RemoveAll(x => x.safe_spot != null || !x.crysis_point.IsCross);
        }
        //Draw direction arrows - marking how fast vehicles can go on this trajectory
        public void DrawDirectionArrows(SDLApp app, Transform camera, bool end)
        {
            if (!Finished) return;
            for (float i = 0; i < MaxSpeed; i++)
            {
                float seg = end ? SegmentCount - i : i;
                app.DrawArrowTop(Color.DarkGray, GetPos(seg), GetDerivative(seg).Normalized(), camera);
            }
        }

        //destroy this trajectory
        protected override void DestroyI()
        {
            base.DestroyI();
            //remove associated edge from graph
            var e1 = from.RemoveForwardEdge(this);
            var e2 = to.RemoveBackwardEdge(this);
            //if somehow, we were removing a different, matching edge from forward and backward nodes, write an error
            if (e1 != e2) throw new InvalidOperationException("Destroying, but removed edges do not match");
            //remove this trajectory from all crysis points
            for (int i = 0; i < Parts.Count; i++)
            {
                if (Parts[i].crysis_point != null)
                {
                    if (Parts[i].crysis_point.OnDeleteTrajectory(this)) i--;
                }
            }
            //destroy the edge
            if (e1 != null) e1.Destroy();
        }

        //remove the crysis point in question
        public void RemoveCrysisPoint(CrysisPoint c)
        {
            Parts.RemoveAll(p => p.crysis_point == c);
        }

        public void VehicleEnters(Vehicle vehicle) => VehicleList.FrameAdd(vehicle);
        public void VehicleLeaves() => VehicleList.FrameRemoveLast();

        //remove a vehicle from this trajectory, e.g. when it is destroyed
        public void RemoveVehicle(Vehicle vehicle) => VehicleList.Remove(vehicle);
        public void AddCrysisPoint(CrysisPoint p) => Parts.Add(new TrajectoryPart(p));

        void AddSafeSpot(SimulationObjectCollection world, float from, float to)
        {
            //if safe spot fits at least one car, add it
            if ((to - from).SegmentsToDist() > Vehicle.min_vehicle_distance) Parts.Add(new TrajectoryPart(new SafeSpot(world, from, to)));
        }
        public override string ToString()
        {
            return $"{base.ToString()}, Id:{Id}";
        }
    }




    //holds info about a part of a crysis point on one of its' trajectories
    [JsonObject(MemberSerialization.OptIn)]
    class CrysisPointBranchInfo
    {
        [JsonProperty]
        public Trajectory trajectory;
        //priority of this trajectory in this crysis point
        [JsonProperty]
        public int priority;

        public float from = 0;
        public float to = 0;

        //index of crysis point on current trajectory, used just for drawing
        public int on_trajectory_index;

        //when will the nearest vehicle arrive, and when will it exit
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
        //entry time is the closest one
        public void SetEntryTime(float entry_time_) => InterlockedMin(ref entry_time.NextValue, entry_time_);
        //exit time is the furthest reported one
        public void SetExitTime(float exit_time_) => InterlockedMax(ref exit_time.NextValue, exit_time_);

        public void PostUpdate()
        {
            entry_time.PostUpdate();
            exit_time.PostUpdate();
        }
        //min over all values reported this update
        static void InterlockedMin(ref float location, float new_value) => InterlockedExchange(ref location, new_value, (new_x, old_x) => new_x >= old_x);
        //max over all values reported this update
        static void InterlockedMax(ref float location, float new_value) => InterlockedExchange(ref location, new_value, (new_x, old_x) => new_x <= old_x);
        
        //try writing new value to location, either until success, or until when_to_abort(new_value, current_value) is true
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



        
        //draw this crysis point - these are the the dashed lines in crossroads
        public void Draw(SDLApp app, Transform camera)
        {
            float marker_step = Constant.crysis_point_marker_step_dist.DistToSegments();
            //whether to draw above the trajectory or below
            float normal_k = (on_trajectory_index % 2 == 0) ? -1 : 1;
            //draw the dashed lines
            for (float x = from; x < to - marker_step; x += 2 * marker_step)
            {
                //shift - move below/above the trajectory
                Vector2 shift = (trajectory.GetDerivative(x).Normalized() * normal_k).Rotate90CW() * Constant.crysis_point_notmal_alternating_shift;
                app.DrawLine(Constant.crysis_point_priority_colors[priority], trajectory.GetPos(x)+shift, trajectory.GetPos(x + marker_step)+shift, camera);
            }
        }
        public void Destroy(CrysisPoint c)
        {
            trajectory.RemoveCrysisPoint(c);
        }
    }






    //represents one crysis point
    [JsonObject(MemberSerialization.OptIn)]
    class CrysisPoint : VehicleCounterDrivingSimulationObject
    {
        //blocks on all participating trajectories
        [JsonProperty]
        public List<CrysisPointBranchInfo> blocks;
        public ResetBuffered<int> trajectory_inside = new(-1);

        //whether point is active this frame (vehicle is inside), and how long has it been inactive
        bool active_now = false;
        int inactive_time = 0;

        enum Type
        {
            SPLIT, MERGE, CROSS
        }

        public bool IsCross => type == Type.CROSS;


        [JsonProperty]
        readonly Type type;

        
        public override DrawLayer DrawZ => DrawLayer.CRYSIS_POINTS;

        [JsonConstructor]
        private CrysisPoint() : base(null) {}

        //create a block for each trajectory, and add this point to all trajectories
        private CrysisPoint(SimulationObjectCollection world, List<Trajectory> trajectories, List<int> priorities, Type type) : base(world.ParentWorld)
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

        //find a crysis point end on the first curve
        static float FindCrysisPointEnd(BezierCurve get_end_on, float intersection_1, BezierCurve other, float intersection_2, bool search_forward)
        {
            //best approximation we have so far - start at intersection
            float crysis_end = intersection_1;
            //search step - start with the remaining length of the curve in the search direction, divide by two each time search fails
            float step = search_forward ? get_end_on.SegmentCount - intersection_1 : -intersection_1;
            //until the error is <0.01
            while (Math.Abs(step) > 0.01)
            {
                //compute new position, clamp it to curve range
                float pos = crysis_end + step;
                pos = Math.Clamp(pos, 0, get_end_on.SegmentCount);
                //if this point is crysis, move crysis end to this position
                if (IsCrysis(get_end_on, pos, other, intersection_2))
                {
                    crysis_end = pos;
                    //if we are at either end of the curve, return this point. We cannot search behind the bounds of a curve
                    if (pos == get_end_on.SegmentCount || pos == 0) return crysis_end;
                }
                //divide step by 2 -> binary search
                step /= 2;
            }
            return crysis_end;
        }

        


        //=what to multiply gradient by before adding
        const float default_search_speed = 0.1f;
        const int default_max_iterations = 100;
        static bool IsCrysis(BezierCurve b1, float t, BezierCurve b2, float b2_intersection_pos)
        {
            //checking whether this position is a crysis point
            Vector2 check_pos = b1.GetPos(t);
            //start in intersection pos on the other curve
            float t2 = b2_intersection_pos;
            //choose a large step so while cycle doesn't end immediately, 
            float step;
            float search_speed = default_search_speed;
            int max_iterations = default_max_iterations;
            for (int i = 0; i < 10; i++)
            {
                //while i didn't run out of iterations, and step is large (we can do steps to improve the result)
                for (int j = 0; j < max_iterations; j++)
                {
                    //compute how far we are from the check point, if we are close enough, this point is a crysis
                    Vector2 dif = b2.ExactPos(t2, b2.SegmentCount) - check_pos;
                    if (dif.Length() < Vehicle.min_vehicle_distance) return true;
                    //otherwise, improve on the previous result - try coming closer using gradient descent, minimizing either distance of the square of distance (don't remember which one)
                    step = (2 * dif * b2.ExactDerivative(t2, b2.SegmentCount)).Sum();
                    t2 -= step * search_speed;
                    //if error is small enough and we still don't believe this is a crysis point, return false
                    if (Math.Abs(step) < 0.01) return false;
                }
                //if we failed to figure out whether a point is crysis, it was probably because we diverged. Try searching again with more steps and smaller search speed.
                search_speed /= 2;
                max_iterations *= 2;
            }
            return false;
        }
        
        //return a branch info for the given trajectory
        public CrysisPointBranchInfo GetBranchInfo(Trajectory t)
        {
            foreach(CrysisPointBranchInfo br in blocks)
            {
                if (br.trajectory == t) return br;
            }
            throw new ApplicationException("Failed to find a trajectory in crysis point");
        }

        //true if vehicles on given trajectory can enter the crysis point freely - either, that trajectory is inside, or the point is free altogether
        public bool FreeForTrajectory(int trajectory_id)
        {
            return trajectory_inside == -1 || trajectory_inside == trajectory_id;
        }

        public void MovingInside() => active_now = true;

        //finish - compute crysis point ends
        protected override void FinishI(FinishPhase phase)
        {
            base.FinishI(phase);
            if (phase == FinishPhase.COMPUTE_CRYSIS_POINTS)
            {
                if (type == Type.CROSS)
                {
                    //find intersection of both trajectories
                    var t1 = blocks[0].trajectory;
                    var t2 = blocks[1].trajectory;
                    t1.Intersect(t2, out float i1, out float i2);
                    if (i1 < 0 || i2 < 0)
                    {
                        Console.WriteLine("Crysis point is being created but has no intersection");
                        return;
                    }
                    //then, compute from and to points for both crossing trajectories
                    blocks[0].from = FindCrysisPointEnd(t1, i1, t2, i2, false);
                    blocks[0].to = FindCrysisPointEnd(t1, i1, t2, i2, true);
                    blocks[1].from = FindCrysisPointEnd(t2, i2, t1, i1, false);
                    blocks[1].to = FindCrysisPointEnd(t2, i2, t1, i1, true);
                }
                else
                {
                    bool merge = type == Type.MERGE;
                    //for every trajectory
                    for (int t1 = 0; t1 < blocks.Count; t1++)
                    {
                        var trajectory_1 = blocks[t1].trajectory;
                        //if merging, the point ends at segment count
                        if (merge)
                        {
                            blocks[t1].to = trajectory_1.SegmentCount;
                            blocks[t1].from = trajectory_1.SegmentCount;
                        }
                        //if split, the point starts at 0
                        else
                        {
                            blocks[t1].from = 0;
                            blocks[t1].to = 0;
                        }
                        //go through all trajectories that cause crysis point on t1
                        for (int t2 = 0; t2 < blocks.Count; t2++)
                        {
                            if (t1 == t2) continue;
                            var trajectory_2 = blocks[t2].trajectory;
                            if (merge) //merge - find crysis point start relative to this trajectory, then set it, if it is more restrictive than the one currently there
                            {
                                blocks[t1].from = Math.Min(blocks[t1].from, FindCrysisPointEnd(trajectory_1, trajectory_1.SegmentCount, trajectory_2, trajectory_2.SegmentCount, false));
                            }
                            else //split - find crysis point end instead
                            {
                                blocks[t1].to = Math.Max(blocks[t1].to, FindCrysisPointEnd(trajectory_1, 0, trajectory_2, 0, true));
                            }
                        }
                    }
                }
            }
        }
        protected override void UnfinishI()
        {
            base.UnfinishI();
            //merge and split points are created during Finish, so they should be destroyed (Cross is created with crossroads, so leave it be)
            if (!IsCross) Destroy();
        }



        protected override void PostUpdateI()
        {
            //if there is a vehicle inside this point, it is active -> reset counter, else increase it
            if (active_now) inactive_time = 0;
            else inactive_time++;
            active_now = false;
            //update all sub-objects
            trajectory_inside.PostUpdate();
            foreach (CrysisPointBranchInfo br in blocks) br.PostUpdate();
            base.PostUpdateI();
        }

        //draw - only draw gray circles if cryis point is inactive
        protected override void DrawI(SDLApp app, Transform camera)
        {
            if (!Finished) return;
            foreach (var b in blocks)
            {
                b.Draw(app, camera);
            }
        }

        //a vehicle wants to pass through this point, and will occupy it for given time period. Must the vehicle wait, or can it go?
        public float WaitTimeUntilFree(float time_from, float time_to, Trajectory t)
        {
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
        //when trajectory is deleted. Return true if crysis point is destroyed too
        public bool OnDeleteTrajectory(Trajectory t)
        {
            //if there are only two trajectories, now there would be one -> destroy the point alltogether
            if (blocks.Count == 2)
            {
                Destroy();
                return true;
            }
            else
            {
                //else, just remove the block with the trajectory given
                blocks.RemoveAll(b => b.trajectory == t);
                return false;
            }
        }
        //remove all blocks -> remove crysis points from trajectories
        protected override void DestroyI()
        {
            base.DestroyI();
            foreach (var b in blocks) b.Destroy(this);
        }
    }
}
