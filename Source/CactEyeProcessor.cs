using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CactEye
{
    public class CactEyeProcessor : PartModule, IScienceDataContainer
    {
        [KSPField(isPersistant = true)]
        public bool isActive = false;
        public bool isFunctional = false;

        [KSPField(isPersistant = true)]
        public float discoveryRate = 0f;

        [KSPField(isPersistant = true)]
        public bool fullRecovery = false;

        [KSPField(isPersistant = false)]
        public float maxScience = 0.25f;
        [KSPField(isPersistant = false)]
        public float maxDiscoveryRate = 0f;
        [KSPField(isPersistant = false)]
        public float consumeRate = 2f;
        [KSPField(isPersistant = false)]
        public float minFOV = 0.3f;
        [KSPField(isPersistant = false)]
        public string techType = "Planetary";
        [KSPField(isPersistant = false)]
        public string experimentID = "CactEyeWFC1";
        [KSPField(isPersistant = false)]
        public string occAstExperimentID = "CactEyeOccultationAsteroid";
        [KSPField(isPersistant = false)]
        public string pName = "Unnamed Processor";

        [KSPField(isPersistant = false, guiActive = true, guiName = "Status", guiActiveEditor = false)]
        public string status = "Off";

        //saving the current occultation experiment in the persistence
        [KSPField(isPersistant = true)]
        public double occTime = 0;
        [KSPField(isPersistant = true)]
        public string occType = "None";
        [KSPField(isPersistant = true)]
        public string occBody = "";
        [KSPField(isPersistant = true)]
        public string occAst = "";

        //private bool storedOpticsState = false;
        private CactEyeOptics opticsModule = null;
        //private bool smallOptics = false;

        private float timer = 5f;
        private double storedTime = 0f;

        private List<ScienceData> storedData = new List<ScienceData>();
        [KSPField(isPersistant = true)]
        private bool dataAsteroid = false;
        [KSPField(isPersistant = true)]
        public string storedPath = "";

        private GUIStyle styleStored;        
        private GUIStyle progressStyle;
        private GUISkin skinStored;
        private GUIStyleState styleDefault;

        private Texture2D tex = new Texture2D(Screen.width, Screen.height);

        private bool checkSetup()
        {
            Part mount = part.parent;
            if (mount == null)
            {
                deactivate();
                status = "Processor not mounted";
                return false;
            }

            //If there's multiple sets of optics (why?) choose the best suited one.
            Part optics = null;
            int opticsPriority = 0;
            foreach (Part p in vessel.parts)
            {
                if (opticsPriority != 3)
                {
                    CactEyeOptics oM = p.GetComponent<CactEyeOptics>();
                    if (oM != null)
                    {
                        //Large and functional optics get top priority
                        if (!oM.isSmallOptics && oM.isFunctional)
                        {
                            opticsPriority = 3;
                            optics = p;
                            opticsModule = oM;
                        }
                        //Small and functional optics get second
                        else if (oM.isSmallOptics && oM.isFunctional && opticsPriority < 2)
                        {
                            opticsPriority = 2;
                            optics = p;
                            opticsModule = oM;
                        }
                        //Nonfunctional optics get last priority
                        else if (!oM.isFunctional && opticsPriority < 1)
                        {
                            opticsPriority = 1;
                            optics = p;
                            opticsModule = oM;
                        }
                    }
                }
            }
            if (optics == null)
            {
                deactivate();
                status = "No optics detected";
                return false;
            }
            else
            {
                if (opticsPriority == 0 || opticsPriority == 1)
                {
                    deactivate();
                    status = "Aperture closed";
                    return false;
                }

                if (isActive)
                {
                    //Try to consume power
                    double consumeAmount = (consumeRate * (Planetarium.GetUniversalTime() - storedTime));
                    if (part.RequestResource("ElectricCharge", consumeAmount) < consumeAmount * 0.95) //separated from other if statement because it actually eats up ElectricCharge when it's called
                    {
                        deactivate();
                        status = "Insufficient ElectricCharge";
                        timer = 0f;
                        ScreenMessages.PostScreenMessage("Processor Shutting Down (Insufficient ElectricCharge)", 6, ScreenMessageStyle.UPPER_CENTER);
                        return false;
                    }
                    else
                    {
                        isFunctional = true;
                        status = "Functioning...";
                        return true;
                    }
                }
                else
                {
                    if (timer > 3f)
                        status = "Off";
                    return true;
                }
            }
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();

            timer += (float)(Planetarium.GetUniversalTime() - storedTime);
            isFunctional = checkSetup();

            UpdateVarsEntries();

            ////If the aperture has opened/closed, recalculate stuff
            //if (opticsModule != null)
            //{
            //    if (opticsModule.isFunctional != storedOpticsState)
            //    {
            //        checkSetup();
            //    }
            //}

            if (isFunctional && isActive)
            {
                if (opticsModule != null)
                {
                    if (opticsModule.isSmallOptics)
                        discoveryRate = 0.5f * maxDiscoveryRate;
                    else
                        discoveryRate = maxDiscoveryRate;
                }
            }
            else
                discoveryRate = 0f;

            if (isFunctional && isActive && storedData.Count() == 0)
            {
                //Events["observation"].active = true;
                Events["eventReviewScience"].active = false;
                Events["eventDumpData"].active = false;
            }
            else
            {
                //Events["observation"].active = false;
                Events["eventReviewScience"].active = true;
                Events["eventDumpData"].active = true;
            }

            //If the sun is visible and the telescope is pointed at it, bad things should happen!
            if(opticsModule != null && opticsModule.isFunctional)
            {
                if (CactEyeVars.CheckOccult(FlightGlobals.Bodies[0]) == "")
                {
                    Vector3d heading = (FlightGlobals.Bodies[0].position - FlightGlobals.ship_position).normalized;
                    if (Vector3d.Dot(opticsModule.part.transform.up, heading) > 0.90)
                        CactEyeGUI.DisplayText("WARNING: Telescope pointing dangerously close to the sun!");
                    if (Vector3d.Dot(opticsModule.part.transform.up, heading) > 0.95)
                    {
                        ScreenMessages.PostScreenMessage("Telescope pointed directly at sun, optics damaged and processor fried!", 6, ScreenMessageStyle.UPPER_CENTER);
                        opticsModule.BreakScope();
                        CactEyeGUI.pMList.Remove(this);
                        part.explode(); //officially the best function ever
                    }
                }
            }

            if (!fullRecovery)
            {
                if ((vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.SPLASHED) && storedData.Count > 0)
                {
                    ScreenMessages.PostScreenMessage("Only data from high tech processors taken with full optics is recoverable!", 6, ScreenMessageStyle.UPPER_CENTER);
                    ScreenMessages.PostScreenMessage("Telescope data lost!", 6, ScreenMessageStyle.UPPER_CENTER);
                    eventDumpData();
                }
            }

            storedTime = Planetarium.GetUniversalTime();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor)
                return;

            Events["activate"].active = !isActive;
            Events["deactivate"].active = isActive;

            storedTime = Planetarium.GetUniversalTime();

            checkSetup();

            if (!isActive)
                deactivate();
            else
                activate();
        }

        public void UpdateVarsEntries()
        {
            CelestialBody pBody = CactEyeVars.GetPlanetBody(vessel.mainBody);

            if (!CactEyeVars.occultationExpTypes.ContainsKey(pBody))
            {
                print("WAITING");
                return;
            }
            else
            {
                occType = CactEyeVars.occultationExpTypes[pBody];
                occTime = CactEyeVars.occultationExpTimes[pBody];
                if (occType == "Asteroid")
                    occAst = CactEyeVars.occultationExpAsteroids[pBody].id.ToString("N");
                else
                    occBody = CactEyeVars.occultationExpBodies[pBody].bodyName;
            }
        }

        public override string GetInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tech Type: " + techType);

            sb.AppendLine("Possible Science Gain: " + (maxScience * 100) + "%");
            if (maxScience < 1)
                sb.AppendLine("- More advanced processors can gain more science. You can upgrate later with a service mission!");

            sb.AppendLine();

            if (maxDiscoveryRate > 0)
                sb.AppendLine("Will actively discover new asteroids");
            if (maxDiscoveryRate > 0)
                sb.AppendLine("<color=#ffa500ff>Will not find any new asteroids while within 100km of another telescope!</color>");

            sb.AppendLine();

            sb.AppendLine("Magnification: " + string.Format("{0:n0}", 128 / minFOV) + "x");

            sb.AppendLine();

            sb.AppendLine("<color=#99ff00ff>Requires:</color>");
            sb.AppendLine("- ElectricCharge: " + string.Format("{0:0.0##}", consumeRate) + "/sec");

            sb.AppendLine();

            sb.AppendLine("<color=#ffa500ff>Do not point directly at sun while activated!</color>");

            return sb.ToString();
        }

        [KSPEvent(guiActive = true, guiName = "Activate", active = true)]
        public void activate()
        {
            isActive = true;
            Events["activate"].active = !isActive;
            Events["deactivate"].active = isActive;
            checkSetup();
        }

        [KSPEvent(guiActive = true, guiName = "Deactivate", active = true)]
        public void deactivate()
        {
            isActive = false;
            Events["activate"].active = !isActive;
            Events["deactivate"].active = isActive;
            //moduleHC.Events["ActivateCamera"].active = false;
            status = "Off";
        }

        [KSPAction("Activate")]
        public void activate(KSPActionParam param)
        {
            activate();
        }

        [KSPAction("Deactivate")]
        public void deactivate(KSPActionParam param)
        {
            deactivate();
        }

        [KSPAction("Toggle")]
        public void toggle(KSPActionParam param)
        {
            if (isActive)
                deactivate();
            else
                activate();
        }
        
        //[KSPAction("SCIENCE")]
        //public void scienceee(KSPActionParam param)
        //{
        //    observation();
        //}

        //[KSPEvent(guiActive = true, guiName = "Observe", active = true)]
        public void observation()
        {
            if (storedData.Count > 0)
            {
                CactEyeGUI.DisplayText("Processor already contains experiment data!");
                return;
            }
            if (HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX)
            {
                CactEyeGUI.DisplayText("Science experiments unavailable in sandbox mode!");
                return;
            }

            var target = FlightGlobals.fetch.VesselTarget;
            if (target == null)
            {
                CactEyeGUI.DisplayText("You must select a target!");
                return;
            }

            if (target.GetType().Name == "CelestialBody" && techType == "Planetary")
            {
                CelestialBody targetBody = FlightGlobals.Bodies.Find(n => n.GetName() == target.GetName());

                string oBody = CactEyeVars.CheckOccult(targetBody);
                if (oBody != "")
                {
                    CactEyeGUI.DisplayText("Target is behind " + oBody + "!");
                    return;
                }
                else if (targetBody == FlightGlobals.Bodies[0])
                {
                    CactEyeGUI.DisplayText("Cannot target the sun!");
                    return;
                }
                else if (opticsModule.GetTargetPos(targetBody.transform.position, 500f) == new Vector3(-1, -1, 0))
                {
                    CactEyeGUI.DisplayText("Target not in telescope sights!");
                    return;
                }
                else if (opticsModule.fov > CactEyeVars.bodySize[targetBody] * 50f)
                {
                    CactEyeGUI.DisplayText("Telescope not zoomed in far enough!");
                    return;
                }
                else
                    doScience(FlightGlobals.Bodies.Find(n => n.GetName() == target.GetName()));
            }
            else if (target.GetType().Name == "Vessel" && techType == "Asteroid")
            {
                Vessel targetVessel = target.GetVessel();
                if (techType == "Asteroid" && targetVessel.vesselType == VesselType.SpaceObject && targetVessel.DiscoveryInfo.trackingStatus.Value == "Tracking")
                {
                    if (CactEyeVars.GetPlanetBody(vessel.mainBody) == FlightGlobals.Bodies[1])
                    {
                        string oBody = CactEyeVars.CheckOccult(targetVessel);
                        if (oBody != "")
                        {
                            CactEyeGUI.DisplayText("Target is behind " + oBody + "!");
                            return;
                        }
                        else if (opticsModule.GetTargetPos(targetVessel.GetWorldPos3D(), 500f) == new Vector3(-1, -1, 0))
                        {
                            CactEyeGUI.DisplayText("Target not in telescope sights!");
                            return;
                        }
                        else if (opticsModule.fov > 0.5f)
                        {
                            CactEyeGUI.DisplayText("Telescope not zoomed in far enough!");
                            return;
                        }
                        else
                            doScience(targetVessel);
                    }
                    else
                    {
                        CactEyeGUI.DisplayText("Telescope must be near Kerbin to perform asteroid experiment!");
                        return;
                    }
                }
                else
                {
                    CactEyeGUI.DisplayText("Target not valid!");
                    return;
                }
            }
            else if (techType == "Occultation")
            {
                CelestialBody planetBody = CactEyeVars.GetPlanetBody(vessel.mainBody);

                if (occType != "None")
                {
                    print(occTime - Planetarium.GetUniversalTime());
                    if (occTime - Planetarium.GetUniversalTime() < 60) //target time is 30 seconds before limit, experiment should be within 30 seconds before or after target
                    {
                        if (occType == "Asteroid" && target.GetType().Name == "Vessel")
                        {
                            Vessel targetVessel = target.GetVessel();
                            if (targetVessel == CactEyeVars.occultationExpAsteroids[planetBody])
                            {
                                string oBody = CactEyeVars.CheckOccult(targetVessel);
                                if (oBody != "")
                                {
                                    CactEyeGUI.DisplayText("Target is behind " + oBody + "!");
                                    return;
                                }
                                else if (opticsModule.GetTargetPos(targetVessel.GetWorldPos3D(), 500f) == new Vector3(-1, -1, 0))
                                {
                                    CactEyeGUI.DisplayText("Target not in telescope sights!");
                                    return;
                                }
                                else if (opticsModule.fov > 0.5f)
                                {
                                    CactEyeGUI.DisplayText("Telescope not zoomed in far enough!");
                                    return;
                                }
                                else
                                    doScience(targetVessel);
                            }
                            else
                            {
                                CactEyeGUI.DisplayText("Incorrect target!");
                                return;
                            }
                        }
                        else if (occType != "Asteroid" && target.GetType().Name == "CelestialBody")
                        {
                            CelestialBody targetBody = FlightGlobals.Bodies.Find(n => n.GetName() == target.GetName());

                            if (targetBody != CactEyeVars.occultationExpBodies[planetBody])
                            {
                                CactEyeGUI.DisplayText("Incorrect target!");
                                return;
                            }
                            string oBody = CactEyeVars.CheckOccult(targetBody);
                            if (oBody != "")
                            {
                                CactEyeGUI.DisplayText("Target is behind " + oBody + "!");
                                return;
                            }
                            else if (opticsModule.GetTargetPos(targetBody.transform.position, 500f) == new Vector3(-1, -1, 0))
                            {
                                CactEyeGUI.DisplayText("Target not in telescope sights!");
                                return;
                            }
                            else if (opticsModule.fov > CactEyeVars.bodySize[targetBody] * 50f)
                            {
                                CactEyeGUI.DisplayText("Telescope not zoomed in far enough!");
                                return;
                            }
                            else
                                doScience(FlightGlobals.Bodies.Find(n => n.GetName() == target.GetName()));
                        }
                    }
                    else
                    {
                        CactEyeGUI.DisplayText("Occultation experiment must be performed within thirty seconds of target time!");
                        return;
                    }
                }
                else
                {
                    CactEyeGUI.DisplayText("Occultation experiment not available!");
                    return;
                }
            }
            else
            {
                CactEyeGUI.DisplayText("Target not valid!");
                return;
            }
        }

        public void doScience(CelestialBody target)
        {
            print("DOING SCIENCE, " + target.GetName());
            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(experimentID);
            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ExperimentSituations.InSpaceHigh, target, "");

            float sciTrans = Mathf.Max(subject.scientificValue - (1f - (opticsModule.isSmallOptics ? 0.10f : maxScience)), 0.0f);

            if (sciTrans == subject.scientificValue)
                fullRecovery = true;

            print("Current sciTrans: " + sciTrans);
            
            ScienceData data = new ScienceData(Mathf.Max(experiment.baseValue * subject.dataScale * sciTrans, 0.001f), 1.0f, 0.0f, subject.id, pName + " " + target.bodyName + " Observation");

            storedPath = opticsModule.GetTex(true, target.bodyName);
            storedData.Add(data);

            Events["eventReviewScience"].active = true;
            Events["eventDumpData"].active = true;

            eventReviewScience();
        }

        public void doScience(Vessel target)
        {
            print("DOING SCIENCE, " + target.GetName());
            print("CLASS: " + target.DiscoveryInfo.size.Value);
            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment((techType == "Occultation") ? ("CactEyeOccultationAsteroid_" + target.DiscoveryInfo.size.Value) : ("CactEyeAsteroid_" + target.DiscoveryInfo.size.Value));
            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ExperimentSituations.InSpaceHigh, FlightGlobals.Bodies[1], "");

            float sciTrans = Mathf.Max(subject.scientificValue - (1f - (opticsModule.isSmallOptics ? 0.10f : maxScience)), 0.0f);

            if (sciTrans == subject.scientificValue)
                fullRecovery = true;

            print("Current sciTrans: " + sciTrans);

            ScienceData data = new ScienceData(Mathf.Max(experiment.baseValue * subject.dataScale * sciTrans, 0.001f), 1.0f, 0.0f, subject.id, pName + " " + experiment.experimentTitle);

            storedData.Add(data);
            dataAsteroid = true;

            Events["eventReviewScience"].active = true;
            Events["eventDumpData"].active = true;

            eventReviewScience();
        }

        [KSPEvent(guiActive = true, guiName = "Review Data", name = "eventReviewScience", active = false)]
        public void eventReviewScience()
        {
            foreach (ScienceData data in storedData)
                ReviewDataItem(data);
        }

        [KSPEvent(guiActive = true, guiName = "Dump Data", name = "eventDumpData", active = false)]
        public void eventDumpData()
        {
            fullRecovery = false;
            foreach (ScienceData data in storedData)
                DumpData(data);
        }

        public void ReviewData()
        {
            eventReviewScience();
        }

        private void _onPageDiscard(ScienceData data)
        {
            fullRecovery = false;
            storedPath = "";
            storedData.Remove(data);
            ResetExperimentGUI();
        }

        private void _onPageKeep(ScienceData data)
        {
            ResetExperimentGUI();
        }

        private void _onPageTransmit(ScienceData data)
        {
            List<IScienceDataTransmitter> transmitters = vessel.FindPartModulesImplementing<IScienceDataTransmitter>();
            if (transmitters.Count > 0 && storedData.Contains(data))
            {
                fullRecovery = false;
                transmitters.First().TransmitData(new List<ScienceData> { data });
                storedPath = "";
                storedData.Remove(data);
                ResetExperimentGUI();
            }
        }

        //[KSPEvent(active = true, externalToEVAOnly = true, guiActiveUnfocused = true, guiName = "Collect Data", unfocusedRange = 2)]
        //public void CollectScience()
        //{
        //    List<ModuleScienceContainer> containers = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>();
        //    foreach (ModuleScienceContainer container in containers)
        //    {
        //        if (storedData.Count > 0)
        //        {
        //            if (container.StoreData(new List<IScienceDataContainer>() { this }, false))
        //                ScreenMessages.PostScreenMessage("Transferred Data to " + vessel.vesselName, 3f, ScreenMessageStyle.UPPER_CENTER);
        //        }
        //    }
        //}

        private void _onPageSendToLab(ScienceData data)
        {
        }

        public void ReviewDataItem(ScienceData data)
        {
            StartCoroutine(ReviewDataCoroutine(data));
        }

        public void DumpData(ScienceData data)
        {
            storedPath = "";
            storedData.Remove(data);
            ResetExperimentGUI();
        }

        public ScienceData[] GetData()
        {
            print("Getting data? ^^");
            return storedData.ToArray();
        }
        
        public System.Collections.IEnumerator ReviewDataCoroutine(ScienceData data)
        {
            yield return new WaitForEndOfFrame();

            ExperimentResultDialogPage page = new ExperimentResultDialogPage(FlightGlobals.ActiveVessel.rootPart, data, 1.0f, 0, false, "", false, false,
                new Callback<ScienceData>(_onPageDiscard), new Callback<ScienceData>(_onPageKeep), new Callback<ScienceData>(_onPageTransmit), new Callback<ScienceData>(_onPageSendToLab));

            page.scienceValue = 0.0f;

            ExperimentsResultDialog dialog = ExperimentsResultDialog.DisplayResult(page);

            progressStyle = dialog.guiSkin.customStyles.Where(n => n.name == "progressBarFill2").First();
            progressStyle.fixedWidth = 0.1f;
            progressStyle.border = new RectOffset(0, 0, 0, 0);
            progressStyle.overflow = new RectOffset(0, 0, 0, 0);
            
            //progressStyleBGCopy = new Texture2D(progressStyle.normal.background.width, progressStyle.normal.background.height);
            //progressStyleBGCopy = progressStyle.normal.background;
            //if (!fullRecovery)

            if (!dataAsteroid)
            {
                GUIStyle style = dialog.guiSkin.box;
                styleDefault = style.normal;
                styleStored = style;
                skinStored = dialog.guiSkin;

                print("Attempting to access " + CactEyeVars.root + storedPath);
                if (System.IO.File.Exists(CactEyeVars.root + storedPath))
                {
                    WWW www = new WWW("file://" + CactEyeVars.root + storedPath);
                    yield return www;
                    www.LoadImageIntoTexture(tex);
                    style.normal.background = tex;

                    dialog.guiSkin.window.fixedWidth = 587f;
                    style.fixedWidth = 512f;
                    style.fixedHeight = 288f;

                    page.resultText = "Screenshot saved to " + storedPath;
                }
                else
                    print("Unable to find " + storedPath + " !");
            }
        }

        private void ResetExperimentGUI()
        {
            print("Resetting GUI...");
            if (techType != "Asteroid")
            {
                skinStored.box.normal = styleDefault;
                skinStored.box.normal.background = GameDatabase.Instance.GetTexture("CactEye/Icons/ExperimentGUIBackground", false);
                skinStored.box.fixedWidth = 0f;
                skinStored.box.fixedHeight = 0f;
                skinStored.window.fixedWidth = 400f;
                styleStored.fixedHeight = 0f;
            }
            progressStyle.fixedWidth = 0;
            progressStyle.border = new RectOffset(4, 4, 4, 4);
            progressStyle.overflow = new RectOffset(0, 0, -3, -3);
            //progressStyle.fixedHeight = 18f;

            dataAsteroid = false;

            //Events["observation"].active = true;
            Events["eventReviewScience"].active = false;
            Events["eventDumpData"].active = false;
        }

        public bool IsRerunnable()
        {
            return true;
        }

        public int GetScienceCount()
        {
            return storedData.Count();
        }
    }
}
