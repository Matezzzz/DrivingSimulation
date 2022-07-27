namespace DrivingSimulation
{
    class MainCls
    {
        //Benchmarks (Final version), Debug config:

        //Main road: (100000 its.)
        //1 -> 10752ms; 2 -> 6645ms; 4 -> 4980; 8 -> 4311; 16 -> 5474

        //Pankrac
        //1 -> ; 2 ->; 4 ->; 8 ->; 16 ->;


        //SAFE SPOT CAPACITY? WORTH LOOKING INTO

        static void Main(string[] _)
        {
            SDLApp app = new(1400, 1400);

            DrivingSimulation sim = new(World.MAIN_ROAD, 4, false, app);

            //sim.Benchmark();

            // Main loop for the program
            while (app.running)
            {
                sim.Update();
                app.Fill(Color.DarkGray);
                sim.Draw();
                app.Present();
            }
            sim.Dispose();
        }
    }
}
