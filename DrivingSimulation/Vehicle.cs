using System;
using System.Collections.Generic;


namespace DrivingSimulation
{
    class Vehicle : SimulationObject
    {

        readonly Queue<Trajectory> m_planned_path;
        readonly Queue<PathEvent> m_path_events;

        Trajectory CurTrajectory { get => m_planned_path.Peek(); }

        new Vector2 WorldPosition => CurTrajectory.GetPos(position);
        new Vector2 WorldDirection => CurTrajectory.GetDerivative(position);

        double total_distance_travelled = 0;
        public readonly CumulativeBuffered<float> position = new(0);
        readonly CumulativeBuffered<float> speed = new(0.5f);
        Vector2 smoothed_direction;

        const float max_acceleration = 1;
        const float braking_force = 2;
        public const float vehicle_radius = 0.2f;
        public const float min_vehicle_distance = 2 * vehicle_radius;
        public const float preferred_spacing = 0.6f;

        readonly MoveTransform my_transform;

        readonly CircularObject triangle_handle;


        public bool selected;

        public int Id;
        static int global_id_counter = 0;

        public override DrawLayer DrawZ => DrawLayer.VEHICLES;

        public Vehicle(RoadWorld world, Queue<Trajectory> path, Color color) : base(world)
        {
            position = new(0);
            speed = new(0.5f);
            m_planned_path = path;
            m_path_events = Search.Path(m_planned_path);
            CurTrajectory.AddVehicle(this);
            triangle_handle = new(this, vehicle_radius, Texture.Vehicle)
            {
                color = color
            };
            smoothed_direction = WorldDirection;
            Id = global_id_counter++;
            my_transform = new(WorldPosition);
            state = ObjectState.FINISHED;
        }

        static void SolveQuadratic(float a, float b, float c, out float x1, out float x2)
        {
            float D_ = b * b - 4 * a * c;
            if (D_ < 0) throw new ArithmeticException("Solving equation failed: negative discriminant");
            D_ = MathF.Sqrt(D_);
            x1 = (-b - D_) / 2 / a;
            x2 = (-b + D_) / 2 / a;
        }

        float MinTimeToTravel(float distance)
        {
            if (distance < 0) return 0;
            SolveQuadratic(max_acceleration / 2, speed, -distance, out float _, out float time);
            float overspeed_time = time - (CurTrajectory.MaxSpeed - speed) / max_acceleration;
            if (overspeed_time < 0) return time;
            return time + overspeed_time * overspeed_time * max_acceleration / 2 / CurTrajectory.MaxSpeed;
        }

        public static float DistToBreak(float cur_speed, float target_speed)
        {
            float t = (cur_speed - target_speed) / braking_force;
            return cur_speed * t - braking_force * t * t / 2;
        }

        protected override void UpdateI(float dt)
        {
            base.UpdateI(dt);
            float stop_distance = float.PositiveInfinity;
            float cur_acceleration = max_acceleration;


            //do not go faster than the vehicle ahead
            var vehicle_ahead = Search.VehicleAhead(this, m_planned_path);
            if (vehicle_ahead.Exists())
            {
                float brake_dist = DistToBreak(1.2f * speed, vehicle_ahead.vehicle.speed);
                float reserve_dist = vehicle_ahead.distance - preferred_spacing;
                if (reserve_dist < brake_dist) cur_acceleration = -braking_force;
                if (vehicle_ahead.vehicle.speed == 0) stop_distance = reserve_dist;
            }

            bool inside_safe_point = false;
            float safe_spot_remaining_distance = 0;

            int safe_spots_found = 0;
            float max_wait_time_until_safe = 0;

            bool next_safe_spot_full = false;


            foreach (var ev in m_path_events)
            {
                float ev_from = ev.from - (float)total_distance_travelled;
                float ev_to = ev.to - (float)total_distance_travelled;
                if (ev_from < 0) ev.VehicleInside();
                if (ev.IsCrysisPoint)
                {
                    float time_from = MinTimeToTravel(ev_from);
                    float time_to = MinTimeToTravel(ev_to);
                    float wait_time = 0;
                    //all further events will be too far in the future for current observations to mean anything. We will figure it out when we get there
                    if (time_from > 5) break;
                    if (ev_from < 0)
                    {
                        ev.crysis_point.trajectory_inside.Set(ev.trajectory.Id);
                        ev.crysis_point.GetBranchInfo(ev.trajectory).SetExitTime(time_to);
                        ev.crysis_point.MovingInside();
                    }
                    else
                    {
                        wait_time = ev.crysis_point.WaitTimeUntilFree(time_from, time_to, ev.trajectory);
                        max_wait_time_until_safe = Math.Max(wait_time, max_wait_time_until_safe);
                        if (wait_time != 0 || !ev.crysis_point.FreeForTrajectory(ev.trajectory.Id)) stop_distance = Math.Min(stop_distance, ev_from);
                    }
                }
                else
                {
                    if (safe_spots_found == 0)
                    {
                        if (ev_from < 0) inside_safe_point = true;
                        safe_spot_remaining_distance = ev_to;
                    }
                    else if (safe_spots_found == 1)
                    {
                        if (ev.safe_spot.VehiclesInside + Search.CountVehicles(this, m_planned_path, ev_from + ev.trajectory.MaxSpeed * dt + 1e-4f) + 1 > ev.safe_spot.Capacity)
                        {
                            next_safe_spot_full = true;
                        }
                    }
                    safe_spots_found++;
                }
            }

            if (inside_safe_point && (max_wait_time_until_safe != 0 || next_safe_spot_full))
            {
                stop_distance = Math.Min(stop_distance, safe_spot_remaining_distance);
            }

            if (stop_distance < DistToBreak(speed, 0))
            {
                float required_brake_to_stop = speed * speed / stop_distance;
                cur_acceleration = Math.Min(cur_acceleration, -required_brake_to_stop);
            }

            smoothed_direction = (0.8f * smoothed_direction + 0.2f * WorldDirection);

            speed.Set(Math.Clamp(speed + dt * cur_acceleration, 0, CurTrajectory.MaxSpeed));

            float dpos = (dt * speed).DistToSegments();
            if (dpos > stop_distance) dpos = stop_distance - 1e-4f;

            my_transform.Set(WorldPosition);

            position.Set(Math.Max(0, position + dpos));
            total_distance_travelled += (position.NextValue - position.Value).SegmentsToDist();
            while (m_path_events.Count != 0 && m_path_events.Peek().to - total_distance_travelled < 0) m_path_events.Dequeue().VehicleLeaves();
            int segments;
            while (position.NextValue > (segments = CurTrajectory.SegmentCount))
            {
                position.NextRef -= segments;
                m_planned_path.Dequeue().RemoveLastVehicle();
                if (m_planned_path.Count == 0)
                {
                    Destroy();
                    break;
                }
                CurTrajectory.AddVehicle(this);
            }
            
        }
        protected override void PostUpdateI()
        {
            position.PostUpdate();
            speed.PostUpdate();
        }
        protected override void DrawI(SDLApp app, Transform camera)
        {
            triangle_handle.Draw(app, camera, smoothed_direction);
            if (selected)
            {
                float from = position;
                foreach (Trajectory t in m_planned_path)
                {
                    t.DrawCurve(app, camera, Color.Magenta, from);
                    from = 0;
                }
            }
        }
        protected override Transform GetTransform()
        {
            return my_transform;
        }

        protected override void UnfinishI()
        {
            base.UnfinishI();
            Destroy();
        }

        protected override void InteractI(Inputs inputs)
        {
            base.InteractI(inputs);
            bool collides = CircularObject.Collides(WorldPosition, inputs.MouseWorldPos, vehicle_radius);
            if (inputs.MouseLeft.Down) selected = collides;
            if (inputs.MouseRight.Down && collides) Destroy();
        }
        public override string ToString()
        {
            return $"Vehicle(Position:{WorldPosition}, Color:{triangle_handle.color})";
        }
        protected override void DestroyI()
        {
            base.DestroyI();
            if (m_planned_path.Count != 0) CurTrajectory.RemoveVehicle(this);
            foreach (var ev in m_path_events) if (ev.inside) ev.VehicleLeaves();
            base.Destroy();
        }
    }
}
