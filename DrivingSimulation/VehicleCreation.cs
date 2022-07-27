using System;
using System.Collections.Generic;

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


    abstract class ColorPicker
    {
        public abstract Color PickColor();
    }

    class ConstantColorPicker : ColorPicker
    {
        Color color;
        public ConstantColorPicker(Color c)
        {
            color = c;
        }
        public override Color PickColor()
        {
            return color;
        }
    }

    class LoopColorPicker : ColorPicker
    {
        readonly static Color[] colors = new Color[] { Color.Green, Color.Cyan, Color.Blue, Color.Magenta, Color.Red, Color.Yellow };
        int counter = 0;
        public override Color PickColor()
        {
            return colors[counter++ % colors.Length];
        }
    }


    abstract class Garage
    {
        public Garage()
        {}
        public abstract CreatedCar Update(float dt, bool additional_condition = true);

        public abstract Garage Copy();
    }

    class EmptyGarage : Garage
    {
        public EmptyGarage()
        { }
        public override CreatedCar Update(float dt, bool additional_condition = true)
        {
            return new CreatedCar();
        }
        public override Garage Copy()
        {
            return new EmptyGarage();
        }
    }


    class PeriodicGarage : Garage
    {
        float current_cooldown = 0;
        protected float max_cooldown;
        int vehicle_counter = 0;
        readonly protected ColorPicker color_picker;

        protected int VehicleCount { get => vehicle_counter; }

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


    class SpontaneusGarage : PeriodicGarage
    {
        readonly float chance;
        readonly Random random;
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

    class PeriodicBurstGarage : PeriodicGarage
    {
        readonly float car_cd;
        readonly float pause;
        readonly int car_count;
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




    abstract class RoadEndObject : DrivingSimulationObject
    {
        Vector2 position;
        Vector2 size;
        Color color;

        protected override bool PreDraw => false;

        public RoadEndObject(RoadWorld world, RoadPlug road, Color color, Vector2 added_size) : base(world)
        {
            position = road.GetPosition();
            size = road.GetSize() + added_size;
            this.color = color;
        }
        protected override void Draw(SDLApp app, Transform transform)
        {
            app.DrawRect(color, transform.Apply(position - size / 2), transform.ApplyDirection(size));
        }
    }





    class GarageSpawn : DrivingSimulationObject
    {
        readonly int spawn_i;
        readonly Garage garage;
        readonly RoadWorld world;

        public override int DrawLayer => 4;
        protected override bool PreDraw => false;
        public GarageSpawn(RoadWorld world, int spawn_index, Garage garage) : base(world)
        {
            spawn_i = spawn_index;
            this.garage = garage;
            this.world = world;
        }
        public override void Update(float dt)
        {
            CreatedCar car = garage.Update(dt);
            if (car.created)
            {
                float ratio = 1f * world.VehicleCount / world.Graph.RecommendedVehicleCount;

                Queue<Trajectory> path;
                int max_tries = 3;
                PathPlanner planner = world.GetPathPlanner();
                Random rand = planner.GetRandom();
                //chance is 1 when <= to recommended count, then linearily decreases until 2 * recommended count
                //also, vehicle has to pass the generation intensity check - sometimes, less vehicles should be generated
                if (ratio - 1 < rand.NextDouble() && rand.NextDouble() < world.VehicleGenerationIntensity)
                {
                    for (int i = 0; i < max_tries; i++)
                    {
                        int target = world.Graph.SelectVehicleSink();
                        path = planner.PlanPath(spawn_i, target);
                        if (path != null)
                        {
                            if (path.Peek().VehicleList.Count == 0 || path.Peek().VehicleList.First.Value.position.Value.SegmentsToDist() > Vehicle.min_vehicle_distance)
                            {
                                _ = new Vehicle(world, path, car.color);
                                break;
                            }
                        }
                    }
                }
                world.ReturnPathPlanner(planner);
            }
        }
    }



    class GarageObject : RoadEndObject
    {
        public override int DrawLayer => 5;
        public GarageObject(RoadWorld world, Garage garage, RoadPlug road) : base(world, road, Color.Black, new Vector2(.5f))
        {
            foreach (var p in road.Forward)
            {
                if (p.node.edges_forward.Count == 0) Console.WriteLine("Creating invalid garage - no forward edges connected");
                GarageSpawn _ = new(world, world.Graph.FindNode(p.node), garage.Copy());
            }
        }
    }

    class VehicleSink : RoadEndObject
    {
        public override int DrawLayer => 5;
        public VehicleSink(RoadWorld world, float weight, RoadPlug road, bool garage_present = false) : base(world, road, Color.DarkGray, new Vector2(garage_present ? .25f : .5f))
        {
            foreach (var p in road.Backward)
            {
                if (p.node.edges_backward.Count == 0) Console.WriteLine("Creating invalid vehicle sink - no backward edges connected");
                world.Graph.AddVehicleSink(world.Graph.FindNode(p.node), weight);
            }
        }
    }
}
