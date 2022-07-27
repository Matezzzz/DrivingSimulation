using System;
using static SDL2.SDL;
using System.Diagnostics;

namespace DrivingSimulation
{

    enum World
    {
        DEBUG, CROSSROADS_T, CROSSROADS_X, QUAD_CROSSROADS, MAIN_ROAD, PANKRAC
    }

    class DrivingSimulation : IDisposable
    {
        readonly SDLApp app;
        readonly RoadWorld world;
        readonly Camera camera;
        bool paused = false;
        int steps_per_frame = 1;

        public DrivingSimulation(World initial_world, int thread_count, bool predraw_enabled, SDLApp app)
        {
            this.app = app;
            camera = new(app.ScreenSize);
            world = thread_count == 1 ? new SingleThreadedRoadWorld(app, predraw_enabled) : new MultiThreadedRoadWorld(app, predraw_enabled, thread_count);
            SetNewWorld(initial_world);
            world.Finish();
        }
        public void SetNewWorld(World world_type)
        {
            RoadGraph g = world_type switch
            {
                World.DEBUG => new DebugCrossroadsRoadGraph(world),
                World.CROSSROADS_T => new CrossroadsTRoadGraph(world),
                World.CROSSROADS_X => new CrossroadsXRoadGraph(world),
                World.QUAD_CROSSROADS => new QuadCrossroadsRoadGraph(world),
                World.MAIN_ROAD => new MainRoadWorldGraph(world),
                World.PANKRAC => new PankracWorldGraph(world),
                _ => throw new NotImplementedException("Invalid world type")
            };
            camera.Reset(g);
        }

        const float dt = 1 / 60f;
        public void Update()
        {
            PollEvents();
            if (!paused)
            {
                for (int i = 0; i < steps_per_frame; i++)
                {
                    world.Update(dt);
                    world.PostUpdate();
                }
            }
            camera.Update();
        }


        void PollEvents()
        {
            foreach (SDL_Event e in app.PollEvents())
            {
                if (e.type == SDL_EventType.SDL_MOUSEWHEEL) camera.Zoom(e.wheel.preciseY);
                else if (e.type == SDL_EventType.SDL_MOUSEMOTION && (e.motion.state & SDL_BUTTON_LMASK) != 0) camera.Move(e.motion.xrel, e.motion.yrel);
                else if (e.type == SDL_EventType.SDL_MOUSEBUTTONDOWN && e.button.button == SDL_BUTTON_RIGHT)
                {
                    Action<Vehicle> action = app.CtrlPressed ? ((Vehicle v) => v.Disable()) : ((Vehicle v) => v.Select());
                    world.OverlappingCarsAction(camera.transform, e.button.x, e.button.y, action);
                }
                else if (e.type == SDL_EventType.SDL_KEYDOWN)
                {
                    switch (e.key.keysym.sym)
                    {
                        case SDL_Keycode.SDLK_SPACE:
                            paused = !paused;
                            break;
                        case SDL_Keycode.SDLK_b:
                            Benchmark();
                            break;
                        case SDL_Keycode.SDLK_UP:
                            steps_per_frame++;
                            break;
                        case SDL_Keycode.SDLK_DOWN:
                            steps_per_frame = Math.Max(steps_per_frame - 1, 1);
                            break;
                        case SDL_Keycode.SDLK_g:
                            world.DebugGridEnabled = !world.DebugGridEnabled;
                            break;
                        case SDL_Keycode.SDLK_LEFT:
                            world.VehicleGenerationIntensity = Math.Max(world.VehicleGenerationIntensity - 0.1f, 0);
                            break;
                        case SDL_Keycode.SDLK_RIGHT:
                            world.VehicleGenerationIntensity = Math.Min(world.VehicleGenerationIntensity + 0.1f, 1);
                            break;
                    }
                }
            }
        }


        const int benchmark_steps = 100000;
        public void Benchmark()
        {
            Stopwatch time = new();
            time.Start();

            for (int i = 0; i < benchmark_steps; i++)
            {
                Update();
            }
            time.Stop();
            Console.WriteLine($"Time to run 100000 frames:{time.ElapsedMilliseconds}ms");
        }
        public void Draw()
        {
            world.Draw(app, camera.transform);
        }
        public void Dispose()
        {
            ((IDisposable) world).Dispose();
        }
    }
}
