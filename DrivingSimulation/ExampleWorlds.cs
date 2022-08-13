
using System;
using System.Collections.Generic;


namespace DrivingSimulation
{
    //Empty world. Build something yourself :D
    class ExampleWorld
    {
        protected RoadWorld Wr;
        public ExampleWorld(RoadWorld world, Vector2 size)
        {
            Wr = world;
            world.settings.SetWorldSize(size);
        }




        //All code is just to create crossroads in one line. DEF = default = road heading from crossroads, INV = inverted = heading to crossroads. No difference for 2-sided roads
        //Is sort-of cryptic, just see how it is used in pankrac example below

        protected const int DEF1 = 0, INV1 = 1, DEF2 = 2;



        delegate RoadPlugView GetPlug1Side(SimulationObjectCollection g, bool inv);
        delegate RoadPlugView GetPlug2Side(SimulationObjectCollection g);
        //create a road in a given collection, with a function for 1 side, 2 side, 
        static RoadPlugView Get(SimulationObjectCollection c, int i, GetPlug1Side side1, GetPlug2Side side2)
        {
            if (i == DEF1) return side1(c, false);
            if (i == INV1) return side1(c, true);
            if (i == DEF2) return side2(c);
            throw new ArgumentException($"Wrong i for get: {i}");
        }

        //lambda for every road type and side
        readonly GetPlug1Side l1 = (a, inv) => a.DefaultLeft1Side(inv).GetView(false);
        readonly GetPlug2Side l2 = (a) => a.DefaultLeft2Side().GetView(false);
        readonly GetPlug1Side b1 = (a, inv) => a.DefaultBottom1Side(inv).GetView(false);
        readonly GetPlug2Side b2 = (a) => a.DefaultBottom2Side().GetView(false);
        readonly GetPlug1Side r1 = (a, inv) => a.DefaultRight1Side(inv).GetView(false);
        readonly GetPlug2Side r2 = (a) => a.DefaultRight2Side().GetView(false);
        readonly GetPlug1Side t1 = (a, inv) => a.DefaultTop1Side(inv).GetView(false);
        readonly GetPlug2Side t2 = (a) => a.DefaultTop2Side().GetView(false);

        //given 3 ints from (DEF1, INV1, DEF2) set, create a T crossroads on position x, y, with given rotation, scale and priorities
        protected CrossroadsT CrossroadsT(int left, int bot, int right, double x, double y, float rotation, float scale = 3, List<int> priorities = null)
        {
            return CrossroadsT(left, bot, right, x, y, rotation, new Vector2(scale), priorities);
        }
        //same as above, but with Vector2 scale
        protected CrossroadsT CrossroadsT(int left, int bot, int right, double x, double y, float rotation, Vector2 scale, List<int> priorities = null)
        {
            return Wr.CrossroadsT(new Vector2(x, y), -rotation, scale, new List<Func<SimulationObjectCollection, RoadPlugView>>() { x => Get(x, left, l1, l2), x => Get(x, bot, b1, b2), x => Get(x, right, r1, r2) }, priorities);
        }
        //given 4 ints from (DEF1, INV1, DEF2) set, create a T crossroads on position x, y, with given rotation, scale and priorities
        protected CrossroadsX CrossroadsX(int top, int left, int bot, int right, double x, double y, float rotation, float scale = 3, List<int> priorities = null)
        {
            return Wr.CrossroadsX(new Vector2(x, y), -rotation, scale, new List<Func<SimulationObjectCollection, RoadPlugView>>() { x => Get(x, top, t1, t2), x => Get(x, left, l1, l2), x => Get(x, bot, b1, b2), x => Get(x, right, r1, r2) }, priorities);
        }


    }



    //Just X crossroads with two garages. To see how the bare minimum of giving right of way looks
    class DebugCrossroadsExampleWorld : ExampleWorld
    {
        public DebugCrossroadsExampleWorld(RoadWorld w) : base(w, new Vector2(16, 16))
        {
            var C = w.CrossroadsX(new Vector2(8, 8), 0, 3);
            w.GarageRoad(C.T, new PeriodicGarage(8, new ConstantColorPicker(Color.White)));
            w.GarageRoad(C.B, new EmptyGarage(), false); //doesnt spawn or accept cars
            w.GarageRoad(C.L, new PeriodicBurstGarage(.75f, 6, 4, new LoopColorPicker()), false);
            w.GarageRoad(C.R, new EmptyGarage()); //doesn't spawn cars
        }
    }

    //just a basic T crossroads with unidirectional roads and no cross crysis points
    class CrossroadsTExampleWorld : ExampleWorld
    {
        public CrossroadsTExampleWorld(RoadWorld w) : base(w, new Vector2(16, 10))
        {
            var C = w.CrossroadsT(new Vector2(8, 2), 0, new Vector2(3), new List<Func<SimulationObjectCollection, RoadPlugView>>() { x => x.DefaultLeft1Side(true, .5f).GetView(false), x => x.DefaultBottom2Side(0.5f).GetView(false), x => x.DefaultRight1Side(false, .5f).GetView(false) });
            SpontaneusGarage g = new(1, 0.03f, new LoopColorPicker());
            w.GarageRoad(C.L, g); w.GarageRoad(C.B, g); w.GarageRoad(C.R, g);
        }
    }


    //crossroads X with all roads bidirectional
    class CrossroadsXExampleWorld : ExampleWorld
    {
        public CrossroadsXExampleWorld(RoadWorld w) : base(w, new Vector2(16, 16))
        {
            var C = w.CrossroadsX(new Vector2(8, 8), 0, 3);

            w.GarageRoad(C.T, new PeriodicBurstGarage(.5f, 2, 8, new LoopColorPicker()), 2);
            w.GarageRoad(C.B, new PeriodicBurstGarage(.5f, 3, 10, new LoopColorPicker()));
            w.GarageRoad(C.L, new PeriodicGarage(8, new ConstantColorPicker(Color.White)));
            w.GarageRoad(C.R, new SpontaneusGarage(5f, 0.03f, new ConstantColorPicker(Color.DarkCyan)));
        }
    }

    // a roundabout with 4 incoming paths
    class RoundaboutExampleWorld : ExampleWorld
    {
        public RoundaboutExampleWorld(RoadWorld w) : base(w, new Vector2(16, 16))
        {
            var B = CrossroadsT(INV1, DEF2, DEF1, 8,11,   0, 1, new List<int>() { 1, 0, 1});
            var R = CrossroadsT(INV1, DEF2, DEF1,11, 8,  90, 1, new List<int>() { 1, 0, 1 });
            var T = CrossroadsT(INV1, DEF2, DEF1, 8, 5, 180, 1, new List<int>() { 1, 0, 1 });
            var L = CrossroadsT(INV1, DEF2, DEF1, 5, 8, 270, 1, new List<int>() { 1, 0, 1 });

            w.GarageRoad(B.B, new SpontaneusGarage(3f, .01f, new ConstantColorPicker(Color.Red)), true, 3);
            w.GarageRoad(R.B, new SpontaneusGarage(3f, .01f, new ConstantColorPicker(Color.Blue)), true, 3);
            w.GarageRoad(T.B, new SpontaneusGarage(3f, .01f, new ConstantColorPicker(Color.Green)), true, 3);
            w.GarageRoad(L.B, new SpontaneusGarage(3f, .01f, new ConstantColorPicker(Color.Black)), true, 3);

            w.Connect(B.R, R.L); w.Connect(R.R, T.L); w.Connect(T.R, L.L); w.Connect(L.R, B.L);
        }
    }



    //4 bidirectional X crossroads with main roads left-to-right and garages on all sides
    class QuadCrossroadsExampleWorld : ExampleWorld
    {
        public QuadCrossroadsExampleWorld(RoadWorld w) : base(w, new Vector2(26, 26))
        {
            Garage g = new SpontaneusGarage(5, 0.01f, new LoopColorPicker());
            var A = w.CrossroadsXMainLR(new Vector2(8, 8), 0, 3);
            var B = w.CrossroadsXMainLR(new Vector2(18, 8), 0, 3);
            var C = w.CrossroadsXMainLR(new Vector2(8, 18), 0, 3);
            var D = w.CrossroadsXMainLR(new Vector2(18, 18), 0, 3);

            foreach (RoadPlugView p in new RoadPlugView[] { A.T, B.T, A.L, C.L, C.B, D.B, B.R, D.R })
            {
                w.GarageRoad(p, g.Copy());
            }
            w.Connect(A.B, C.T); w.Connect(B.B, D.T); w.Connect(A.R, B.L); w.Connect(C.R, D.L);
        }
    }


    //A high speed, main road in the middle, and two smaller roads on top and bottom.
    class MainRoadExampleWorld : ExampleWorld
    {
        public MainRoadExampleWorld(RoadWorld Wr) : base(Wr, new Vector2(40, 30))
        {
            //top crossroads
            var T1 = Wr.CrossroadsT(new Vector2(10, 2), 0, 2);
            var T2 = Wr.CrossroadsT(new Vector2(30, 2), 0, 2);
            //top roads and garages
            SpontaneusGarage top_spawn = new(5, 0.02f, new ConstantColorPicker(Color.Black));
            Wr.GarageRoad(T1.L, top_spawn);
            Wr.Connect(T1.R, T2.L);
            Wr.GarageRoad(T2.R, top_spawn);

            //middle, high speed road
            var M1 = Wr.CrossroadsXMainLR(new Vector2(10, 15), 0, 5).SetMaxSpeed(3);
            var M2 = Wr.CrossroadsXMainLR(new Vector2(30, 15), 0, 5).SetMaxSpeed(3);
            //connections mid to top
            var TW1 = Wr.Road(M1.T, 3, 1).SetMaxSpeed(2); Wr.Connect(T1.B, TW1.A);
            var TW2 = Wr.Road(M2.T, 3, 1).SetMaxSpeed(2); Wr.Connect(T2.B, TW2.A);

            //mid roads
            Wr.GarageRoad(M1.L, new PeriodicBurstGarage(.5f, 6, 3, new LoopColorPicker()), 4).SetMaxSpeed(3);
            Wr.Connect(M1.R, M2.L).SetMaxSpeed(3);
            Wr.GarageRoad(M2.R, new PeriodicBurstGarage(.5f, 6, 3, new LoopColorPicker()), 4).SetMaxSpeed(3);

            //bottom crossroads
            var B = Wr.CrossroadsT(new Vector2(10, 28), 180, 2);

            //roads mid-to-bottom
            var BW1 = Wr.Road(M1.B, 3, 1).SetMaxSpeed(2); Wr.Connect(B.B, BW1.A);
            var BW2 = Wr.Road(M2.B, 3, 1).SetMaxSpeed(2);

            //bottom turn
            var BRT = Wr.Road(BW2.A, 3, 1);
            var BRL = Wr.Road(B.L, 16);
            Wr.Connect(BRT.A, BRL.A);

            //bottom garage
            SpontaneusGarage bot_spawn = new(5, 0.02f, new ConstantColorPicker(Color.DarkGray));
            Wr.GarageRoad(B.R, bot_spawn);
        }
    }


    //A replica of the real-life road system near Pankrac, Prague 4, where I got my drivers license. Bigger than all other maps combined.
    //Search for the road 'U družstva ideál' to check it out on google maps
    class PankracExampleWorld : ExampleWorld
    {

 

        public PankracExampleWorld(RoadWorld w) : base(w, new Vector2(120, 120))
        {
            RoadWorld.default_curve_coeff = 0.5f;

            //modify camera settings - zoom faster on the large map
            w.settings.CameraZFrom = 5;
            w.settings.CameraZTo = 80;
            w.settings.CameraZoomSpeed = 0.3f;
            w.settings.PathRandomizationFrom = 0.5f;
            w.settings.PathRandomizationTo = 3;
            w.settings.RecommendedVehicleCount = 600;

            //Well, just list all the crossroads, roads and made up garages. Nothing interesting here.


            //DO = Druzstevni ochoz
            var DO_A = Wr.Base2Side(new Vector2(32.4, 36.3), -45).Placed.SetRoadWidth(1.5f).GetObject();
            var DO_B = CrossroadsT(DEF2, DEF1, DEF2, 54.7, 22.8, 25); Wr.Connect(DO_A.GetView(false), DO_B.L);
            var DO_C = CrossroadsT(DEF2, DEF2, DEF2, 70.8, 15.6, 25); Wr.Connect(DO_B.R, DO_C.L);
            var DO_D = CrossroadsT(DEF2, DEF2, DEF2, 78.9, 11.8, 25); Wr.Connect(DO_C.R, DO_D.L);
            var DO_DE = CrossroadsT(DEF2, DEF2, DEF2, 95.8, 9.2, 150); Wr.Connect(DO_D.R, DO_DE.R); w.GarageRoad(DO_DE.B, new PeriodicBurstGarage(.75f, 6, 4, new LoopColorPicker()));
            var DO_E = CrossroadsT(DEF2, DEF2, DEF2, 97.4, 26.0, -100); Wr.Connect(DO_DE.L, DO_E.L); Wr.Connect(DO_D.B, DO_E.B);
            var DO_F = CrossroadsT(DEF2, DEF1, DEF2, 95.8, 34.3, -100); Wr.Connect(DO_E.R, DO_F.L);
            var DO_G = CrossroadsT(DEF2, INV1, DEF2, 92.5, 52.5, -90, new Vector2(1.5, 3)); Wr.Connect(DO_F.R, DO_G.L);
            var DO_H = CrossroadsT(DEF2, INV1, DEF2, 93.0, 56.8, -80,  new Vector2(1.5, 3));  Wr.Connect(DO_G.R, DO_H.L);
            var DO_I = CrossroadsT(DEF2, INV1, DEF2, 102.1, 76.2, -75, new Vector2(3, 1.25), new List<int>() { 1, 0, 1}); Wr.Connect(DO_H.R, DO_I.L);
            var DO_IJ = CrossroadsT(DEF2, DEF2, DEF2, 109.1, 92.3, 50); Wr.Connect(DO_I.R, DO_IJ.R); w.GarageRoad(DO_IJ.B, new PeriodicBurstGarage(.75f, 6, 4, new LoopColorPicker()));
            var DO_J = CrossroadsT(DEF2, DEF1, DEF2, 87.2, 98.0, 190, new Vector2(2, 3)); Wr.Connect(DO_IJ.L, DO_J.L);
            var DO_K = CrossroadsT(DEF2, INV1, DEF2, 81.5, 98.5, 180, new Vector2(2, 3)); Wr.Connect(DO_J.R, DO_K.L);
            var DO_L = CrossroadsT(DEF2, DEF1, DEF2, 47.3, 96.2, -190); Wr.Connect(DO_K.R, DO_L.L);
            var DO_M = CrossroadsX(DEF2, DEF2, INV1, DEF1, 21.0, 81.9, 135);  Wr.Connect(DO_L.R, DO_M.L); w.GarageRoad(DO_M.T, new PeriodicBurstGarage(.75f, 6, 10, new LoopColorPicker()), 0.5f);
            
            var DO_MN = Wr.Base1Side(new Vector2(14.0, 59.4), -55, true, 5); Wr.Connect(DO_MN.GetView(false), DO_M.R);
            var DO_N = Wr.Base1Side(new Vector2(19.5, 50.2), -45, true, 2); Wr.Connect(DO_MN.GetView(true), DO_N.GetView(false));

            //R = Rovnobezna
            var R_A = CrossroadsT(DEF1, DEF1, INV1, 28.7, 54.8, 143); Wr.Connect(DO_N.GetView(true), R_A.R);
            var R_B = CrossroadsT(DEF2, INV1, DEF1, 37.8, 44.2, 323); Wr.Connect(DO_A.GetView(true), R_B.L); Wr.Connect(R_A.B, R_B.B);


            //4 rotations = 53, 143, 233, 323

            //S = Sdruzeni
            var S_A = CrossroadsT(DEF1, INV1, INV1, 29.0, 71.4, 53); Wr.Connect(S_A.L, DO_M.B);
            var S_B = CrossroadsT(INV1, INV1, DEF1, 36.4, 61.4, 233); Wr.Connect(S_A.R, S_B.R); Wr.Connect(S_B.B, R_A.L);
            var S_C = CrossroadsT(INV1, INV1, DEF1, 45.8, 49.3, 233); Wr.Connect(S_B.L, S_C.R); Wr.Connect(S_C.B, R_B.R);
            var S_D = CrossroadsT(DEF1, INV1, INV1, 54.3, 39.4, 53); Wr.Connect(S_C.L, S_D.L);
            var S_E = CrossroadsT(INV1, INV1, DEF1, 59.2, 32.6, 233); Wr.Connect(S_D.R, S_E.R); Wr.Connect(DO_B.B, S_E.B);
            var S_F = CrossroadsT(DEF2, DEF1, INV1, 75.3, 24.8, -60, new Vector2(2.5, 3)); Wr.Connect(S_E.L, S_F.B); Wr.Connect(DO_C.B, S_F.L);

            //UDI - U druzstva ideal
            var UDI_A = CrossroadsT(DEF2, INV1, DEF1, 38.3, 79.0, 143, new Vector2(1.5, 3)); Wr.Connect(UDI_A.R, S_A.B);
            var UDI_B = CrossroadsT(INV1, DEF1, DEF2, 41.5, 81.5, 143, new Vector2(1.5, 3)); Wr.Connect(UDI_A.L, UDI_B.R);
            var UDI_AC =CrossroadsT(INV1, DEF2, DEF1, 44.6, 70.6, 233); Wr.Connect(UDI_A.B, UDI_AC.R); w.GarageRoad(UDI_AC.B, new PeriodicBurstGarage(.75f, 6, 4, new LoopColorPicker()));
            var UDI_C = CrossroadsT(DEF1, DEF1, INV1, 63.7, 46.7, 323, new Vector2(.9, 3)); Wr.Connect(UDI_C.L, S_D.B); Wr.Connect(UDI_C.B, UDI_AC.L);
            var UDI_D = CrossroadsT(DEF1, INV1, DEF1, 67.0, 49.1, 323, new Vector2(.9, 3)); Wr.Connect(UDI_D.B, UDI_B.B);
            var UDI_E = CrossroadsT(INV1, DEF1, DEF1, 65.4, 47.9, 143, new Vector2(.9, 3)); Wr.Connect(UDI_E.R, UDI_C.R); Wr.Connect(UDI_E.L, UDI_D.L);
            var UDI_F = CrossroadsT(DEF1, INV1, INV1, 79.7, 29.9, 323, new Vector2(2.5, 3)); Wr.Connect(UDI_F.L, S_F.R); Wr.Connect(UDI_F.B, UDI_E.B);

            //UDZ - U druzstva zivot
            var UDZ_A = CrossroadsT(DEF1, DEF1, INV1, 50.9, 88.1, 233); Wr.Connect(DO_L.B, UDZ_A.R); Wr.Connect(UDI_B.L, UDZ_A.B);
            var UDZ_B = CrossroadsX(DEF2, INV1, DEF1, DEF1, 59.4, 79.2, 53); Wr.Connect(UDZ_B.L, UDZ_A.L); w.GarageRoad(UDZ_B.T, new PeriodicBurstGarage(.75f, 6, 10, new LoopColorPicker()), 0.5f);
            var UDZ_C = CrossroadsT(DEF1, INV1, INV1, 76.7, 56.0, 233); Wr.Connect(UDZ_C.B, UDI_D.R); Wr.Connect(UDZ_C.R, UDZ_B.R);
            var UDZ_D = CrossroadsT(INV1, DEF1, DEF1, 82.1, 49.7, 53); Wr.Connect(UDZ_D.L, UDZ_C.L); Wr.Connect(UDZ_D.B, DO_G.B);
            var UDZ_E = CrossroadsT(DEF1, INV1, INV1, 85.5, 33.0, -20, new Vector2(2.5, 3)); Wr.Connect(UDZ_E.L, UDI_F.R); Wr.Connect(UDZ_E.B, UDZ_D.R); Wr.Connect(UDZ_E.R, DO_F.B);

            //D = Druznosti
            var D_A = CrossroadsT(DEF1, DEF1, INV1, 68.2, 86.0, 143); Wr.Connect(D_A.L, DO_K.B); Wr.Connect(D_A.R, UDZ_B.B);
            var D_B = CrossroadsT(INV1, DEF1, DEF1, 75.2, 77.2, 53); Wr.Connect(D_B.L, D_A.B);
            var D_C = CrossroadsT(INV1, DEF1, DEF1, 82.3, 68.0, 53); Wr.Connect(D_C.L, D_B.R); Wr.Connect(D_C.R, DO_H.B);

            //Z = Zdaru
            var Z_A = CrossroadsT(DEF1, INV1, INV1, 91.6, 89.5, 233); Wr.Connect(Z_A.B, D_B.B); Wr.Connect(Z_A.R, DO_J.B);
            var Z_B = CrossroadsT(DEF1, INV1, INV1, 98.1, 80.2, 233); Wr.Connect(Z_B.L, DO_I.B); Wr.Connect(Z_B.B, D_C.B); Wr.Connect(Z_B.R, Z_A.L);
        }
    }

}
