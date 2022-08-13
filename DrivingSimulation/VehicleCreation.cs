using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DrivingSimulation
{
    //whether a car was created and with what color
    struct CreatedCar
    {
        public bool created;
        public Color color;
        public CreatedCar() : this(false, Color.Black)
        { }
        public CreatedCar(Color c) : this(true, c)
        { }
        public CreatedCar(bool created, Color c)
        {
            this.created = created;
            color = c;
        }
    }



    //picks colors for cars
    [JsonObject]
    abstract class ColorPicker
    {
        public abstract Color PickColor();
    }

    //picks one color all the time
    [JsonObject(MemberSerialization.OptIn)]
    class ConstantColorPicker : ColorPicker
    {
        [JsonProperty]
        Color color;

        [JsonConstructor]
        private ConstantColorPicker() { }
        public ConstantColorPicker(Color c) => color = c;
        public override Color PickColor() => color;
    }

    //loops over internal colors when selecting
    [JsonObject]
    class LoopColorPicker : ColorPicker
    {
        readonly static Color[] colors = new Color[] { Color.Green, Color.Cyan, Color.Blue, Color.Magenta, Color.Red, Color.Yellow };
        int counter = 0;
        public override Color PickColor()
        {
            return colors[counter++ % colors.Length];
        }
    }


    //garage - reponsible for determining whether a vehicle should be spawned
    [JsonObject]
    abstract class Garage
    {
        //check whether a car should be generated. additional condition is used so parent classes can pass their conditions to children
        public abstract CreatedCar Update(bool additional_condition = true);

        //create a copy of this garage with respective settings
        public abstract Garage Copy();
    }

    //spawn no cars (but cars can still be set to arrive here during in-world garage creation)
    [JsonObject]
    class EmptyGarage : Garage
    {
        public override CreatedCar Update(bool additional_condition = true)
        {
            return new CreatedCar();
        }
        public override Garage Copy()
        {
            return new EmptyGarage();
        }
    }

    //spawns a vehicle periodically
    [JsonObject(MemberSerialization.OptIn)]
    class PeriodicGarage : Garage
    {
        //how long until the next spawn
        float current_cooldown = 0;

        //total vehicle cooldown
        [JsonProperty]
        protected float max_cooldown;

        //counts how many vehicles were spawned since last reset
        int vehicle_counter = 0;

        [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
        readonly protected ColorPicker color_picker;

        protected int VehicleCount => vehicle_counter;


        [JsonConstructor]
        protected PeriodicGarage() { }
        public PeriodicGarage(float cooldown, ColorPicker color_pick)
        {
            max_cooldown = cooldown;
            color_picker = color_pick;
        }

        //subtract from cooldown. If it is up, increase counter, reset cooldown, and create a new car
        public override CreatedCar Update(bool additional_condition = true)
        {
            current_cooldown -= Constant.dt;
            if (CooldownExpired() && additional_condition)
            {
                current_cooldown = max_cooldown;
                vehicle_counter++;
                return new CreatedCar(color_picker.PickColor());
            }
            return new CreatedCar();
        }

        bool CooldownExpired() => current_cooldown <= 0;

        protected void SetCooldown(float cd)
        {
            max_cooldown = cd;
            current_cooldown = cd;
        }
        protected void ResetCount()
        {
            vehicle_counter = 0;
        }
        public override Garage Copy()
        {
            return new PeriodicGarage(max_cooldown, color_picker);
        }
    }


    //when a cooldown is expired, has a random, set chance to spawn a vehicle 
    [JsonObject]
    class SpontaneusGarage : PeriodicGarage
    {
        //chance to spawn after cooldown is expired
        [JsonProperty]
        readonly float chance;
        readonly Random random;

        [JsonConstructor]
        private SpontaneusGarage() {
            random = new();
        }
        public SpontaneusGarage(float cooldown, float chance, ColorPicker color_picker) : base(cooldown, color_picker)
        {
            this.chance = chance;
            random = new();
        }
        //if random double is less than chance, and cooldown is expired (checked via parent), spawn a vehicle
        public override CreatedCar Update(bool additional_condition = true)
        {
            return base.Update(random.NextDouble() < chance && additional_condition);
        }
        public override Garage Copy()
        {
            return new SpontaneusGarage(max_cooldown, chance, color_picker);
        }
    }




    //two states, burst and paused - burst spawns vehicles quickly, and pause is a time where none are spawned at all
    [JsonObject]
    class PeriodicBurstGarage : PeriodicGarage
    {
        //burst car cooldown
        [JsonProperty]
        readonly float car_cd;

        //pause (how long to wait from last car to first one of next burst)
        [JsonProperty]
        readonly float pause;

        //how many cars should be spawned in burst
        [JsonProperty]
        readonly int car_count;

        [JsonConstructor]
        private PeriodicBurstGarage() { }
        public PeriodicBurstGarage(float period_car_cd, int car_count, float pause, ColorPicker color_picker) : base(period_car_cd, color_picker)
        {
            car_cd = period_car_cd;
            this.pause = pause;
            this.car_count = car_count;
        }
        public override CreatedCar Update(bool additional_condition = true)
        {
            int old_count = VehicleCount;
            //if cooldown is up
            var spawn = base.Update(additional_condition);
            //if vehicle was spawned, and it increased vehiclecount accordingly: Set cooldown to pause duration, and reset the counter
            if (VehicleCount == car_count)
            {
                SetCooldown(pause);
                ResetCount();
            }
            //if this is the first vehicle to be spawned (we are after the pause), set cooldown to burst car cd
            else if (VehicleCount == 1 && old_count == 0)
            {
                SetCooldown(car_cd);
            }
            return spawn;
        }
        public override Garage Copy()
        {
            return new PeriodicBurstGarage(car_cd, car_count, pause, color_picker);
        }
    }




    //represents a garage/vehicle sink that is drawn to a screen
    [JsonObject(MemberSerialization.OptIn)]
    abstract class RoadEndObject : SimulationObject
    {
        [JsonProperty]
        readonly RoadPlugView plug;

        //will always encompass 
        [JsonProperty]
        readonly Vector2 added_size;
        [JsonProperty]
        readonly bool is_input;

        [JsonConstructor]
        protected RoadEndObject() : base(null) { }
        public RoadEndObject(RoadWorld world, RoadPlugView road, bool is_input, Vector2 added_size) : base(world.ParentWorld)
        {
            plug = road;
            this.added_size = added_size;
            this.is_input = is_input;
        }
        protected override void DrawI(SDLApp app, Transform camera)
        {
            //get a bounding box around the given road plug, then render it
            Rect bbox = plug.GetBoundingBox().AddSize(added_size);
            app.DrawRect(is_input ? Color.Black : Color.DarkGray, bbox, camera);
        }
    }



    //represents one node that spawns vehicles
    [JsonObject(MemberSerialization.OptIn)]
    class GarageSpawn : SimulationObject
    {
        [JsonProperty]
        readonly GraphNode<BaseData> spawn_point;

        //defines when vehicles will be spawned
        [JsonProperty(TypeNameHandling =TypeNameHandling.Auto)]
        readonly Garage garage;



        RoadWorld World => (RoadWorld) parent;

        [JsonConstructor]
        protected GarageSpawn() : base(null) { }
        public GarageSpawn(RoadWorld world, GraphNode<BaseData> spawn, Garage garage) : base(world)
        {
            spawn_point = spawn;
            this.garage = garage;
        }
        protected override void UpdateI()
        {
            if (!Finished) return;
            //try creating a car, and if one should be created
            CreatedCar car = garage.Update();
            if (car.created)
            {
                //ratio - how many vehicles are present in the world vs. how many should be
                float ratio = 1f * World.VehicleCount / World.settings.RecommendedVehicleCount;

                Queue<Trajectory> path;
                //how many times do we try to select a target and plan path. E.g. some targets might be unreachable, and we have no idea in advance, so we try selecting one multiple times
                int max_tries = 3;
                //get a path planner
                PathPlanner planner = World.GetPathPlanner();
                Random rand = planner.GetRandom();
                //chance is 1 when <= to recommended count, then linearily decreases until 2 * recommended count
                //also, vehicle has to pass the generation intensity check - sometimes, less vehicles should be generated
                if (ratio - 1 < rand.NextDouble() && rand.NextDouble() < World.VehicleGenerationIntensity)
                {
                    //if vehicle passed all checks, try planning a path for it
                    for (int i = 0; i < max_tries; i++)
                    {
                        int source = World.Graph.FindNode(spawn_point);
                        //if source node was deleted -> destroy itself
                        if (source == -1)
                        {
                            Destroy();
                            break;
                        }
                        //select a target
                        int target = World.Graph.FindNode(World.Graph.SelectVehicleSink());
                       
                        //try planning a path, if one was found, and there is enough space in front of the garage, create the vehicle
                        path = planner.PlanPath(source, target);
                        if (path != null)
                        {
                            if (path.Peek().VehicleList.Count == 0 || path.Peek().VehicleList[0].position.Value.SegmentsToDist() > Vehicle.preferred_surround_spacing)
                            {
                                _ = new Vehicle(World, path, car.color);
                                break;
                            }
                        }
                    }
                }
                //return the borrowed path planner
                World.ReturnPathPlanner(planner);
            }
        }
    }

    //a garage object -> creates garage spawn objects for spawning vehicles, derived from roadendobject to work with rendering
    [JsonObject(MemberSerialization.OptIn)]
    class GarageObject : RoadEndObject
    {
        public override DrawLayer DrawZ => DrawLayer.GARAGES;

        [JsonConstructor]
        protected GarageObject() { }

        //call parent, added size is .5f -> will be slightly larger than the roads below
        public GarageObject(RoadWorld world, Garage garage, RoadPlugView road) : base(world, road, true, new Vector2(.5f))
        {
            //create spawning objects for every outgoing road
            foreach (var p in road.Forward)
            {
                if (p.node.edges_forward.Count == 0) Console.WriteLine("Creating invalid garage - no forward edges connected");
                GarageSpawn _ = new(world, p.node, garage.Copy());
            }
        }
    }

    //vehicle sink -> vehicles can plan a path and end at this object. Parent object does rendering
    [JsonObject(MemberSerialization.OptIn)]
    class VehicleSink : RoadEndObject
    {
        public override DrawLayer DrawZ => DrawLayer.VEHICLE_SINKS;

        [JsonConstructor]
        protected VehicleSink() { }

        //call parent, if there is a garage present at this field as well, make added size smaller - sink will be a smaller rectangle on top of the bigger, garage one
        public VehicleSink(RoadWorld world, float weight, RoadPlugView road, bool garage_present = false) : base(world, road, false, new Vector2(garage_present ? .25f : .5f))
        {
            //create vehicle sink for every edge going in
            foreach (var p in road.Backward)
            {
                if (p.node.edges_backward.Count == 0) Console.WriteLine("Creating invalid vehicle sink - no backward edges connected");
                world.Graph.AddVehicleSink(p.node, weight);
            }
        }
    }
}
