using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DrivingSimulation
{


    //
    struct BaseData
    {
        public GraphNodeObject obj;
        public BaseData(GraphNodeObject o) => obj = o;
    }


    //dataT is additional data attached to this node - this is a GraphNodeObject reference when using BaseData above, special dataT is used for A* searchable graphs
    [JsonObject(MemberSerialization.OptIn)]
    class GraphNode<dataT>
    {
        //the road connection vector this graph node represents
        [JsonProperty]
        public RoadConnectionVector parent;
        [JsonProperty]
        public List<GraphEdgeReference<dataT>> edges_forward;
        [JsonProperty]
        public List<GraphEdgeReference<dataT>> edges_backward;
        //additional attached data
        
        [JsonProperty]
        public dataT Data { get; set; }

        //position -> refer to parent RoadConnectionVector
        public Vector2 Position => parent.WorldPosition;

        [JsonConstructor]
        private GraphNode() { }

        //create with no edges and given parent. Register node in graph
        private GraphNode(OrientedGraph<dataT> graph, RoadConnectionVector parent)
        {
            edges_forward = new();
            edges_backward = new();
            this.parent = parent;
            graph.AddNode(this);
        }
        //create a default node for given road connection vector
        public static GraphNode<BaseData> NewOriginal(OrientedGraph<BaseData> graph, SimulationObjectCollection world, RoadConnectionVector parent)
        {
            var n = new GraphNode<BaseData>(graph, parent);
            n.Data = new BaseData(new(world.ParentWorld, n));
            return n;
        }

        //create a copy of this node with a specified data type 
        public GraphNode<T> Copy<T>(OrientedGraph<T> graph) => new (graph, parent);

        
        public void AddForward(GraphEdgeReference<dataT> e) => edges_forward.Add(e);
        public void AddBackward(GraphEdgeReference<dataT> e) => edges_backward.Add(e);

        public void RemoveForward(GraphEdgeReference<dataT> e) => edges_forward.Remove(e);
        public void RemoveBackward(GraphEdgeReference<dataT> e) => edges_backward.Remove(e);


        public override string ToString()
        {
            return $"Node(Forward:{edges_forward.Count}, Back:{edges_backward.Count})";
        }
        public void Destroy()
        {
            //deleting nodes is only supported for default ones, where we can get to the parent graph
            if (this is GraphNode<BaseData> n)
            {
                var g = parent.RoadGraph;
                g.RemoveNode(n);
                //destroy all edges from this node as well - edges delete themselves form node when deleted,t
                while (edges_forward.Count > 0) n.edges_forward[0].Get(g).Destroy();
                while (edges_backward.Count > 0) n.edges_backward[0].Get(g).Destroy();
                n.Data.obj.Destroy();
            }
            else throw new Exception("Destroying non-default nodes is not supported");

        }
    }


    //represents one edge in road graph
    [JsonObject(MemberSerialization.OptIn)]
    class GraphEdge<dataT>
    {
        //trajectory describing this edge
        [JsonProperty]
        public Trajectory trajectory;
        [JsonProperty]
        public GraphNode<dataT> node_from;
        [JsonProperty]
        public GraphNode<dataT> node_to;

        GraphEdge<BaseData> Default => this as GraphEdge<BaseData>;
        GraphEdgeReference<dataT> Ref => new(node_from.parent.RoadGraph.FindEdge(Default));

        [JsonConstructor]
        private GraphEdge() { }

        //create an edge in given graph between two nodes
        public GraphEdge(OrientedGraph<dataT> graph, Trajectory t, GraphNode<dataT> from, GraphNode<dataT> to)
        {
            trajectory = t;
            node_from = from;
            node_to = to;
            graph.AddEdge(this);
        }
        public void Destroy()
        {
            //deleting edges is only supported for default ones, where we can get to the parent graph
            if (this is GraphEdge<BaseData> e)
            {
                node_from.RemoveForward(Ref);
                node_to.RemoveBackward(Ref);
                node_from.parent.RoadGraph.RemoveEdge(e);
                trajectory.Destroy();
            }
            else throw new InvalidOperationException("Destroying non-default edges is not supported");
        }
    }


    //is responsible for drawing and finishing of a graph node in the default world 
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    class GraphNodeObject : SimulationObject
    {
        public override DrawLayer DrawZ => DrawLayer.TRAJECTORY_ARROWS;

        [JsonProperty(IsReference = true)]
        readonly GraphNode<BaseData> node;

        RoadWorld World => (RoadWorld)parent;

        [JsonConstructor]
        private GraphNodeObject() : base(null) { }

        public GraphNodeObject(RoadWorld world, GraphNode<BaseData> node) : base(world) => this.node = node;

        protected override void DrawI(SDLApp app, Transform camera)
        {
            //if there is just one edge going forward/backward, draw speed arrows
            if (node.edges_forward.Count == 1) node.edges_forward[0].Get(World.Graph).trajectory.DrawDirectionArrows(app, camera, false);
            else if (node.edges_backward.Count == 1) node.edges_backward[0].Get(World.Graph).trajectory.DrawDirectionArrows(app, camera, true);
        }
        protected override void FinishI(FinishPhase phase)
        {
            base.FinishI(phase);
            if (phase == FinishPhase.CREATE_CRYSIS_POINTS)
            {
                //if there is more than one edge starting in this point, create a split crysis point
                if (node.edges_forward.Count > 1) CrysisPoint.CreateSplitCrysisPoint(World, node.edges_forward.ConvertAll(x => x.Get(World.Graph).trajectory));
                //if there is more than one edge ending in this point, create a merge crysis point
                if (node.edges_backward.Count > 1) CrysisPoint.CreateMergeCrysisPoint(World, node.edges_backward.ConvertAll(x => x.Get(World.Graph).trajectory));
            }
        }
    }


    //A reference to an edge
    [JsonObject(MemberSerialization.OptIn)]
    struct GraphEdgeReference<dataT>
    {
        [JsonProperty]
        readonly int index;
        public GraphEdgeReference(int i) => index = i;
        public GraphEdge<dataT> Get(OrientedGraph<dataT> graph) => graph.GetEdge(index);
    }




    //Holds an oriented graph
    [JsonObject(MemberSerialization.OptIn)]
    class OrientedGraph<dataT>
    {
        //nodes are a list - and can be deleted, I can just work directly with graph node objects (unlike edges where I need the reference hack for JSON saving, see README)
        [JsonProperty]
        readonly List<GraphNode<dataT>> nodes;

        //edges are a dictionary of [index, edge] so references still work, even when deleting some objects
        [JsonProperty]
        readonly Dictionary<int, GraphEdge<dataT>> edges;

        int next_edge_id = 0;

        public List<GraphNode<dataT>> Nodes => nodes;

        public OrientedGraph() : this(new()) { }
        public OrientedGraph(List<GraphNode<dataT>> graph_nodes)
        {
            nodes = graph_nodes;
            edges = new();
        }

        public void AddEdge(GraphEdge<dataT> edge) => edges.Add(next_edge_id++, edge);
        public GraphEdge<dataT> GetEdge(int i) => edges[i];
        public int FindEdge(GraphEdge<dataT> edge) => edges.First(x => x.Value == edge).Key;
        public void RemoveEdge(GraphEdge<dataT> edge) => edges.Remove(FindEdge(edge));

        public void AddNode(GraphNode<dataT> node) => nodes.Add(node);

        //Has to be used carefully! Index can stop being valid when some nodes are deleted
        public GraphNode<dataT> GetNode(int i) => nodes[i];
        public int FindNode(GraphNode<dataT> node) => nodes.FindIndex(x => x == node);
        public void RemoveNode(GraphNode<dataT> node) => nodes.Remove(node);

        //Create a copy with new data type in each node
        public OrientedGraph<T> CopyWithDataT<T>()
        {
            OrientedGraph<T> copy = new();
            //copy nodes and edges, then return
            foreach (GraphNode<dataT> node in nodes) node.Copy(copy);
            CopyEdges(copy);
            return copy;
        }


        private GraphEdgeReference<Tto> ConvertEdge<Tto>(OrientedGraph<Tto> to, GraphEdge<dataT> edge)
        {
            //create a new edge between the given nodes in the target graph
            GraphEdge<Tto> e = new(to, edge.trajectory, to.GetNode(FindNode(edge.node_from)), to.GetNode(FindNode(edge.node_to)));
            //then, return a reference to it
            return new GraphEdgeReference<Tto>(to.FindEdge(e));
        }

        private void CopyEdges<T>(OrientedGraph<T> copy_to)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                //we only go through edges forward - each edge is there exactly once
                foreach (GraphEdgeReference<dataT> edge in GetNode(i).edges_forward)
                {
                    var e = ConvertEdge(copy_to, edge.Get(this));
                    e.Get(copy_to).node_from.AddForward(e);
                    e.Get(copy_to).node_to.AddBackward(e);
                }
            }
        }
    }

    //an oriented graph, with added vehicle sinks. Serves as a definition of the whole world
    [JsonObject(MemberSerialization.OptIn)]
    class RoadGraph : OrientedGraph<BaseData>
    {
        //where vehicles can end their travel and despawn
        [JsonProperty]
        protected readonly VehicleSinks vehicle_sinks = new();

        [JsonConstructor]
        public RoadGraph()
        { }

        //select a random target
        public GraphNode<BaseData> SelectVehicleSink() => vehicle_sinks.Select();
        public void AddVehicleSink(GraphNode<BaseData> node, float weight) => vehicle_sinks.Add(node, weight);
    }



    //contains all vehicle targets. Each target has a weight -> how probable is selecting it.
    //Probability of selecting each sink is computed using the softmax function, see here https://en.wikipedia.org/wiki/Softmax_function, in section Definition. z_1, z_2, ... are the sink weights.
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    class VehicleSinks
    {
        //list of all sink nodes + their weights
        [JsonProperty]
        readonly List<(GraphNode<BaseData> n, float W)> sinks = new();
        readonly Random select_random = new();

        //max sink weight. Used for normalizing all weights to prevent float errors from getting in the way. -> final formula used will be x_i = e^(w_i - max_w) / (e^(w_1 - max_w) + ... + e^(w_k - max_w))
        [JsonProperty]
        float max_w = float.NegativeInfinity;

        //the bottom part in softmax formula - (e^(w_1 - max_w) + ... + e^(w_k - max_w))
        [JsonProperty]
        float div = 0;

        [JsonConstructor]
        public VehicleSinks() { }

        public void Add(GraphNode<BaseData> spawn_, float weight = 1)
        {
            //add the new sink and update maximum weight
            sinks.Add((spawn_, weight));
            max_w = Math.Max(max_w, weight);
            //compute the bottom part
            div = 0;
            foreach ((GraphNode<BaseData> _, float W) in sinks) div += MathF.Exp(W - max_w);
        }

        const float tolerance = 1e-5f;
        //select a sink
        public GraphNode<BaseData> Select()
        {
            //get a double between 0 and 1
            double k = select_random.NextDouble();
            //subtract probability of each sink. When k gets below 0, this is the sink we select. Tolerance is here to avoid errors near the last element
            foreach (var (i, W) in sinks)
            {
                k -= MathF.Exp(W - max_w) / div;
                if (k <= tolerance) return i;
            }
            //something would have to go very wrong to get here
            throw new Exception("Softmax error");
        }
    }


    //used to plan a path in an oriented graph. Can be used by one thread at a time
    class PathPlanner
    {
        //the graph to search in
        readonly OrientedGraph<Search.A_star_data> graph;
        //for randomizing path
        readonly Random random;
        //when adding path length in A* algorithm, it is multiplied by a random constant between these two values. Used to make path more random
        readonly float random_from;
        readonly float random_to;

        public PathPlanner(RoadWorld w)
        {
            //create a copy of original graph to search in
            graph = w.Graph.CopyWithDataT<Search.A_star_data>();
            random = new Random();
            random_from = w.settings.PathRandomizationFrom;
            random_to = w.settings.PathRandomizationTo;
        }
        public Queue<Trajectory> PlanPath(int from, int to) => Search.FindAPath(graph, from, to, random, random_from, random_to);
        //used for randomization during vehicle spawning
        public Random GetRandom() => random;
    }
}
