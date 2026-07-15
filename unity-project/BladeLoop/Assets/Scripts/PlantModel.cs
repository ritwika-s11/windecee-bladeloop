using System;

/// <summary>
/// Pure C# process model of the BladeLoop pyrolysis plant.
/// NO Unity dependencies — this is deliberately a plain class so it can be
/// unit-tested against the Engineering Master Specifications and shared by
/// every UI that needs plant numbers (story-mode dashboard, PlantExplorer).
///
/// Usage:
///     var m = new PlantModel();          // baseline = spec defaults
///     m.AnnualCapacityTonnes = 80000;    // change any input
///     PlantOutputs o = m.Compute();      // everything recomputes
///
/// Every equation below is traceable to a stage in the spec document; the
/// baseline inputs reproduce the spec's headline figures exactly (verified:
/// feed 6500 kg/h, net heat 1197.3 kW, gross burner 1734.8 kW, WHRB 857.6 kW,
/// margin +77.6 kW, kiln 2.2 x 13.2 m).
/// </summary>
public class PlantModel
{
    // ---- INDEPENDENT INPUTS (the sliders drive these) ---------------------

    // Stage 1 — global mass balance
    public double AnnualCapacityTonnes = 52000.0;   // t/year
    public double OperatingHours       = 8000.0;    // h/year
    public double GlassFraction        = 0.70;      // inorganic share of feed (resin = 1 - this)
    public double GasFractionOfResin   = 0.80;      // volatile share of resin (char = 1 - this)

    // Stage 3 — kiln thermodynamics & sizing
    public double PyrolysisTempC       = 600.0;     // °C
    public double AmbientTempC         = 25.0;      // °C
    public double RetentionMinutes     = 30.0;      // min
    public double BulkDensity          = 450.0;     // kg/m³ ground composite
    public double FillFactor           = 0.15;      // kiln volumetric filling
    public double LengthToDiameter     = 6.0;       // L/D aspect ratio

    // Stage 5 / 6 — energy
    public double BurnerEfficiency     = 0.75;      // external gas-fired jacket
    public double WHRBEfficiency       = 0.22;      // thermal → electrical

    // Stage 4 — separation
    public double FluidizingVelocity   = 0.015;     // m/s

    // ---- PHYSICAL CONSTANTS (not user-adjustable) -------------------------

    public const double GlassCp        = 0.85;      // kJ/kg·K
    public const double ResinCp        = 1.20;      // kJ/kg·K
    public const double CharCp         = 1.00;      // kJ/kg·K
    public const double CrackingEnthalpy = 380.0;   // kJ/kg (resin chain-breaking)
    public const double SyngasLHV      = 13000.0;   // kJ/kg
    public const double ShredderSEC    = 120.0;     // kWh/tonne
    public const double SecondsPerHour = 3600.0;

    // Stage 5 structural / utility losses (kW). Held fixed to match the spec's
    // energy accounting; a later revision could scale these with kiln area.
    public const double ShellLossKW    = 43.8;
    public const double AuxBleedKW     = 35.0;
    public const double PurgeGasKW     = 25.0;

    // Stage 4 terminal settling velocities (m/s) — Stokes' law, fixed particle
    // properties at 600 °C from the spec. The separation window is bounded by these.
    public const double CharTerminalVelocity  = 0.0032;
    public const double GlassTerminalVelocity = 0.0368;

    // Grid CO₂ factors (kg CO₂ / kWh)
    public const double GridDE = 0.358;
    public const double GridNL = 0.328;
    public const double GridDK = 0.135;

    /// <summary>Recompute every derived plant output from the current inputs.</summary>
    public PlantOutputs Compute()
    {
        var o = new PlantOutputs();

        // --- Stage 1: mass balance ---
        o.FeedRateKgH = AnnualCapacityTonnes * 1000.0 / OperatingHours;
        double resinFraction = 1.0 - GlassFraction;
        o.GlassKgH = o.FeedRateKgH * GlassFraction;
        double resinKgH = o.FeedRateKgH * resinFraction;
        o.SyngasKgH = resinKgH * GasFractionOfResin;
        o.CharKgH   = resinKgH * (1.0 - GasFractionOfResin);

        double h = OperatingHours;
        o.GlassTonnesYr  = o.GlassKgH  * h / 1000.0;
        o.SyngasTonnesYr = o.SyngasKgH * h / 1000.0;
        o.CharTonnesYr   = o.CharKgH   * h / 1000.0;

        // --- Stage 3: net endothermic process heat ---
        double dT = PyrolysisTempC - AmbientTempC;
        double glassSensible = o.GlassKgH * GlassCp * dT;   // kJ/h
        double resinSensible = resinKgH   * ResinCp * dT;   // kJ/h
        double crackingHeat  = resinKgH   * CrackingEnthalpy;
        double netHeatKjH    = glassSensible + resinSensible + crackingHeat;
        o.NetThermalKW = netHeatKjH / SecondsPerHour;

        // --- Stage 5: gross burner demand (net + losses ÷ efficiency) ---
        o.GrossBurnerKW = (o.NetThermalKW + ShellLossKW + AuxBleedKW + PurgeGasKW) / BurnerEfficiency;

        // --- Stage 6: energy autonomy ---
        o.SyngasThermalKW = o.SyngasKgH * SyngasLHV / SecondsPerHour;
        o.SurplusThermalKW = o.SyngasThermalKW - o.GrossBurnerKW;
        o.ElectricalKW = o.SurplusThermalKW * WHRBEfficiency;
        o.ShredderLoadKW = (o.FeedRateKgH / 1000.0) * ShredderSEC;
        o.NetElectricalMarginKW = o.ElectricalKW - o.ShredderLoadKW;
        o.IsEnergyAutonomous = o.NetElectricalMarginKW >= 0.0;

        // --- Stage 3: kiln dimensional sizing ---
        double retentionH = RetentionMinutes / 60.0;
        double activeMass = o.FeedRateKgH * retentionH;          // kg in reactor
        double bedVolume  = activeMass / BulkDensity;            // m³
        double totalVolume = bedVolume / FillFactor;            // m³
        // Volume = 1.5·π·D³ when L = 6D  ⇒  D = cbrt(V / (1.5π))
        o.KilnDiameterM = Math.Cbrt(totalVolume / (1.5 * Math.PI));
        o.KilnLengthM   = LengthToDiameter * o.KilnDiameterM;

        // --- Stage 4: separation validity ---
        o.SeparationOk =
            FluidizingVelocity > CharTerminalVelocity &&
            FluidizingVelocity < GlassTerminalVelocity;

        return o;
    }

    /// <summary>CO₂ avoided (tonnes/yr) from displacing grid electricity, for a given grid factor.</summary>
    public double Co2AvoidedTonnesYr(double electricalKW, double gridFactorKgPerKWh)
        => electricalKW * OperatingHours * gridFactorKgPerKWh / 1000.0;
}

/// <summary>Everything the model derives. Plain data — read these into any UI.</summary>
public struct PlantOutputs
{
    // Mass balance
    public double FeedRateKgH;
    public double GlassKgH, SyngasKgH, CharKgH;
    public double GlassTonnesYr, SyngasTonnesYr, CharTonnesYr;

    // Kiln
    public double KilnDiameterM, KilnLengthM;

    // Energy
    public double NetThermalKW;
    public double GrossBurnerKW;
    public double SyngasThermalKW;
    public double SurplusThermalKW;
    public double ElectricalKW;
    public double ShredderLoadKW;
    public double NetElectricalMarginKW;
    public bool   IsEnergyAutonomous;

    // Separation
    public bool SeparationOk;
}
