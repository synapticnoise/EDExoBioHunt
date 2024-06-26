﻿using Octurnion.EliteDangerousUtils;
using Octurnion.EliteDangerousUtils.EDSM;

namespace EDExoBioHunt;

public class ScanSystem : ICoordinates
{
    public ScanSystem(EdsmSystem system, BioEntityScan[] scans) 
    {
        System = system;
        Scans = scans;
    }

    public readonly EdsmSystem System;
    public readonly BioEntityScan[] Scans;

    public double X => System.Coordinates?.X ?? throw new InvalidOperationException($"System {System.Name!} is missing coordinates.");
    public double Y => System.Coordinates?.Y ?? throw new InvalidOperationException($"System {System.Name!} is missing coordinates.");
    public double Z => System.Coordinates?.Z ?? throw new InvalidOperationException($"System {System.Name!} is missing coordinates.");
}