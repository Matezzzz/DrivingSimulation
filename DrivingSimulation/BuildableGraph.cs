using System;
using System.Collections.Generic;


namespace DrivingSimulation
{
    class BuildableRoadGraph : RoadGraph
    {
        public BuildableRoadGraph(RoadWorld world, Vector2 world_size) : base(world, world_size) { }


        protected void Garage(RoadPlugView v, Garage garage, float sink_weight = 1)
        {
            RoadPlugView inv = v.Invert();
            bool creating_garage = inv.Forward.Count != 0;
            if (creating_garage) _ = new GarageObject(world, garage, inv);
            if (sink_weight != float.NegativeInfinity && inv.Backward.Count != 0) _ = new VehicleSink(world, sink_weight, inv, creating_garage);
        }

        protected void Garage(RoadPlugView v, Garage garage, bool enable_sink = true)
        {
            Garage(v, garage, enable_sink ? 1 : float.NegativeInfinity);
        }

        protected Road GarageRoad(RoadPlugView plug, Garage garage, float sink_weight, float length = 4)
        {
            Road garage_road = Wr.Road(plug, length);
            Garage(garage_road.A, garage, sink_weight);
            return garage_road;
        }
        protected Road GarageRoad(RoadPlugView plug, Garage garage, bool enable_sink = true, float length = 4)
        {
            return GarageRoad(plug, garage, enable_sink ? 1 : float.NegativeInfinity, length);
        }

        protected void VehicleSink(RoadPlugView v, float sink_weight)
        {
            foreach (var b in v.Backward)
            {
                if (b.node.edges_backward.Count == 0)
                {
                    Console.WriteLine("Invalid vehicle sink - no incoming edges exist");
                }
                vehicle_sinks.Add(b.node, sink_weight);
            }
        }
    }
}
