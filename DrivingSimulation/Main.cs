namespace DrivingSimulation
{
    class MainCls
    {
        //spot becomes inactive due to safe spot being full - both roads go in at once -> two enter at the same time, error (probably fixed - modulo hack?)
        //saving and loading - static variables!! - marked as property, fixed?
        //deformed crossroads - sometimes loads forever!
        //check all files for unitialized variables
        //Fix 'error' being written - either delete the line or fix the cause

        static void Main(string[] _)
        {
            //DrivingSimulation.SaveDefaultWorlds();

            SDLApp app = new(1400, 1400);

            //DrivingSimulation sim = new("quad_crossroads", 8, app);
            DrivingSimulation sim = new(World.MAIN_ROAD, 1, app);

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
