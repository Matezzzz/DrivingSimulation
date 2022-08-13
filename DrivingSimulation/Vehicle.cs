using System;
using System.Collections.Generic;



namespace DrivingSimulation
{

    //represents one simulated vehicle
    class Vehicle : SimulationObject
    {
        
        readonly Queue<Trajectory> m_planned_path;
        //crisis points & safe points that will occur along a path
        readonly Queue<PathEvent> m_path_events;

        //current trajectory - the first one in planned path
        Trajectory CurTrajectory => m_planned_path.Peek();

        new Vector2 WorldPosition => CurTrajectory.GetPos(position);
        new Vector2 WorldDirection => CurTrajectory.GetDerivative(position);

        //total distance travelled since spawning, on all trajectories before current one
        float total_past_trajectory_distance_travelled = 0;
        //position on current trajectory (unit = segments)
        public readonly Buffered<float> position = new(0);
        //currentspeed. unit = distance / time
        public readonly Buffered<float> speed = new(0.5f);
        //direction smoothed over time, so sharp turns dont cause fast jumps in displayed rotation
        Vector2 smoothed_direction;

        //acceleration - distance / time^2
        const float max_acceleration = Constant.vehicle_acceleration;
        //braking force - how fast the vehicles want to break. The actual breaking force is unlimited, if we need it to avoid a collision
        const float braking_force = Constant.vehicle_preferred_braking_force;
        public const float radius = Constant.vehicle_radius;
        public const float min_vehicle_distance = 2 * radius;
        //minimal distance between first vehicle center and second vehicle center
        public const float preferred_surround_spacing = min_vehicle_distance + Constant.preferred_spacing;

        //for rendering a rotated traingle
        readonly MoveTransform my_transform;
        readonly CircularObject triangle_handle;


        public bool selected;

        public override DrawLayer DrawZ => DrawLayer.VEHICLES;

        public Vehicle(RoadWorld world, Queue<Trajectory> path, Color color) : base(world)
        {
            m_planned_path = path;
            //search given path for all events to come
            m_path_events = Search.Path(m_planned_path);
            CurTrajectory.VehicleEnters(this);
            triangle_handle = new(this, radius, Texture.Vehicle)
            {
                color = color
            };
            smoothed_direction = WorldDirection;
            my_transform = new(WorldPosition);
            //starts as finished -> unfinish can destroy it properly
            state = ObjectState.FINISHED;
        }

        //solve quadratic equation ax^2+bx+c = 0
        static void SolveQuadratic(float a, float b, float c, out float x1, out float x2)
        {
            float D_ = b * b - 4 * a * c;
            if (D_ < 0) throw new ArithmeticException("Solving equation failed: negative discriminant");
            D_ = MathF.Sqrt(D_);
            x1 = (-b - D_) / 2 / a;
            x2 = (-b + D_) / 2 / a;
        }

        //compute the minimal time to travel the given distance from where the vehicle is now
        float MinTimeToTravel(float distance)
        {
            if (distance < 0) return 0;
            //solve 1/2at^2 + vt = dist -> how long will it take me to travel distance, if there is no speed cap. Take the larger result (t2)
            SolveQuadratic(max_acceleration / 2, speed, -distance, out float _, out float time);
            //during computing the above, we could've went over the max speed -> compute how much further have we travelled due to this
            float overspeed_time = time - (CurTrajectory.MaxSpeed - speed) / max_acceleration;
            //if we didn't go over the cap, above time is valid
            if (overspeed_time < 0) return time;
            //else, correct the error -> take how much distance was gained due to overspeeding, and add how long it would take to go there normally
            return time + overspeed_time * overspeed_time * max_acceleration / 2 / CurTrajectory.MaxSpeed;
        }

        //how much distance will I gain on the target, if i want to break to his speed
        public static float DistToBreak(float cur_speed, float target_speed)
        {
            //time to break
            float t = (cur_speed - target_speed) / braking_force;
            //distance travelled in that time
            return cur_speed * t - braking_force * t * t / 2;
        }

        protected override void UpdateI()
        {
            base.UpdateI();
            //how far from my current position must I come to a stop (used e.g. for stopping before a crisis point)
            float stop_distance = float.PositiveInfinity;
            //how fast I am accelerating now
            float cur_acceleration = max_acceleration;




           
            bool inside_safe_point = false;
            float safe_spot_remaining_distance = 0;

            int safe_spots_found = 0;
            float max_wait_time_until_safe = 0;

            bool next_safe_spot_full = false;


            float total_distance_travelled = total_past_trajectory_distance_travelled + position.Value.SegmentsToDist();

            //go over all future safe spots & crisis points
            foreach (var ev in m_path_events)
            {
                //distance where event starts/ends
                float ev_from = ev.from - (float)total_distance_travelled;
                float ev_to = ev.to - (float)total_distance_travelled;
                //ev_from < 0 -> vehicle is inside.
                if (ev_from < 0) ev.VehicleInside();
                if (ev.IsCrisisPoint)
                {

                    //compute time to arrive and leave
                    float time_from = MinTimeToTravel(ev_from);
                    float time_to = MinTimeToTravel(ev_to);
                    float wait_time = 0;
                    //all further events will be too far in the future for current observations to mean anything. We will figure it out when we get there
                    if (time_from > 5) break;
                    //if I am inside, register vehicle, tell crisis point when I will exit, and mark crisis point as active
                    if (ev_from < 0)
                    {
                        ev.crisis_point.trajectory_inside.NextValue = ev.trajectory.Id;
                        ev.crisis_point.GetBranchInfo(ev.trajectory).SetExitTime(time_to);
                        ev.crisis_point.MovingInside();
                    }
                    else
                    {
                        //how long must I wait before this crisis point is free for my trajectory
                        wait_time = ev.crisis_point.WaitTimeUntilFree(time_from, time_to, ev.trajectory);
                        max_wait_time_until_safe = Math.Max(wait_time, max_wait_time_until_safe);
                        //if the crisis point is too close and we need to wait for others to exit, we need to stop before it
                        //if the vehicles inside the point are our own trajectory, we can go in anyway
                        if (wait_time != 0 || !ev.crisis_point.FreeForTrajectory(ev.trajectory.Id)) stop_distance = Math.Min(stop_distance, ev_from);
                    }
                }
                else
                {
                    //this is the first safe spot -> check whether we are inside
                    if (safe_spots_found == 0)
                    {
                        if (ev_from < 0) inside_safe_point = true;
                        safe_spot_remaining_distance = ev_to;
                    }
                    //the second safe spot -> check whether there is free space in it, even counting vehicles on the road to it
                    else if (safe_spots_found == 1)
                    {
                        if (ev.safe_spot.VehiclesInside + Search.CountVehicles(this, m_planned_path, ev_from + ev.trajectory.MaxSpeed * Constant.dt + 1e-4f) + 1 > ev.safe_spot.Capacity)
                        {
                            next_safe_spot_full = true;
                        }
                    }
                    safe_spots_found++;
                }
            }

            //if we are inside a safe point, and we need to wait in a crisis point, or the next safe spot is full, stop before the end of current safe spot
            if (inside_safe_point && (max_wait_time_until_safe != 0 || next_safe_spot_full))
            {
                stop_distance = Math.Min(stop_distance, safe_spot_remaining_distance);
            }


            //find the first vehicle ahead, and it there is one
            var vehicle_ahead = Search.VehicleAhead(this, m_planned_path);

            if (vehicle_ahead.Exists)
            {
                //compute how much will I should break if I want to stop as near to the vehicle ahead as possible without infringing its' safe space
                //1.1f makes me keep a small reserve -> even with inaccurate integration, that I use, I should stop correctly
                float brake_dist = 1.1f * DistToBreak(speed, vehicle_ahead.vehicle.speed);
                float vehicle_ahead_dist = vehicle_ahead.distance - preferred_surround_spacing;
                //if there isn't enough space, start braking full power
                if (vehicle_ahead_dist < brake_dist) cur_acceleration = Math.Min(cur_acceleration, -braking_force);
                //if the vehicle ahead is stopped, set stop distance as well (this just ensures that all vehicles will have the same distance between themselves when stopped)
                if (vehicle_ahead.vehicle.speed == 0) stop_distance = Math.Min(stop_distance, vehicle_ahead_dist);
            }

            //if we need to stop soon, compute required braking speed to match
            if (stop_distance < 1.1f*DistToBreak(speed, 0))
            {
                float required_brake_to_stop = speed * speed / stop_distance;
                cur_acceleration = Math.Min(cur_acceleration, -required_brake_to_stop);
            }
            //update smoothed direction -> 0.8* old one + 0.2* new one
            smoothed_direction = (0.8f * smoothed_direction + 0.2f * WorldDirection);

            //update speed based on current acceleration and current trajectory max speed
            speed.NextValue = Math.Clamp(speed + Constant.dt * cur_acceleration, 0, CurTrajectory.MaxSpeed);

            //compute change in position. If it would cause us to stop beyond the actual stop distance, clamp it
            float dpos = (Constant.dt * speed).DistToSegments();
            if (dpos > stop_distance) dpos = stop_distance - 1e-4f;

            //update transform and next position
            my_transform.Move = WorldPosition;

            position.NextValue = Math.Max(0, position + dpos);

            //remove all path events we passed during this frame
            while (m_path_events.Count != 0 && m_path_events.Peek().to - total_distance_travelled < 0) m_path_events.Dequeue().VehicleLeaves();
            int segments;
            //while the next position is after the end of current trajectory
            while (position.NextValue > (segments = CurTrajectory.SegmentCount))
            {
                //subtract current trajectory segments from position, then add trajectory length to total past distance travelled
                position.NextValue -= segments;
                total_past_trajectory_distance_travelled += CurTrajectory.Length;
                m_planned_path.Dequeue().VehicleLeaves();
                //if we reached our target, destroy the vehicle
                if (m_planned_path.Count == 0)
                {
                    Destroy();
                    break;
                }
                CurTrajectory.VehicleEnters(this);
            }
        }
        protected override void PostUpdateI()
        {
            position.PostUpdate();
            speed.PostUpdate();
        }
        //draw the vehicle, and if it is selected, the future trajectory
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
        protected override Transform GetTransform() => my_transform;

        //doesn't exist in edit mode. just destroy it
        protected override void UnfinishI()
        {
            base.UnfinishI();
            Destroy();
        }

        //select it if left clicked, destroy it if right clicked
        protected override void InteractI(Inputs inputs)
        {
            base.InteractI(inputs);
            bool collides = CircularObject.Collides(WorldPosition, inputs.MouseWorldPos, radius);
            if (inputs.MouseLeft.Down) selected = collides;
            if (inputs.MouseRight.Down && collides) Destroy();
        }
        public override string ToString()
        {
            return $"Vehicle(Position:{WorldPosition}, Color:{triangle_handle.color})";
        }
        //remove vehicle from current trajectory and all path events, inside which it is
        protected override void DestroyI()
        {
            base.DestroyI();
            if (m_planned_path.Count != 0) CurTrajectory.RemoveVehicle(this);
            foreach (var ev in m_path_events) if (ev.inside) ev.VehicleLeaves();
        }
    }
}
