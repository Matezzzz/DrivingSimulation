using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DrivingSimulation
{
    [JsonObject(MemberSerialization.OptOut)]
    abstract class Crossroads : SimulationObjectListCollection
    {
        [JsonProperty]
        readonly TrajectoryList trajectories;

        public TrajectoryList Trajectories => trajectories;

        [JsonProperty]
        protected List<RoadPlugView> Plugs;

        [JsonConstructor]
        protected Crossroads() { }
        public Crossroads(SimulationObjectCollection world) : base(world)
        {
            trajectories = new(world);
        }

        protected abstract void CreateCrossroadCrysisPoints(List<List<List<Trajectory>>> trajectories, List<int> priorities);
        protected void CreateCrossroads(List<RoadPlugView> plugs, List<int> priorities)
        {
            Plugs = new();
            foreach (var p in plugs) Plugs.Add(p);
            List<RoadPlugView> connections = new();
            foreach (var plug in plugs) connections.Add(plug.Invert());
            var created_trajectories = trajectories.ConnectAll(connections);
            CreateCrossroadCrysisPoints(created_trajectories, priorities);
            base.UpdateObjects();
            trajectories.UpdateObjects();
        }
    }
    static class CrossroadsExt
    {
        public static T SetMaxSpeed<T>(this T crossroads, float speed) where T : Crossroads
        {
            crossroads.Trajectories.SetMaxSpeed(speed);
            return crossroads;
        }
    }

    class Road : Crossroads
    {
        public RoadPlugView A => Plugs[1];

        [JsonConstructor]
        private Road() { }

        Road(SimulationObjectCollection world, RoadPlugView p1, RoadPlugView p2) : base(world)
        {
            CreateCrossroads(new List<RoadPlugView>() { p1, p2 }, null);
        }
        public Road(SimulationObjectCollection world, RoadPlugView p1, float length, float lane_dist) : this(world, p1.Invert(), p1.MovedCopy(length))
        {
            if (lane_dist != 0) A.plug.Placed.SetRoadWidth(lane_dist);
        }
        protected override void CreateCrossroadCrysisPoints(List<List<List<Trajectory>>> trajectories, List<int> priorities) { }
    }




    [JsonObject(MemberSerialization.OptIn)]
    class CrossroadsT : Crossroads
    {
        public RoadPlugView L { get => Plugs[0]; }
        public RoadPlugView B { get => Plugs[1]; }
        public RoadPlugView R { get => Plugs[2]; }


        [JsonConstructor]
        private CrossroadsT() { }
        public CrossroadsT(SimulationObjectCollection world, List<int> priorities = null) : base(world)
        {
            CreateCrossroads(new List<RoadPlugView>() { this.DefaultLeft2Side().GetView(false), this.DefaultBottom2Side().GetView(false), this.DefaultRight2Side().GetView(false) }, priorities);
        }
        public CrossroadsT(SimulationObjectCollection world, List<Func<SimulationObjectCollection, RoadPlugView>> plug_fs, List<int> priorities = null) : base(world)
        {
            List<RoadPlugView> plugs = new(); foreach (var f in plug_fs) plugs.Add(f(this));
            CreateCrossroads(plugs, priorities);
        }
        protected override void CreateCrossroadCrysisPoints(List<List<List<Trajectory>>> ts, List<int> priorities)
        {
            var prio = (int i) => priorities == null ? 0 : priorities[i];
            for (int me = 0; me < ts.Count; me++)
            {
                int right = (me + 1) % ts.Count, left = (me + 2) % ts.Count;
                int p_me = prio(me), p_right = prio(right), p_left = prio(left);

                if (p_me >= p_left) ts[me][0].DisableSafeSpots();
                if (p_me > p_right) ts[me][1].DisableSafeSpots();

                this.CreateCrysisPoints(ts[right][1], ts[me][1], p_right, p_me);

                int right_merge_prio = p_left > p_me ? 0 : 1;
                int left_merge_prio = p_right >= p_me ? 0 : 1;
                ts[me][0].SetMergePriority(right_merge_prio); ts[me][1].SetMergePriority(left_merge_prio);
            }
        }
    }



    [JsonObject(MemberSerialization.OptIn)]
    class CrossroadsX : Crossroads
    {
        public RoadPlugView T { get => Plugs[0]; }
        public RoadPlugView L { get => Plugs[1]; }
        public RoadPlugView B { get => Plugs[2]; }
        public RoadPlugView R { get => Plugs[3]; }

        [JsonConstructor]
        private CrossroadsX() { }
        public CrossroadsX(SimulationObjectCollection world, List<int> priorities = null) : base(world)
        {
            CreateCrossroads(new List<RoadPlugView>() { this.DefaultTop2Side().GetView(false), this.DefaultLeft2Side().GetView(false), this.DefaultBottom2Side().GetView(false), this.DefaultRight2Side().GetView(false) }, priorities);
        }
        public CrossroadsX(SimulationObjectCollection world, List<Func<SimulationObjectCollection, RoadPlugView>> plug_fs, List<int> priorities = null) : base(world)
        {
            List<RoadPlugView> plugs = new(); foreach (var f in plug_fs) plugs.Add(f(this));
            CreateCrossroads(plugs, priorities);
        }
        protected override void CreateCrossroadCrysisPoints(List<List<List<Trajectory>>> ts, List<int> priorities)
        {
            var prio = (int i) => priorities == null ? 0 : priorities[i];

            for (int me = 0; me < ts.Count; me++)
            {
                int right = (me + 1) % ts.Count, front = (me + 2) % ts.Count, left = (me + 3) % ts.Count;
                int p_me = prio(me), p_right = prio(right), p_front = prio(front), p_left = prio(left);

                if (p_me >= p_left) ts[me][0].DisableSafeSpots();
                if (p_me > p_right && p_me >= p_left) ts[me][1].DisableSafeSpots();
                if (p_me > p_front && p_me > p_right) ts[me][2].DisableSafeSpots();

                this.CreateCrysisPoints(ts[right][1], ts[me][1], p_right, p_me);
                this.CreateCrysisPoints(ts[right][2], ts[me][1], p_right, p_me);
                this.CreateCrysisPoints(ts[right][2], ts[me][2], p_right, p_me);
                this.CreateCrysisPoints(ts[front][1], ts[me][2], p_front, p_me);

                int right_merge_prio = (p_front > p_me ? 0 : 1) + (p_left > p_me ? 0 : 1);
                int front_merge_prio = (p_right >= p_me ? 0 : 1) + (p_left > p_me ? 0 : 1);
                int left_merge_prio = (p_right >= p_me ? 0 : 1) + (p_front >= p_me ? 0 : 1);
                ts[me][0].SetMergePriority(right_merge_prio); ts[me][1].SetMergePriority(front_merge_prio); ts[me][2].SetMergePriority(left_merge_prio);
            }
        }
    }
}
