
using System;


namespace DrivingSimulation
{
    class DebugCrossroadsRoadGraph : BuildableRoadGraph
    {
        public DebugCrossroadsRoadGraph(RoadWorld world) : base(world, new Vector2(16, 16))
        {
            var C = CrossroadsX(new ScaleMove(new Vector2(8, 8), 3));
            GarageRoad(C.T, new PeriodicGarage(8, new ConstantColorPicker(Color.White)));
            GarageRoad(C.B, new EmptyGarage(), false);
            GarageRoad(C.L, new PeriodicBurstGarage(.75f, 6, 4, new LoopColorPicker()), false);
            GarageRoad(C.R, new EmptyGarage());
        }
    }


    class CrossroadsXRoadGraph : BuildableRoadGraph
    {
        public CrossroadsXRoadGraph(RoadWorld world) : base(world, new Vector2(16, 16))
        {
            var C = CrossroadsX(new ScaleMove(new Vector2(8, 8), 3));

            GarageRoad(C.T, new PeriodicBurstGarage(.5f, 2, 8, new LoopColorPicker()), 2);
            GarageRoad(C.B, new PeriodicBurstGarage(.5f, 3, 10, new LoopColorPicker()));
            GarageRoad(C.L, new PeriodicGarage(8, new ConstantColorPicker(Color.White)));
            GarageRoad(C.R, new SpontaneusGarage(5f, 0.03f, new ConstantColorPicker(Color.DarkCyan)));
        }
    }

    class CrossroadsTRoadGraph : BuildableRoadGraph
    {
        public CrossroadsTRoadGraph(RoadWorld world) : base(world, new Vector2(16, 10))
        {
            var C = CrossroadsT(DefaultLeft1Side(true), DefaultBottom2Side(), DefaultRight1Side(), new ScaleMove(new Vector2(8, 2), 3));
            SpontaneusGarage g = new(1, 0.03f, new LoopColorPicker());
            GarageRoad(C.L, g); GarageRoad(C.B, g); GarageRoad(C.R, g);
        }
    }




    class QuadCrossroadsRoadGraph : BuildableRoadGraph
    {
        public QuadCrossroadsRoadGraph(RoadWorld world) : base(world, new Vector2(26, 26))
        {
            Garage g = new SpontaneusGarage(5, 0.01f, new LoopColorPicker());
            var A = CrossroadsXMainLR(new ScaleMove(new Vector2(8, 8), 3));
            var B = CrossroadsXMainLR(new ScaleMove(new Vector2(18, 8), 3));
            var C = CrossroadsXMainLR(new ScaleMove(new Vector2(8, 18), 3));
            var D = CrossroadsXMainLR(new ScaleMove(new Vector2(18, 18), 3));

            foreach (RoadPlug p in new RoadPlug[] { A.T, B.T, A.L, C.L, C.B, D.B, B.R, D.R })
            {
                GarageRoad(p, g.Copy());
            }
            Connect(A.B, C.T); Connect(B.B, D.T); Connect(A.R, B.L); Connect(C.R, D.L);
        }
    }



    class MainRoadWorldGraph : BuildableRoadGraph
    {
        public MainRoadWorldGraph(RoadWorld world) : base(world, new Vector2(40, 30))
        {
            var T1 = CrossroadsT(new ScaleMove(new Vector2(10, 2), 2));
            var T2 = CrossroadsT(new ScaleMove(new Vector2(30, 2), 2));
            SpontaneusGarage top_spawn = new(5, 0.02f, new ConstantColorPicker(Color.Black));
            GarageRoad(T1.L, top_spawn);
            Connect(T1.R, T2.L);
            GarageRoad(T2.R, top_spawn);

            var M1 = CrossroadsXMainLR(new ScaleMove(new Vector2(10, 15), 5));
            var M2 = CrossroadsXMainLR(new ScaleMove(new Vector2(30, 15), 5));
            M1.SetMaxSpeed(3);
            M2.SetMaxSpeed(3);

            var TW1 = Road(M1.T, 3, 1); Connect(T1.B, TW1);
            var TW2 = Road(M2.T, 3, 1); Connect(T2.B, TW2);

            GarageRoad(M1.L, new PeriodicBurstGarage(.5f, 6, 3, new LoopColorPicker()), 4).SetMaxSpeed(3);
            Connect(M1.R, M2.L).SetMaxSpeed(3);
            GarageRoad(M2.R, new PeriodicBurstGarage(.5f, 6, 3, new LoopColorPicker()), 4).SetMaxSpeed(3);

            var B = CrossroadsT(new ScaleRotateMove(new Vector2(10, 28), 180, 2));

            var BW1 = Road(M1.B, 3, 1); Connect(B.B, BW1);
            var BW2 = Road(M2.B, 3, 1);

            var BRT = Road(BW2, 3);
            var BRL = Road(B.L, 16);
            Connect(BRT, BRL);

            SpontaneusGarage bot_spawn = new(5, 0.02f, new ConstantColorPicker(Color.DarkGray));
            GarageRoad(B.R, bot_spawn);
        }
    }



    class PankracWorldGraph : BuildableRoadGraph
    {
        public override float CameraZFrom => 5;
        public override float CameraZTo => 80;
        public override float CameraZoomSpeed => 1;
        public override float PathRandomizationFrom => 0.5f;
        public override float PathRandomizationTo => 3;

        public override int RecommendedVehicleCount => 600;

        const int DEF1 = 0, INV1 = 1, DEF2 = 2;



        delegate RoadPlug GetPlug1Side(BuildableRoadGraph g, bool inv);
        delegate RoadPlug GetPlug2Side(BuildableRoadGraph g);
        RoadPlug Get(int i, GetPlug1Side side1, GetPlug2Side side2)
        {
            if (i == DEF1) return side1(this, false);
            if (i == INV1) return side1(this, true);
            if (i == DEF2) return side2(this);
            throw new ArgumentException($"Wrong i for get: {i}");
        }


        readonly GetPlug1Side l1 = (a, inv) => a.DefaultLeft1Side(inv);
        readonly GetPlug2Side l2 = (a) => a.DefaultLeft2Side();
        readonly GetPlug1Side b1 = (a, inv) => a.DefaultBottom1Side(inv);
        readonly GetPlug2Side b2 = (a) => a.DefaultBottom2Side();
        readonly GetPlug1Side r1 = (a, inv) => a.DefaultRight1Side(inv);
        readonly GetPlug2Side r2 = (a) => a.DefaultRight2Side();
        readonly GetPlug1Side t1 = (a, inv) => a.DefaultTop1Side(inv);
        readonly GetPlug2Side t2 = (a) => a.DefaultTop2Side();
        CrossroadsTPart CrossroadsT(int left, int bot, int right, double x, double y, float rotation, float scale = 3)
        {
            return CrossroadsT(left, bot, right, x, y, rotation, new Vector2(scale));
        }
        CrossroadsTPart CrossroadsT(int left, int bot, int right, double x, double y, float rotation, Vector2 scale)
        {
            return CrossroadsT(Get(left, l1, l2), Get(bot, b1, b2), Get(right, r1, r2), new ScaleRotateMove(new Vector2(x, y), -rotation, scale));
        }
        CrossroadsXPart CrossroadsX(int top, int left, int bot, int right,  double x, double y, float rotation, float scale = 3)
        {
            return CrossroadsX(Get(top, t1, t2), Get(left, l1, l2), Get(bot, b1, b2), Get(right, r1, r2), new ScaleRotateMove(new Vector2(x, y), -rotation, scale));
        }

        public PankracWorldGraph(RoadWorld world) : base(world, new Vector2(120, 120))
        {
            //DO = Druzstevni ochoz
            var DO_A = Base2Side(new Vector2(32.4, 36.3), -45).SetRoadWidth(1.5f);
            var DO_B = CrossroadsT(DEF2, DEF1, DEF2, 54.7, 22.8, 25);   Connect(DO_A  , DO_B.L);
            var DO_C = CrossroadsT(DEF2, DEF2, DEF2, 70.8, 15.6, 25);   Connect(DO_B.R, DO_C.L);
            var DO_D = CrossroadsT(DEF2, DEF2, DEF2, 78.9, 11.8, 25);   Connect(DO_C.R, DO_D.L);
            var DO_DE = CrossroadsT(DEF2, DEF2, DEF2, 95.8, 9.2, 150); Connect(DO_D.R, DO_DE.R); GarageRoad(DO_DE.B, new PeriodicBurstGarage(.75f, 6, 4, new LoopColorPicker()));
            var DO_E = CrossroadsT(DEF2, DEF2, DEF2, 97.4, 26.0, -100); Connect(DO_DE.L, DO_E.L); Connect(DO_D.B, DO_E.B);
            var DO_F = CrossroadsT(DEF2, DEF1, DEF2, 95.8, 34.3, -100); Connect(DO_E.R, DO_F.L);
            var DO_G = CrossroadsT(DEF2, INV1, DEF2, 92.5, 52.5, -90, new Vector2(1.5, 3)); Connect(DO_F.R, DO_G.L);
            var DO_H = CrossroadsT(DEF2, INV1, DEF2, 93.0, 56.8, -80,  new Vector2(1.5, 3));  Connect(DO_G.R, DO_H.L);
            var DO_I = CrossroadsT(DEF2, INV1, DEF2, 102.1, 76.2, -75, new Vector2(3, 1.25)); Connect(DO_H.R, DO_I.L);
            var DO_IJ = CrossroadsT(DEF2, DEF2, DEF2, 109.1, 92.3, 50); Connect(DO_I.R, DO_IJ.R); GarageRoad(DO_IJ.B, new PeriodicBurstGarage(.75f, 6, 4, new LoopColorPicker()));
            var DO_J = CrossroadsT(DEF2, DEF1, DEF2, 87.2, 98.0, 190, new Vector2(2, 3)); Connect(DO_IJ.L, DO_J.L);
            var DO_K = CrossroadsT(DEF2, INV1, DEF2, 81.5, 98.5, 180, new Vector2(2, 3)); Connect(DO_J.R, DO_K.L);
            var DO_L = CrossroadsT(DEF2, DEF1, DEF2, 47.3, 96.2, -190); Connect(DO_K.R, DO_L.L);
            var DO_M = CrossroadsX(DEF2, DEF2, INV1, DEF1, 21.0, 81.9, 135);  Connect(DO_L.R, DO_M.L); GarageRoad(DO_M.T, new PeriodicBurstGarage(.75f, 6, 10, new LoopColorPicker()), 0.5f);

            var DO_MN = Base1Side(new Vector2(14.0, 59.4), -55, 5).Invert(); Connect(DO_MN, DO_M.R);
            var DO_N = Base1Side(new Vector2(19.5, 50.2), -45, 2).Invert(); Connect(DO_MN.Invert(), DO_N);

            //R = Rovnobezna
            var R_A = CrossroadsT(DEF1, DEF1, INV1, 28.7, 54.8, 143); Connect(DO_N.Invert(), R_A.R);
            var R_B = CrossroadsT(DEF2, INV1, DEF1, 37.8, 44.2, 323); Connect(DO_A.Invert(), R_B.L); Connect(R_A.B, R_B.B);


            //4 rotations = 53, 143, 233, 323

            //S = Sdruzeni
            var S_A = CrossroadsT(DEF1, INV1, INV1, 29.0, 71.4, 53); Connect(S_A.L, DO_M.B);
            var S_B = CrossroadsT(INV1, INV1, DEF1, 36.4, 61.4, 233); Connect(S_A.R, S_B.R); Connect(S_B.B, R_A.L);
            var S_C = CrossroadsT(INV1, INV1, DEF1, 45.8, 49.3, 233); Connect(S_B.L, S_C.R); Connect(S_C.B, R_B.R);
            var S_D = CrossroadsT(DEF1, INV1, INV1, 54.3, 39.4, 53); Connect(S_C.L, S_D.L);
            var S_E = CrossroadsT(INV1, INV1, DEF1, 59.2, 32.6, 233); Connect(S_D.R, S_E.R); Connect(DO_B.B, S_E.B);
            var S_F = CrossroadsT(DEF2, DEF1, INV1, 75.3, 24.8, -60, new Vector2(2.5, 3)); Connect(S_E.L, S_F.B); Connect(DO_C.B, S_F.L);

            //UDI - U druzstva ideal
            var UDI_A = CrossroadsT(DEF2, INV1, DEF1, 38.3, 79.0, 143, new Vector2(1.5, 3)); Connect(UDI_A.R, S_A.B);
            var UDI_B = CrossroadsT(INV1, DEF1, DEF2, 41.5, 81.5, 143, new Vector2(1.5, 3)); Connect(UDI_A.L, UDI_B.R);
            var UDI_AC =CrossroadsT(INV1, DEF2, DEF1, 44.6, 70.6, 233); Connect(UDI_A.B, UDI_AC.R); GarageRoad(UDI_AC.B, new PeriodicBurstGarage(.75f, 6, 4, new LoopColorPicker()));
            var UDI_C = CrossroadsT(DEF1, DEF1, INV1, 63.7, 46.7, 323, new Vector2(.9, 3)); Connect(UDI_C.L, S_D.B); Connect(UDI_C.B, UDI_AC.L);
            var UDI_D = CrossroadsT(DEF1, INV1, DEF1, 67.0, 49.1, 323, new Vector2(.9, 3)); Connect(UDI_D.B, UDI_B.B);
            var UDI_E = CrossroadsT(INV1, DEF1, DEF1, 65.4, 47.9, 143, new Vector2(.9, 3)); Connect(UDI_E.R, UDI_C.R); Connect(UDI_E.L, UDI_D.L);
            var UDI_F = CrossroadsT(DEF1, INV1, INV1, 79.7, 29.9, 323, new Vector2(2.5, 3)); Connect(UDI_F.L, S_F.R); Connect(UDI_F.B, UDI_E.B);

            //UDZ - U druzstva zivot
            var UDZ_A = CrossroadsT(DEF1, DEF1, INV1, 50.9, 88.1, 233); Connect(DO_L.B, UDZ_A.R); Connect(UDI_B.L, UDZ_A.B);
            var UDZ_B = CrossroadsX(DEF2, INV1, DEF1, DEF1, 59.4, 79.2, 53); Connect(UDZ_B.L, UDZ_A.L); GarageRoad(UDZ_B.T, new PeriodicBurstGarage(.75f, 6, 10, new LoopColorPicker()), 0.5f);
            var UDZ_C = CrossroadsT(DEF1, INV1, INV1, 76.7, 56.0, 233); Connect(UDZ_C.B, UDI_D.R); Connect(UDZ_C.R, UDZ_B.R);
            var UDZ_D = CrossroadsT(INV1, DEF1, DEF1, 82.1, 49.7, 53); Connect(UDZ_D.L, UDZ_C.L); Connect(UDZ_D.B, DO_G.B);
            var UDZ_E = CrossroadsT(DEF1, INV1, INV1, 85.5, 33.0, -20, new Vector2(2.5, 3)); Connect(UDZ_E.L, UDI_F.R); Connect(UDZ_E.B, UDZ_D.R); Connect(UDZ_E.R, DO_F.B);

            //D = Druznosti
            var D_A = CrossroadsT(DEF1, DEF1, INV1, 68.2, 86.0, 143); Connect(D_A.L, DO_K.B); Connect(D_A.R, UDZ_B.B);
            var D_B = CrossroadsT(INV1, DEF1, DEF1, 75.2, 77.2, 53); Connect(D_B.L, D_A.B);
            var D_C = CrossroadsT(INV1, DEF1, DEF1, 82.3, 68.0, 53); Connect(D_C.L, D_B.R); Connect(D_C.R, DO_H.B);

            //Z = Zdaru
            var Z_A = CrossroadsT(DEF1, INV1, INV1, 91.6, 89.5, 233); Connect(Z_A.B, D_B.B); Connect(Z_A.R, DO_J.B);
            var Z_B = CrossroadsT(DEF1, INV1, INV1, 98.1, 80.2, 233); Connect(Z_B.L, DO_I.B); Connect(Z_B.B, D_C.B); Connect(Z_B.R, Z_A.L);
        }
    }

}
