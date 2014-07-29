using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CactEye
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    class AsteroidSpawnTweak : MonoBehaviour
    {
        public void Start()
        {
            if (HighLogic.LoadedScene == GameScenes.FLIGHT || HighLogic.LoadedScene == GameScenes.TRACKSTATION || HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                StartCoroutine(DelayedStart());
            }
        }

        public System.Collections.IEnumerator DelayedStart()
        {
            while (HighLogic.CurrentGame.scenarios.Find(scenario => scenario.moduleRef is ScenarioDiscoverableObjects) == null)
                yield return 0;

            print("Checking number of asteroid-seeking vessels...");
            float discoveryRate = 0f;
            List<Vector3> vPos = new List<Vector3>();
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                print("Checking vessel " + v.vesselName);

                float maxDiscoveryRate = 0f;

                List<ProtoPartModuleSnapshot> processors = new List<ProtoPartModuleSnapshot>();
                foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
                {
                    ProtoPartModuleSnapshot proc = p.modules.Find(n => n.moduleName == "CactEyeProcessor");
                    if (proc != null)
                        processors.Add(proc);
                }

                print("Found " + processors.Count() + " processors");

                foreach (ProtoPartModuleSnapshot proc in processors)
                {
                    print("Disc rate: " + proc.moduleValues.GetValue("discoveryRate"));
                    float thisDiscRate = float.Parse(proc.moduleValues.GetValue("discoveryRate"));
                    if (thisDiscRate > maxDiscoveryRate)
                        maxDiscoveryRate = thisDiscRate;
                }

                if (maxDiscoveryRate > 0)
                {
                    foreach (Vector3 pos in vPos)
                    {
                        if (Vector3.Distance(pos, v.GetWorldPos3D()) < 100000)
                        {
                            print("Telescope found too close to other telescope; will not discover asteroids");
                            maxDiscoveryRate = 0f;
                        }
                    }
                }

                if (maxDiscoveryRate > 0)
                    vPos.Add(v.GetWorldPos3D());

                discoveryRate += maxDiscoveryRate;
            }
            print("Discovery rate is " + discoveryRate);

            ScenarioDiscoverableObjects sDO = (ScenarioDiscoverableObjects)HighLogic.CurrentGame.scenarios.Find(scenario => scenario.moduleRef is ScenarioDiscoverableObjects).moduleRef;

            if (sDO == null)
                print("Could not find sDO?");

            sDO.spawnOddsAgainst = (int)((100 / Mathf.Pow(discoveryRate + 1, 2)) + 1);
        }
    }
}
