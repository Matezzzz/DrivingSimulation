using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrivingSimulation
{
    class BuildableRoadGraph : RoadGraph
    {
        const float default_2side_lane_distance = TwoSideRoadPlug.default_lane_dist;
        const float default_curve_coeff = 0.5f;

        public BuildableRoadGraph(RoadWorld world, Vector2 world_size) : base(world, world_size) { }


        public RoadPlug Vertical2Side(Vector2 pos, float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return new TwoSideRoadPlug(world, pos, new Vector2(0, 1), curve_coeff, lane_distance);
        }
        public RoadPlug Vertical2SideInv(Vector2 pos, float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return Vertical2Side(pos, curve_coeff, lane_distance).Invert();
        }
        public RoadPlug Horizontal2Side(Vector2 pos, float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return new TwoSideRoadPlug(world, pos, new Vector2(-1, 0), curve_coeff, lane_distance);
        }
        public RoadPlug Horizontal2SideInv(Vector2 pos, float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return Horizontal2Side(pos, curve_coeff, lane_distance).Invert();
        }


        public RoadPlug DefaultTop2Side(float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return Horizontal2SideInv(new Vector2(0, -1), curve_coeff, lane_distance);
        }
        public RoadPlug DefaultBottom2Side(float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return Horizontal2Side(new Vector2(0, 1), curve_coeff, lane_distance);
        }
        public RoadPlug DefaultLeft2Side(float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return Vertical2SideInv(new Vector2(-1, 0), curve_coeff, lane_distance);
        }
        public RoadPlug DefaultRight2Side(float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return Vertical2Side(new Vector2(1, 0), curve_coeff, lane_distance);
        }





        public RoadPlug Vertical1Side(Vector2 pos, bool invert = false, float curve_coeff = default_curve_coeff)
        {
            var p = new OneSideRoadPlug(world, pos, new Vector2((invert ? -1 : 1) * curve_coeff, 0));
            return p;
        }
        /*public RoadPlug Vertical1SideInv(Vector2 pos, bool invert = false, float curve_coeff = default_curve_coeff)
        {
            return Vertical1Side(pos, invert, curve_coeff).Invert();
        }*/
        public RoadPlug Horizontal1Side(Vector2 pos, bool invert = false, float curve_coeff = default_curve_coeff)
        {
            var p = new OneSideRoadPlug(world, pos, new Vector2(0, (invert ? -1 : 1) * curve_coeff));
            return p;
        }
        /*public RoadPlug Horizontal1SideInv(Vector2 pos, bool invert = false, float curve_coeff = default_curve_coeff)
        {
            return Horizontal1Side(pos, invert, curve_coeff).Invert();
        }*/


        public RoadPlug DefaultTop1Side(bool invert = false, float curve_coeff = default_curve_coeff)
        {
            var p = Horizontal1Side(new Vector2(0, -1), !invert, curve_coeff);
            if (invert) p.Invert();
            return p;
        }
        public RoadPlug DefaultBottom1Side(bool invert = false, float curve_coeff = default_curve_coeff)
        {
            var p = Horizontal1Side(new Vector2(0, 1), invert, curve_coeff);
            if (invert) p.Invert();
            return p;
        }
        public RoadPlug DefaultLeft1Side(bool invert = false, float curve_coeff = default_curve_coeff)
        {
            var p = Vertical1Side(new Vector2(-1, 0), !invert, curve_coeff);
            if (invert) p.Invert();
            return p;
        }
        public RoadPlug DefaultRight1Side(bool invert = false, float curve_coeff = default_curve_coeff)
        {
            var p = Vertical1Side(new Vector2(1, 0), invert, curve_coeff);
            if (invert) p.Invert();
            return p;
        }

        public RoadPlug Base2Side(Vector2 pos, float rotation, float curve_coeff = default_curve_coeff)
        {
            return new TwoSideRoadPlug(world, pos, new Vector2(MathF.Cos((rotation + 90) / 180 * MathF.PI), MathF.Sin((rotation + 90) / 180 * MathF.PI)), curve_coeff);
        }
        public RoadPlug Base1Side(Vector2 pos, float rotation, float curve_coeff = default_curve_coeff)
        {
            return new OneSideRoadPlug(world, pos, new Vector2(MathF.Cos(rotation / 180 * MathF.PI), MathF.Sin(rotation / 180 * MathF.PI)) * curve_coeff);
        }




        protected List<Trajectory> Connect(RoadPlug p1, RoadPlug p2, Transform transform = null)
        {
            return RoadPlug.Connect(this, p1, p2, transform);
        }



        protected BasicRoadPart Road(RoadPlug plug, float length, float lane_dist = 0)
        {
            var road_plug = plug.MovedCopy(world, plug.GetDirection().Normalized() * length, lane_dist).Invert();
            List<Trajectory> t = Connect(plug, road_plug);
            return new BasicRoadPart(road_plug.Invert(), t);
        }


        protected void Garage(RoadPlug v, Garage garage, float sink_weight = 1)
        {
            v.Invert();
            bool creating_garage = v.Forward.Count != 0;
            if (creating_garage) _ = new GarageObject(world, garage, v);


            if (sink_weight != float.NegativeInfinity && v.Backward.Count != 0) _ = new VehicleSink(world, sink_weight, v, creating_garage);
            v.Invert();
        }




        protected void VehicleSink(RoadPlug v, float sink_weight)
        {
            foreach (var b in v.Backward)
            {
                if (b.node.edges_backward.Count == 0)
                {
                    Console.WriteLine("Invalid vehicle sink - no incoming edges exist");
                }
                vehicle_sinks.Add(FindNode(b.node), sink_weight);
            }
        }

        protected void Garage(RoadPlug v, Garage garage, bool enable_sink = true)
        {
            Garage(v, garage, enable_sink ? 1 : float.NegativeInfinity);
        }
        protected BasicRoadPart GarageRoad(RoadPlug plug, Garage garage, float sink_weight, float length = 4)
        {
            BasicRoadPart garage_road = Road(plug, length);
            Garage(garage_road, garage, sink_weight);
            return garage_road;
        }
        protected BasicRoadPart GarageRoad(RoadPlug plug, Garage garage, bool enable_sink = true, float length = 4)
        {
            return GarageRoad(plug, garage, enable_sink ? 1 : float.NegativeInfinity, length);
        }


        protected delegate void CreateCrysisPointsFunc(List<List<List<Trajectory>>> roads);
        protected void CreateCrossroads(CrossroadsPart part, Transform transform, CreateCrysisPointsFunc crysis_points_func)
        {
            List<RoadPlug> connects = new();
            foreach (RoadPlug plug in part.Plugs) connects.Add(plug.Invert());
            var trajectories = ConnectAll(connects, transform);
            part.Transform(transform);
            crysis_points_func(trajectories);
            foreach (RoadPlug plug in part.Plugs) plug.Invert();
            part.AddTrajectories(trajectories);
        }




        protected class RoadPart
        {
            readonly List<Trajectory> trajectories;

            public RoadPart()
            {
                trajectories = new();
            }
            public RoadPart(List<Trajectory> trajectories)
            {
                this.trajectories = trajectories;
            }

            public void AddTrajectories(List<List<List<Trajectory>>> ts)
            {
                foreach (var x in ts)
                {
                    foreach (var y in x)
                    {
                        foreach (var z in y)
                        {
                            trajectories.Add(z);
                        }
                    }
                }
            }
            public RoadPart SetMaxSpeed(float max_speed)
            {
                trajectories.SetMaxSpeed(max_speed);
                return this;
            }
        }

        protected class BasicRoadPart : RoadPart
        {
            readonly RoadPlug end;
            public BasicRoadPart(RoadPlug plug, List<Trajectory> trajectories) : base(trajectories)
            {
                end = plug;
            }

            public static implicit operator RoadPlug(BasicRoadPart p)
            {
                return p.end;
            }
        }



        protected class CrossroadsPart : RoadPart
        {
            public List<RoadPlug> Plugs { get; }
            public CrossroadsPart(List<RoadPlug> plugs)
            {
                Plugs = plugs;
            }

            public void Transform(Transform transform)
            {
                for (int i = 0; i < Plugs.Count; i++) Plugs[i] = Plugs[i].Transform(transform);
            }
        }

        protected class CrossroadsXPart : CrossroadsPart
        {
            public RoadPlug T { get => Plugs[0]; }
            public RoadPlug L { get => Plugs[1]; }
            public RoadPlug B { get => Plugs[2]; }
            public RoadPlug R { get => Plugs[3]; }
            public CrossroadsXPart(RoadPlug top, RoadPlug left, RoadPlug bot, RoadPlug right) : base(new List<RoadPlug>() { top, left, bot, right })
            { }
        }

        protected class CrossroadsTPart : CrossroadsPart
        {
            public RoadPlug L { get => Plugs[0]; }
            public RoadPlug B { get => Plugs[1]; }
            public RoadPlug R { get => Plugs[2]; }

            public CrossroadsTPart(RoadPlug left, RoadPlug bot, RoadPlug right) : base(new List<RoadPlug>() { left, bot, right })
            { }
        }


        delegate int PriorityFunc(int i, List<int> priorities);


        protected CrossroadsXPart CrossroadsX(RoadPlug top, RoadPlug left, RoadPlug bot, RoadPlug right, Transform transform, List<int> priorities = null)
        {
            var prio = (int i) => priorities == null ? 0 : priorities[i];
            CrossroadsXPart part = new(top, left, bot, right);
            CreateCrossroads(part, transform, ts =>
            {
                for (int p1 = 0; p1 < ts.Count; p1++)
                {
                    int p1_right = (p1 + 1) % ts.Count;
                    int p1_front = (p1 + 2) % ts.Count;
                    int p1_left = (p1 + 3) % ts.Count;

                    CreateCrysisPoints(ts[p1_right][1], ts[p1][1], prio(p1_right), prio(p1));
                    CreateCrysisPoints(ts[p1_right][2], ts[p1][1], prio(p1_right), prio(p1));
                    CreateCrysisPoints(ts[p1_right][2], ts[p1][2], prio(p1_right), prio(p1));
                    CreateCrysisPoints(ts[p1_front][1], ts[p1][2], prio(p1_front), prio(p1));

                    int right_merge_prio = (prio(p1_front) >  prio(p1) ? 0 : 1) + (prio(p1_left)  >  prio(p1) ? 0 : 1);
                    int front_merge_prio = (prio(p1_right) >= prio(p1) ? 0 : 1) + (prio(p1_left)  >  prio(p1) ? 0 : 1);
                    int left_merge_prio =  (prio(p1_right) >= prio(p1) ? 0 : 1) + (prio(p1_front) >= prio(p1) ? 0 : 1);
                    ts[p1][0].SetMergePriority(right_merge_prio); ts[p1][1].SetMergePriority(front_merge_prio); ts[p1][2].SetMergePriority(left_merge_prio);
                }
            });
            return part;
        }
        protected CrossroadsXPart CrossroadsX(Transform transform, List<int> priorities = null)
        {
            return CrossroadsX(DefaultTop2Side(), DefaultLeft2Side(), DefaultBottom2Side(), DefaultRight2Side(), transform, priorities);
        }

        protected CrossroadsXPart CrossroadsXMainLT(Transform transform)
        {
            return CrossroadsX(transform, new List<int>() { 1, 1, 0, 0 });
        }
        protected CrossroadsXPart CrossroadsXMainLR(Transform transform)
        {
            return CrossroadsX(transform, new List<int>() { 0, 1, 0, 1 });
        }

        protected CrossroadsTPart CrossroadsT(RoadPlug left, RoadPlug bot, RoadPlug right, Transform transform, List<int> priorities = null)
        {
            var prio = (int i) => priorities == null ? 0 : priorities[i];
            CrossroadsTPart plugs = new(left, bot, right);
            CreateCrossroads(plugs, transform, ts =>
            {
                for (int p1 = 0; p1 < ts.Count; p1++)
                {
                    int p1_right = (p1 + 1) % ts.Count;
                    int p1_left = (p1 + 2) % ts.Count;
                    CreateCrysisPoints(ts[p1_right][1], ts[p1][1], prio(p1_right), prio(p1));
                    int right_merge_prio = prio(p1_left) > prio(p1) ? 0 : 1;
                    int left_merge_prio = prio(p1_right) >= prio(p1) ? 0 : 1;
                    ts[p1][0].SetMergePriority(right_merge_prio); ts[p1][1].SetMergePriority(left_merge_prio);
                }
            });
            return plugs;
        }
        protected CrossroadsTPart CrossroadsT(Transform transform, List<int> priorities = null)
        {
            return CrossroadsT(DefaultLeft2Side(), DefaultBottom2Side(), DefaultRight2Side(), transform, priorities);
        }
        protected CrossroadsTPart CrossroadsTMainLR(Transform transform)
        {
            return CrossroadsT(transform, new List<int>() { 1, 0, 1 });
        }
        protected CrossroadsTPart CrossroadsTMainLB(Transform transform)
        {
            return CrossroadsT(transform, new List<int>() { 1, 1, 0 });
        }
        protected CrossroadsTPart CrossroadsTMainBR(Transform transform)
        {
            return CrossroadsT(transform, new List<int>() { 0, 1, 1 });
        }

        public void CreateCrysisPoints(List<Trajectory> t1, List<Trajectory> t2, int prio1 = 0, int prio2 = 0)
        {
            foreach (Trajectory a in t1)
            {
                foreach (Trajectory b in t2)
                {
                    CrysisPoint.TryCreatingCrysisPoint(world, a, b, prio1, prio2);
                }
            }
        }
        List<List<List<Trajectory>>> ConnectAll(List<RoadPlug> roads, Transform transform = null)
        {
            List<List<List<Trajectory>>> list = new();
            for (int i = 0; i < roads.Count; i++)
            {
                list.Add(new List<List<Trajectory>>());
                for (int j = 1; j < roads.Count; j++)
                {
                    int rel_i = (i + j) % roads.Count;
                    list.Last().Add(RoadPlug.Connect(this, roads[i].Forward, roads[rel_i].Backward, transform));
                }
            }
            return list;
        }
    }
}
