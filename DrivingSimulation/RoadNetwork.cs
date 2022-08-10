using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;

namespace DrivingSimulation
{
    [JsonObject(MemberSerialization.OptIn)]
    class RoadConnectionVector : SimulationObject
    {
        [JsonProperty]
        public GraphNode<BaseData> node;

        public EditableRoadConnectionVector Placed => (EditableRoadConnectionVector)parent;

        [JsonConstructor]
        private RoadConnectionVector() : base(null) { }
        public RoadConnectionVector(SimulationObjectCollection world) : base(world)
        {
            node = GraphNode<BaseData>.NewOriginal(world.GetParentWorld().Graph, world, this);
        }
        public RoadConnectionVectorView GetView(bool inverted)
        {
            return new RoadConnectionVectorView(this, inverted);
        }
        protected override void DestroyI()
        {
            base.DestroyI();
            node.Destroy();
        }
    }



    [JsonObject(MemberSerialization.OptIn)]
    class RoadConnectionVectorView
    {
        [JsonProperty]
        readonly RoadConnectionVector v;
        [JsonProperty]
        readonly bool invert;


        public RoadConnectionVector Connection => v;
        public Vector2 From => v.WorldPosition;
        public Vector2 To => v.WorldPosition + (invert ? -1 : 1) * v.WorldDirection;

        [JsonConstructor]
        private RoadConnectionVectorView() { }
        public RoadConnectionVectorView(RoadConnectionVector v, bool invert)
        {
            this.v = v;
            this.invert = invert;
        }

        public void Connect(GraphEdgeReference<BaseData> edge)
        {
            if (invert) v.node.AddBackward(edge);
            else v.node.AddForward(edge);
        }
        public GraphEdge<BaseData> RemoveForwardEdge(Trajectory t)
        {
            return Remove(v.node.edges_forward, t);
        }
        public GraphEdge<BaseData> RemoveBackwardEdge(Trajectory t)
        {
            return Remove(v.node.edges_backward, t);
        }
        GraphEdge<BaseData> Remove(List<GraphEdgeReference<BaseData>> l, Trajectory t)
        {
            var g = v.GetParentWorld().Graph;
            var e = l.Where(e => e.Get(g).trajectory == t).ToList();
            if (e.Count != 1) throw new Exception("Found invalid number of matching edges");
            return e[0].Get(g);
        }
    }


    [JsonObject(MemberSerialization=MemberSerialization.OptIn)]
    struct RoadPlugView
    {
        [JsonProperty(IsReference = true)]
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
        public Vector2 GetWorldPosition()
        {
            return (Forward.Sum(x => x.WorldPosition) + Backward.Sum(x => x.WorldPosition)) / (Forward.Count + Backward.Count);
        }
        public Vector2 GetWorldDirection()
        {
            return (Forward.Sum(x => x.WorldDirection) - Backward.Sum(x => x.WorldDirection)) / (Forward.Count + Backward.Count);
        }
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
        public List<Trajectory> Connect(SimulationObjectCollection graph, RoadPlugView opposing)
        {
            return Connect(graph, Forward, opposing.Backward).Merge(Connect(graph, opposing.Forward, Backward));
        }
        public static List<Trajectory> Connect(SimulationObjectCollection graph, List<RoadConnectionVector> forw, List<RoadConnectionVector> back)
        {
            var list = new List<Trajectory>();
            if (forw.Count == 0 || back.Count == 0) return list;
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
            else if (forw.Count == back.Count)
            {
                for (int i = 0; i < forw.Count; i++)
                {
                    list.Add(graph.Connect(forw[i], back[i]));
                }
            }
            else
            {
                throw new InvalidOperationException("Cannot Wr.Connect two road plugs: incompatible sizes.");
            }
            return list;
        }
        public RoadPlugView Invert()
        {
            return new RoadPlugView(plug, !inverted);
        }
        public RoadPlugView MovedCopy(float dist)
        {
            RoadPlug copy = plug.Copy(new EditableRoadPlug(plug.GetParentWorld(), plug.WorldPosition + GetWorldDirection().Normalized() * dist * (inverted ? -1 : 1), plug.WorldDirection.Rotation(), plug.WorldSize.Abs()));
            return copy.GetView(false);
        }
    }


    [JsonObject(MemberSerialization.OptIn)]
    class RoadPlug : SimulationObjectListCollection
    {
        [JsonProperty]
        public readonly List<RoadConnectionVector> forward;
        [JsonProperty]
        public readonly List<RoadConnectionVector> backward;

        public EditableRoadPlug Placed => (EditableRoadPlug) parent;

        [JsonConstructor]
        protected RoadPlug() : base(null) { }
        public RoadPlug(SimulationObjectCollection world) : base(world)
        {
            forward = new();
            backward = new();
        }
        public RoadPlugView GetView(bool inverted)
        {
            return new RoadPlugView(this, inverted);
        }
        public RoadPlug Copy(SimulationObjectCollection world)
        {
            RoadPlug pl = new(world);
            foreach (var v in forward) pl.CreateForward(v.LocalPosition, v.LocalRotation, MathF.Abs(v.LocalScaleF));
            foreach (var v in backward) pl.CreateBackward(v.LocalPosition, v.LocalRotation, MathF.Abs(v.LocalScaleF));
            return pl;
        }
        RoadConnectionVector Create(Vector2 pos, float rotation, float scale = 1)
        {
            return new RoadConnectionVector(new EditableRoadConnectionVector(this, pos, rotation, scale));
        }
        protected void CreateForward(Vector2 pos, float rotation, float scale=1)
        {
            forward.Add(Create(pos, rotation, scale));
        }
        protected void CreateBackward(Vector2 pos, float rotation, float scale = 1)
        {
            backward.Add(Create(pos, rotation, scale));
        }
    }





    class OneSideRoadPlug : RoadPlug
    {
        [JsonConstructor]
        private OneSideRoadPlug() { }
        public OneSideRoadPlug(SimulationObjectCollection world, bool invert) : base(world)
        {
            if (invert) CreateBackward(Vector2.Zero, 0);
            else CreateForward(Vector2.Zero, 0);
        }
    }




    class TwoSideRoadPlug : RoadPlug
    {
        [JsonConstructor]
        private TwoSideRoadPlug() { }
        public TwoSideRoadPlug(SimulationObjectCollection world, float curve_coeff) : base(world)
        {
            CreateForward(Vector2.UnitY, 0, curve_coeff);
            CreateBackward(-Vector2.UnitY, 180, curve_coeff);
        }
    }



    [JsonObject(MemberSerialization.OptIn)]
    struct GraphEdgeReference<dataT>
    {
        [JsonProperty]
        readonly int index;
        public GraphEdgeReference(int i)
        {
            index = i;
        }
        public GraphEdge<dataT> Get(GraphNodeCollection<dataT> graph)
        {
            return graph.GetEdge(index);
        }
    } 
    


    struct BaseData {
        public GraphNodeObject obj;
        public BaseData(GraphNodeObject o) => obj = o;
    }



    [JsonObject(MemberSerialization.OptIn)]
    class GraphNode<dataT>
    {
        [JsonProperty(IsReference = true)]
        public RoadConnectionVector parent;
        [JsonProperty(IsReference = true)]
        public List<GraphEdgeReference<dataT>> edges_forward;
        [JsonProperty(IsReference = true)]
        public List<GraphEdgeReference<dataT>> edges_backward;
        [JsonProperty]
        public dataT data;

        public Vector2 Position => parent.WorldPosition;

        [JsonConstructor]
        private GraphNode() {}
        private GraphNode(GraphNodeCollection<dataT> graph, RoadConnectionVector parent)
        {
            edges_forward = new();
            edges_backward = new();
            this.parent = parent;
            graph.AddNode(this);
        }
        public static GraphNode<BaseData> NewOriginal(GraphNodeCollection<BaseData> graph, SimulationObjectCollection world, RoadConnectionVector parent)
        {
            var n = new GraphNode<BaseData>(graph, parent);
            n.SetData(new BaseData(new(world.GetParentWorld(), n)));
            return n;
        }
        public void SetData(dataT data)
        {
            this.data = data;
        }
        public GraphNode<T> Copy<T>(GraphNodeCollection<T> graph)
        {
            return new GraphNode<T>(graph, parent);
        }

        public void AddForward(GraphEdgeReference<dataT> e) { edges_forward.Add(e); }
        public void AddBackward(GraphEdgeReference<dataT> e) { edges_backward.Add(e); }

        public override string ToString()
        {
            return $"Node(Forward:{edges_forward.Count}, Back:{edges_backward.Count})";
        }
        public void Destroy()
        {
            if (this is GraphNode<BaseData> n)
            {
                var g = parent.GetParentWorld().Graph;
                g.RemoveNode(n);
                foreach (GraphEdgeReference<BaseData> e in n.edges_forward) e.Get(g).Destroy();
                foreach (GraphEdgeReference<BaseData> e in n.edges_backward) e.Get(g).Destroy();
                n.data.obj.Destroy();
            }
        }
    }


    [JsonObject(MemberSerialization.OptIn)]
    class GraphEdge<dataT>
    {
        [JsonProperty]
        public Trajectory trajectory;
        [JsonProperty]
        public GraphNode<dataT> node_from;
        [JsonProperty]
        public GraphNode<dataT> node_to;
        public bool disabled = false;


        [JsonConstructor]
        private GraphEdge() { }
        public GraphEdge(GraphNodeCollection<dataT> graph, Trajectory t, GraphNode<dataT> from, GraphNode<dataT> to)
        {
            trajectory = t;
            node_from = from;
            node_to = to;
            graph.AddEdge(this);
        }
        public void Destroy()
        {
            bool x = disabled;
            disabled = true;
            if (!x) trajectory.Destroy();
            //disabled = true;//node_from.parent.GetParentWorld().Graph.RemoveEdge(e);
        }
    }





    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    class GraphNodeObject : SimulationObject
    {
        public override DrawLayer DrawZ => DrawLayer.TRAJECTORY_ARROWS;

        [JsonProperty (IsReference = true)]
        readonly GraphNode<BaseData> node;

        RoadWorld World => (RoadWorld)parent;

        [JsonConstructor]
        private GraphNodeObject() : base(null) { }
        public GraphNodeObject(RoadWorld world, GraphNode<BaseData> node) : base(world)
        {
            this.node = node;
        }
        protected override void DrawI(SDLApp app, Transform camera)
        {
            if (node.edges_forward.Count == 1) node.edges_forward[0].Get(World.Graph).trajectory.DrawDirectionArrows(app, camera, false);
            else if (node.edges_backward.Count == 1) node.edges_backward[0].Get(World.Graph).trajectory.DrawDirectionArrows(app, camera, true);
        }
        protected override void FinishI(FinishPhase phase)
        {
            base.FinishI(phase);
            var g = World.Graph;
            if (phase == FinishPhase.CREATE_CRYSIS_POINTS)
            {
                var filter = (List<GraphEdgeReference<BaseData>> e) => e.ConvertAll(x => x.Get(g)).Where(x => !x.disabled).ToList();
                var filtered_f = filter(node.edges_forward);
                var filtered_b = filter(node.edges_backward);
                if (filtered_f.Count > 1)
                {
                    CrysisPoint.CreateSplitCrysisPoint(World, filtered_f.ConvertAll(x => x.trajectory));
                }
                if (filtered_b.Count > 1)
                {
                    CrysisPoint.CreateMergeCrysisPoint(World, filtered_b.ConvertAll(x => x.trajectory));
                }
            }
        }
    }








    class PathPlanner
    {
        readonly GraphNodeCollection<Search.A_star_data> graph;
        readonly Random random;
        readonly float random_from;
        readonly float random_to;

        public PathPlanner(RoadGraph g)
        {
            graph = g.CopyWithDataT<Search.A_star_data>();
            random = new Random();
            random_from = g.PathRandomizationFrom;
            random_to = g.PathRandomizationTo;
        }
        public Queue<Trajectory> PlanPath(int from, int to)
        {
            return Search.FindAPath(graph, from, to, random, random_from, random_to);
        }
        public Random GetRandom()
        {
            return random;
        }
    }




    class BufferedCollection<ContainerT, T> : IEnumerable<T> where ContainerT : ICollection<T>, new()
    {
        public readonly ContainerT values;
        readonly ContainerT add_values;
        readonly ContainerT remove_values;

        public int Count => values.Count;

        public BufferedCollection()
        {
            values = new();
            add_values = new();
            remove_values = new();
        }

        public void Add(T val)
        {
            add_values.Add(val);
        }
        public void Remove(T val)
        {
            remove_values.Add(val);
        }
        
        public void Update()
        {
            foreach (T val in add_values) values.Add(val);
            foreach (T val in remove_values)
            {
                if (!values.Remove(val))
                {
                    throw new Exception("Removing object failed");
                }
            }
            add_values.Clear();
            remove_values.Clear();
        }
        public static implicit operator ContainerT(BufferedCollection<ContainerT, T> a) => a.values;
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public IEnumerator<T> GetEnumerator()
        {
            return values.GetEnumerator();
        }
    }





    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    class DrivingSimulationObjectList : IEnumerable<SimulationObject>
    {
        [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
        public readonly BufferedCollection<List<SimulationObject>, SimulationObject> values;

        public const int DrawSteps = (int)SimulationObject.DrawLayer.DRAW_LAYER_COUNT;
        public const int FinishSteps = (int) SimulationObject.FinishPhase.PHASE_COUNT;
        
        public int VehicleCount = 0;

        public DrivingSimulationObjectList()
        {
            values = new();
        }
        public void Add(SimulationObject val)
        {
            values.Add(val);
            if (val is Vehicle) VehicleCount++;
        }
        public void Remove(SimulationObject val)
        {
            values.Remove(val);
            if (val is Vehicle) VehicleCount--;
        }

        public void UpdateValues() => values.Update();
        public void Add(IEnumerable<SimulationObject> new_objs)
        {
            foreach (var o in new_objs)
            {
                Add(o);
            }
        }
        public void Draw(SDLApp app, Transform camera)
        {
            for (int i = 0; i <= DrawSteps; i++)
            {
                foreach (var v in values.values)
                {
                    v.Draw(app, camera, (SimulationObject.DrawLayer) i);
                }
            }
        }
        public static implicit operator List<SimulationObject>(DrivingSimulationObjectList l) => l.values;

        public List<SimulationObject> Get() => values;

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public IEnumerator<SimulationObject> GetEnumerator()
        {
            return values.GetEnumerator();
        }
    }

    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    class VehicleSinks
    {
        [JsonProperty]
        readonly List<(GraphNode<BaseData> n, float W)> sinks;
        readonly Random select_random;
        [JsonProperty]
        float max_w = float.NegativeInfinity;
        [JsonProperty]
        float div = 0;
        public VehicleSinks()
        {
            sinks = new();
            select_random = new Random();
        }
        public void Add(GraphNode<BaseData> spawn_, float weight = 1)
        {
            sinks.Add((spawn_, weight));
            max_w = Math.Max(max_w, weight);
            div = 0;
            foreach ((GraphNode<BaseData> _, float W) in sinks) div += MathF.Exp(W - max_w);
        }
        public GraphNode<BaseData> Select()
        {
            double k = select_random.NextDouble();
            foreach (var (i, W) in sinks)
            {
                k -= MathF.Exp(W - max_w) / div;
                if (k <= 0) return i;
            }
            Console.WriteLine("Softmax error");
            return sinks[0].n;
        }
    }



    [JsonObject(MemberSerialization.OptIn)]
    class GraphNodeCollection<dataT>
    {
        [JsonProperty]
        readonly List<GraphNode<dataT>> nodes;
        [JsonProperty]
        readonly List<GraphEdge<dataT>> edges;

        public List<GraphNode<dataT>> Nodes => nodes;
        
        public GraphNodeCollection() : this(new()) {}
        public GraphNodeCollection(List<GraphNode<dataT>> graph_nodes)
        {
            nodes = graph_nodes;
            edges = new();
        }

        public void AddEdge(GraphEdge<dataT> edge) => edges.Add(edge);
        public GraphEdge<dataT> GetEdge(int i) => edges[i];
        public int FindEdge(GraphEdge<dataT> edge) => edges.FindIndex(x => x == edge);
        public void RemoveEdge(GraphEdge<dataT> edge) => edges.Remove(edge);

        public void AddNode(GraphNode<dataT> node) => nodes.Add(node);
        public GraphNode<dataT> GetNode(int i) => nodes[i];
        public int FindNode(GraphNode<dataT> node) => nodes.FindIndex(x => x == node);
        public void RemoveNode(GraphNode<dataT> node) => nodes.Remove(node);


        public GraphNodeCollection<T> CopyWithDataT<T>()
        {
            GraphNodeCollection<T> copy = new();
            foreach (GraphNode<dataT> node in nodes) node.Copy(copy);
            CopyEdges(copy);
            return copy;
        }


        private GraphEdgeReference<Tto> ConvertEdge<Tto>(GraphNodeCollection<Tto> to, GraphEdge<dataT> edge)
        {
            GraphEdge<Tto> e = new(to, edge.trajectory, to.GetNode(FindNode(edge.node_from)), to.GetNode(FindNode(edge.node_to)));
            return new GraphEdgeReference<Tto>(to.FindEdge(e));
        }

        private void CopyEdges<T>(GraphNodeCollection<T> copy_to)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                foreach (GraphEdgeReference<dataT> edge in GetNode(i).edges_forward)
                {
                    if (edge.Get(this).disabled) continue;
                    copy_to.GetNode(i).AddForward(ConvertEdge(copy_to, edge.Get(this)));
                }
                foreach (GraphEdgeReference<dataT> edge in GetNode(i).edges_backward)
                {
                    if (edge.Get(this).disabled) continue;
                    copy_to.GetNode(i).AddBackward(ConvertEdge(copy_to, edge.Get(this)));
                }
            }
        }
    }





    [JsonObject(MemberSerialization.OptIn)]
    class RoadGraph : GraphNodeCollection<BaseData>
    {
        [JsonProperty]
        protected readonly VehicleSinks vehicle_sinks;
        [JsonProperty]
        public readonly Vector2 WorldSize;

        public Vector2 CameraPosition => WorldSize / 2;
        [JsonProperty]
        public float CameraZ;
        [JsonProperty]
        public float CameraZFrom = 2;
        [JsonProperty]
        public float CameraZTo = 40;
        [JsonProperty]
        public float CameraZoomSpeed = .1f;
        [JsonProperty]
        public float PathRandomizationFrom = 0.9f;
        [JsonProperty]
        public float PathRandomizationTo = 1.1f;

        [JsonProperty]
        //generate less vehicles if there are more than this number - can prevent deadlocks
        public int RecommendedVehicleCount = int.MaxValue;

        [JsonProperty(IsReference = true)]
        protected RoadWorld world;

        protected SimulationObjectCollection Wr => world;

        [JsonConstructor]
        private RoadGraph() {}
        public RoadGraph(RoadWorld world, Vector2 worldSize)
        {
            vehicle_sinks = new();
            WorldSize = worldSize;
            CameraZ = WorldSize.Max();
            this.world = world;
            world.SetGraph(this);
        }
        public void SetWorld(RoadWorld world)
        {
            this.world = world;
        }
        public GraphNode<BaseData> SelectVehicleSink()
        {
            return vehicle_sinks.Select();
        }
        public void AddVehicleSink(GraphNode<BaseData> node, float weight)
        {
            vehicle_sinks.Add(node, weight);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    class DebugGrid : SimulationObject
    {
        public override DrawLayer DrawZ => DrawLayer.GROUND;

        [JsonProperty]
        Vector2 world_size;

        public bool Enabled = false;

        [JsonConstructor]
        private DebugGrid() : base(null) { }
        public DebugGrid(RoadWorld world) : base(world)
        {
            world_size = world.Graph.WorldSize;
        }

        protected override void DrawI(SDLApp app, Transform camera)
        {
            if (!Enabled) return;
            for (int x = 0; x < world_size.X; x++)
            {
                Color c = (x % 10) == 0 ? Color.Blue : ((x % 5 == 0) ? Color.Magenta : Color.DarkGray);
                app.DrawLine(c, new Vector2(x, 0), new Vector2(x, world_size.Y), camera);
            }
            for (int y = 0; y < world_size.Y; y++)
            {
                Color c = (y % 10) == 0 ? Color.Green : ((y % 5 == 0) ? Color.Yellow : Color.DarkGray);
                app.DrawLine(c, new Vector2(0, y), new Vector2(world_size.X, y), camera);
            }
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    class BackgroundRect : SimulationObject
    {
        public override DrawLayer DrawZ => DrawLayer.GROUND;

        [JsonProperty]
        Vector2 world_size;


        [JsonConstructor]
        private BackgroundRect() : base(null) { }
        public BackgroundRect(SimulationObjectCollection world, Vector2 world_size) : base(world)
        {
            this.world_size = world_size;
        }

        protected override void DrawI(SDLApp app, Transform camera)
        {
            app.DrawRect(Color.LightGray, new Rect(Vector2.Zero, world_size), camera);
        }
    }




    interface IRoadWorldObjectContainer : IEnumerable<SimulationObject>
    {
        void Add(SimulationObject o);
        void Clear();
    }



    [JsonObject(MemberSerialization.OptIn)]
    abstract class RoadWorld : SimulationObjectCollection
    {
        [JsonProperty]
        public RoadGraph Graph;

        [JsonProperty]
        protected DrivingSimulationObjectList Objects = new();

        [JsonProperty]
        DebugGrid debug_grid;

        protected abstract IRoadWorldObjectContainer AddObjects { get; }
        protected abstract IRoadWorldObjectContainer RemoveObjects { get; }


        public float VehicleGenerationIntensity = 1;

        public override DrawLayer DrawZ => DrawLayer.WORLD;

        void CopyAll(RoadWorld copy_from)
        {
            Graph = copy_from.Graph;
            Objects = copy_from.Objects;
            debug_grid = copy_from.debug_grid;
            VehicleGenerationIntensity = copy_from.VehicleGenerationIntensity;
        }
       

        public bool DebugGridEnabled { get => debug_grid.Enabled; set => debug_grid.Enabled = value; }
        public int VehicleCount => Objects.VehicleCount;

        public RoadWorld() : base(null)
        { }

        public void SetGraph(RoadGraph graph)
        {
            Graph = graph;
            debug_grid = new(this);
            _ = new BackgroundRect(this, Graph.WorldSize);
        }

        protected override void DrawI(SDLApp app, Transform camera)
        {
            Objects.Draw(app, camera);
        }
        public abstract void Finish();

        public override void Add(SimulationObject o)
        {
            if (Finished)
            {
                AddFrame(o);
            }
            else
            {
                Objects.Add(o);
            }
        }
        public override void Remove(SimulationObject o)
        {
            if (Finished)
            {
                RemoveFrame(o);
            }
            else
            {
                Objects.Remove(o);
            }
        }

        protected override void PostUpdateI()
        {
            base.PostUpdateI();
            AddNewObjects();
            Objects.UpdateValues();
        }
        protected override void InteractI(Inputs inputs)
        {
            base.InteractI(inputs);
            foreach (var o in Objects) o.Interact(inputs);
            AddNewObjects();
            Objects.UpdateValues();
        }

        public void AddFrame(SimulationObject obj)
        {
            AddObjects.Add(obj);
        }
        public void RemoveFrame(SimulationObject obj)
        {
            RemoveObjects.Add(obj);
        }
        public void AddNewObjects()
        {
            foreach (var o in AddObjects) Objects.values.Add(o);
            foreach (var o in RemoveObjects) Objects.values.Remove(o);
            Objects.UpdateValues();
            AddObjects.Clear();
            RemoveObjects.Clear();
        }
        public abstract PathPlanner GetPathPlanner();
        public abstract void ReturnPathPlanner(PathPlanner planner);

        public void Save(string filename)
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings { PreserveReferencesHandling=PreserveReferencesHandling.Objects, ReferenceLoopHandling=ReferenceLoopHandling.Error, MaxDepth=128});
            System.IO.File.WriteAllText($"maps/{filename}.json", json);
        }
        public void Load(string filename)
        {
            var world = JsonConvert.DeserializeObject<LoadableRoadWorld>(System.IO.File.ReadAllText($"maps/{filename}.json"), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All, PreserveReferencesHandling=PreserveReferencesHandling.All, MaxDepth=256 });
            CopyAll(world);
            Graph.SetWorld(this);
            foreach (var o in Objects) o.parent = this;
        }
    }

    class LoadableRoadWorld : RoadWorld
    {
        protected override IRoadWorldObjectContainer AddObjects => throw new NotImplementedException();
        protected override IRoadWorldObjectContainer RemoveObjects => throw new NotImplementedException();
        public override void Finish() => throw new NotImplementedException();
        protected override void UpdateI(float dt) => throw new NotImplementedException();
        protected override void InteractI(Inputs inputs) => throw new NotImplementedException();
        public override PathPlanner GetPathPlanner() => throw new NotImplementedException();
        public override void ReturnPathPlanner(PathPlanner planner) => throw new NotImplementedException();

    }
}
