using System;
using System.Collections.Generic;
using UnityEngine;

namespace RHCommunityHack.Interaction
{
    // Spawns a beat at a fixed interval, picking a flavour at random.
    //
    // This is scaffolding, not the real level system: the design calls for beats to come from
    // a recorded performance (see Docs/), where both the timing and the position come from the
    // recording rather than from a timer and a random point. Keep that replacement in mind
    // before building anything substantial on top of this.
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

        [SerializeField] BeatSpawnArea spawnArea;
        [SerializeField] List<BeatFlavour> flavours = new List<BeatFlavour>();

        [Header("Timing")]
        [SerializeField] bool spawnOnStart = true;
        [Tooltip("Seconds between spawns.")]
        [SerializeField] float spawnInterval = 2f;
        [Tooltip("Seconds to wait before the first spawn.")]
        [SerializeField] float startDelay = 0f;

        double nextSpawnDsp;
        bool running;

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

            SpawnRandomBeat();

            // Advance by the interval rather than from "now", so small per-frame overshoot
            // doesn't accumulate into audible drift.
            nextSpawnDsp += spawnInterval;

            // But after a real stall (editor pause, a long hitch) don't fire a burst of
            // catch-up beats at the player - just resync to the present.
            if (nextSpawnDsp < now) nextSpawnDsp = now + spawnInterval;
        }

        public BeatTarget SpawnRandomBeat()
        {
            if (spawnArea == null)
            {
                Debug.LogWarning("BeatSpawner has no spawn area assigned.", this);
                return null;
            }

            BeatFlavour flavour = PickFlavour();
            if (flavour == null)
            {
                Debug.LogWarning("BeatSpawner has no usable flavours (each needs both a prefab and a config).", this);
                return null;
            }

            BeatTarget instance = Instantiate(flavour.prefab, spawnArea.GetRandomPoint(), Quaternion.identity);
            instance.Initialize(flavour.config, AudioSettings.dspTime + flavour.config.ringLeadTime);
            return instance;
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
