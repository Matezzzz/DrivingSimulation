using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;

namespace DrivingSimulation
{

    //represents one vector with position, and contains a child graph node
    [JsonObject(MemberSerialization.OptIn)]
    class RoadConnectionVector : SimulationObject
    {
        [JsonProperty]
        public GraphNode<BaseData> node;

        [JsonConstructor]
        private RoadConnectionVector() : base(null) { }

        //create a new road connection vector with node
        public RoadConnectionVector(SimulationObjectCollection world) : base(world)
        {
            //create a new node for this vector
            node = GraphNode<BaseData>.NewOriginal(world.RoadGraph, world, this);
        }
        //get a view, inverted if needed
        public RoadConnectionVectorView GetView(bool inverted)
        {
            return new RoadConnectionVectorView(this, inverted);
        }
        //destroy the child node, then call the parent destroy method
        protected override void DestroyI()
        {
            base.DestroyI();
            node.Destroy();
        }
    }


    //Contains a road connection vector and whether its' inverted
    [JsonObject(MemberSerialization.OptIn)]
    class RoadConnectionVectorView
    {
        //the actual vector, and whether it's inverted
        [JsonProperty]
        readonly RoadConnectionVector v;
        [JsonProperty]
        readonly bool invert;

        public RoadConnectionVector Vector => v;
        //return the world position from which this vector leads
        public Vector2 From => v.WorldPosition;
        //return the world position to which the vector leads - add/subtract world direction based on inversion
        public Vector2 To => v.WorldPosition + (invert ? -1 : 1) * v.WorldDirection;

        [JsonConstructor]
        private RoadConnectionVectorView() { }
        public RoadConnectionVectorView(RoadConnectionVector v, bool invert)
        {
            this.v = v;
            this.invert = invert;
        }
        //connect an edge to this vector reference
        public void Connect(GraphEdgeReference<BaseData> edge)
        {
            if (invert) v.node.AddBackward(edge);
            else v.node.AddForward(edge);
        }
        //remove forward edge - called when the given trajectory is destroyed
        public GraphEdge<BaseData> RemoveForwardEdge(Trajectory t) => Remove(v.node.edges_forward, t);

        //remove backward edge - called when the given trajectory is destroyed
        public GraphEdge<BaseData> RemoveBackwardEdge(Trajectory t) => Remove(v.node.edges_backward, t);
        
        //remove an edge from given list where the trajectory matches
        GraphEdge<BaseData> Remove(List<GraphEdgeReference<BaseData>> l, Trajectory t)
        {
            var g = v.RoadGraph;
            var e = l.Where(e => e.Get(g).trajectory == t).ToList();
            if (e.Count == 0) return null; //edge was already removed before, nothing to do here
            if (e.Count >= 2) throw new Exception("Found invalid number of matching edges");
            l.Remove(e[0]);
            return e[0].Get(g);
        }
    }



    //Contains a road plug and whether it's inverted
    [JsonObject(MemberSerialization=MemberSerialization.OptIn)]
    struct RoadPlugView
    {
        [JsonProperty]
        public readonly RoadPlug plug;
        [JsonProperty]
        readonly bool inverted;

        public List<RoadConnectionVector> Forward { get => inverted ? plug.backward : plug.forward; }
        public List<RoadConnectionVector> Backward { get => inverted ? plug.forward : plug.backward; }

        public RoadPlugView(RoadPlug p, bool invert)
        {
            plug = p;
            inverted = invert;
        }
        //average the world position of all subset connection vectors
        public Vector2 GetWorldPosition()
        {
            return (Forward.Sum(x => x.WorldPosition) + Backward.Sum(x => x.WorldPosition)) / (Forward.Count + Backward.Count);
        }
        //average the world direction of all forward vectors and all inverted backward vectors
        public Vector2 GetWorldDirection()
        {
            return (Forward.Sum(x => x.WorldDirection) - Backward.Sum(x => x.WorldDirection)) / (Forward.Count + Backward.Count);
        }
        //find a world bounding box around all points - find min and max in both coordinates
        public Vector2 GetWorldSize()
        {
            Vector2 min = new(float.PositiveInfinity);
            Vector2 max = new(float.NegativeInfinity);
            foreach (var c in Forward)
            {
                min = Vector2.Min(min, c.WorldPosition);
                max = Vector2.Max(max, c.WorldPosition);
            }
            foreach (var c in Backward)
            {
                min = Vector2.Min(min, c.WorldPosition);
                max = Vector2.Max(max, c.WorldPosition);
            }
            return max - min;
        }
        //get a bounding box encompassing this road plug
        public Rect GetBoundingBox()
        {
            Vector2 min = GetMin();
            Vector2 max = GetMax();
            return new Rect(min, max - min);
        }
        // = get the left top coordinate of a bounding box
        Vector2 GetMin() => AggregateVectors(Vector2.MaxValue, (x, y) => Vector2.Min(x, y.WorldPosition));

        // = get the bottom right coordinate of a bbox
        Vector2 GetMax() => AggregateVectors(Vector2.MinValue, (x, y) => Vector2.Max(x, y.WorldPosition));


        Vector2 AggregateVectors(Vector2 init_value, Func<Vector2, RoadConnectionVector, Vector2> f)
        {
            foreach (var c in Forward)  init_value = f(init_value, c);
            foreach (var c in Backward) init_value = f(init_value, c);
            return init_value;
        }


        //connect two road plugs -> first connect my forward to his backward, then vice versa
        public List<Trajectory> Connect(SimulationObjectCollection graph, RoadPlugView opposing) => Connect(graph, Forward, opposing.Backward).Merge(Connect(graph, opposing.Forward, Backward));
        
        //Connect two lists of connection vectors
        public static List<Trajectory> Connect(SimulationObjectCollection graph, List<RoadConnectionVector> forw, List<RoadConnectionVector> back)
        {
            var list = new List<Trajectory>();
            //if any has zero vectors, do nothing
            if (forw.Count == 0 || back.Count == 0) return list;
            //if one has one vector and the other many, connect all of them to the one
            if (forw.Count == 1 || back.Count == 1)
            {
                foreach (var f in forw)
                {
                    foreach (var b in back)
                    {
                        list.Add(graph.Connect(f, b));
                    }
                }
            }
            //if they have the same amount, connect i-th to i-th
            else if (forw.Count == back.Count)
            {
                for (int i = 0; i < forw.Count; i++)
                {
                    list.Add(graph.Connect(forw[i], back[i]));
                }
            }
            //if none of the above holds, throw an exception
            else throw new InvalidOperationException("Cannot Wr.Connect two road plugs: incompatible sizes.");
            return list;
        }
        public RoadPlugView Invert() => new (plug, !inverted);

        //create a copy and move it in the direction of its' normal
        public RoadPlugView MovedCopy(float dist)
        {
            //Create in parent world - this plug will be editable by itself, it isn't a part of crossroads. Editable road plug wrapper will hold the new position, rotation and scale.
            RoadPlug copy = plug.Copy(new EditableRoadPlug(plug.ParentWorld, plug.WorldPosition + GetWorldDirection().Normalized() * dist * (inverted ? -1 : 1), plug.WorldDirection.Rotation(), plug.WorldSize.Abs()));
            //return a non-inverted view
            return copy.GetView(false);
        }
    }


    //road plug - contains vectors forward and backward
    [JsonObject(MemberSerialization.OptIn)]
    class RoadPlug : SimulationObjectListCollection
    {
        [JsonProperty]
        public readonly List<RoadConnectionVector> forward;
        [JsonProperty]
        public readonly List<RoadConnectionVector> backward;

        //all road plugs in the world are placed - they have their own position, rotation and scale. This is contained in their parent, which is of type EditableRoadPlug
        public EditableRoadPlug Placed => (EditableRoadPlug) parent;

        [JsonConstructor]
        protected RoadPlug() : base(null) { }
        public RoadPlug(SimulationObjectCollection world) : base(world)
        {
            forward = new();
            backward = new();
        }
       
        public RoadPlugView GetView(bool inverted) => new (this, inverted);

        //create a copy of this plug - copy all forward & backward vectors, keeping their local position, rotation and scale intact
        public RoadPlug Copy(SimulationObjectCollection world)
        {
            RoadPlug pl = new(world);
            foreach (var v in forward)
            {
                //if this road connection vector has no local space edit, and its' parent is road plug directly (used for unidirectional roads), create the same vector again. Else copy local transform
                if (v.parent is RoadPlug) pl.CreateForward();
                else pl.CreateForward(v.LocalPosition, v.LocalRotation, MathF.Abs(v.LocalScaleF));
            }
            foreach (var v in backward)
            {
                //same logic as for forward
                if (v.parent is RoadPlug) pl.CreateBackward();
                else pl.CreateBackward(v.LocalPosition, v.LocalRotation, MathF.Abs(v.LocalScaleF));
            }
            return pl;
        }
        //create new RoadConnectionVector with editable position, rotation and scale
        RoadConnectionVector Create(Vector2 pos, float rotation, float scale = 1) => new(new EditableRoadConnectionVector(this, pos, rotation, scale));
        //create a forward connection vector with given transform
        protected void CreateForward(Vector2 pos, float rotation, float scale=1) => forward.Add(Create(pos, rotation, scale));
        //create a backward connection vector with given transform
        protected void CreateBackward(Vector2 pos, float rotation, float scale = 1) => backward.Add(Create(pos, rotation, scale));

        //create a new, non-editable road connection vector. This is useful if there is only one road connection vector per road plug
        RoadConnectionVector Create() => new(this);
        //create a non-editable forward connection vector
        protected void CreateForward() => forward.Add(Create());
        //create a non-editable backward connection vector
        protected void CreateBackward() => backward.Add(Create());
    }





    class OneSideRoadPlug : RoadPlug
    {
        [JsonConstructor]
        private OneSideRoadPlug() { }

        //rotation and such managed by parent. We only care whether this goes forward or backwards. Create vector accordingly
        public OneSideRoadPlug(SimulationObjectCollection world, bool invert) : base(world)
        {
            if (invert) CreateBackward();
            else CreateForward();
        }
    }




    class TwoSideRoadPlug : RoadPlug
    {
        [JsonConstructor]
        private TwoSideRoadPlug() { }

        //rotation and such managed by parent. Create one vector for each direction.
        public TwoSideRoadPlug(SimulationObjectCollection world) : base(world)
        {
            CreateForward(Vector2.UnitY, 0);
            CreateBackward(-Vector2.UnitY, 180);
        }
    }



    //An object collection that supports drawing order and tracks the total amount of vehicles
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    class SimulationObjectDrawCollection : SimulationObjectListCollection
    {
        public const int DrawSteps = (int) DrawLayer.DRAW_LAYER_COUNT;
        public const int FinishSteps = (int) FinishPhase.PHASE_COUNT;
        
        public int VehicleCount = 0;

        public SimulationObjectDrawCollection(SimulationObjectCollection world) : base(world)
        {}
        //add object, and update counter
        public override void Add(SimulationObject val) => AddI(val);
        void AddI(SimulationObject val)
        {
            base.Add(val);
            if (val is Vehicle) VehicleCount++;
        }
        //remove object, and update counter
        public override void Remove(SimulationObject val) => RemoveI(val);
        void RemoveI(SimulationObject val)
        {
            base.RemoveDirect(val);
            if (val is Vehicle) VehicleCount--;
        }

        public void AddRange(IEnumerable<SimulationObject> new_objs)
        {
            foreach (var o in new_objs) AddI(o);
        }
        public void RemoveRange(IEnumerable<SimulationObject> remove)
        {
            foreach (var o in remove) RemoveI(o);
        }
        //draw objects in order. Done using brute force. I assume that the actual rendering will take much longer than going through all the objects a few times
        public void DrawWorld(SDLApp app, Transform camera)
        {
            for (int i = 0; i <= DrawSteps; i++)
            {
                foreach (var v in objects)
                {
                    v.Draw(app, camera, (DrawLayer) i);
                }
            }
        }
    }


    // A container that stores newly added/removed objects. Can be implemented to support multithreading.
    interface IRoadWorldObjectContainer : IEnumerable<SimulationObject>
    {
        void Add(SimulationObject o);
        void Clear();
    }


    //world settings - everything has to be set before creating camera and path planners
    [JsonObject(MemberSerialization.OptOut)]
    class WorldSettings
    {
        public Vector2 WorldSize;
        public Vector2 CameraPosition;

        public float CameraZ = 10;
        public float CameraZFrom = 2;
        public float CameraZTo = 40;
        public float CameraZoomSpeed = .05f;
        public float PathRandomizationFrom = 0.9f;
        public float PathRandomizationTo = 1.1f;

        //generate less vehicles if there are more than this number - can prevent deadlocks
        public int RecommendedVehicleCount = int.MaxValue;

        //set world size, then change camera position and z relative to it
        public void SetWorldSize(Vector2 worldSize)
        {
            WorldSize = worldSize;
            CameraPosition = worldSize / 2;
            CameraZ = worldSize.Max();
        }
    }



    [JsonObject(MemberSerialization.OptIn)]
    abstract class RoadWorld : SimulationObjectDrawCollection
    {
        //underlying road graph
        [JsonProperty]
        public RoadGraph Graph = new();

        [JsonProperty]
        public WorldSettings settings = new();

        [JsonProperty]
        DebugGrid debug_grid;



        //Can be used to set default curve coefficient and lane distance for objects created in the future
        public static float DefaultCurveCoeff { set => default_curve_coeff = value; }
        public static float Default2SideLaneDistance { set => default_2lane_dist = value; }

        //a container for adding and removing objects. Container can support multithreading
        protected abstract IRoadWorldObjectContainer AddObjects { get; }
        protected abstract IRoadWorldObjectContainer RemoveObjects { get; }

        //how many vehicles should spawn, starts at 100%
        public float VehicleGenerationIntensity = 1;

        
        public bool DebugGridEnabled { get => debug_grid.Enabled; set => debug_grid.Enabled = value; }





        //create both background rect and debug grid
        [JsonConstructor]
        public RoadWorld() : base(null)
        {
            //if loading using JSON, do not create any objects
            if (this is LoadableRoadWorld) return;
            _ = new BackgroundRect(this);
            debug_grid = new(this);
        }

        //finish, after every step, update active collection
        public virtual void Finish()
        {
            for (FinishPhase i = 0; (int)i < FinishSteps; i++)
            {
                base.Finish(i);
                UpdateObjects();
            }
        }

        //unfinish, then update active collection
        protected override void UnfinishI()
        {
            base.UnfinishI();
            UpdateObjects();
        }

        
        public override void Add(SimulationObject o) => AddObjects.Add(o);
        public override void Remove(SimulationObject o) => RemoveObjects.Add(o);

        //interact - go through all interactable objects, then add/remove the new ones to the active collection
        protected override void InteractI(Inputs inputs)
        {
            base.InteractI(inputs);
            UpdateObjects();
        }
        public void UpdateObjects()
        {
            AddRange(AddObjects);
            RemoveRange(RemoveObjects);
            AddObjects.Clear();
            RemoveObjects.Clear();
        }
        //get/return a path planner (for A* vehicle path search)
        public abstract PathPlanner GetPathPlanner();
        public abstract void ReturnPathPlanner(PathPlanner planner);

        //save to a JSON
        public void Save(string filename)
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings { PreserveReferencesHandling=PreserveReferencesHandling.Objects, ReferenceLoopHandling=ReferenceLoopHandling.Error});
            System.IO.File.WriteAllText($"maps/{filename}.json", json);
        }
        //Load from a JSON
        public void Load(string filename)
        {
            var world = JsonConvert.DeserializeObject<LoadableRoadWorld>(System.IO.File.ReadAllText($"maps/{filename}.json"), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All, PreserveReferencesHandling=PreserveReferencesHandling.All });
            CopyAll(world);
            foreach (var o in this) o.parent = this;
        }
        //copy everything from the other world to this one 
        void CopyAll(RoadWorld copy_from)
        {
            Graph = copy_from.Graph;
            debug_grid = copy_from.debug_grid;
            settings = copy_from.settings;
            //copy object collection
            base.CopyAll(copy_from);
        }

        public void RemoveAllVehicles()
        {
            foreach (SimulationObject o in objects)
            {
                if (o is Vehicle v) v.Destroy();
            }
            UpdateObjects();
        }


        //Create a garage at given road plug - combination of vehicle spawn and sink
        public void Garage(RoadPlugView v, Garage garage, float sink_weight)
        {
            RoadPlugView inv = v.Invert();
            //create garage if there are edges heading forward, and vehicle sink, if not disabled and there are edges backward
            bool creating_garage = inv.Forward.Count != 0;
            if (creating_garage) _ = new GarageObject(this, garage, inv);
            if (sink_weight != float.NegativeInfinity && inv.Backward.Count != 0) _ = new VehicleSink(this, sink_weight, inv, creating_garage);
        }
        public void Garage(RoadPlugView v, Garage garage, bool enable_sink = true) => Garage(v, garage, enable_sink ? 1 : float.NegativeInfinity);

        //create a road from given plug, and add a garage to its' end
        public Road GarageRoad(RoadPlugView plug, Garage garage, float sink_weight, float length = Constant.default_road_length)
        {
            Road garage_road = Road(plug, length);
            Garage(garage_road.A, garage, sink_weight);
            return garage_road;
        }
        public Road GarageRoad(RoadPlugView plug, Garage garage, bool enable_sink = true, float length = Constant.default_road_length) => GarageRoad(plug, garage, enable_sink ? 1 : float.NegativeInfinity, length);
    }


    //just a class that is not abstract and can be easily loaded from JSON
    //cannot really do anything, all properties should be copied to real RoadWorld after loading is finished
    class LoadableRoadWorld : RoadWorld
    {
        protected override IRoadWorldObjectContainer AddObjects => throw new NotImplementedException();
        protected override IRoadWorldObjectContainer RemoveObjects => throw new NotImplementedException();
        public override PathPlanner GetPathPlanner() => throw new NotImplementedException();
        public override void ReturnPathPlanner(PathPlanner planner) => throw new NotImplementedException();
    }
}
