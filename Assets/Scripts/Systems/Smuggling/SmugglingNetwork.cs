using System;
using System.Collections.Generic;
using UnityEngine;
using XCon.UI;
using XCon.UI.Boxes;

namespace XCon.Smuggling
{
    /// <summary>All tuning numbers for the smuggling network and warhead events.</summary>
    public static class NetworkRules
    {
        // Leads per Mule outcome (doubled while the informant is active).
        public static int LeadsFor(MuleOutcome outcome, bool isWarhead) => outcome switch
        {
            MuleOutcome.Scanned => 1,
            MuleOutcome.Recovered => isWarhead ? 3 : 2,
            MuleOutcome.DownedIntact => 1,
            _ => 0,
        };

        // Total leads needed for stage 1 (courier route), 2 (receiver) and 3 (broker).
        public static readonly int[] StageThresholds = { 3, 6, 10 };

        public const float BrokerWarheadWeightMultiplier = 0.5f;
        public const float BrokerEducationGain = 3f;

        // Receiver decision.
        public const int RaidHumanEscalationDelta = -2;
        public const float RaidPermitRisk = 5f;

        // Warhead custody decision.
        public const float HandOverPermitRiskDelta = -10f;
        public const float HandOverEducationGain = 2f;
        public const float StudyPermitRisk = 8f;
        public const int BaitLeads = 4;

        // Loose warhead crisis.
        public const float CrisisSeconds = 6f * 60f;
        public const int CrisisLeadsNeeded = 3;
        public const float CrisisExtraSecondsPerWarhead = 2f * 60f;
        public const int CrisisExtraLeadsPerWarhead = 2;
        public const float RecoveredEducationGain = 2f;
        public const float BlackmailPermitRisk = 25f;

        // Score.
        public const int RaidScore = 600;
        public const int BrokerScore = 800;
        public const int StudyScore = 400;
        public const int RecoveredScore = 500;
        public const int BlackmailPenalty = 800;
    }

    /// <summary>Everything about the network that is kept in the save slot.</summary>
    [Serializable]
    public sealed class SmugglingNetworkState
    {
        public int leads;
        public int stage;
        public bool informant;
        public bool raided;
        public bool custodyDecided;
        public int warheadsHandedOver;
        public int warheadsStudied;
        public int warheadsBaited;
        public int warheadsRecovered;
        public int blackmailEvents;

        // Open story beats, so a save made mid-event resumes it.
        public bool custodyPending;
        public bool crisisActive;
        public float crisisRemainingSeconds;
        public int crisisLeadsGathered;
        public int crisisLeadsNeeded;
        public int crisisWarheads;
    }

    /// <summary>
    /// NIA intelligence on the criminal network behind the Mules (GDD 16, 17, 21), plus the
    /// warhead story events: the custody decision on a seized warhead and the loose warhead crisis.
    /// </summary>
    public sealed class SmugglingNetwork : MonoBehaviour
    {
        public static SmugglingNetwork Instance { get; private set; }

        private SmugglingNetworkState state = new();
        private UFOManager ufo;

        private bool crisisActive;
        private float crisisRemainingSeconds;
        private int crisisLeadsGathered;
        private int crisisLeadsNeeded;
        private int crisisWarheads;

        public int Leads => state.leads;
        public int Stage => state.stage;
        public bool CourierRouteKnown => state.stage >= 1;
        public bool InformantActive => state.informant;
        public float WarheadWeightMultiplier => state.stage >= 3 ? NetworkRules.BrokerWarheadWeightMultiplier : 1f;
        public int WarheadsRecovered => state.warheadsRecovered;
        public int BlackmailEvents => state.blackmailEvents;

        private void Awake()
        {
            Instance = this;
            ufo = GetComponent<UFOManager>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private bool resumeOpenEventsPending;

        public string ExportStateJson()
        {
            state.crisisActive = crisisActive;
            state.crisisRemainingSeconds = crisisRemainingSeconds;
            state.crisisLeadsGathered = crisisLeadsGathered;
            state.crisisLeadsNeeded = crisisLeadsNeeded;
            state.crisisWarheads = crisisWarheads;
            return JsonUtility.ToJson(state);
        }

        /// <summary>Restore from a save, including a running warhead crisis and unanswered decisions.</summary>
        public void ImportStateJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var loaded = JsonUtility.FromJson<SmugglingNetworkState>(json);
            if (loaded != null)
            {
                state = loaded;
                state.stage = Mathf.Clamp(state.stage, 0, NetworkRules.StageThresholds.Length);
                crisisActive = state.crisisActive && state.crisisWarheads > 0;
                crisisRemainingSeconds = Mathf.Max(1f, state.crisisRemainingSeconds);
                crisisLeadsGathered = Mathf.Max(0, state.crisisLeadsGathered);
                crisisLeadsNeeded = Mathf.Max(1, state.crisisLeadsNeeded);
                crisisWarheads = crisisActive ? state.crisisWarheads : 0;

                // Decisions and the crisis notice are raised on the next frame, once the scene is up.
                resumeOpenEventsPending = true;
            }
        }

        // ---------------------------------------------------------------------
        // Mule outcomes
        // ---------------------------------------------------------------------

        public void OnMuleOutcome(int contactId, MuleCargo cargo, MuleOutcome outcome)
        {
            var isWarhead = cargo.Type == MuleCargoType.NuclearWarhead;
            var leads = NetworkRules.LeadsFor(outcome, isWarhead);
            if (leads > 0)
            {
                if (state.informant)
                {
                    leads *= 2;
                }

                AddLeads(leads, $"Contact #{contactId}: {DescribeOutcome(outcome)}");
            }

            if (!isWarhead)
            {
                return;
            }

            if (outcome == MuleOutcome.Recovered || outcome == MuleOutcome.DownedIntact)
            {
                OnWarheadSeized();
            }
            else if (outcome == MuleOutcome.Delivered)
            {
                OnWarheadDelivered();
            }
        }

        /// <summary>Leads from other sources (e.g. a recon squad). Doubled by the informant like Mule leads.</summary>
        public void AddBonusLeads(int amount, string reason)
        {
            AddLeads(state.informant ? amount * 2 : amount, reason);
        }

        private static string DescribeOutcome(MuleOutcome outcome) => outcome switch
        {
            MuleOutcome.Scanned => "hold scan traced to a shipping contact",
            MuleOutcome.Recovered => "manifests and markings recovered from the hold",
            MuleOutcome.DownedIntact => "wreckage gave up a flight log",
            _ => "intel gathered",
        };

        private void AddLeads(int amount, string reason)
        {
            if (amount <= 0)
            {
                return;
            }

            state.leads += amount;
            if (crisisActive)
            {
                crisisLeadsGathered += amount;
            }

            var next = state.stage < NetworkRules.StageThresholds.Length
                ? $"Next breakthrough at {NetworkRules.StageThresholds[state.stage]} leads."
                : "The network is fully mapped.";
            Publish(
                $"network/lead/{state.leads}",
                BoxSeverity.Info,
                "Network Lead",
                $"{reason}.\nLeads: {state.leads} (+{amount}). {next}" +
                (crisisActive ? $"\nWarhead trace: {Mathf.Min(crisisLeadsGathered, crisisLeadsNeeded)}/{crisisLeadsNeeded}" : string.Empty));

            CheckStages();
            CheckCrisisResolved();
        }

        private void CheckStages()
        {
            while (state.stage < NetworkRules.StageThresholds.Length && state.leads >= NetworkRules.StageThresholds[state.stage])
            {
                state.stage++;
                switch (state.stage)
                {
                    case 1:
                        Publish(
                            "network/stage/1",
                            BoxSeverity.Warn,
                            "Courier Route Mapped",
                            "NIA has mapped the courier route the Mules fly.\n" +
                            "From now on, Mule alerts include the cargo's hazard class.");
                        break;

                    case 2:
                        ShowReceiverDecision();
                        break;

                    case 3:
                        if (ufo != null)
                        {
                            ufo.AddEducationFromHazard(NetworkRules.BrokerEducationGain);
                        }

                        Publish(
                            "network/stage/3",
                            BoxSeverity.Critical,
                            "Broker Exposed",
                            "NIA has exposed the broker who finances the Mule flights.\n" +
                            "Accounts are frozen and the warhead pipeline is breaking up.\n\n" +
                            $"Warhead Mules are now {Mathf.RoundToInt((1f - NetworkRules.BrokerWarheadWeightMultiplier) * 100f)}% less common. Score +{NetworkRules.BrokerScore}.");
                        break;
                }
            }
        }

        private void ShowReceiverDecision()
        {
            StoryDecisionPanel.Show(
                "NIA",
                "Receiver Identified",
                "NIA has identified who receives the Mule shipments: a front company running a cold-storage depot on the coast.\n\n" +
                "They can be taken down now, or quietly turned into an informant. NIA's rule: never sack the spy.",
                new StoryOption(
                    "Raid the depot",
                    $"Score +{NetworkRules.RaidScore}. Smuggler escalation drops. Permit risk +{NetworkRules.RaidPermitRisk:0} for a public operation.",
                    () =>
                    {
                        state.raided = true;
                        if (ufo != null)
                        {
                            ufo.AdjustHumanEscalation(NetworkRules.RaidHumanEscalationDelta);
                            ufo.AddPermitRiskFromHazard(NetworkRules.RaidPermitRisk);
                        }

                        Publish("network/receiver/raid", BoxSeverity.Warn, "Depot Raided",
                            "The depot is shut down and its staff detained. The smugglers will need time to rebuild.");
                    }),
                new StoryOption(
                    "Turn them as an informant",
                    "No score now. Mule alerts show the exact cargo, and every lead counts double.",
                    () =>
                    {
                        state.informant = true;
                        Publish("network/receiver/informant", BoxSeverity.Info, "Informant Active",
                            "The depot manager now reports to NIA. Shipments are named before they fly.");
                    }));
        }

        // ---------------------------------------------------------------------
        // Warhead events
        // ---------------------------------------------------------------------

        private void OnWarheadSeized()
        {
            if (state.custodyDecided)
            {
                // The first custody decision sets the policy; later warheads follow it quietly.
                state.warheadsHandedOver++;
                ApplyHandOverEffects();
                Publish("network/warhead/handover", BoxSeverity.Info, "Warhead Handed Over",
                    "Following standing policy, the seized warhead was handed to the Global Defense Organization.");
                return;
            }

            state.custodyPending = true;
            StoryDecisionPanel.Show(
                "Command",
                "Warhead Custody",
                "A nuclear warhead is in your custody. Within the hour, three parties want it:\n" +
                "the Global Defense Organization, a Scientific Coalition, and NIA.\n\n" +
                "Whatever you decide, the world will hear about it.",
                new StoryOption(
                    "Hand it to the Global Defense Organization",
                    $"Permit risk {NetworkRules.HandOverPermitRiskDelta:0}, education +{NetworkRules.HandOverEducationGain:0}. The safe, public choice.",
                    () =>
                    {
                        state.custodyDecided = true;
                        state.custodyPending = false;
                        state.warheadsHandedOver++;
                        ApplyHandOverEffects();
                        Publish("network/warhead/handover", BoxSeverity.Info, "Warhead Handed Over",
                            "The warhead is in international custody. Allies welcome the decision.");
                    }),
                new StoryOption(
                    "Keep it for the Scientific Coalition",
                    $"Score +{NetworkRules.StudyScore}. Permit risk +{NetworkRules.StudyPermitRisk:0} when the press finds out.",
                    () =>
                    {
                        state.custodyDecided = true;
                        state.custodyPending = false;
                        state.warheadsStudied++;
                        if (ufo != null)
                        {
                            ufo.AddPermitRiskFromHazard(NetworkRules.StudyPermitRisk);
                        }

                        Publish("network/warhead/study", BoxSeverity.Warn, "Warhead Kept",
                            "The Scientific Coalition takes the warhead apart for study. The story leaks within days: a political scandal.");
                    }),
                new StoryOption(
                    "Let NIA use it as bait",
                    $"+{NetworkRules.BaitLeads} network leads. NIA tags the casing and lets the buyers come looking.",
                    () =>
                    {
                        state.custodyDecided = true;
                        state.custodyPending = false;
                        state.warheadsBaited++;
                        AddLeads(NetworkRules.BaitLeads, "NIA bait operation: buyers tracked to the warhead");
                    }));
        }

        private void ApplyHandOverEffects()
        {
            if (ufo == null)
            {
                return;
            }

            ufo.AddPermitRiskFromHazard(NetworkRules.HandOverPermitRiskDelta);
            ufo.AddEducationFromHazard(NetworkRules.HandOverEducationGain);
        }

        private void OnWarheadDelivered()
        {
            if (crisisActive)
            {
                crisisWarheads++;
                crisisRemainingSeconds += NetworkRules.CrisisExtraSecondsPerWarhead;
                crisisLeadsNeeded += NetworkRules.CrisisExtraLeadsPerWarhead;
            }
            else
            {
                crisisActive = true;
                crisisWarheads = 1;
                crisisRemainingSeconds = NetworkRules.CrisisSeconds;
                crisisLeadsGathered = 0;
                crisisLeadsNeeded = NetworkRules.CrisisLeadsNeeded;
            }

            Publish(
                "network/crisis/start",
                BoxSeverity.Critical,
                "Loose Warhead",
                $"{(crisisWarheads > 1 ? $"{crisisWarheads} nuclear warheads are" : "A nuclear warhead is")} in criminal hands.\n" +
                $"NIA needs {crisisLeadsNeeded} new network leads within {FormatSeconds(crisisRemainingSeconds)} to trace it.\n\n" +
                "Track, recover or down Mules intact to gather leads. If time runs out, expect nuclear blackmail.");
        }

        private void ResumeOpenEvents()
        {
            resumeOpenEventsPending = false;

            if (state.stage >= 2 && !state.raided && !state.informant)
            {
                ShowReceiverDecision();
            }

            if (state.custodyPending && !state.custodyDecided)
            {
                state.custodyPending = false;
                OnWarheadSeized();
            }

            if (crisisActive)
            {
                Publish(
                    "network/crisis/resume",
                    BoxSeverity.Critical,
                    "Loose Warhead",
                    "The warhead hunt continues from the saved mission.\n" +
                    $"Trace {Mathf.Min(crisisLeadsGathered, crisisLeadsNeeded)}/{crisisLeadsNeeded} leads, {FormatSeconds(crisisRemainingSeconds)} left.");
            }
        }

        private void Update()
        {
            if (resumeOpenEventsPending)
            {
                ResumeOpenEvents();
            }

            if (!crisisActive || GameMenuController.IsPaused)
            {
                return;
            }

            crisisRemainingSeconds -= Time.unscaledDeltaTime;
            if (crisisRemainingSeconds <= 0f)
            {
                FailCrisis();
            }
        }

        private void CheckCrisisResolved()
        {
            if (!crisisActive || crisisLeadsGathered < crisisLeadsNeeded)
            {
                return;
            }

            crisisActive = false;
            state.warheadsRecovered += crisisWarheads;
            if (ufo != null)
            {
                ufo.AddEducationFromHazard(NetworkRules.RecoveredEducationGain);
            }

            Publish(
                "network/crisis/recovered",
                BoxSeverity.Critical,
                "Warhead Recovered",
                $"NIA traced the {(crisisWarheads > 1 ? "warheads" : "warhead")} to a lock-up near the receiving depot. " +
                $"A special forces team recovered {(crisisWarheads > 1 ? "them" : "it")} without a shot fired.\n\n" +
                $"Score +{NetworkRules.RecoveredScore * crisisWarheads}.");
            crisisWarheads = 0;
        }

        private void FailCrisis()
        {
            crisisActive = false;
            state.blackmailEvents += crisisWarheads;
            if (ufo != null)
            {
                ufo.AddPermitRiskFromHazard(NetworkRules.BlackmailPermitRisk);
            }

            Publish(
                "network/crisis/blackmail",
                BoxSeverity.Critical,
                "Nuclear Blackmail",
                "The trace went cold. A video arrives at three capitals: the warhead, a date, and a price.\n" +
                "Governments are paying in secret, and public trust in the defense program is collapsing.\n\n" +
                $"Permit risk +{NetworkRules.BlackmailPermitRisk:0}. Score -{NetworkRules.BlackmailPenalty * crisisWarheads}.");
            crisisWarheads = 0;
        }

        // ---------------------------------------------------------------------
        // HUD and score
        // ---------------------------------------------------------------------

        public string BuildHudLine()
        {
            var parts = new List<string>(2);
            if (state.leads > 0 || state.stage > 0)
            {
                var stageName = state.stage switch
                {
                    0 => "unmapped",
                    1 => "route mapped",
                    2 => state.informant ? "informant active" : "receiver known",
                    _ => "broker exposed",
                };
                parts.Add($"Network: {state.leads} leads ({stageName})");
            }

            if (crisisActive)
            {
                parts.Add($"<color=#FF8A8A>LOOSE WARHEAD {FormatSeconds(crisisRemainingSeconds)}  trace {Mathf.Min(crisisLeadsGathered, crisisLeadsNeeded)}/{crisisLeadsNeeded}</color>");
            }

            return string.Join("  |  ", parts);
        }

        /// <summary>Score contributions as (label, points). Points are negative for penalties.</summary>
        public void AppendScoreLines(List<KeyValuePair<string, int>> lines)
        {
            if (state.raided)
            {
                lines.Add(new KeyValuePair<string, int>("Depot raided", NetworkRules.RaidScore));
            }

            if (state.stage >= 3)
            {
                lines.Add(new KeyValuePair<string, int>("Broker exposed", NetworkRules.BrokerScore));
            }

            if (state.warheadsStudied > 0)
            {
                lines.Add(new KeyValuePair<string, int>($"Warheads studied ({state.warheadsStudied})", state.warheadsStudied * NetworkRules.StudyScore));
            }

            if (state.warheadsRecovered > 0)
            {
                lines.Add(new KeyValuePair<string, int>($"Warheads recovered ({state.warheadsRecovered})", state.warheadsRecovered * NetworkRules.RecoveredScore));
            }

            if (state.blackmailEvents > 0)
            {
                lines.Add(new KeyValuePair<string, int>($"Nuclear blackmail ({state.blackmailEvents})", -state.blackmailEvents * NetworkRules.BlackmailPenalty));
            }
        }

        private static string FormatSeconds(float seconds)
        {
            var total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private static void Publish(string key, BoxSeverity severity, string title, string body)
        {
            var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
            if (queue == null)
            {
                return;
            }

            queue.Publish(new BoxMessage(
                triggerKey: key,
                channel: BoxChannel.Info,
                severity: severity,
                sourceTag: "NIA",
                title: title,
                body: body));
        }

        // ---------------------------------------------------------------------
        // Debug
        // ---------------------------------------------------------------------

        [ContextMenu("Debug/Network: +3 Leads")]
        private void DebugAddLeads()
        {
            if (Application.isPlaying)
            {
                AddLeads(3, "Debug intel");
            }
        }

        [ContextMenu("Debug/Warhead: Seized (Custody Decision)")]
        private void DebugWarheadSeized()
        {
            if (Application.isPlaying)
            {
                state.custodyDecided = false;
                OnWarheadSeized();
            }
        }

        [ContextMenu("Debug/Warhead: Delivered (Loose Warhead Crisis)")]
        private void DebugWarheadDelivered()
        {
            if (Application.isPlaying)
            {
                OnWarheadDelivered();
            }
        }
    }
}
