using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CactEye
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CactEyeVars : MonoBehaviour
    {
        public static Dictionary<CelestialBody, double> bodyDist = new Dictionary<CelestialBody, double>();
        public static Dictionary<CelestialBody, double> bodySize = new Dictionary<CelestialBody, double>();
        public static Dictionary<CelestialBody, Vector3d> bodyAngle = new Dictionary<CelestialBody, Vector3d>();

        public static Dictionary<CelestialBody, List<CelestialBody>> bodyChildren = new Dictionary<CelestialBody, List<CelestialBody>>();

        public static Dictionary<CelestialBody, double> occultationExpTimes = new Dictionary<CelestialBody, double>();
        public static Dictionary<CelestialBody, string> occultationExpTypes = new Dictionary<CelestialBody, string>(); //"Moon", "Planet", "PlanetMoon", "Asteroid"
        public static Dictionary<CelestialBody, CelestialBody> occultationExpBodies = new Dictionary<CelestialBody, CelestialBody>();
        public static Dictionary<CelestialBody, Vessel> occultationExpAsteroids = new Dictionary<CelestialBody, Vessel>();

        //public static List<Vessel> astList = new List<Vessel>();

        //public static ConfigNode settings;

        public static string root = "";
        public static bool KASavailable = false;

        public void Awake()
        {
            //print("ROOTPATH: |" + KSPUtil.ApplicationRootPath);

            root = KSPUtil.ApplicationRootPath.Replace("\\", "/");
            
            foreach (AssemblyLoader.LoadedAssembly assembly in AssemblyLoader.loadedAssemblies)
            {
                if (assembly.name == "KAS")
                    KASavailable = true;
            }

            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (body != FlightGlobals.Bodies[0])
                {
                    if (!bodyChildren.ContainsKey(body.referenceBody))
                        bodyChildren.Add(body.referenceBody, new List<CelestialBody>());

                    bodyChildren[body.referenceBody].Add(body);
                }
            }

            print("Searching all vessels for current occultation experiment data...");
            foreach (ProtoVessel v in HighLogic.CurrentGame.flightState.protoVessels)
            {
                print("CHECKING VESSEL " + v.vesselName);
                    
                foreach (ProtoPartSnapshot p in v.protoPartSnapshots)
                {
                    foreach (ProtoPartModuleSnapshot m in p.modules)
                    {
                        if (m.moduleName == "CactEyeProcessor")
                        {
                            CelestialBody pBody = GetPlanetBody(FlightGlobals.Bodies[v.orbitSnapShot.ReferenceBodyIndex]);
                            if (!occultationExpTypes.ContainsKey(pBody))
                            {
                                print("FOUND PROCESSOR | " + m.moduleValues.GetValue("occType") + " | " + double.Parse(m.moduleValues.GetValue("occTime")));
                                print("- does not contain key already");
                                if (m.moduleValues.GetValue("occType") != "None" && double.Parse(m.moduleValues.GetValue("occTime")) > Planetarium.GetUniversalTime())
                                {
                                    print("adding definition from " + v.vesselName + " for " + pBody.bodyName);
                                    occultationExpTypes.Add(pBody, m.moduleValues.GetValue("occType"));
                                    occultationExpTimes.Add(pBody, double.Parse(m.moduleValues.GetValue("occTime")));
                                    if (m.moduleValues.GetValue("occType") == "Asteroid")
                                        occultationExpAsteroids.Add(pBody, FlightGlobals.Vessels.Where(n => n.id == new Guid(m.moduleValues.GetValue("occAst"))).First());
                                    else
                                        occultationExpBodies.Add(pBody, FlightGlobals.Bodies.Find(n => n.bodyName == m.moduleValues.GetValue("occBody")));

                                    print("TIME: " + (occultationExpTimes[pBody]) + "||" + Time(occultationExpTimes[pBody]));
                                    print("TYPE: " + occultationExpTypes[pBody]);
                                }
                            }
                        }
                    }
                }
            }

            foreach (CelestialBody body in bodyChildren.Keys)
            {
                if (body != FlightGlobals.Bodies[0] && !occultationExpTypes.ContainsKey(body))
                    GenerateOccultationExp(body);
            }
        }

        public void Update()
        {
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (bodyDist.ContainsKey(body))
                    bodyDist[body] = body.GetAltitude(FlightGlobals.ship_position) + body.Radius;
                else
                    bodyDist.Add(body, body.GetAltitude(FlightGlobals.ship_position) + body.Radius);

                if (bodySize.ContainsKey(body))
                    bodySize[body] = Math.Acos(Math.Sqrt(Math.Pow(bodyDist[body], 2) - Math.Pow(body.Radius, 2)) / bodyDist[body]) * (180 / Math.PI);
                else
                    bodySize.Add(body, Math.Acos(Math.Sqrt(Math.Pow(bodyDist[body], 2) - Math.Pow(body.Radius, 2)) / bodyDist[body]) * (180 / Math.PI));

                if (bodyAngle.ContainsKey(body))
                    bodyAngle[body] = (body.position - FlightGlobals.ship_position).normalized;
                else
                    bodyAngle.Add(body, (body.position - FlightGlobals.ship_position).normalized);
            }

            CelestialBody[] bodyCheck = occultationExpTimes.Keys.ToArray();
            foreach (CelestialBody body in bodyCheck)
            {
                if (Planetarium.GetUniversalTime() > occultationExpTimes[body])
                {
                    print("REGENERATING OCCULTATION EXP FOR " + body.bodyName);
                    GenerateOccultationExp(body);
                }
            }
        }

        //Is this body covered by any other body?
        public static string CheckOccult(CelestialBody body)
        {
            foreach (CelestialBody bodyC in FlightGlobals.Bodies)
            {
                if (!bodyDist.ContainsKey(bodyC))
                    print("Could not find body " + bodyC.bodyName);
                if (body.name != bodyC.name && bodyDist[bodyC] < bodyDist[body] && bodySize[bodyC] > bodySize[body] && Vector3d.Angle(bodyAngle[body], bodyAngle[bodyC]) < bodySize[bodyC])
                    return bodyC.name;
            }
            return "";
        }

        public static string CheckOccult(Vessel vessel)
        {
            foreach (CelestialBody bodyC in FlightGlobals.Bodies)
            {
                if (!bodyDist.ContainsKey(bodyC))
                    print("Could not find body " + bodyC.bodyName);
                if (bodyDist[bodyC] < Vector3d.Distance(FlightGlobals.ship_position, vessel.GetWorldPos3D()) && Vector3d.Angle((vessel.GetWorldPos3D() - FlightGlobals.ship_position).normalized, bodyAngle[bodyC]) < bodySize[bodyC])
                    return bodyC.name;
            }
            return "";
        }

        public static string Time()
        {
            return Time(Planetarium.GetUniversalTime());
        }

        public static string Time(double t)
        {
            int y;
            int d;
            int h;
            int m;
            int s;
            if (GameSettings.KERBIN_TIME)
            {
                y = (int)Math.Floor(t / 9201600); //426 days per Kerbin year
                d = (int)Math.Floor((t - (y * 9201600)) / 21600); //6 hours per Kerbin day
                h = (int)Math.Floor((t - (y * 9201600) - (d * 21600)) / 3600);
                m = (int)Math.Floor((t - (y * 9201600) - (d * 21600) - (h * 3600)) / 60);
                s = (int)Math.Floor(t - (y * 9201600) - (d * 21600) - (h * 3600) - (m * 60));
                y += 1; //starts from year 1
                d += 1; //no day 0 either

                return y + "y-" + d + "d-" + h + "h-" + m + "m-" + s + "s";
            }
            else
            {
                y = (int)Math.Floor(t / 31536000); //365 days per Earth year
                d = (int)Math.Floor((t - (y * 31536000)) / 86400); //24 hours per Earth day
                h = (int)Math.Floor((t - (y * 31536000) - (d * 86400)) / 3600);
                m = (int)Math.Floor((t - (y * 31536000) - (d * 86400) - (h * 3600)) / 60);
                s = (int)Math.Floor(t - (y * 31536000) - (d * 86400) - (h * 3600) - (m * 60));
                y += 1; //starts from year 1
                d += 1; //no day 0 either

                return y + "y-" + d + "d-" + h + "h-" + m + "m-" + s + "s";
            }
        }

        public static void GenerateOccultationExp(CelestialBody planetBody)
        {
            print("GENERATING EXP FOR " + planetBody.bodyName);

            if (occultationExpTimes.ContainsKey(planetBody))
                RemoveOccultationExpEntry(planetBody);

            double TimeToWait = UnityEngine.Random.Range(86400, 691200); //Anywhere between 4 Kerbin days (1 Earth day) and 32 Kerbin days (8 Earth days)
            float n = UnityEngine.Random.Range(0f, 1f);
            print("TIME: " + (TimeToWait + Planetarium.GetUniversalTime()) + "||" + Time(TimeToWait + Planetarium.GetUniversalTime()) + " |||| N = " + n);
            if (n > 0.9f)
            {
                if (planetBody != FlightGlobals.Bodies[1])
                {
                    List<Vessel> astList = new List<Vessel>();
                    foreach (Vessel v in FlightGlobals.Vessels)
                    {
                        foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
                        {
                            foreach (ProtoPartModuleSnapshot m in p.modules)
                            {
                                if (m.moduleName == "ModuleAsteroid")
                                {
                                    if (v.DiscoveryInfo.trackingStatus.Value == "Tracking" && !astList.Contains(v))
                                        astList.Add(v);
                                }
                            }
                        }
                    }

                    //List<Guid> asteroidIDs = FlightGlobals.Vessels.Where(a => a.vesselType == VesselType.SpaceObject).ToList();
                    if (astList.Count > 0)
                    {
                        int i = UnityEngine.Random.Range(0, astList.Count - 1);
                        AddOccultationExpEntry(planetBody, astList[i], Planetarium.GetUniversalTime() + TimeToWait);
                        return;
                    }
                    else
                        n = UnityEngine.Random.Range(0f, 0.9f);
                }
                else
                    n = UnityEngine.Random.Range(0f, 0.9f);
            }

            if (n < 0.4f)
            {
                if (bodyChildren.ContainsKey(planetBody))
                {
                    List<CelestialBody> bodies = new List<CelestialBody>(bodyChildren[planetBody]);
                    int i = UnityEngine.Random.Range(0, bodies.Count - 1);
                    AddOccultationExpEntry(planetBody, bodies[i], Planetarium.GetUniversalTime() + TimeToWait, "Moon");
                    return;
                }
                else
                    n = UnityEngine.Random.Range(0.4f, 0.9f); //if reference planet has no moons, use another planet instead
            }

            if (n < 0.6f)
            {
                List<CelestialBody> planets = new List<CelestialBody>(bodyChildren[planetBody]);
                planets.Remove(planetBody); // don't select current reference body!

                int i = UnityEngine.Random.Range(0, planets.Count - 1);
                if (bodyChildren.ContainsKey(planets[i]))
                {
                    List<CelestialBody> bodies = bodyChildren[planets[i]];
                    int k = UnityEngine.Random.Range(0, bodies.Count - 1);
                    AddOccultationExpEntry(planetBody, bodies[k], Planetarium.GetUniversalTime() + TimeToWait, "PlanetMoon");
                    return;
                }
                else
                {
                    AddOccultationExpEntry(planetBody, planets[i], Planetarium.GetUniversalTime() + TimeToWait, "Planet"); //if the other planet has no moons, just select that planet
                    return;
                }
            }

            if (n <= 0.9f)
            {
                List<CelestialBody> planets = new List<CelestialBody>(bodyChildren[FlightGlobals.Bodies[0]]);
                planets.Remove(planetBody); // don't select current reference body!

                int i = UnityEngine.Random.Range(0, planets.Count - 1);
                AddOccultationExpEntry(planetBody, planets[i], Planetarium.GetUniversalTime() + TimeToWait, "Planet");
            }
        }

        public static void AddOccultationExpEntry(CelestialBody referenceBody, CelestialBody targetBody, double time, string type)
        {
            occultationExpTimes.Add(referenceBody, time);
            occultationExpTypes.Add(referenceBody, type);
            occultationExpBodies.Add(referenceBody, targetBody);

            //ConfigNode node = saveNode.AddNode("OccultationExperiment");
            //node.AddValue("referenceBodyName", referenceBody.bodyName);
            //node.AddValue("time", time);
            //node.AddValue("type", type);
            //node.AddValue("targetBodyName", targetBody.bodyName);
            //saveNode.Save(KSPUtil.ApplicationRootPath + "GameData/CactEye/ExperimentSaveData.cfg");
        }

        public static void AddOccultationExpEntry(CelestialBody referenceBody, Vessel targetAsteroid, double time)
        {
            occultationExpTimes.Add(referenceBody, time);
            occultationExpTypes.Add(referenceBody, "Asteroid");
            occultationExpAsteroids.Add(referenceBody, targetAsteroid);

            //ConfigNode node = saveNode.AddNode("OccultationExperiment");
            //node.name = referenceBody.bodyName;
            ////node.AddValue("referenceBodyName", referenceBody.bodyName);
            //node.AddValue("time", time);
            //node.AddValue("type", "Asteroid");
            //node.AddValue("targetAsteroidID", targetAsteroid.id.ToString("N"));
            //saveNode.Save(KSPUtil.ApplicationRootPath + "GameData/CactEye/ExperimentSaveData.cfg");
        }

        public static void RemoveOccultationExpEntry(CelestialBody referenceBody)
        {
            print("removing " + referenceBody.bodyName);
            if (occultationExpTypes.ContainsKey(referenceBody))
            {
                if (occultationExpTypes[referenceBody] == "Asteroid")
                    occultationExpAsteroids.Remove(referenceBody);
                else
                    occultationExpBodies.Remove(referenceBody);
                occultationExpTimes.Remove(referenceBody);
                occultationExpTypes.Remove(referenceBody);

                ////ConfigNode node = occultationExpNode.GetNodes("OccultationExperiment").Where(n => n.GetValue("referenceBodyName") == referenceBody.bodyName).First();
                //saveNode.RemoveNode(referenceBody.bodyName);
            }
        }

        public static string GetOccultationExpTimeString(CelestialBody referenceBody)
        {
            string r = Time(occultationExpTimes[referenceBody] - 30f); //we're subtracting thirty because the time recorded is the LIMIT, this is what we tell the user to shoot for
            r = r.Replace("-", " ");
            return r;
        }

        public static CelestialBody GetPlanetBody(CelestialBody body)
        {
            if (body == FlightGlobals.Bodies[0])
                return null;

            CelestialBody referenceBody = body.referenceBody; //Reference body is supposed to be the main planet the telescope is in the SOI of
            if (referenceBody == FlightGlobals.Bodies[0])
                referenceBody = body;

            return referenceBody;
        }
    }
}
