namespace DrivingSimulation
{
    class MainCls
    {
        static void Main(string[] _)
        {
            DrivingSimulation.SaveDefaultWorlds();

            SDLApp app = new(1400, 1400);

            DrivingSimulation sim = new(InitialWorld.MAIN_ROAD, 8, app);

            // Main loop for the program
            while (app.running)
            {
                sim.Update();
                app.Fill(Color.DarkGray);
                sim.Draw();
                app.Present();
            }
            sim.Destroy();
            app.Destroy();
        }
    }
}
