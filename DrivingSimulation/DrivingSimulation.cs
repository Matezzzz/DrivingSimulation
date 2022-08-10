using System;
using static SDL2.SDL;
using System.Diagnostics;
using System.Collections.Generic;
using static SDL2.SDL.SDL_Keycode;

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
        int steps_per_frame = 1;
        bool finished = false;
        readonly Inputs inputs;
        readonly AngleMenu add_new_menu;

        const float dt = 1 / 60f;
        const int benchmark_steps = 100000;


        readonly static List<(string, World)> default_worlds = new() {("debug", World.DEBUG), ("crossroads_t", World.CROSSROADS_T), ("crossroads_x", World.CROSSROADS_X), ("quad_crossroads", World.QUAD_CROSSROADS), ("main_road", World.MAIN_ROAD), ("pankrac", World.PANKRAC) };

        static RoadWorld CreateWorld(int thread_count)
        {
            return thread_count == 1 ? new SingleThreadedRoadWorld() : new MultiThreadedRoadWorld(thread_count);
        }
        
        public static void SaveDefaultWorlds()
        {
            foreach ((string fname, World world) in default_worlds)
            {
                LoadableRoadWorld x = new();
                LoadWorldGraph(x, world);
                x.Save(fname);
            }
        }

        private DrivingSimulation(int thread_count, SDLApp app)
        {
            this.app = app;
            world = CreateWorld(thread_count);
            camera = new(app.ScreenSize);
            inputs = new(app);
            add_new_menu = new(new AngleMenuOption[] {
                new(Texture.CrossroadsX, () => world.CrossroadsX(inputs.MouseWorldPos)),
                new(Texture.CrossroadsT, () => world.CrossroadsT(inputs.MouseWorldPos)),
                new(Texture.TwoWay,      () => world.Vertical2Side(inputs.MouseWorldPos, false)),
                new(Texture.OneWay,      () => world.Vertical1Side(inputs.MouseWorldPos, false, false))
            });
        }

        public DrivingSimulation(World init_world, int thread_count, SDLApp app) : this(thread_count, app)
        {
            LoadWorldGraph(world, init_world);
            camera.Reset(world.Graph);
        }

        public DrivingSimulation(string world_filename, int thread_count, SDLApp app) : this(thread_count, app)
        {
            Load(world_filename);
        }
        public static void LoadWorldGraph(RoadWorld world, World world_type)
        {
            RoadGraph _ = world_type switch
            {
                World.DEBUG => new DebugCrossroadsRoadGraph(world),
                World.CROSSROADS_T => new CrossroadsTRoadGraph(world),
                World.CROSSROADS_X => new CrossroadsXRoadGraph(world),
                World.QUAD_CROSSROADS => new QuadCrossroadsRoadGraph(world),
                World.MAIN_ROAD => new MainRoadWorldGraph(world),
                World.PANKRAC => new PankracWorldGraph(world),
                _ => throw new NotImplementedException("Invalid world type")
            };
        }

        
        public void Update()
        {
            camera.Update();
            inputs.Update(world, camera.transform);
            inputs.PollEvents();
            CheckEvents();
            add_new_menu.Interact(inputs);

            for (int i = 0; i < steps_per_frame; i++)
            {
                world.Update(dt);
                world.PostUpdate();
            }
            world.Interact(inputs);
        }


        void CheckEvents()
        {
            if (inputs.Get(SDLK_SPACE).Pressed) camera.ZUp();
            if (inputs.Get(SDLK_LSHIFT).Pressed) camera.ZDown();
            if (inputs.Get(SDLK_w).Pressed) camera.Up();
            if (inputs.Get(SDLK_a).Pressed) camera.Left();
            if (inputs.Get(SDLK_s).Pressed) {
                if (inputs.Get(SDLK_LCTRL).Pressed) Save();
                else camera.Down();
            }
            if (inputs.Get(SDLK_d).Pressed) camera.Right();





            if (inputs.Get(SDLK_b).Down) Benchmark();
            if (inputs.Get(SDLK_LCTRL).Pressed && inputs.Get(SDLK_o).Pressed) Load();

            if (inputs.Get(SDLK_UP).Down) steps_per_frame++;
            if (inputs.Get(SDLK_DOWN).Down) steps_per_frame = Math.Max(steps_per_frame - 1, 0);

            if (inputs.Get(SDLK_g).Down) world.DebugGridEnabled = !world.DebugGridEnabled;

            if (inputs.Get(SDLK_LEFT).Down) world.VehicleGenerationIntensity = Math.Max(world.VehicleGenerationIntensity - 0.1f, 0);
            if (inputs.Get(SDLK_RIGHT).Down) world.VehicleGenerationIntensity = Math.Min(world.VehicleGenerationIntensity + 0.1f, 1);



            if (inputs.Get(SDLK_RETURN).Down)
            {
                if (finished) world.Unfinish();
                else world.Finish();
                finished = !finished;
            }
        }

        public void Save()
        {
            Console.Write("Save the map as maps/");
            string filename = Console.ReadLine();
            world.Save(filename);
        }
        public void Load(string filename = null)
        {
            if (filename == null)
            {
                Console.Write("Load the map from file maps/");
                filename = Console.ReadLine();
            }
            world.Load(filename);
            camera.Reset(world.Graph);
        }

        
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
            world.Draw(app, camera.transform, SimulationObject.DrawLayer.WORLD);
            add_new_menu.Draw(app);
        }
        public void Dispose()
        {
            world.Destroy();
        }
    }
}
