using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DrivingSimulation
{
    //An abstract collection - allows adding & removing of objects, and has a ton of functions for creating new ones in it
    [JsonObject(MemberSerialization.OptIn)]
    abstract class SimulationObjectCollection : SimulationObject
    {
        public SimulationObjectCollection(SimulationObjectCollection world) : base(world)
        { }

        //add - is called by objects created in this collection automatically
        public abstract void Add(SimulationObject o);
        //remove - when object is destroyed, it calls this to be removed from this collection
        public abstract void Remove(SimulationObject o);



        public static float default_2lane_dist = Constant.default_2side_lane_distance;
        public static float default_curve_coeff = Constant.default_curve_coeff;


        //FOR ALL FOLLOWING
        //* lane_dist is how far will the lanes forward/backward be
        //* curve_coeff is how curved will the road be. 0 = straight line, more curved from thereon

        private static float LaneDist(float d) => d == 0 ? default_2lane_dist : d;
        private static float CurveCoeff(float c) => c == 0 ? default_curve_coeff : c;
        

        //vertical bidirectional road plug
        public RoadPlug Vertical2Side(Vector2 pos, bool invert, float curve_coeff = 0, float lane_distance = 0)
        {
            return new TwoSideRoadPlug(new EditableRoadPlug(this, pos, invert ? 180 : 0, new Vector2(CurveCoeff(curve_coeff), LaneDist(lane_distance) / 2)));
        }
        //horizontal bidirectional road plug
        public RoadPlug Horizontal2Side(Vector2 pos, bool invert, float curve_coeff = 0, float lane_distance = 0)
        {
            return new TwoSideRoadPlug(new EditableRoadPlug(this, pos, invert ? 270 : 90, new Vector2(CurveCoeff(curve_coeff), LaneDist(lane_distance) / 2)));
        }


        //The following four are roads, distance 1 from the center in their direction, heading further that way
        public RoadPlug DefaultTop2Side(float curve_coeff = 0, float lane_distance = 0)
        {
            return Horizontal2Side(new Vector2(0, -1), true, curve_coeff, lane_distance);
        }
        public RoadPlug DefaultBottom2Side(float curve_coeff = 0, float lane_distance = 0)
        {
            return Horizontal2Side(new Vector2(0, 1), false, curve_coeff, lane_distance);
        }
        public RoadPlug DefaultLeft2Side(float curve_coeff = 0, float lane_distance = 0)
        {
            return Vertical2Side(new Vector2(-1, 0), true, curve_coeff, lane_distance);
        }
        public RoadPlug DefaultRight2Side(float curve_coeff = 0, float lane_distance = 0)
        {
            return Vertical2Side(new Vector2(1, 0), false, curve_coeff, lane_distance);
        }


        //vertical unidirectional road. invert_dir specifies vector direction - false = going right, true = going left
        //invert_forw_back specifies how new roads will be connected - e.g. to which side will a connection to two-way road connect.

        public RoadPlug Vertical1Side(Vector2 pos, bool invert_dir, bool invert_forw_back, float curve_coeff = 0)
        {
            return new OneSideRoadPlug(new EditableRoadPlug(this, pos, (invert_dir != invert_forw_back) ? 180 : 0, new Vector2(CurveCoeff(curve_coeff), 1)), invert_forw_back);
        }
        //horizontal unidirectional road. invert_dir specifies vector direction - false = going down, true = going up
        //invert_forw_back specifies how new roads will be connected - e.g. to which side will a connection to two-way road connect.
        public RoadPlug Horizontal1Side(Vector2 pos, bool invert_dir, bool invert_forw_back, float curve_coeff = 0)
        {
            return new OneSideRoadPlug(new EditableRoadPlug(this, pos, (invert_dir != invert_forw_back) ? 270 : 90, new Vector2(CurveCoeff(curve_coeff), 1)), invert_forw_back);
        }



        //The following are four roads, distance 1 from center, in their respective direction. invert = false -> they head from center, invert=true -> they heading into center
        public RoadPlug DefaultTop1Side(bool invert = false, float curve_coeff = 0)
        {
            return Horizontal1Side(new Vector2(0, -1), true, invert, curve_coeff);
        }
        public RoadPlug DefaultBottom1Side(bool invert = false, float curve_coeff = 0)
        {
            return Horizontal1Side(new Vector2(0, 1), false, invert, curve_coeff);
        }
        public RoadPlug DefaultLeft1Side(bool invert = false, float curve_coeff = 0)
        {
            return Vertical1Side(new Vector2(-1, 0), true, invert, curve_coeff);
        }
        public RoadPlug DefaultRight1Side(bool invert = false, float curve_coeff = 0)
        {
            return Vertical1Side(new Vector2(1, 0), false, invert, curve_coeff);
        }


        //bidirectional road with position and rotation
        public RoadPlug Base2Side(Vector2 pos, float rotation, float curve_coeff = 0)
        {
            return new TwoSideRoadPlug(new EditableRoadPlug(this, pos, rotation, new Vector2(CurveCoeff(curve_coeff), 1)));
        }
        //unidirectional road with position, rotation and inversion
        public RoadPlug Base1Side(Vector2 pos, float rotation, bool invert, float curve_coeff = 0)
        {
            return new OneSideRoadPlug(new EditableRoadPlug(this, pos, rotation, new Vector2(CurveCoeff(curve_coeff), 1)), invert);
        }



        //Create a new road plug, to which a road of given length will lead. Lane dist can be specified to scale the ending road plug before placing it, 0 does nothing.
        public Road Road(RoadPlugView plug, float length, float lane_dist = 0)
        {
            return new Road(this, plug, length, lane_dist);
        }




        //Create a new X crossroads from position, rotation and scale, given a function to create each of four plugs. Priorities can be used to specify a main road
        public CrossroadsX CrossroadsX(Vector2 pos, float rotation, float scale, List<Func<SimulationObjectCollection, RoadPlugView>> plugs, List<int> priorities = null)
        {
            return new CrossroadsX(new EditableCrossroadsX(this, pos, rotation, scale), plugs, priorities);
        }
        //Create default crossroads with given transform. Priorities can be used to specify a main road
        public CrossroadsX CrossroadsX(Vector2 pos, float rotation = 0, float scale = 1, List<int> priorities = null)
        {
            return new CrossroadsX(new EditableCrossroadsX(this, pos, rotation, scale), priorities);
        }
        //Create default crossroads with main road left-to-top
        public CrossroadsX CrossroadsXMainLT(Vector2 pos, float rotation, float scale)
        {
            return CrossroadsX(pos, rotation, scale, new List<int>() { 1, 1, 0, 0 });
        }
        //Create default crossroads with main road left-to-right
        public CrossroadsX CrossroadsXMainLR(Vector2 pos, float rotation, float scale)
        {
            return CrossroadsX(pos, rotation, scale, new List<int>() { 0, 1, 0, 1 });
        }

        //Create a new T crossroads from position, rotation and scale, given a function to create each of three plugs. Priorities can be used to specify a main road
        public CrossroadsT CrossroadsT(Vector2 pos, float rotation, Vector2 scale, List<Func<SimulationObjectCollection, RoadPlugView>> plugs, List<int> priorities = null)
        {
            return new CrossroadsT(new EditableCrossroadsT(this, pos, rotation, scale), plugs, priorities);
        }
        //Create default crossroads with given transform. Priorities can be used to specify a main road
        public CrossroadsT CrossroadsT(Vector2 pos, float rotation = 0, float scale = 1, List<int> priorities = null)
        {
            return new CrossroadsT(new EditableCrossroadsT(this, pos, rotation, scale), priorities);
        }
        //Create default crossroads with main road left-to-right
        public CrossroadsT CrossroadsTMainLR(Vector2 pos, float rotation, float scale)
        {
            return CrossroadsT(pos, rotation, scale, new List<int>() { 1, 0, 1 });
        }
        //Create default crossroads with main road left-to-bottom
        public CrossroadsT CrossroadsTMainLB(Vector2 pos, float rotation, float scale)
        {
            return CrossroadsT(pos, rotation, scale, new List<int>() { 1, 1, 0 });
        }
        //Create default crossroads with main road bottom-to-right
        public CrossroadsT CrossroadsTMainBR(Vector2 pos, float rotation, float scale)
        {
            return CrossroadsT(pos, rotation, scale, new List<int>() { 0, 1, 1 });
        }







        //Connect two road plugs
        public List<Trajectory> Connect(RoadPlugView p1, RoadPlugView p2)
        {
            return p1.Connect(this, p2);
        }
        //Connect two road connection vectors
        public Trajectory Connect(RoadConnectionVector p1, RoadConnectionVector p2)
        {
            return Connect(p1.GetView(false), p2.GetView(true));
        }
        //Connect two road connection vector views
        public Trajectory Connect(RoadConnectionVectorView v1, RoadConnectionVectorView v2)
        {
            //create the connection trajectory
            Trajectory t = new(this, v1, v2);
            var g = RoadGraph;
            //now create the graph edge representing the trajectory
            GraphEdge<BaseData> e = new(g, t, v1.Vector.node, v2.Vector.node);
            //create a reference to it, then add it to both connected nodes
            GraphEdgeReference<BaseData> r = new(g.FindEdge(e));
            v1.Connect(r);
            v2.Connect(r);
            return t;
        }


        //Connect all road plug views in roads together
        //result is a List<List<List<Trajectory>>> - the indexing means[source plug][target plug][trajectory_i]
        public List<List<List<Trajectory>>> ConnectAll(List<RoadPlugView> roads)
        {
            //create the list
            List<List<List<Trajectory>>> list = new();
            //for every source plug
            for (int i = 0; i < roads.Count; i++)
            {
                list.Add(new List<List<Trajectory>>());
                //for every target plug
                for (int j = 1; j < roads.Count; j++)
                {
                    //connect roads in the relative order -> list[x][0] will always be the roads going right from x, list[x][1] will be the ones going forward, etc.
                    int rel_i = (i + j) % roads.Count;
                    //actually connect all
                    list.Last().Add(RoadPlugView.Connect(this, roads[i].Forward, roads[rel_i].Backward));
                }
            }
            return list;
        }

        //Create cross crysis points between two sets of trajectories
        public void CreateCrysisPoints(List<Trajectory> t1, List<Trajectory> t2, int prio1 = 0, int prio2 = 0)
        {
            foreach (Trajectory a in t1)
            {
                foreach (Trajectory b in t2)
                {
                    CrysisPoint.CreateCrossCrysisPoint(this, a, b, prio1, prio2);
                }
            }
        }
    }




    //A collection of simulation objects, represented by a list
    [JsonObject(MemberSerialization.OptIn)]
    class SimulationObjectListCollection : SimulationObjectCollection, IEnumerable<SimulationObject>
    {
        //list of all simulation objects
        [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
        protected List<SimulationObject> objects;

        //objects removed during current operation - cannot be removed directly, otherwise foreach is broken
        readonly List<SimulationObject> removed_objects = new();

        [JsonConstructor]
        protected SimulationObjectListCollection() : base(null) { }
        public SimulationObjectListCollection(SimulationObjectCollection world) : base(world)
        {
            objects = new();
        }
        public override void Add(SimulationObject o) => objects.Add(o);
        public override void Remove(SimulationObject o) => removed_objects.Add(o);

        //if we know there won't be any problems with removing during an iteration, we can remove an object directly
        public void RemoveDirect(SimulationObject o) => objects.Remove(o);


        //All follow the same principle - call the method on all child objects, then return
        protected override void FinishI(FinishPhase phase)
        {
            base.FinishI(phase);
            foreach (var o in objects) o.Finish(phase);
        }
        protected override void UnfinishI()
        {
            base.UnfinishI();
            foreach (var o in objects) o.Unfinish();
        }
        protected override void UpdateI()
        {
            base.UpdateI();
            foreach (var o in objects) o.Update();
        }
        protected override void PostUpdateI()
        {
            base.PostUpdateI();
            foreach (var o in objects) o.PostUpdate();
        }
        protected override void InteractI(Inputs inputs)
        {
            base.InteractI(inputs);
            foreach (var o in objects) o.Interact(inputs);
            RemoveObjects();
        }
        protected override void DrawCollectionI(SDLApp app, Transform camera, DrawLayer layer)
        {
            base.DrawCollectionI(app, camera, layer);
            foreach (var o in objects) o.Draw(app, camera, layer);
        }
        protected override void DestroyI()
        {
            base.DestroyI();
            foreach (var o in objects) o.Destroy();
            RemoveObjects();
        }

        void RemoveObjects()
        {
            foreach (var o in removed_objects) objects.Remove(o);
            removed_objects.Clear();
        }

        //copy all objects from another collection to this
        protected void CopyAll(SimulationObjectListCollection copy_from)
        {
            objects = copy_from.objects;
        }
        //implement the IEnumerable interface, so that foreach can work with this collection
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerator<SimulationObject> GetEnumerator() => objects.GetEnumerator();
    }



    //holds a list of trajectories
    [JsonObject(MemberSerialization.OptIn)]
    class TrajectoryList : SimulationObjectListCollection
    {
       
        //compute how many vehicles are currently passing through all trajectories. One vehicle passing through one trajectory adds one throughput in total
        public float GetThroughput()
        {
            float a = 0;
            foreach (var o in objects) a += ((Trajectory) o).AverageThroughput;
            return a;
        }

        //get the average speed of a vehicle present on all trajectories here
        public float GetAverageSpeed()
        {
            float a = 0;
            int vehicle_count = 0;
            //for all trajectories, if there are vehicles present, add their total speed and their count
            foreach (var o in objects)
            {
                var t = ((Trajectory)o);
                
                a += t.AverageVehicleSpeed * t.VehicleCount;
                vehicle_count += t.VehicleCount;
            }
            //return average speed, or -1, if there are no vehicles present
            if (vehicle_count == 0) return -1;
            return a / vehicle_count;
        }

        //figure out the max speed on any trajectory here
        public float GetMaxSpeed()
        {
            float max = 0;
            foreach (var o in objects) max = Math.Max(((Trajectory)o).MaxSpeed, max);
            return max;
        }

        [JsonConstructor]
        private TrajectoryList() { }

        public TrajectoryList(SimulationObjectCollection world) : base(world.ParentWorld)
        { }

        //go through all trajectories, set max speed for each one
        public void SetMaxSpeed(float max_speed)
        {
            foreach (SimulationObject o in objects) ((Trajectory)o).MaxSpeed = max_speed;
        }
        //complain if the object added is not a trajectory
        public override void Add(SimulationObject o)
        {
            if (o is Trajectory) base.Add(o);
            else throw new InvalidOperationException("Adding wrong object to list - isn't a trajectory");
        }
    }

}
