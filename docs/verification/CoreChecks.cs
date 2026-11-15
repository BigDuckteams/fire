using System;
using System.Collections.Generic;
using PokaGoryu;

public static class CoreChecks
{
    static int checks;
    static void Assert(bool ok, string label)
    {
        checks++;
        if (!ok) throw new Exception("FAIL " + label);
    }
    static Pt Center(CellPos p) { return CityMap.CenterOf(p.X, p.Z); }
    static void Landmarks(List<CellPos> items, string name, Pt start)
    {
        foreach (CellPos p in items)
        {
            Assert(CityMap.IsRoadCell(p.X, p.Z), name + " on a road");
            Assert(CityMap.FindPath(start, Center(p), true, 0f) != null, name + " reachable after burning walls");
        }
    }
    public static int Main()
    {
        try
        {
            GameConfig.ResetDefaults();
            CityMap.Build();
            Pt start = CityMap.CenterOf(CityMap.StartX, CityMap.StartZ);
            Pt bus = CityMap.CenterOf(CityMap.BusX, CityMap.BusZ);
            Assert(CityMap.Survivors.Count == 10 && CityMap.Pizzas.Count == 8 && CityMap.Hearths.Count == 4, "landmark counts");
            Assert(CityMap.FindPath(start, bus, true, 0f) != null, "route to bus exists");
            Assert(CityMap.FindPath(start, bus, false, 0f) == null, "W1 gates route to bus");
            Landmarks(CityMap.Survivors, "survivor", start);
            Landmarks(CityMap.Pizzas, "pizza", start);
            Landmarks(CityMap.Hearths, "hearth", start);
            Landmarks(CityMap.Generators, "generator", start);
            int roads = 0;
            for (int x = 0; x < CityMap.W; x++)
                for (int z = 0; z < CityMap.H; z++)
                    if (CityMap.IsRoadCell(x, z))
                    {
                        roads++;
                        Assert(CityMap.DistBus[x, z] < 1e8f, "finite bus distance at " + x + "," + z);
                    }
            CellRect w1 = CityMap.Walls[0], w2 = CityMap.Walls[1];
            Assert(CityMap.FindPath(CityMap.CenterOf(w1.X0 - 1, w1.Z0), CityMap.CenterOf(w1.X1 + 1, w1.Z0), false, 0f) == null,
                "W1 has no bypass");
            Assert(CityMap.FindPath(CityMap.CenterOf(w2.X0 - 1, w2.Z0), CityMap.CenterOf(w2.X1 + 1, w2.Z0), false, 0f) != null,
                "W2 has a bypass");
            CityMap.Build();
            Assert(CityMap.Survivors.Count == 10 && CityMap.Pizzas.Count == 8, "rebuild does not duplicate landmarks");

            LightLedger ledger = new LightLedger();
            ledger.Reset(75);
            ledger.Record(LightReason.Lamp, 10, 0);
            ledger.Record(LightReason.Shot, -5, 30);
            ledger.Record(LightReason.Pizza, 2, 60);
            Assert(ledger.ExpectedLight == 82 && ledger.TotalIncome == 12 && ledger.TotalExpense == 5, "conservation of light");
            Assert(ledger.Amount(LightReason.Lamp, true, true) == 0 && ledger.Amount(LightReason.Pizza, true, true) == 2,
                "rolling minute expires old income");
            Assert(ledger.Amount(LightReason.Shot, false, true) == 5, "rolling minute retains recent expense");
            ledger.Tick(100);
            Assert(ledger.Amount(LightReason.Shot, false, true) == 0 && ledger.TotalExpense == 5, "expiry preserves run totals");
            ledger.Tick(10000);
            Assert(ledger.Amount(LightReason.Pizza, true, true) == 0, "long jumps expire the entire minute");
            ledger.Reset(55);
            Assert(ledger.ExpectedLight == 55 && ledger.TotalIncome == 0 && ledger.TotalExpense == 0, "new run clears ledger");
            bool rejected = false;
            try { ledger.Record(LightReason.Other, double.NaN, 0); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Assert(rejected, "non-finite telemetry rejected");

            GameDifficulty.Mode = Challenge.Standard;
            Assert(GameDifficulty.DrainScale == 1 && GameDifficulty.OrbScale == 1 && GameDifficulty.PressureScale == 1,
                "standard preserves all difficulty multipliers");
            Assert(Math.Abs(GameConfig.SmolderPerSec + GameConfig.AuraCostK * 25 - 1.24) < 0.0001, "golden passive drain 1.24/s");
            GameDifficulty.Mode = Challenge.Gentle;
            Assert(GameDifficulty.DrainScale < 1 && GameDifficulty.OrbScale > 1 && GameDifficulty.PressureScale < 1,
                "gentle eases light economy and spawn pacing");
            GameDifficulty.Mode = Challenge.Harsh;
            Assert(GameDifficulty.DrainScale > 1 && GameDifficulty.OrbScale < 1 && GameDifficulty.PressureScale > 1,
                "harsh increases light pressure and spawn pacing");
            GameConfig.SmolderPerSec = 9;
            GameConfig.ResetDefaults();
            Assert(GameConfig.SmolderPerSec == 0.24f && GameConfig.BusNeed == 60 && GameConfig.PizzaLight == 24,
                "live balance reset restores baseline");
            GameDifficulty.Mode = Challenge.Standard;
            Console.WriteLine("CORE CHECKS: OK; " + checks + " assertions, " + roads + " road cells");
            return 0;
        }
        catch (Exception e) { Console.WriteLine(e); return 1; }
    }
}
