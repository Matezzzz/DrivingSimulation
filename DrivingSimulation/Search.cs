using System.Collections.Generic;
using System;

namespace DrivingSimulation
{
    static class Search
    {
        public struct PathEvent
        {
            public CrysisPoint crysis_point;
            public Trajectory trajectory;
            public float from;
            public float to;
            public PathEvent(CrysisPoint cp, Trajectory t, float from, float to)
            {
                crysis_point = cp;
                trajectory = t;
                this.from = from;
                this.to = to;
            }
            public PathEvent(CrysisPoint cp, Trajectory t, float dist) : this(cp, t, dist + cp.GetBranchInfo(t).from.SegmentsToDist(), dist + cp.GetBranchInfo(t).to.SegmentsToDist())
            { }
            public PathEvent(Trajectory t, float dist, float from, float to) : this(null, t, dist + from.SegmentsToDist(), dist + to.SegmentsToDist())
            { }
            public bool IsCrysisPoint()
            {
                return crysis_point != null;
            }
        }
        public static IEnumerable<PathEvent> Path(float current_pos, Queue<Trajectory> path)
        {
            float dist = 0;
            foreach (Trajectory t in path)
            {
                foreach (TrajectoryPart part in t.Parts)
                {
                    if (part.crysis_point != null)
                    {
                        var block = part.crysis_point.GetBranchInfo(t);
                        if (block.to < current_pos) continue;
                        yield return new PathEvent(part.crysis_point, t, dist - current_pos.SegmentsToDist());
                    }
                    else
                    {
                        var safe_spot = part.safe_spot;
                        if (safe_spot.to < current_pos) continue;
                        yield return new PathEvent(t, dist - current_pos.SegmentsToDist(), safe_spot.from, safe_spot.to);
                    }
                }
                dist += (t.SegmentCount - current_pos).SegmentsToDist();
                current_pos = 0;
            }
            yield return new PathEvent(null, dist, 0, 0);
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
        public static FoundVehicle VehicleAhead(Vehicle vehicle, Queue<Trajectory> path)
        {
            float dist = 0;
            float current_pos = vehicle.position;
            foreach (Trajectory t in path)
            {
                foreach (Vehicle v in t.VehicleList)
                {
                    float vehicle_dist = (v.position - current_pos).SegmentsToDist();
                    //vehicles are ordered - first with dist > 0 is the closest one
                    //could use binary search if I wasn't using queue - better data struct?
                    if (vehicle_dist > 0 || (vehicle_dist > -Vehicle.vehicle_radius / 2 && vehicle.Id > v.Id))
                    {
                        return new FoundVehicle(v, vehicle_dist + dist);
                    }
                }
                dist += (t.SegmentCount - current_pos).SegmentsToDist();
                current_pos = 0;
            }
            return new FoundVehicle(null, float.PositiveInfinity);
        }

        public class A_star_data
        {
            public float dist_begin;
            public bool end;
            public GraphEdge<A_star_data> arrival_edge;
        }
        static float AStarHeuristic<T>(GraphNode<T> n1, GraphNode<T> n2)
        {
            return (n1.position - n2.position).Length();
        }
        public static Queue<Trajectory> FindAPath(List<GraphNode<A_star_data>> graph, int start, int end, Random random = null, float random_dist_min = 1, float random_dist_max = 1)
        {
            List<GraphNode<A_star_data>> active_nodes = new() { graph[start] };

            foreach (var node in graph)
            {
                if (node.data == null) node.data = new A_star_data();
                node.data.dist_begin = float.PositiveInfinity;
                node.data.end = false;
                node.data.arrival_edge = null;
            }
            graph[start].data.dist_begin = 0;
            graph[end].data.end = true;

            while (active_nodes.Count > 0)
            {
                int best_i = -1;
                float best_val = float.PositiveInfinity;
                for (int i = 0; i < active_nodes.Count; i++)
                {
                    float dist = active_nodes[i].data.dist_begin;
                    float val = dist + AStarHeuristic(active_nodes[i], graph[end]);
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
                    float trajectory_len = edge_forward.trajectory.Length;
                    if (edge_forward.trajectory.Occupancy > 0.5f) trajectory_len *= 100;
                    //randomize the trajectory length a bit, if enabled -> can choose a path that is slightly worse than best
                    if (random != null) trajectory_len *= ((float) random.NextDouble() * (random_dist_max - random_dist_min) + random_dist_min);
                    float new_dist = node.data.dist_begin + trajectory_len;
                    var node_to = edge_forward.node_to;
                    if (new_dist < node_to.data.dist_begin)
                    {
                        node_to.data.dist_begin = new_dist;
                        node_to.data.arrival_edge = edge_forward;
                        if (!active_nodes.Contains(node_to)) active_nodes.Add(node_to);
                    }
                }
                active_nodes.RemoveAt(best_i);
            }
            return null;
        }
    }
}
