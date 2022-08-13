using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DrivingSimulation
{
    /**
     * This file holds classes that create crossroads (and normal roads as well)
     */



    [JsonObject(MemberSerialization.OptOut)]
    abstract class Crossroads : SimulationObjectListCollection
    {
        //all trajectories this crossroads are composed of
        [JsonProperty]
        readonly TrajectoryList trajectories;

        public TrajectoryList Trajectories => trajectories;

        //all border plugs - the ones more connections should be made to
        [JsonProperty]
        protected List<RoadPlugView> Plugs;

        float average_speed = 0;
        float average_throughput = 0;

        [JsonConstructor]
        protected Crossroads() { }
        public Crossroads(SimulationObjectCollection world) : base(world)
        {
            trajectories = new(world);
        }

        protected abstract void CreateCrossroadCrysisPoints(List<List<List<Trajectory>>> trajectories, List<int> priorities);
        protected void CreateCrossroads(List<RoadPlugView> plugs, List<int> priorities)
        {
            //save all plugs - all point outward from the middle at this point
            Plugs = new();
            foreach (var p in plugs) Plugs.Add(p);

            //invert all plugs, and then connect them, each to each
            //result is a List<List<List<Trajectory>>> - the indexing means [source plug][target plug][trajectory_i]
            var created_trajectories = trajectories.ConnectAll(Plugs.ConvertAll(x => x.Invert()));
            //create crossing crysis points - this method is specifically overriden for every crossroads type
            CreateCrossroadCrysisPoints(created_trajectories, priorities);
        }
        protected override void DrawCollectionI(SDLApp app, Transform camera, DrawLayer layer)
        {
            base.DrawCollectionI(app, camera, layer);
            //draw crossroads performance metrics
            if (Finished && layer == DrawLayer.PERFORMANCE_MARKERS)
            {
                Vector2 world_pos = WorldPosition;

                float speed = trajectories.GetAverageSpeed();
                //if average speed is defined (there are more than 0 vehicles in these crossroads)
                if (speed != -1) Smooth(ref average_speed, speed);
                //smooth average speed and throughput to avoid sudden jumps
                Smooth(ref average_throughput, trajectories.GetThroughput());

                //size, pad = big cube size and padding
                float size = Constant.metric_marker_size;
                float pad = Constant.metric_marker_padding;
                float pad_shift = pad / 2;
                

                //render both metrics, alligned vertically and horizontally. Horizontal align is done relative to max speed in this crossroads
                float max_speed = trajectories.GetMaxSpeed();
                int max_boxes = ((int)(max_speed - 1e-4f)) + 1;
                float max_speed_w = max_speed * size + max_boxes * pad;
                float default_shift = -max_speed_w/2;
                float world_x = world_pos.X + default_shift;
                Vector2 speed_pos = new (world_x, world_pos.Y -size - pad_shift);
                //render max speed rectangle, to encompass red boxes with given padding 
                app.DrawRect(Color.DarkGray, new Rect(speed_pos - new Vector2(pad_shift), new Vector2(max_speed_w, size + pad)), camera);
                //render smoothed average speed and throughput
                DrawMetric(app, camera, Color.Red, speed_pos, average_speed, max_boxes);
                DrawMetric(app, camera, Color.Green, new Vector2(world_x, world_pos.Y + pad_shift), average_throughput, max_boxes);
            }
        }

        //reset metric trackers
        protected override void FinishI(FinishPhase phase)
        {
            average_speed = 0;
            average_throughput = 0;
            base.FinishI(phase);
        }

        //draw one metric row. Start a next row every break_index cubes
        static void DrawMetric(SDLApp app, Transform camera, Color c, Vector2 position, float value, int break_index)
        {
            //if value is smaller than one, draw mini metric cubes, and the first, faster growing large one
            if (value < 1)
            {
                DrawMetricInternal(app, camera, c, position, Math.Min(value * 5, 4), Constant.metric_mini_marker_size, Constant.metric_mini_marker_padding, 2);
                if (value > 0.8) DrawMetricRect(app, camera, c, position, Constant.metric_marker_size, (value - 0.8f) / 0.2f);
            }
            //else draw large cubes
            else
            {
                DrawMetricInternal(app, camera, c, position, value, Constant.metric_marker_size, Constant.metric_marker_padding, break_index);
            }
        }


        static void DrawMetricInternal(SDLApp app, Transform camera, Color c, Vector2 position, float value, float marker_size_f, float marker_pad, int row_w)
        {
            Vector2 move = new Vector2(marker_size_f) + new Vector2(marker_pad);
            //while less than value, draw cubes
            for (int i = 0; i < value; i++)
            {
                //create cubes in rows of length row_w
                Vector2 move_k = new (i % row_w, i / row_w);
                //render one cube. Min -> make last cube a rectangle to show current value
                DrawMetricRect(app, camera, c, position + move * move_k, marker_size_f, Math.Min(value - i, 1));
            }
        }
        //draw one metric rectangle
        static void DrawMetricRect(SDLApp app, Transform camera, Color c, Vector2 position, float marker_size, float width = 1)
        {
            app.DrawRect(c, new Rect(position, new Vector2(marker_size * width, marker_size)), camera);
        }

        //smooth a value over time. By default uses the constant from settings
        static void Smooth(ref float smoothed, float new_val, float k = Constant.crossroads_performance_smoothing_coeff)
        {
            smoothed = smoothed * k + (1 - k) * new_val;
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


    //A simple road. Is initialized with one road plug, length in the direction of its' normal, and target lane distance
    class Road : Crossroads
    {
        //one plug is given to constructor from outside, so we don't need a getter for it. This returns the one that was created
        public RoadPlugView A => Plugs[1];

        [JsonConstructor]
        private Road() { }

        Road(SimulationObjectCollection world, RoadPlugView p1, RoadPlugView p2) : base(world)
        {
            //just connects the two plugs
            CreateCrossroads(new List<RoadPlugView>() { p1, p2 }, null);
        }
        //create a road. If lane_dist is 0, leave it as default
        public Road(SimulationObjectCollection world, RoadPlugView p1, float length, float lane_dist = 0) : this(world, p1.Invert(), p1.MovedCopy(length))
        {
            //if lane dist isn't zero, scale the road in local space accordingly
            if (lane_dist != 0) A.plug.Placed.SetRoadWidth(lane_dist);
        }
        //there are no cross crysis points on a normal road, do nothing
        protected override void CreateCrossroadCrysisPoints(List<List<List<Trajectory>>> trajectories, List<int> priorities) { }

        protected override void DrawCollectionI(SDLApp app, Transform camera, DrawLayer layer)
        {
            //do not render performance metrics
        }
    }




    [JsonObject(MemberSerialization.OptIn)]
    class CrossroadsT : Crossroads
    {
        //left plug
        public RoadPlugView L { get => Plugs[0]; }
        //bottom plug
        public RoadPlugView B { get => Plugs[1]; }
        //right plug
        public RoadPlugView R { get => Plugs[2]; }


        [JsonConstructor]
        private CrossroadsT() { }

        //Create with default settings. Priorities can define main roads.
        public CrossroadsT(SimulationObjectCollection world, List<int> priorities = null) : base(world)
        {
            //Get default 2side roads
            CreateCrossroads(new List<RoadPlugView>() { this.DefaultLeft2Side().GetView(false), this.DefaultBottom2Side().GetView(false), this.DefaultRight2Side().GetView(false) }, priorities);
        }
        //Use a list of lambdas to create custom road plug views, then create a crossroads using them
        public CrossroadsT(SimulationObjectCollection world, List<Func<SimulationObjectCollection, RoadPlugView>> plug_fs, List<int> priorities = null) : base(world)
        {
            CreateCrossroads(plug_fs.ConvertAll(x => x(this)), priorities);
        }
        //Create crysis points
        protected override void CreateCrossroadCrysisPoints(List<List<List<Trajectory>>> ts, List<int> priorities)
        {
            //a function to get a priority of a side
            var prio = (int i) => priorities == null ? 0 : priorities[i];
            //go through all three sides
            for (int me = 0; me < ts.Count; me++)
            {
                //right and left indices into the ts array
                int right = (me + 1) % ts.Count, left = (me + 2) % ts.Count;
                //my priority, one of the right and one on the left
                int p_me = prio(me), p_right = prio(right), p_left = prio(left);

                //disable safe spots for all roads that do not have to wait for anyone (they are a main road throughout the whole crossroads)
                if (p_me >= p_left) ts[me][0].DisableSafeSpots();
                if (p_me > p_right) ts[me][1].DisableSafeSpots();

                //T crossroads has just one cross crysis point per iteration - when both I and the direction to the right turn left
                this.CreateCrysisPoints(ts[right][1], ts[me][1], p_right, p_me);

                //set merge priorities - what trajectory is main when merging at me
                int right_merge_prio = p_left > p_me ? 0 : 1;
                int left_merge_prio = p_right >= p_me ? 0 : 1;
                ts[me][0].SetMergePriority(right_merge_prio); ts[me][1].SetMergePriority(left_merge_prio);
            }
        }
    }



    [JsonObject(MemberSerialization.OptIn)]
    class CrossroadsX : Crossroads
    {
        //top plug
        public RoadPlugView T { get => Plugs[0]; }
        //left plug
        public RoadPlugView L { get => Plugs[1]; }
        //bottom plug
        public RoadPlugView B { get => Plugs[2]; }
        //right plug
        public RoadPlugView R { get => Plugs[3]; }

        [JsonConstructor]
        private CrossroadsX() { }
        //Create with default settings. Priorities can define main roads.
        public CrossroadsX(SimulationObjectCollection world, List<int> priorities = null) : base(world)
        {
            CreateCrossroads(new List<RoadPlugView>() { this.DefaultTop2Side().GetView(false), this.DefaultLeft2Side().GetView(false), this.DefaultBottom2Side().GetView(false), this.DefaultRight2Side().GetView(false) }, priorities);
        }
        //Use a list of lambdas to create custom road plug views, then create a crossroads using them
        public CrossroadsX(SimulationObjectCollection world, List<Func<SimulationObjectCollection, RoadPlugView>> plug_fs, List<int> priorities = null) : base(world)
        {
            CreateCrossroads(plug_fs.ConvertAll(x => x(this)), priorities);
        }
        protected override void CreateCrossroadCrysisPoints(List<List<List<Trajectory>>> ts, List<int> priorities)
        {
            //a function to get a priority of a side
            var prio = (int i) => priorities == null ? 0 : priorities[i];
            //go through all four sides
            for (int me = 0; me < ts.Count; me++)
            {
                //indices of roads to the right, front and left
                int right = (me + 1) % ts.Count, front = (me + 2) % ts.Count, left = (me + 3) % ts.Count;
                //priorities of me, road to the right, front and left
                int p_me = prio(me), p_right = prio(right), p_front = prio(front), p_left = prio(left);

                //disable safe spots for all roads that do not have to wait for anyone (they are a main road throughout the whole crossroads)
                if (p_me >= p_left) ts[me][0].DisableSafeSpots();
                if (p_me > p_right && p_me >= p_left) ts[me][1].DisableSafeSpots();
                if (p_me > p_front && p_me > p_right) ts[me][2].DisableSafeSpots();


                //create for crossing points
                this.CreateCrysisPoints(ts[right][1], ts[me][1], p_right, p_me); //me going forward, right going forward
                this.CreateCrysisPoints(ts[right][2], ts[me][1], p_right, p_me); //me going forward, right going left
                this.CreateCrysisPoints(ts[right][2], ts[me][2], p_right, p_me); //me going left, right going left
                this.CreateCrysisPoints(ts[front][1], ts[me][2], p_front, p_me); //me going left, front going forward

                //set merge priorities - what trajectory is main when merging at me
                int right_merge_prio = (p_front > p_me ? 0 : 1) + (p_left > p_me ? 0 : 1);
                int front_merge_prio = (p_right >= p_me ? 0 : 1) + (p_left > p_me ? 0 : 1);
                int left_merge_prio = (p_right >= p_me ? 0 : 1) + (p_front >= p_me ? 0 : 1);
                ts[me][0].SetMergePriority(right_merge_prio); ts[me][1].SetMergePriority(front_merge_prio); ts[me][2].SetMergePriority(left_merge_prio);
            }
        }
    }
}
