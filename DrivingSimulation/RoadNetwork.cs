using System;
using System.Collections;
using System.Collections.Generic;

namespace DrivingSimulation
{
    abstract class DrivingSimulationObject
    {
        public abstract int DrawLayer { get; }

        protected abstract bool PreDraw { get; }

        public virtual bool Disabled { get => false; }

        protected DrivingSimulationObject(RoadWorld world)
        {
            if (world != null) world.Add(this);
        }
        public virtual void Finish() { }
        public virtual void Update(float dt) { }
        public virtual void PostUpdate() { }

        public void Draw(SDLApp app, Transform transform, int predraw)
        {
            if ((predraw == 1) == PreDraw || predraw == 2)
            {
                Draw(app, transform);
            }
        }
        protected virtual void Draw(SDLApp app, Transform transform) { }
    }



    struct RoadConnectionVector
    {
        public Vector2 direction;
        public GraphNode<NoData> node;
        public RoadConnectionVector(Vector2 direction, GraphNode<NoData> node)
        {
            this.direction = direction;
            this.node = node;
        }

        public RoadConnectionVector(RoadWorld world, Vector2 pos, Vector2 dir)
        {
            direction = dir;
            node = GraphNode<NoData>.NewOriginal(world, pos);
        }
        public void Draw(SDLApp a, Transform transform)
        {
            a.DrawArrow(Color.Red, transform.Apply(node.position), transform.Apply(node.position + direction.Normalized()));
        }

        public RoadConnectionVector Transform(Transform transform)
        {
            return new RoadConnectionVector(transform.ApplyDirection(direction), node.Transform(transform));
        }
        public RoadConnectionVector Copy(RoadWorld world)
        {
            return new RoadConnectionVector(direction, GraphNode<NoData>.NewOriginal(world, node.position));
        }
    }


    class RoadPlug : DrivingSimulationObject
    {
        readonly List<RoadConnectionVector> forward;
        readonly List<RoadConnectionVector> backward;
        bool inverted;

        public List<RoadConnectionVector> Forward { get => inverted ? backward : forward; }
        public List<RoadConnectionVector> Backward { get => inverted ? forward : backward; }

        public override int DrawLayer => 1;
        protected override bool PreDraw => true;
        public RoadPlug(RoadWorld world, List<RoadConnectionVector> forward, List<RoadConnectionVector> backward, bool inverted = false) : base(world)
        {
            this.forward = forward;
            this.backward = backward;
            this.inverted = inverted;
        }
        public RoadPlug(RoadWorld world, RoadConnectionVector forward, RoadConnectionVector backward) : this(world, new List<RoadConnectionVector>() { forward}, new List<RoadConnectionVector>() { backward})
        { }
        public RoadPlug(RoadWorld world, RoadConnectionVector forward) : this(world, new List<RoadConnectionVector>() { forward}, new List<RoadConnectionVector>())
        { }
        public RoadPlug Invert()
        {
            inverted = !inverted;
            return this;
        }
        public RoadPlug Transform(Transform transform)
        {
            for (int i = 0; i <  forward.Count; i++)  forward[i] =  forward[i].Transform(transform);
            for (int i = 0; i < backward.Count; i++) backward[i] = backward[i].Transform(transform);
            return this;
        }
        public RoadPlug Copy(RoadWorld world)
        {
            List<RoadConnectionVector> newf = new(), newb = new();
            forward.ForEach(x => {newf.Add(x.Copy(world)); });
            backward.ForEach(x => {newb.Add(x.Copy(world)); });

            return new RoadPlug(world, newf, newb, inverted);
        }
        public RoadPlug MovedCopy(RoadWorld world, Vector2 move, float road_width = 0)
        {
            var copy = Copy(world);
            if (road_width != 0)
            {
                copy.SetRoadWidth(road_width);
            }
            return copy.Transform(new MoveTransform(move));
        }
        public RoadPlug SetRoadWidth(float road_width)
        {
            float max_dist = float.NegativeInfinity;
            foreach (var x in forward)
            {
                foreach (var y in backward)
                {
                    max_dist = Math.Max(max_dist, (x.node.position - y.node.position).Length());
                }
            }
            Vector2 middle = GetPosition();
            return Transform(new ScaleFromPoint(middle, road_width / max_dist));
        }

        protected override void Draw(SDLApp app, Transform transform)
        {
            foreach (var x in  forward) x.Draw(app, transform);
            foreach (var x in backward) x.Draw(app, transform);
        }


        public Vector2 GetSize()
        {
            Vector2 min = new(float.PositiveInfinity);
            Vector2 max = new(float.NegativeInfinity);
            foreach (var c in forward)
            {
                min = Vector2.Min(min, c.node.position);
                max = Vector2.Max(max, c.node.position);
            }
            foreach (var c in backward)
            {
                min = Vector2.Min(min, c.node.position);
                max = Vector2.Max(max, c.node.position);
            }
            return max - min;
        }
        public Vector2 GetPosition()
        {
            return (forward.Sum(x => x.node.position) + backward.Sum(x => x.node.position)) / (Forward.Count + Backward.Count);   
        }
        public Vector2 GetDirection()
        {
            return (Forward.Sum(x => x.direction) - Backward.Sum(x => x.direction)) / (Forward.Count + Backward.Count);
        }
        public static List<Trajectory> Connect(RoadGraph graph, RoadPlug a, RoadPlug b, Transform transform = null)
        {
            return Connect(graph, a.Forward, b.Backward, transform).Merge(Connect(graph, b.Forward, a.Backward, transform));
        }
        public static List<Trajectory> Connect(RoadGraph graph, List<RoadConnectionVector> forw, List<RoadConnectionVector> back, Transform transform)
        {
            var list = new List<Trajectory>();
            if (forw.Count == 0 || back.Count == 0) return list;
            if (forw.Count == 1 || back.Count == 1)
            {
                foreach (var f in forw)
                {
                    foreach (var b in back)
                    {
                        list.Add(graph.Connect(f, b, transform));
                    }
                }
            }
            else if (forw.Count == back.Count)
            {
                for (int i = 0; i < forw.Count; i++)
                {
                    list.Add(graph.Connect(forw[i], back[i], transform));
                }
            }
            else
            {
                throw new InvalidOperationException("Cannot connect two road plugs: incompatible sizes.");
            }
            return list;
        }
    }





    class OneSideRoadPlug : RoadPlug
    {
        public OneSideRoadPlug(RoadWorld world, Vector2 pos, Vector2 dir) : base(world, new RoadConnectionVector(world, pos, dir))
        {}
    }




    class TwoSideRoadPlug : RoadPlug
    {
        public const float default_lane_dist = 0.5f;

        public TwoSideRoadPlug(RoadWorld world, Vector2 pos_f, Vector2 pos_b, Vector2 forward) : base(world, new RoadConnectionVector(world, pos_f, forward), new RoadConnectionVector(world, pos_b, -forward))
        {}
        private TwoSideRoadPlug(RoadWorld world, Vector2 pos, Vector2 dir_normed, Vector2 lane_vec, float curve_coeff) :
            base(world, new RoadConnectionVector(world, pos + lane_vec, dir_normed.Rotate90CW() * curve_coeff), new RoadConnectionVector(world, pos - lane_vec, dir_normed.Rotate270CW() * curve_coeff))
        {}
        public TwoSideRoadPlug(RoadWorld world, Vector2 pos, Vector2 dir_normal, float curve_coeff = 1, float lane_distance = default_lane_dist) :
            this(world, pos, dir_normal.Normalized(), dir_normal.Normalized() * lane_distance / 2, curve_coeff)
        {}
    }

    struct NoData { }

    class GraphNode<dataT>
    {
        public Vector2 position;
        public List<GraphEdge<dataT>> edges_forward;
        public List<GraphEdge<dataT>> edges_backward;
        public dataT data;
        private GraphNode(Vector2 position)
        {
            edges_forward = new();
            edges_backward = new();
            this.position = position;
        }
        public static GraphNode<NoData> NewOriginal(RoadWorld world, Vector2 pos)
        {
            var n = new GraphNode<NoData>(pos);
            world.Graph.AddNode(n);
            return n;
        }
        public GraphNode<T> Copy<T>()
        {
            return new GraphNode<T>(position);
        }

        public void AddForward(GraphEdge<dataT> e) { edges_forward.Add(e); }
        public void AddBackward(GraphEdge<dataT> e) { edges_backward.Add(e); }

        public override string ToString()
        {
            return $"Node at:{position}, Forward:{edges_forward.Count}, Back:{edges_backward.Count}";
        }
        public GraphNode<dataT> Transform(Transform transform)
        {
            position = transform.Apply(position);
            return this;
        }


    }


    class GraphNodeObject : DrivingSimulationObject
    {
        public override int DrawLayer => 1;
        protected override bool PreDraw => true;

        readonly GraphNode<NoData> node;
        readonly RoadWorld world;
        public GraphNodeObject(RoadWorld world, GraphNode<NoData> node) : base(world)
        {
            this.node = node;
            this.world = world;
        }
        protected override void Draw(SDLApp app, Transform transform)
        {
            if (node.edges_forward.Count == 1) node.edges_forward[0].trajectory.DrawDirectionArrows(app, transform, false);
            if (node.edges_backward.Count == 1) node.edges_backward[0].trajectory.DrawDirectionArrows(app, transform, true);
        }
        public override void Finish()
        {
            if (node.edges_forward.Count > 1)
            {
                CrysisPoint.CreateSplitCrysisPoint(world, node.edges_forward.ConvertAll(x => x.trajectory));
            }
            if (node.edges_backward.Count > 1)
            {
                CrysisPoint.CreateMergeCrysisPoint(world, node.edges_backward.ConvertAll(x => x.trajectory));
            }
        }
    }




    class GraphEdge<dataT>
    {
        public Trajectory trajectory;
        public GraphNode<dataT> node_from;
        public GraphNode<dataT> node_to;
        public GraphEdge(Trajectory t, GraphNode<dataT> from, GraphNode<dataT> to)
        {
            trajectory = t;
            node_from = from;
            node_to = to;
        }
    }




    class PathPlanner
    {
        readonly List<GraphNode<Search.A_star_data>> graph;
        readonly Random random;
        readonly float random_from;
        readonly float random_to;

        public PathPlanner(RoadGraph g)
        {
            graph = g.GetSearchableGraph<Search.A_star_data>();
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












    class DrivingSimulationObjectList : IEnumerable<DrivingSimulationObject>
    {
        List<DrivingSimulationObject> cleaned_values;
        public List<DrivingSimulationObject> values;
        int max_draw_layer = 0;
        public int VehicleCount = 0;
        public int Count => values.Count;

        public DrivingSimulationObject this[int key]
        {
            get => values[key];
        }

        public DrivingSimulationObjectList()
        {
            values = new();
            cleaned_values = new();
        }
        public void Add(DrivingSimulationObject val)
        {
            if (val is Vehicle) VehicleCount++;
            values.Add(val);
            max_draw_layer = Math.Max(val.DrawLayer, max_draw_layer);
        }
        public void PostUpdate()
        {
            cleaned_values.Clear();
            foreach (var val in values)
            {
                if (!val.Disabled)
                {
                    cleaned_values.Add(val);
                }
                else
                {
                    if (val is Vehicle) VehicleCount--;
                }
            }
            (cleaned_values, values) = (values, cleaned_values);
        }
        public void Add(IEnumerable<DrivingSimulationObject> new_objs)
        {
            foreach (var o in new_objs)
            {
                Add(o);
            }
        }
        public void Draw(SDLApp app, Transform transform, int predraw)
        {
            for (int i = 0; i <= max_draw_layer; i++)
            {
                foreach(var v in values)
                {
                    if (v.DrawLayer == i)
                    {
                        v.Draw(app, transform, predraw);
                    }
                }
            }
        }
        public static implicit operator List<DrivingSimulationObject>(DrivingSimulationObjectList l) => l.values;

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
        public IEnumerator<DrivingSimulationObject> GetEnumerator()
        {
            return values.GetEnumerator();
        }
    }


    class VehicleSinks
    {
        readonly List<(int i, float W)> sinks;
        readonly Random select_random;
        float max_w = float.NegativeInfinity;
        float div = 0;
        public VehicleSinks()
        {
            sinks = new();
            select_random = new Random();
        }
        public void Add(int spawn_, float weight = 1)
        {
            sinks.Add((spawn_, weight));
            max_w = Math.Max(max_w, weight);
            div = 0;
            foreach ((int _, float W) in sinks) div += MathF.Exp(W - max_w);
        }
        public int Select()
        {
            double k = select_random.NextDouble();
            foreach (var (i, W) in sinks)
            {
                k -= MathF.Exp(W - max_w) / div;
                if (k <= 0) return i;
            }
            Console.WriteLine("Softmax error");
            return sinks[0].i;
        }
    }









    class RoadGraph
    {
        readonly List<GraphNode<NoData>> graph_nodes;

        protected readonly VehicleSinks vehicle_sinks;

        public readonly Vector2 WorldSize;
        public virtual Vector2 CameraPosition => WorldSize / 2;
        public virtual float CameraZ => WorldSize.Max();
        public virtual float CameraZFrom => 2;
        public virtual float CameraZTo => 40;
        public virtual float CameraZoomSpeed => .5f;
        public virtual float PathRandomizationFrom => 0.9f;
        public virtual float PathRandomizationTo => 1.1f;

        //generate less vehicles if there are more than this number - can prevent deadlocks
        public virtual int RecommendedVehicleCount => int.MaxValue;

        protected readonly RoadWorld world;

        public RoadGraph(RoadWorld world, Vector2 worldSize)
        {
            graph_nodes = new();
            vehicle_sinks = new();
            WorldSize = worldSize;
            this.world = world;
            world.SetGraph(this);
        }


        public int FindNode(GraphNode<NoData> node)
        {
            return FindNode(graph_nodes, node);
        }
        protected static int FindNode<T>(List<GraphNode<T>> nodes, GraphNode<T> node)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == node) return i;
            }
            return -1;
        }
        static GraphEdge<Tto> ConvertEdge<Tfrom, Tto>(List<GraphNode<Tfrom>> from_nodes, List<GraphNode<Tto>> to_nodes, GraphEdge<Tfrom> edge)
        {
            return new GraphEdge<Tto>(edge.trajectory, to_nodes[FindNode(from_nodes, edge.node_from)], to_nodes[FindNode(from_nodes, edge.node_to)]);
        }

        public List<GraphNode<T>> GetSearchableGraph<T>()
        {
            List<GraphNode<T>> nodes = new();
            foreach (GraphNode<NoData> node in graph_nodes)
            {
                nodes.Add(node.Copy<T>());
            }
            for (int i = 0; i < nodes.Count; i++)
            {
                foreach (GraphEdge<NoData> edge in graph_nodes[i].edges_forward)
                {
                    nodes[i].AddForward(ConvertEdge(graph_nodes, nodes, edge));
                }
                foreach (GraphEdge<NoData> edge in graph_nodes[i].edges_backward)
                {
                    nodes[i].AddBackward(ConvertEdge(graph_nodes, nodes, edge));
                }
            }
            return nodes;
        }


        public void AddNode(GraphNode<NoData> node)
        {
            graph_nodes.Add(node);
            _ = new GraphNodeObject(world, node);
        }
        


        public int SelectVehicleSink()
        {
            return vehicle_sinks.Select();
        }


        public void AddVehicleSink(int index, float weight)
        {
            vehicle_sinks.Add(index, weight);
        }


        public Trajectory Connect(RoadConnectionVector p1, RoadConnectionVector p2, Transform transform = null)
        {
            Trajectory t = Trajectory.FromDir(world, p1.node.position, p1.direction, p2.node.position, -p2.direction, transform);
            GraphEdge<NoData> e = new(t, p1.node, p2.node);
            p1.node.AddForward(e);
            p2.node.AddBackward(e);
            return t;
        }
    }


    class DebugGrid : DrivingSimulationObject
    {
        public override int DrawLayer => 0;
        protected override bool PreDraw => false;

        Vector2 world_size;

        public bool Enabled = false;
        public DebugGrid(RoadWorld world) : base(world)
        {
            world_size = world.Graph.WorldSize;
        }

        protected override void Draw(SDLApp app, Transform transform)
        {
            if (!Enabled) return;
            for (int x = 0; x < world_size.X; x++)
            {
                Color c = (x % 10) == 0 ? Color.Blue : ((x % 5 == 0) ? Color.Magenta : Color.DarkGray);
                app.DrawLine(c, transform.Apply(new Vector2(x, 0)), transform.Apply(new Vector2(x, world_size.Y)));
            }
            for (int y = 0; y < world_size.Y; y++)
            {
                Color c = (y % 10) == 0 ? Color.Green : ((y % 5 == 0) ? Color.Yellow : Color.DarkGray);
                app.DrawLine(c, transform.Apply(new Vector2(0, y)), transform.Apply(new Vector2(world_size.X, y)));
            }
        }
    }






    abstract class RoadWorld : IDisposable
    {
        public RoadGraph Graph;

        protected DrivingSimulationObjectList Objects;

        bool initialization_complete = false;

        public float VehicleGenerationIntensity = 1;

        DebugGrid debug_grid;
        public bool DebugGridEnabled { get => debug_grid.Enabled; set => debug_grid.Enabled = value; }

        public int VehicleCount => Objects.VehicleCount;

        public RoadWorld()
        {
            Objects = new();
        }
        public void SetGraph(RoadGraph graph)
        {
            Graph = graph;
            debug_grid = new(this);
        }

        //predraw - 0 -> dont predraw, 1 -> do predraw, 2 -> do both non- and predraw objects
        public void Draw(SDLApp app, Transform transform, int prerender)
        {
            if (prerender != 0) app.DrawRect(Color.LightGray, transform.Apply(new Vector2(0, 0)), transform.ApplyDirection(Graph.WorldSize));
            Objects.Draw(app, transform, prerender);
        }

        public void Add(DrivingSimulationObject o)
        {
            if (initialization_complete)
            {
                AddFrame(o);
            }
            else
            {
                Objects.Add(o);
            }
        }
        public virtual void Finish()
        {
            initialization_complete = true;
        }
        
        public abstract void Update(float dt);
        public virtual void PostUpdate()
        {
            Objects.PostUpdate();
            AddNewObjects();
        }
        public abstract void Draw(SDLApp app, Transform transform);



        public void OverlappingCarsAction(Transform camera_transform, float mouse_x, float mouse_y, Action<Vehicle> action)
        {
            Vector2 world_pos = camera_transform.Inverse(new Vector2(mouse_x, mouse_y));
            foreach (object o in Objects.values)
            {
                if (o is Vehicle vehicle)
                {
                    if (vehicle.Overlaps(world_pos))
                    {
                        action(vehicle);
                    }
                }
            }
        }

        public abstract void AddFrame(DrivingSimulationObject obj);
        public abstract void AddNewObjects();
        public abstract PathPlanner GetPathPlanner();
        public abstract void ReturnPathPlanner(PathPlanner planner);
        public void Dispose() { }
    }



    abstract class PreRenderableRoadWorld : RoadWorld, IDisposable
    {
        class PreRenderedWorld : IDisposable
        {
            readonly SDLApp app;
            readonly RoadWorld world;
            const int base_resolution = 45;
            const int mipmap_count = 4;
            readonly List<Texture> textures;
            readonly Vector2 screen_size;

            Vector2 WorldSize => world.Graph.WorldSize;

            public PreRenderedWorld(SDLApp app, RoadWorld world)
            {
                this.app = app;
                this.world = world;
                textures = new();
                screen_size = app.ScreenSize;
            }
            public void Finish()
            {
                for (int i = 0; i < mipmap_count; i++)
                {
                    int resolution = base_resolution * (int)MathF.Pow(2, i);
                    textures.Add(app.CreateTexture(WorldSize.Xi * resolution, WorldSize.Yi * resolution));
                    app.SetRenderTarget(textures.Last());
                    app.Fill(Color.LightGray);
                    world.Draw(app, new ScaleTransform(resolution), 1);
                }
                app.UnsetRenderTarget();
            }
            public void Draw(SDLApp app, Transform transform)
            {
                Vector2 tex_scr_pos = transform.Apply(Vector2.Zero).Clamp(Vector2.Zero, screen_size);
                Vector2 tex_scr_end = tex_scr_pos + transform.ApplyDirection(WorldSize).Clamp(Vector2.Zero, screen_size);
                Vector2 tex_pos = transform.Inverse(tex_scr_pos);
                Vector2 tex_size = transform.Inverse(tex_scr_end) - tex_pos;

                float requested_resolution = (tex_scr_end.X - tex_scr_pos.X) / tex_size.X;
                int tex = Math.Clamp((int)(MathF.Log2(requested_resolution / base_resolution) + 0.5f), 0, mipmap_count - 1);
                int resolution = base_resolution * (int)MathF.Pow(2, tex);

                tex_pos *= resolution; tex_size *= resolution;
                app.DrawTexture(textures[tex], SDLExt.NewRect(tex_pos.Xi, tex_pos.Yi, tex_size.Xi, tex_size.Yi), SDLExt.NewFRect(tex_scr_pos, tex_scr_end - tex_scr_pos));
            }
            public void Dispose()
            {
                foreach (var t in textures) t.Dispose();
            }
        }

        readonly PreRenderedWorld prerendered;

        bool Prerender => prerendered != null;
        

        public PreRenderableRoadWorld(SDLApp app, bool prerender) : base()
        {
            if (prerender) prerendered = new(app, this);
        }

        public override void Finish()
        {
            if (Prerender) prerendered.Finish();
            base.Finish();
        }
        public override void Draw(SDLApp app, Transform transform)
        {
            if (Prerender) prerendered.Draw(app, transform);
            else app.DrawRect(Color.LightGray, transform.Apply(new Vector2(0, 0)), transform.ApplyDirection(Graph.WorldSize));

            base.Draw(app, transform, Prerender ? 0 : 2);
        }
        public new void Dispose()
        {
            if (Prerender) prerendered.Dispose();
        }
    }
}
