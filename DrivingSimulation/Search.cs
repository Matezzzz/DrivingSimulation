using System.Collections.Generic;
using System;

namespace DrivingSimulation
{
    class PathEvent
    {
        public SafeSpot safe_spot;
        public CrysisPoint crysis_point;
        public Trajectory trajectory;
        public float from;
        public float to;
        public bool inside = false;

        public bool IsCrysisPoint => crysis_point != null;
        public PathEvent(SafeSpot ss, CrysisPoint cp, Trajectory t, float from, float to)
        {
            safe_spot = ss;
            crysis_point = cp;
            trajectory = t;
            this.from = from;
            this.to = to;
        }
        public PathEvent(CrysisPoint cp, Trajectory t, float dist) : this(null, cp, t, dist + cp.GetBranchInfo(t).from.SegmentsToDist(), dist + cp.GetBranchInfo(t).to.SegmentsToDist())
        { }
        public PathEvent(SafeSpot ss, Trajectory t, float dist) : this(ss, null, t, dist + ss.from.SegmentsToDist(), dist + ss.to.SegmentsToDist())
        { }
        public void VehicleInside()
        {
            if (!inside)
            {
                if (IsCrysisPoint) crysis_point.VehicleEnters();
                else safe_spot.VehicleEnters();
            }
            inside = true;
        }
        public void VehicleLeaves()
        {
            if (IsCrysisPoint) crysis_point.VehicleLeaves();
            else safe_spot.VehicleLeaves();
        }
    }
    static class Search
    {
        public static Queue<PathEvent> Path(Queue<Trajectory> path)
        {
            Queue<PathEvent> events = new();
            float dist = 0;
            foreach (Trajectory t in path)
            {
                foreach (TrajectoryPart part in t.Parts)
                {
                    if (part.crysis_point != null)
                    {
                        events.Enqueue(new PathEvent(part.crysis_point, t, dist));
                    }
                    else
                    {
                        events.Enqueue(new PathEvent(part.safe_spot, t, dist));
                    }
                }
                dist += t.Length;
            }
            return events;
        }
        public struct FoundVehicle
        {
            public Vehicle vehicle;
            public float distance;
            public FoundVehicle(Vehicle v, float dist)
            {
                vehicle = v;
                distance = dist;
            }
            public bool Exists()
            {
                return vehicle != null;
            }
        }
        enum VehicleSearchMode { FIRST, COUNT};
        static void VehicleSearch(Vehicle vehicle, Queue<Trajectory> path, float search_until, VehicleSearchMode mode, out FoundVehicle first, out int count)
        {
            float dist = 0;
            float current_pos = vehicle.position;
            first = new FoundVehicle(null, float.PositiveInfinity);
            count = 0;
            foreach (Trajectory t in path)
            {
                foreach (Vehicle v in t.VehicleList)
                {
                    float vehicle_dist = dist + (v.position - current_pos).SegmentsToDist();
                    if (vehicle_dist > search_until) return;
                    //vehicles are ordered - first with dist > 0 is the closest one
                    //could use binary search if I wasn't using queue - better data struct?
                    if (vehicle_dist > 0 || (vehicle_dist > -Vehicle.vehicle_radius / 2 && vehicle.Id > v.Id))
                    {
                        if (mode == VehicleSearchMode.FIRST)
                        {
                            first = new FoundVehicle(v, vehicle_dist + dist);
                            return;
                        }
                        count++;
                    }
                }
                dist += (t.SegmentCount - current_pos).SegmentsToDist();
                current_pos = 0;
            }
        }
        public static FoundVehicle VehicleAhead(Vehicle vehicle, Queue<Trajectory> path)
        {
            VehicleSearch(vehicle, path, 5, VehicleSearchMode.FIRST, out FoundVehicle v, out int _);
            return v;
        }
        public static int CountVehicles(Vehicle vehicle, Queue<Trajectory> path, float dist)
        {
            VehicleSearch(vehicle, path, dist, VehicleSearchMode.COUNT, out FoundVehicle _, out int count);
            return count;
        }


        public class A_star_data
        {
            public float dist_begin;
            public bool end;
            public GraphEdge<A_star_data> arrival_edge;
        }
        static float AStarHeuristic<T>(GraphNode<T> n1, GraphNode<T> n2)
        {
            return (n1.Position - n2.Position).Length();
        }
        public static Queue<Trajectory> FindAPath(GraphNodeCollection<A_star_data> graph, int start, int end, Random random = null, float random_dist_min = 1, float random_dist_max = 1)
        {
            List<GraphNode<A_star_data>> active_nodes = new() { graph.GetNode(start) };

            foreach (var node in graph.Nodes)
            {
                if (node.data == null) node.data = new A_star_data();
                node.data.dist_begin = float.PositiveInfinity;
                node.data.end = false;
                node.data.arrival_edge = null;
            }
            graph.GetNode(start).data.dist_begin = 0;
            graph.GetNode(end).data.end = true;

            while (active_nodes.Count > 0)
            {
                int best_i = -1;
                float best_val = float.PositiveInfinity;
                for (int i = 0; i < active_nodes.Count; i++)
                {
                    float dist = active_nodes[i].data.dist_begin;
                    float val = dist + AStarHeuristic(active_nodes[i], graph.GetNode(end));
                    if (val < best_val)
                    {
                        best_val = val;
                        best_i = i;
                    }
                }
                var node = active_nodes[best_i];

                if (node.data.end)
                {
                    List<Trajectory> path = new();
                    while (node.data.arrival_edge != null)
                    {
                        path.Add(node.data.arrival_edge.trajectory);
                        node = node.data.arrival_edge.node_from;
                    }
                    Queue<Trajectory> path_queue = new();
                    for (int i = path.Count - 1; i >= 0; i--)
                    {
                        path_queue.Enqueue(path[i]);
                    }
                    return path_queue;
                }

                foreach (var edge_forward in node.edges_forward)
                {
                    float trajectory_len = edge_forward.Get(graph).trajectory.Length;
                    if (edge_forward.Get(graph).trajectory.Occupancy > 0.5f) trajectory_len *= 100;
                    //randomize the trajectory length a bit, if enabled -> can choose a path that is slightly worse than best
                    if (random != null) trajectory_len *= ((float) random.NextDouble() * (random_dist_max - random_dist_min) + random_dist_min);
                    float new_dist = node.data.dist_begin + trajectory_len;
                    var node_to = edge_forward.Get(graph).node_to;
                    if (new_dist < node_to.data.dist_begin)
                    {
                        node_to.data.dist_begin = new_dist;
                        node_to.data.arrival_edge = edge_forward.Get(graph);
                        if (!active_nodes.Contains(node_to)) active_nodes.Add(node_to);
                    }
                }
                active_nodes.RemoveAt(best_i);
            }
            return null;
        }
    }
}
