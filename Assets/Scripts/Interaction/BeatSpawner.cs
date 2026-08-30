using System;
using System.Collections.Generic;
using UnityEngine;

namespace RHCommunityHack.Interaction
{
    // Spawns beats at a fixed interval, asking a BeatPlacementSource where to put them.
    //
    // Half of the design's end state: positions can now come from a recorded performance
    // (DanceRecordingBeatSource), while the CADENCE is still this timer rather than the
    // recording's own rhythm. Beat detection from the take - which would replace the timer too -
    // is still unbuilt.
    public class BeatSpawner : MonoBehaviour
    {
        // Prefab and config are two separate assets per flavour, so pair them explicitly here
        // rather than relying on two parallel lists staying in the same order.
        [Serializable]
        public class BeatFlavour
        {
            public string label = "Flavour";
            public BeatTarget prefab;
            public BeatTargetConfig config;

            public bool IsValid => prefab != null && config != null;
        }

        [Tooltip("Where beats go. BeatSpawnArea scatters them; DanceRecordingBeatSource " +
                 "puts them where a recorded dancer's hands were.")]
        [SerializeField] BeatPlacementSource placementSource;

        // Raised for every beat as it is created, so anything that scores or reacts to judgments
        // can subscribe to that beat's OnResolved without the spawner knowing who is listening.
        public event Action<BeatTarget> OnBeatSpawned;
        [SerializeField] List<BeatFlavour> flavours = new List<BeatFlavour>();

        [Header("Timing")]
        [SerializeField] bool spawnOnStart = true;
        [Tooltip("Seconds between spawns.")]
        [SerializeField] float spawnInterval = 2f;
        [Tooltip("Seconds to wait before the first spawn.")]
        [SerializeField] float startDelay = 0f;

        readonly List<BeatPlacement> placements = new List<BeatPlacement>(4);
        double nextSpawnDsp;
        bool running;
        bool warnedAboutLeadTimes;

        void Start()
        {
            if (spawnOnStart) StartSpawning();
        }

        public void StartSpawning()
        {
            // Scheduled against the same audio clock the beats are judged on, so spawn cadence
            // and hit timing can't drift apart over a long song.
            nextSpawnDsp = AudioSettings.dspTime + startDelay;
            running = true;
        }

        public void StopSpawning() => running = false;

        void Update()
        {
            if (!running) return;

            double now = AudioSettings.dspTime;
            if (now < nextSpawnDsp) return;

            SpawnTick();

            // Advance by the interval rather than from "now", so small per-frame overshoot
            // doesn't accumulate into audible drift.
            nextSpawnDsp += spawnInterval;

            // But after a real stall (editor pause, a long hitch) don't fire a burst of
            // catch-up beats at the player - just resync to the present.
            if (nextSpawnDsp < now) nextSpawnDsp = now + spawnInterval;
        }

        // One tick may produce several beats - a recording-driven source hands back one per
        // hand, so both of a dancer's hands get a target at the same moment.
        public void SpawnTick()
        {
            if (placementSource == null)
            {
                Debug.LogWarning("BeatSpawner has no placement source assigned.", this);
                return;
            }

            if (!TryGetLeadTime(out float leadTime))
            {
                Debug.LogWarning("BeatSpawner has no usable flavours (each needs both a prefab and a config).", this);
                return;
            }

            // The source is asked where the hand should be at the moment the beat is HIT, not
            // when it appears. Sampling a performance at spawn time instead would place every
            // beat a lead-time behind the dance it came from.
            double perfectTimeDsp = AudioSettings.dspTime + leadTime;

            placements.Clear();
            placementSource.GetPlacements(perfectTimeDsp, placements);

            foreach (BeatPlacement placement in placements)
            {
                BeatFlavour flavour = PickFlavourFor(placement.hand);
                if (flavour == null) continue;

                BeatTarget instance = Instantiate(flavour.prefab, placement.position, Quaternion.identity);
                instance.Initialize(flavour.config, perfectTimeDsp);
                OnBeatSpawned?.Invoke(instance);
            }
        }

        // A source is asked about a single moment, so the whole tick shares one lead time.
        // Flavours that disagree would each want a different moment - warn rather than silently
        // telegraphing one of them wrongly.
        bool TryGetLeadTime(out float leadTime)
        {
            leadTime = 0f;
            bool found = false;

            foreach (var flavour in flavours)
            {
                if (flavour == null || !flavour.IsValid) continue;

                if (!found)
                {
                    leadTime = flavour.config.ringLeadTime;
                    found = true;
                    continue;
                }

                if (!warnedAboutLeadTimes && !Mathf.Approximately(leadTime, flavour.config.ringLeadTime))
                {
                    warnedAboutLeadTimes = true;
                    Debug.LogWarning($"BeatSpawner's flavours disagree on ringLeadTime ({leadTime} vs " +
                                     $"{flavour.config.ringLeadTime}). All beats in a tick use {leadTime}, " +
                                     "so the others will be telegraphed for the wrong duration.", this);
                }
            }

            return found;
        }

        // A named hand picks the flavour that accepts it; BeatHand.Either falls back to random.
        BeatFlavour PickFlavourFor(BeatHand hand)
        {
            if (hand == BeatHand.Either || hand == BeatHand.None) return PickFlavour();

            foreach (var flavour in flavours)
            {
                if (flavour == null || !flavour.IsValid) continue;
                if ((flavour.config.allowedHands & hand) != BeatHand.None) return flavour;
            }
            return null;
        }

        BeatFlavour PickFlavour()
        {
            // Skip half-configured entries rather than spawning a null and throwing later.
            int usable = 0;
            foreach (var flavour in flavours)
            {
                if (flavour != null && flavour.IsValid) usable++;
            }
            if (usable == 0) return null;

            int pick = UnityEngine.Random.Range(0, usable);
            foreach (var flavour in flavours)
            {
                if (flavour == null || !flavour.IsValid) continue;
                if (pick == 0) return flavour;
                pick--;
            }
            return null;
        }
    }
}
