using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DrivingSimulation
{
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

    [JsonObject]
    abstract class ColorPicker
    {
        public abstract Color PickColor();
    }

    [JsonObject(MemberSerialization.OptIn)]
    class ConstantColorPicker : ColorPicker
    {
        [JsonProperty]
        Color color;

        [JsonConstructor]
        private ConstantColorPicker() { }
        public ConstantColorPicker(Color c)
        {
            color = c;
        }
        public override Color PickColor()
        {
            return color;
        }
    }

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

    [JsonObject]
    abstract class Garage
    {
        public abstract CreatedCar Update(float dt, bool additional_condition = true);

        public abstract Garage Copy();
    }

    [JsonObject]
    class EmptyGarage : Garage
    {
        public override CreatedCar Update(float dt, bool additional_condition = true)
        {
            return new CreatedCar();
        }
        public override Garage Copy()
        {
            return new EmptyGarage();
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    class PeriodicGarage : Garage
    {
        float current_cooldown = 0;
        [JsonProperty]
        protected float max_cooldown;
        int vehicle_counter = 0;
        [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
        readonly protected ColorPicker color_picker;

        protected int VehicleCount { get => vehicle_counter; }


        [JsonConstructor]
        protected PeriodicGarage() { }
        public PeriodicGarage(float cooldown, ColorPicker color_pick)
        {
            max_cooldown = cooldown;
            color_picker = color_pick;
        }
        public override CreatedCar Update(float dt, bool additional_condition = true)
        {
            current_cooldown -= dt;
            if (CooldownExpired() && additional_condition)
            {
                current_cooldown = max_cooldown;
                vehicle_counter++;
                return new CreatedCar(color_picker.PickColor());
            }
            return new CreatedCar();
        }
        bool CooldownExpired()
        {
            return current_cooldown <= 0;
        }
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

    [JsonObject]
    class SpontaneusGarage : PeriodicGarage
    {
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
        public override CreatedCar Update(float dt, bool additional_condition = true)
        {
            return base.Update(dt, random.NextDouble() < chance && additional_condition);
        }
        public override Garage Copy()
        {
            return new SpontaneusGarage(max_cooldown, chance, color_picker);
        }
    }

    [JsonObject]
    class PeriodicBurstGarage : PeriodicGarage
    {
        [JsonProperty]
        readonly float car_cd;
        [JsonProperty]
        readonly float pause;
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
        public override CreatedCar Update(float dt, bool additional_condition = true)
        {
            int old_count = VehicleCount;
            var spawn = base.Update(dt, additional_condition);
            if (VehicleCount == car_count)
            {
                SetCooldown(pause);
                ResetCount();
            }
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



    [JsonObject(MemberSerialization.OptIn)]
    abstract class RoadEndObject : SimulationObject
    {
        [JsonProperty]
        readonly RoadPlugView plug;
        [JsonProperty]
        readonly Vector2 added_size;
        [JsonProperty]
        readonly bool is_input;

        [JsonConstructor]
        protected RoadEndObject() : base(null) { }
        public RoadEndObject(RoadWorld world, RoadPlugView road, bool is_input, Vector2 added_size) : base(world.GetParentWorld())
        {
            plug = road;
            this.added_size = added_size;
            this.is_input = is_input;
        }
        protected override void DrawI(SDLApp app, Transform camera)
        {
            Vector2 size = plug.GetWorldSize() + added_size;
            app.DrawRect(is_input ? Color.Black : Color.DarkGray, new Rect(plug.GetWorldPosition() - size / 2, size), camera);
        }
    }




    [JsonObject(MemberSerialization.OptIn)]
    class GarageSpawn : SimulationObject
    {
        [JsonProperty]
        readonly GraphNode<BaseData> spawn_point;
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
        protected override void UpdateI(float dt)
        {
            if (!Finished) return;
            CreatedCar car = garage.Update(dt);
            if (car.created)
            {
                float ratio = 1f * World.VehicleCount / World.Graph.RecommendedVehicleCount;

                Queue<Trajectory> path;
                int max_tries = 3;
                PathPlanner planner = World.GetPathPlanner();
                Random rand = planner.GetRandom();
                //chance is 1 when <= to recommended count, then linearily decreases until 2 * recommended count
                //also, vehicle has to pass the generation intensity check - sometimes, less vehicles should be generated
                if (ratio - 1 < rand.NextDouble() && rand.NextDouble() < World.VehicleGenerationIntensity)
                {
                    for (int i = 0; i < max_tries; i++)
                    {
                        int target = World.Graph.FindNode(World.Graph.SelectVehicleSink());
                        path = planner.PlanPath(World.Graph.FindNode(spawn_point), target);
                        if (path != null)
                        {
                            if (path.Peek().VehicleList.Count == 0 || path.Peek().VehicleList.First.Value.position.Value.SegmentsToDist() > Vehicle.preferred_spacing)
                            {
                                _ = new Vehicle(World, path, car.color);
                                break;
                            }
                        }
                    }
                }
                World.ReturnPathPlanner(planner);
            }
        }
    }


    [JsonObject(MemberSerialization.OptIn)]
    class GarageObject : RoadEndObject
    {
        public override DrawLayer DrawZ => DrawLayer.GARAGES;

        [JsonConstructor]
        protected GarageObject() { }
        public GarageObject(RoadWorld world, Garage garage, RoadPlugView road) : base(world, road, true, new Vector2(.5f))
        {
            foreach (var p in road.Forward)
            {
                if (p.node.edges_forward.Count == 0) Console.WriteLine("Creating invalid garage - no forward edges connected");
                GarageSpawn _ = new(world, p.node, garage.Copy());
            }
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    class VehicleSink : RoadEndObject
    {
        public override DrawLayer DrawZ => DrawLayer.VEHICLE_SINKS;

        [JsonConstructor]
        protected VehicleSink() { }
        public VehicleSink(RoadWorld world, float weight, RoadPlugView road, bool garage_present = false) : base(world, road, false, new Vector2(garage_present ? .25f : .5f))
        {
            foreach (var p in road.Backward)
            {
                if (p.node.edges_backward.Count == 0) Console.WriteLine("Creating invalid vehicle sink - no backward edges connected");
                world.Graph.AddVehicleSink(p.node, weight);
            }
        }
    }
}
