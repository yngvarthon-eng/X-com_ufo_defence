using System;

namespace XCon.Smuggling
{
    /// <summary>What a Mule (low-tech, cargo-only smuggler craft) is carrying.</summary>
    public enum MuleCargoType
    {
        Contraband = 0,      // Ordinary smuggled goods: harmless to handle.
        HeavyMetals = 1,     // Toxic if the hold breaks open.
        DrugPrecursors = 2,  // Chemicals such as pseudoephedrine: hazardous spill, feeds the drug trade.
        NuclearWarhead = 3,  // Rare. Radioactive if breached. Major story event.
    }

    /// <summary>How a Mule contact ended, as seen by the smuggling network.</summary>
    public enum MuleOutcome
    {
        Scanned = 0,      // Track
        Recovered = 1,    // Dispatch
        DownedIntact = 2, // Intercept, hold held together
        Spilled = 3,      // Intercept, hold broke open
        Delivered = 4,    // Ignore or expired
    }

    public enum CargoHazard
    {
        None = 0,
        Chemical = 1,
        Toxic = 2,
        Radioactive = 3,
    }

    [Serializable]
    public struct MuleCargo
    {
        public MuleCargoType Type;
        public bool Revealed;

        public CargoHazard Hazard => MuleCargoRules.HazardOf(Type);
    }

    /// <summary>Pure rules for Mule cargo. No Unity dependencies, so it is easy to test.</summary>
    public static class MuleCargoRules
    {
        /// <summary>Pick a cargo type from weights using a roll in [0, 1).</summary>
        public static MuleCargoType Roll(float roll01, float contrabandWeight, float heavyMetalsWeight, float precursorsWeight, float warheadWeight)
        {
            var w0 = Math.Max(0f, contrabandWeight);
            var w1 = Math.Max(0f, heavyMetalsWeight);
            var w2 = Math.Max(0f, precursorsWeight);
            var w3 = Math.Max(0f, warheadWeight);
            var total = w0 + w1 + w2 + w3;
            if (total <= 0f)
            {
                return MuleCargoType.Contraband;
            }

            var r = Math.Clamp(roll01, 0f, 0.999999f) * total;
            if (r < w0) return MuleCargoType.Contraband;
            r -= w0;
            if (r < w1) return MuleCargoType.HeavyMetals;
            r -= w1;
            if (r < w2) return MuleCargoType.DrugPrecursors;
            return MuleCargoType.NuclearWarhead;
        }

        public static CargoHazard HazardOf(MuleCargoType type) => type switch
        {
            MuleCargoType.HeavyMetals => CargoHazard.Toxic,
            MuleCargoType.DrugPrecursors => CargoHazard.Chemical,
            MuleCargoType.NuclearWarhead => CargoHazard.Radioactive,
            _ => CargoHazard.None,
        };

        /// <summary>Chance that shooting the Mule down breaks the hold open and spills the cargo.</summary>
        public static float SpillChanceOnIntercept(MuleCargoType type) => HazardOf(type) switch
        {
            CargoHazard.Chemical => 0.5f,
            CargoHazard.Toxic => 0.7f,
            CargoHazard.Radioactive => 0.35f, // Warhead casings are tough, but a breach is catastrophic.
            _ => 0f,
        };

        public static string DisplayName(MuleCargoType type) => type switch
        {
            MuleCargoType.HeavyMetals => "Heavy metals",
            MuleCargoType.DrugPrecursors => "Drug precursors (pseudoephedrine)",
            MuleCargoType.NuclearWarhead => "Nuclear warhead",
            _ => "Contraband goods",
        };

        public static string HazardLabel(CargoHazard hazard) => hazard switch
        {
            CargoHazard.Chemical => "Chemical",
            CargoHazard.Toxic => "Toxic",
            CargoHazard.Radioactive => "RADIOACTIVE",
            _ => "None",
        };
    }
}
