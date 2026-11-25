using System.Collections.Generic;
using UnityEngine;
using XCon.Smuggling;
using XCon.UI.Boxes;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>The kit the ground squad carries on Dispatch (key 4).</summary>
public enum SquadLoadout
{
    Standard,
    Hazmat,
    Heavy,
    Recon,
}

/// <summary>Pure numbers for squad loadouts (see Design_ExodusCraft.md, "Gen 0b: Mule").</summary>
public static class SquadLoadoutRules
{
    /// <summary>Time to switch kits. Dispatch uses the old kit until the refit is done.</summary>
    public const float RefitSeconds = 20f;

    /// <summary>
    /// Added to the education a Dispatch needs against Grey and Dark contacts.
    /// Negative is easier (Heavy), positive is harder (light kits).
    /// </summary>
    public static float HostileEducationOffset(SquadLoadout loadout) => loadout switch
    {
        SquadLoadout.Heavy => -15f,
        SquadLoadout.Hazmat => 15f,
        SquadLoadout.Recon => 15f,
        _ => 0f,
    };

    /// <summary>Light kits that fail against a Dark contact make it worse instead of doing nothing.</summary>
    public static bool IsWeakAgainstHostiles(SquadLoadout loadout) =>
        loadout == SquadLoadout.Hazmat || loadout == SquadLoadout.Recon;

    /// <summary>Chance that soldiers without hazmat suits are exposed when recovering hazardous cargo.</summary>
    public static float ExposureChance(SquadLoadout loadout, CargoHazard hazard)
    {
        if (loadout == SquadLoadout.Hazmat || loadout == SquadLoadout.Heavy)
        {
            return 0f; // Hazmat is sealed; Heavy has its own risk (rupture).
        }

        return hazard switch
        {
            CargoHazard.Chemical => 0.2f,
            CargoHazard.Toxic => 0.35f,
            CargoHazard.Radioactive => 0.5f,
            _ => 0f,
        };
    }

    /// <summary>Permit risk when the squad is exposed (before the difficulty multiplier).</summary>
    public static float ExposurePermitRisk(CargoHazard hazard) => hazard switch
    {
        CargoHazard.Chemical => 2f,
        CargoHazard.Toxic => 4f,
        CargoHazard.Radioactive => 6f,
        _ => 0f,
    };

    /// <summary>Heavy weapons can rupture a hazardous hold: this share of the Intercept spill chance.</summary>
    public const float HeavyRuptureShareOfSpillChance = 0.5f;

    /// <summary>Extra network leads when a Recon squad recovers a Mule.</summary>
    public const int ReconExtraLeads = 1;

    /// <summary>Extra education from a Recon Dispatch on Human and Green contacts.</summary>
    public const float ReconEducationGain = 0.5f;

    public static string Label(SquadLoadout loadout) => loadout switch
    {
        SquadLoadout.Hazmat => "HAZMAT",
        SquadLoadout.Heavy => "HEAVY",
        SquadLoadout.Recon => "RECON",
        _ => "STANDARD",
    };

    public static string Summary(SquadLoadout loadout) => loadout switch
    {
        SquadLoadout.Hazmat => "Sealed suits: safe on hazardous Mules and helps contain hazard sites (H). Weak against Grey and Dark.",
        SquadLoadout.Heavy => "Heavy weapons: strong against Grey and Dark. Can rupture a hazardous Mule's hold. Unsettles Greens.",
        SquadLoadout.Recon => "Light recon: extra network lead from Mules and extra education. Weak against Grey and Dark.",
        _ => "Rifles and body armour: no strengths, no weaknesses. Exposure risk on hazardous Mules.",
    };

    public static SquadLoadout Next(SquadLoadout loadout) => loadout switch
    {
        SquadLoadout.Standard => SquadLoadout.Hazmat,
        SquadLoadout.Hazmat => SquadLoadout.Heavy,
        SquadLoadout.Heavy => SquadLoadout.Recon,
        _ => SquadLoadout.Standard,
    };
}

/// <summary>
/// The ground squad sent by Dispatch. It carries one standing loadout; press L to refit to the next kit.
/// </summary>
public class SquadManager : MonoBehaviour
{
    public List<GameObject> squadMembers = new List<GameObject>();

    public static SquadManager Instance { get; private set; }

    private bool refitting;
    private SquadLoadout refitTarget;
    private float refitRemainingSeconds;
    private int messageSerial; // Unique box keys, so quick L presses are not swallowed as duplicates.

    /// <summary>The kit Dispatch uses right now.</summary>
    public SquadLoadout Loadout { get; private set; } = SquadLoadout.Standard;
    public bool IsRefitting => refitting;
    public int Exposures { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void AddMember(GameObject member)
    {
        squadMembers.Add(member);
        Debug.Log("Squad member added: " + member.name);
    }

    public void RemoveMember(GameObject member)
    {
        squadMembers.Remove(member);
        Debug.Log("Squad member removed: " + member.name);
    }

    public void DeploySquad(Vector3 position)
    {
        // Outcomes are resolved by UFOManager using the current loadout; there is no tactical layer yet.
        Debug.Log($"Squad ({SquadLoadoutRules.Label(Loadout)}) deployed to {position}");
    }

    /// <summary>Restore from a save. An unfinished refit is dropped.</summary>
    public void ApplyPersistentState(string loadout, int exposures)
    {
        if (!string.IsNullOrEmpty(loadout) && System.Enum.TryParse(loadout, out SquadLoadout parsed))
        {
            Loadout = parsed;
        }

        refitting = false;
        Exposures = Mathf.Max(0, exposures);
    }

    /// <summary>Count one exposure incident (score penalty at mission end).</summary>
    public void RegisterExposure()
    {
        Exposures++;
    }

    private void Update()
    {
        if (GameMenuController.IsPaused)
        {
            return;
        }

        if (refitting)
        {
            refitRemainingSeconds -= Time.unscaledDeltaTime;
            if (refitRemainingSeconds <= 0f)
            {
                refitting = false;
                Loadout = refitTarget;
                Publish(
                    $"squad/refit/done/{++messageSerial}",
                    BoxSeverity.Info,
                    "Squad Ready",
                    $"The squad now carries the {SquadLoadoutRules.Label(Loadout)} kit.\n{SquadLoadoutRules.Summary(Loadout)}");
            }
        }

        if (WasLoadoutKeyPressed())
        {
            CycleRefit();
        }
    }

    private void CycleRefit()
    {
        var target = SquadLoadoutRules.Next(refitting ? refitTarget : Loadout);
        if (target == Loadout)
        {
            refitting = false;
            Publish(
                $"squad/refit/cancel/{++messageSerial}",
                BoxSeverity.Info,
                "Refit Cancelled",
                $"The squad keeps the {SquadLoadoutRules.Label(Loadout)} kit.");
            return;
        }

        refitting = true;
        refitTarget = target;
        refitRemainingSeconds = SquadLoadoutRules.RefitSeconds;
        Publish(
            $"squad/refit/start/{++messageSerial}",
            BoxSeverity.Info,
            "Squad Refitting",
            $"Refitting to {SquadLoadoutRules.Label(target)} ({FormatSeconds(refitRemainingSeconds)}).\n" +
            $"{SquadLoadoutRules.Summary(target)}\n\n" +
            $"Until then, Dispatch uses the {SquadLoadoutRules.Label(Loadout)} kit. Press L again for the next kit.");
    }

    /// <summary>One HUD line.</summary>
    public string BuildHudLine()
    {
        var status = refitting
            ? $"refitting to {SquadLoadoutRules.Label(refitTarget)} {FormatSeconds(refitRemainingSeconds)}"
            : "ready";

        var sites = HazardSiteManager.Instance;
        if (sites != null && sites.SquadOnSite)
        {
            status += $"  |  on site #{sites.SquadSiteId} {FormatSeconds(sites.SquadRemainingSeconds)}";
        }

        return $"Squad: {SquadLoadoutRules.Label(Loadout)} kit  |  {status}  [L] change";
    }

    public static string FormatSeconds(float seconds)
    {
        var total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{total / 60}:{total % 60:00}";
    }

    private static void Publish(string key, BoxSeverity severity, string title, string body)
    {
        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            return;
        }

        queue.Publish(new BoxMessage(
            triggerKey: key,
            channel: BoxChannel.Info,
            severity: severity,
            sourceTag: "Squad",
            title: title,
            body: body));
    }

    private static bool WasLoadoutKeyPressed()
    {
        if (GameMenuController.IsTypingInTextField)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.lKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.L))
        {
            return true;
        }
#endif
        return false;
    }
}
