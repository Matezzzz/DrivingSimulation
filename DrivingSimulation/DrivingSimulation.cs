using System;
using static SDL2.SDL;
using System.Diagnostics;
using System.Collections.Generic;
using static SDL2.SDL.SDL_Keycode;

namespace DrivingSimulation
{

    //all loadable worlds. These can be then modified in the edit mode
    enum InitialWorld
    {
        EMPTY, DEBUG, CROSSROADS_T, CROSSROADS_X, ROUNDABOUT, QUAD_CROSSROADS, MAIN_ROAD, PANKRAC
    }


    

    //Runs the whole driving simulation
    class DrivingSimulation
    {
        readonly SDLApp app;
        //contains all simulation objects
        readonly RoadWorld world;
        //manages a single transform, can move and zoom in
        readonly Camera camera;

        //how many times to call update before rendering once
        int steps_per_frame = 1;

        enum AppMode
        {
            EDIT, SIMULATION
        }
        AppMode mode = AppMode.EDIT;

        //manages inputs from keyboard and mouse
        readonly Inputs inputs;

        //for adding new objects to world
        readonly AngleMenu add_new_menu;        

        //list of all default worlds. Can be used to save them all to .json quickly using the method below
        readonly static List<(string, InitialWorld)> default_worlds = new()
        {   
            ("debug", InitialWorld.DEBUG), ("crossroads_t", InitialWorld.CROSSROADS_T), ("crossroads_x", InitialWorld.CROSSROADS_X),
            ("roundabout", InitialWorld.ROUNDABOUT), ("quad_crossroads", InitialWorld.QUAD_CROSSROADS), ("main_road", InitialWorld.MAIN_ROAD), ("pankrac", InitialWorld.PANKRAC)
        };

        //save all default worlds to json
        public static void SaveDefaultWorlds()
        {
            //for all possible worlds, create a new loadable world, load it, then save it as json
            foreach ((string fname, InitialWorld world) in default_worlds)
            {
                SingleThreadedRoadWorld x = new();
                LoadWorldGraph(x, world);
                x.UpdateObjects();
                x.Save(fname);
            }
        }

        //Create a driving simulation with given app on given number of threads
        private DrivingSimulation(int thread_count, SDLApp app)
        {
            this.app = app;
            //select single- or multi-threaded world based on thread count
            world = thread_count == 1 ? new SingleThreadedRoadWorld() : new MultiThreadedRoadWorld(thread_count);
            camera = new(app.ScreenSize);
            inputs = new(app);
            //create object creation menu
            add_new_menu = new(new AngleMenuOption[] {
                new(Texture.CrossroadsX, () => world.CrossroadsX(inputs.MouseWorldPos, 0, 2)),
                new(Texture.CrossroadsT, () => world.CrossroadsT(inputs.MouseWorldPos, 0, 2)),
                new(Texture.TwoWay,      () => world.Vertical2Side(inputs.MouseWorldPos, false)),
                new(Texture.OneWay,      () => world.Vertical1Side(inputs.MouseWorldPos, false, false))
            });
        }

        //Create a simulation with an initial world
        public DrivingSimulation(InitialWorld init_world, int thread_count, SDLApp app) : this(thread_count, app)
        {
            LoadWorldGraph(world, init_world);
            camera.Reset(world);
        }
        //load a simulation from .json. Actual location should be maps/{world_filename}.json
        public DrivingSimulation(string world_filename, int thread_count, SDLApp app) : this(thread_count, app)
        {
            Load(world_filename);
        }
        //load initial world graph into the given world
        public static void LoadWorldGraph(RoadWorld world, InitialWorld world_type)
        {
            _ = world_type switch
            {
                InitialWorld.EMPTY => new ExampleWorld(world, new Vector2(20)),
                InitialWorld.DEBUG => new DebugCrossroadsExampleWorld(world),
                InitialWorld.CROSSROADS_T => new CrossroadsTExampleWorld(world),
                InitialWorld.CROSSROADS_X => new CrossroadsXExampleWorld(world),
                InitialWorld.ROUNDABOUT => new RoundaboutExampleWorld(world),
                InitialWorld.QUAD_CROSSROADS => new QuadCrossroadsExampleWorld(world),
                InitialWorld.MAIN_ROAD => new MainRoadExampleWorld(world),
                InitialWorld.PANKRAC => new PankracExampleWorld(world),
                _ => throw new NotImplementedException("Invalid world type")
            };
        }

        
        public void Update()
        {
            //move and zoom camera smoothly
            camera.Update();
            //update mouse world position, update all key presses, manage selected objects, create new connections/garages
            inputs.Update(world, camera.transform);
            //check all events that happened in the last frame
            inputs.PollEvents();
            //
            Interact();
            add_new_menu.Interact(inputs);

            for (int i = 0; i < steps_per_frame; i++)
            {
                world.Update();
                world.PostUpdate();
            }
            world.Interact(inputs);
        }


        void Interact()
        {
            //camera movement
            camera.Interact(inputs);

            //enable/disable debug grid
            if (inputs.Get(SDLK_BACKQUOTE).Down) world.DebugGridEnabled = !world.DebugGridEnabled;

            switch (mode)
            {
                case AppMode.EDIT:
                    if (inputs.LCtrl.Pressed)
                    {
                        //ctrl + s = save
                        if (inputs.Get(SDLK_s).Down) Save();
                        //ctrl + o = open(load)
                        if (inputs.Get(SDLK_o).Down) Load();
                    }
                    //enter pressed -> go to simulation mode
                    if (inputs.Get(SDLK_RETURN).Down)
                    {
                        mode = AppMode.SIMULATION;
                        world.Finish();
                    }
                    break;
                case AppMode.SIMULATION:
                    //benchmark
                    if (inputs.Get(SDLK_b).Down) Benchmark();
                    //speed up simulation
                    if (inputs.Get(SDLK_UP).Down) steps_per_frame++;
                    //slow down simulation
                    if (inputs.Get(SDLK_DOWN).Down) steps_per_frame = Math.Max(steps_per_frame - 1, 0);
                    //spawn less vehicles
                    if (inputs.Get(SDLK_LEFT).Down) world.VehicleGenerationIntensity = Math.Max(world.VehicleGenerationIntensity - 0.1f, 0);
                    //spawn more vehicles (only up to 100% garage capacity)
                    if (inputs.Get(SDLK_RIGHT).Down) world.VehicleGenerationIntensity = Math.Min(world.VehicleGenerationIntensity + 0.1f, 1);
                    if (inputs.Get(SDLK_RETURN).Down)
                    {
                        mode = AppMode.EDIT;
                        world.Unfinish();
                    }
                    //if delete is pressed, remove all vehicles currently alive
                    if (inputs.Get(SDLK_DELETE).Down) world.RemoveAllVehicles();
                    break;
            }
        }

        //save the word to a JSON file
        public void Save()
        {
            Console.Write("Save the map as maps/");
            string filename = Console.ReadLine();
            world.Save(filename);
        }
        //load the world from a JSON file. if filename is null, get one from user
        public void Load(string filename = null)
        {
            //read filename from console
            if (filename == null)
            {
                Console.Write("Load the map from file maps/");
                filename = Console.ReadLine();
            }
            world.Load(filename);
            //center camera in new world
            camera.Reset(world);
        }

        //run a lot of update steps and measure the time it took to do so
        public void Benchmark()
        {
            Stopwatch time = new();
            time.Start();
            for (int i = 0; i < Constant.benchmark_steps; i++)
            {
                world.Update();
                world.PostUpdate();
                if (i % Constant.benchmark_reset_frequency == 0) world.RemoveAllVehicles();
            }
            time.Stop();
            Console.WriteLine($"Time to run {Constant.benchmark_steps} steps: {time.ElapsedMilliseconds}ms");
        }
        public void Draw()
        {
            //draw the world, then the add new menu
            world.DrawWorld(app, camera.transform);
            add_new_menu.Draw(app);
        }
        public void Destroy() => world.Destroy();
    }
}
