using System;
using System.Collections.Generic;


namespace DrivingSimulation
{
    /*interface SimulationObjectCollection
    {
        //add object to the world
        void Add(SimulationObject o);
        //add object to a collection that is a subset of current one
        void AddIndirect(SimulationObject o);
    }*/


    static class InterfaceExtensions
    {
        const float default_2side_lane_distance = Constant.default_2side_lane_distance;
        const float default_curve_coeff = Constant.default_curve_coeff;
        public static RoadWorld GetParentWorld(this SimulationObject world)
        {
            SimulationObject x = world;
            while (x.parent != null) x = x.parent;
            return (RoadWorld) x;
        }






        /*public static Rect WorldRect(this ISimulationObjectCollection parent, Vector2 pos, Vector2 size, Vector2 world_dir, out float rotation)
        {
            Vector2 world_mid = parent.WorldPos(pos + size / 2);
            Vector2 world_size = new (parent.WorldDir(new Vector2(size.X, 0)).Length(), parent.WorldDir(new Vector2(0, size.Y)).Length());
            rotation = parent.WorldDir(world_dir).Rotation();
            return new Rect(
                world_mid - world_size / 2, world_size
            );
        }*/









        public static RoadPlug Vertical2Side(this SimulationObjectCollection world, Vector2 pos, bool invert, float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return new TwoSideRoadPlug(new EditableRoadPlug(world, pos, invert ? 180 : 0, lane_distance/2), curve_coeff);
        }
        public static RoadPlug Horizontal2Side(this SimulationObjectCollection world, Vector2 pos, bool invert, float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return new TwoSideRoadPlug(new EditableRoadPlug(world, pos, invert ? 270 : 90, lane_distance/2), curve_coeff);
        }
        public static RoadPlug DefaultTop2Side(this SimulationObjectCollection world, float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return world.Horizontal2Side(new Vector2(0, -1), true, curve_coeff, lane_distance);
        }
        public static RoadPlug DefaultBottom2Side(this SimulationObjectCollection world, float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return world.Horizontal2Side(new Vector2(0, 1), false, curve_coeff, lane_distance);
        }
        public static RoadPlug DefaultLeft2Side(this SimulationObjectCollection world, float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return world.Vertical2Side(new Vector2(-1, 0), true, curve_coeff, lane_distance);
        }
        public static RoadPlug DefaultRight2Side(this SimulationObjectCollection world, float curve_coeff = default_curve_coeff, float lane_distance = default_2side_lane_distance)
        {
            return world.Vertical2Side(new Vector2(1, 0), false, curve_coeff, lane_distance);
        }




        public static RoadPlug Vertical1Side(this SimulationObjectCollection world, Vector2 pos, bool invert_dir, bool invert_forw_back, float curve_coeff = default_curve_coeff)
        {
            return new OneSideRoadPlug(new EditableRoadPlug(world, pos, (invert_dir != invert_forw_back) ? 180 : 0, curve_coeff), invert_forw_back);
        }
        public static RoadPlug Horizontal1Side(this SimulationObjectCollection world, Vector2 pos, bool invert_dir, bool invert_forw_back, float curve_coeff = default_curve_coeff)
        {
            return new OneSideRoadPlug(new EditableRoadPlug(world, pos, (invert_dir != invert_forw_back) ? 270 : 90, curve_coeff), invert_forw_back);
        }




        public static RoadPlug DefaultTop1Side(this SimulationObjectCollection world, bool invert = false, float curve_coeff = default_curve_coeff)
        {
            return world.Horizontal1Side(new Vector2(0, -1), true, invert, curve_coeff);
        }
        public static RoadPlug DefaultBottom1Side(this SimulationObjectCollection world, bool invert = false, float curve_coeff = default_curve_coeff)
        {
            return world.Horizontal1Side(new Vector2(0, 1), false, invert, curve_coeff);
        }
        public static RoadPlug DefaultLeft1Side(this SimulationObjectCollection world, bool invert = false, float curve_coeff = default_curve_coeff)
        {
            return world.Vertical1Side(new Vector2(-1, 0), true, invert, curve_coeff);
        }
        public static RoadPlug DefaultRight1Side(this SimulationObjectCollection world, bool invert = false, float curve_coeff = default_curve_coeff)
        {
            return world.Vertical1Side(new Vector2(1, 0), false, invert, curve_coeff);
        }

        public static RoadPlug Base2Side(this SimulationObjectCollection world, Vector2 pos, float rotation, float curve_coeff = default_curve_coeff)
        {
            return new TwoSideRoadPlug(new EditableRoadPlug(world, pos, rotation, default_2side_lane_distance), curve_coeff);
        }
        public static RoadPlug Base1Side(this SimulationObjectCollection world, Vector2 pos, float rotation, bool invert, float curve_coeff = default_curve_coeff)
        {
            return new OneSideRoadPlug(new EditableRoadPlug(world, pos, rotation, curve_coeff), invert);
        }




        public static Road Road(this SimulationObjectCollection world, RoadPlugView plug, float length, float lane_dist = 0)
        {
            return new Road(world, plug, length, lane_dist);
            
        }





        public static CrossroadsX CrossroadsX(this SimulationObjectCollection world, Vector2 pos, float rotation, float scale, List<Func<SimulationObjectCollection, RoadPlugView>> plugs, List<int> priorities = null)
        {
            return new CrossroadsX(new EditableCrossroadsX(world, pos, rotation, scale), plugs, priorities);
        }
        public static CrossroadsX CrossroadsX(this SimulationObjectCollection world, Vector2 pos, float rotation = 0, float scale = 1, List<int> priorities = null)
        {
            return new CrossroadsX(new EditableCrossroadsX(world, pos, rotation, scale), priorities);
        }
        public static CrossroadsX CrossroadsXMainLT(this SimulationObjectCollection world, Vector2 pos, float rotation, float scale)
        {
            return world.CrossroadsX(pos, rotation, scale, new List<int>() { 1, 1, 0, 0 });
        }
        public static CrossroadsX CrossroadsXMainLR(this SimulationObjectCollection world, Vector2 pos, float rotation, float scale)
        {
            return world.CrossroadsX(pos, rotation, scale, new List<int>() { 0, 1, 0, 1 });
        }

        public static CrossroadsT CrossroadsT(this SimulationObjectCollection world, Vector2 pos, float rotation, Vector2 scale, List<Func<SimulationObjectCollection, RoadPlugView>> plugs, List<int> priorities = null)
        {
            return new CrossroadsT(new EditableCrossroadsT(world, pos, rotation, scale), plugs, priorities);
        }
        public static CrossroadsT CrossroadsT(this SimulationObjectCollection world, Vector2 pos, float rotation = 0, float scale = 1, List<int> priorities = null)
        {
            return new CrossroadsT(new EditableCrossroadsT(world, pos, rotation, scale), priorities);
        }
        public static CrossroadsT CrossroadsTMainLR(this SimulationObjectCollection world, Vector2 pos, float rotation, float scale)
        {
            return world.CrossroadsT(pos, rotation, scale, new List<int>() { 1, 0, 1 });
        }
        public static CrossroadsT CrossroadsTMainLB(this SimulationObjectCollection world, Vector2 pos, float rotation, float scale)
        {
            return world.CrossroadsT(pos, rotation, scale, new List<int>() { 1, 1, 0 });
        }
        public static CrossroadsT CrossroadsTMainBR(this SimulationObjectCollection world, Vector2 pos, float rotation, float scale)
        {
            return world.CrossroadsT(pos, rotation, scale, new List<int>() { 0, 1, 1 });
        }








        public static List<Trajectory> Connect(this SimulationObjectCollection world, RoadPlugView p1, RoadPlugView p2)
        {
            return p1.Connect(world, p2);
        }
        public static Trajectory Connect(this SimulationObjectCollection world, RoadConnectionVector p1, RoadConnectionVector p2)
        {
            return Connect(world, p1.GetView(false), p2.GetView(true));
        }
        public static Trajectory Connect(this SimulationObjectCollection world, RoadConnectionVectorView v1, RoadConnectionVectorView v2)
        {
            if (!(world is RoadWorld || world is TrajectoryList)) Console.WriteLine("Creating trajectory in a wrong world - it might behave broken weirdly due to applied transforms");
            Trajectory t = new(world, v1, v2);
            var g = world.GetParentWorld().Graph;
            GraphEdge<BaseData> e = new(g, t, v1.Connection.node, v2.Connection.node);
            GraphEdgeReference<BaseData> r = new(g.FindEdge(e));
            v1.Connect(r);
            v2.Connect(r);
            return t;
        }



        public static List<List<List<Trajectory>>> ConnectAll(this SimulationObjectCollection world, List<RoadPlugView> roads)
        {
            List<List<List<Trajectory>>> list = new();
            for (int i = 0; i < roads.Count; i++)
            {
                list.Add(new List<List<Trajectory>>());
                for (int j = 1; j < roads.Count; j++)
                {
                    int rel_i = (i + j) % roads.Count;
                    list.Last().Add(RoadPlugView.Connect(world, roads[i].Forward, roads[rel_i].Backward));
                }
            }
            return list;
        }

        public static void CreateCrysisPoints(this SimulationObjectCollection world, List<Trajectory> t1, List<Trajectory> t2, int prio1 = 0, int prio2 = 0)
        {
            foreach (Trajectory a in t1)
            {
                foreach (Trajectory b in t2)
                {
                    CrysisPoint.CreateCrossCrysisPoint(world, a, b, prio1, prio2);
                }
            }
        }
    }
}
