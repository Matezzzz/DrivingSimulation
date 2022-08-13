using System.Collections.Generic;
using System;

namespace DrivingSimulation
{

    //contains information about events that will happen along a path. Events include safe spots & crisis points
    class PathEvent
    {
        public SafeSpot safe_spot;
        public CrisisPoint crisis_point;
        public Trajectory trajectory;
        //distance from and to which I am inside this event, from the beginning of vehicle path
        public float from;
        public float to;
        //true if this vehicle already is inside. Used to track vehicle counts
        public bool inside = false;

        public bool IsCrisisPoint => crisis_point != null;
        public PathEvent(SafeSpot ss, CrisisPoint cp, Trajectory t, float from, float to)
        {
            safe_spot = ss;
            crisis_point = cp;
            trajectory = t;
            this.from = from;
            this.to = to;
        }
        //dist here is distance to the start of trajectory where the event happens
        public PathEvent(CrisisPoint cp, Trajectory t, float dist) : this(null, cp, t, dist + cp.GetBranchInfo(t).from.SegmentsToDist(), dist + cp.GetBranchInfo(t).to.SegmentsToDist())
        { }
        //dist here is distance to the start of trajectory where the event happens
        public PathEvent(SafeSpot ss, Trajectory t, float dist) : this(ss, null, t, dist + ss.from.SegmentsToDist(), dist + ss.to.SegmentsToDist())
        { }
        //announce to this event that we are inside - used to track total vehicle count inside each point
        public void VehicleInside()
        {
            if (!inside)
            {
                if (IsCrisisPoint) crisis_point.VehicleEnters();
                else safe_spot.VehicleEnters();
            }
            inside = true;
        }
        //announce that vehicle left this event
        public void VehicleLeaves()
        {
            if (IsCrisisPoint) crisis_point.VehicleLeaves();
            else safe_spot.VehicleLeaves();
        }
    }
    static class Search
    {
        //search a path - return all events that happen along it
        //goes through all trajectories, just adds all events that happen on each one
        public static Queue<PathEvent> Path(Queue<Trajectory> path)
        {
            Queue<PathEvent> events = new();
            float dist = 0;
            foreach (Trajectory t in path)
            {
                foreach (TrajectoryPart part in t.Parts)
                {
                    if (part.crisis_point != null) events.Enqueue(new PathEvent(part.crisis_point, t, dist));
                    else events.Enqueue(new PathEvent(part.safe_spot, t, dist));
                }
                //measure total distance from vehicle spawn point
                dist += t.Length;
            }
            return events;
        }


        //represents the nearest vehicle found on my trajectory
        public struct FoundVehicle
        {
            public Vehicle vehicle;
            public float distance;
            public FoundVehicle(Vehicle v, float dist)
            {
                vehicle = v;
                distance = dist;
            }
            public bool Exists => vehicle != null;
        }
        enum VehicleSearchMode { FIRST, COUNT};

        //search for vehicles along a path - either find the first one and return it, or, count all found until a point in the future
        static void VehicleSearch(Vehicle vehicle, Queue<Trajectory> path, float search_until, VehicleSearchMode mode, out FoundVehicle first, out int count)
        {
            //distance from start of search to beginning of current trajectory
            float dist = 0;
            //position on current trajectory
            float current_pos = vehicle.position;
            first = new FoundVehicle(null, float.PositiveInfinity);
            count = 0;
            //for all trajectories, and vehicles on them
            foreach (Trajectory t in path)
            {
                foreach (Vehicle v in t.VehicleList)
                {
                    //compute distance from current vehicle
                    float vehicle_dist = dist + (v.position - current_pos).SegmentsToDist();
                    //if we have searched far enough, return
                    if (vehicle_dist > search_until) return;
                    //vehicles are ordered - first with dist > 0 is the closest one
                    if (vehicle_dist > 0)
                    {
                        //if looking just for the first vehicle, return
                        if (mode == VehicleSearchMode.FIRST)
                        {
                            first = new FoundVehicle(v, vehicle_dist + dist);
                            return;
                        }
                        //increase found vehicle counter
                        count++;
                    }
                }
                //add distance travelled on current trajectory to total distance
                dist += (t.SegmentCount - current_pos).SegmentsToDist();
                current_pos = 0;
            }
        }
        //finds first vehicle on my path
        public static FoundVehicle VehicleAhead(Vehicle vehicle, Queue<Trajectory> path)
        {
            VehicleSearch(vehicle, path, 5, VehicleSearchMode.FIRST, out FoundVehicle v, out int _);
            return v;
        }
        //counts vehicles on a path, until a certain distance
        public static int CountVehicles(Vehicle vehicle, Queue<Trajectory> path, float dist)
        {
            VehicleSearch(vehicle, path, dist, VehicleSearchMode.COUNT, out FoundVehicle _, out int count);
            return count;
        }



        //all data in one node required for the a* search
        public class A_star_data
        {
            public float dist_begin;
            public bool end;
            public GraphEdge<A_star_data> arrival_edge;
        }
        //return A* heuristic for two nodes (just euclidean distance)
        static float AStarHeuristic<T>(GraphNode<T> n1, GraphNode<T> n2) => (n1.Position - n2.Position).Length();

        //find a path using the A* algorithm
        public static Queue<Trajectory> FindAPath(OrientedGraph<A_star_data> graph, int start, int end, Random random = null, float random_dist_min = 1, float random_dist_max = 1)
        {
            //active nodes
            List<GraphNode<A_star_data>> active_nodes = new() { graph.GetNode(start) };

            //reset data in all nodes - infinite distance, is not an end, and the edge we arrived over
            foreach (var node in graph.Nodes)
            {
                //if node data hasn't been created yet, create it
                if (node.Data == null) node.Data = new A_star_data();
                node.Data.dist_begin = float.PositiveInfinity;
                node.Data.end = false;
                node.Data.arrival_edge = null;
            }
            //set distance for start node to 0, and mark end
            graph.GetNode(start).Data.dist_begin = 0;
            graph.GetNode(end).Data.end = true;

            //while there are active nodes
            while (active_nodes.Count > 0)
            {
                //select best node (distance_from_start + A*_heuristic()) is the smallest of all active ones
                int best_i = -1;
                float best_val = float.PositiveInfinity;
                for (int i = 0; i < active_nodes.Count; i++)
                {
                    float dist = active_nodes[i].Data.dist_begin;
                    float val = dist + AStarHeuristic(active_nodes[i], graph.GetNode(end));
                    if (val < best_val)
                    {
                        best_val = val;
                        best_i = i;
                    }
                }
                var node = active_nodes[best_i];

                //if selected node is an end, construct a path to it using saved arrival edges
                if (node.Data.end)
                {
                    List<Trajectory> path = new();
                    while (node.Data.arrival_edge != null)
                    {
                        path.Add(node.Data.arrival_edge.trajectory);
                        node = node.Data.arrival_edge.node_from;
                    }
                    Queue<Trajectory> path_queue = new();
                    for (int i = path.Count - 1; i >= 0; i--)
                    {
                        path_queue.Enqueue(path[i]);
                    }
                    return path_queue;
                }

                //go to all connected nodes from the active node
                foreach (var edge_forward in node.edges_forward)
                {
                    float trajectory_len = edge_forward.Get(graph).trajectory.Length;
                    //if the trajectory has too many cars on it, multiply it's weight by an absurd amount, making it unlikely to be selected
                    if (edge_forward.Get(graph).trajectory.Occupancy > 0.5f) trajectory_len *= 100;
                    //randomize the trajectory length a bit, if enabled -> can choose a path that is slightly worse than best
                    if (random != null) trajectory_len *= ((float) random.NextDouble() * (random_dist_max - random_dist_min) + random_dist_min);
                    //compute the new fastest way to get to this connected node
                    float new_dist = node.Data.dist_begin + trajectory_len;
                    var node_to = edge_forward.Get(graph).node_to;
                    //if the new dist is better than the old one, save the arrival edge. If the node isn't in active ones yet, add it, otherwise just update the distance to
                    if (new_dist < node_to.Data.dist_begin)
                    {
                        node_to.Data.dist_begin = new_dist;
                        node_to.Data.arrival_edge = edge_forward.Get(graph);
                        if (!active_nodes.Contains(node_to)) active_nodes.Add(node_to);
                    }
                }
                active_nodes.RemoveAt(best_i);
            }
            //if there are no active nodes but path still wasn't found, return null
            return null;
        }
    }
}
