using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CactEye
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CactEyeGUI : MonoBehaviour
    {
        protected Rect windowPos = new Rect(Screen.width / 4, Screen.height / 4, 10f, 10f);
        private ConfigNode windowPosCFG;
        private ConfigNode windowPosNode;
        private string path;

        public bool guiOn = false;
        public bool hasOptics = false;

        public CactEyeOptics opticsModule = null;
        public CactEyeProcessor procModule = null;
        public static List<CactEyeProcessor> pMList = new List<CactEyeProcessor>();
        private List<CactEyeGyro> gMList = new List<CactEyeGyro>();
        private List<CelestialBody> bodies = new List<CelestialBody>(); //List of celestial bodies in alphabetical order
        private string[] gModes = new string[] { "Normal", "Reduced", "Fine" }; //Gyro mode strings - you have no idea how hard it was to resist naming this gStrings
        private int gSel = 0;
        private int gSelSt = 0;
        public IButton button;

        public Texture2D tex = null;
        public Texture2D preview = null;
        public Texture2D crosshair = null;
        public Texture2D targetPointer = null;
        public Texture2D save = null;
        private Rect texRect;
        private bool targetOpen = false;
        private Vector2 targetScr;
        private Vector2 targetScr2;

        static private double timer = 6f;
        private double storedTime = 0f;

        static private string dispText = "";

        private float fov = 0f;
        private string[] uPow = { "\u2070", "\u00B9", "\u00B2", "\u00B3", "\u2074", "\u2075", "\u2076", "\u2077", "\u2078", "\u2079" };

        public void Awake()
        {
            print("%$%");

            path = KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/CactEye/Resources/windowPos.cfg";
            print("path: " + path);

            windowPosCFG = ConfigNode.Load(path);
            windowPosNode = windowPosCFG.GetNode("CactEyeWindowPos");
            windowPos.xMin = float.Parse(windowPosNode.GetValue("xMin"));
            windowPos.yMin = float.Parse(windowPosNode.GetValue("yMin"));
            

            RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));

            if (ToolbarManager.ToolbarAvailable)
            {
                button = ToolbarManager.Instance.add("CactEye", "CactEyeGUI");
                button.TexturePath = "CactEye/Icons/toolbar_disabled";
                button.ToolTip = "CactEye Orbital Telescope";
                button.OnClick += (e) => Toggle();
                button.Visible = false;
            }

            preview = GameDatabase.Instance.GetTexture("CactEye/Icons/preview", false);
            preview.filterMode = FilterMode.Point;
            crosshair = GameDatabase.Instance.GetTexture("CactEye/Icons/crosshair", false);
            targetPointer = GameDatabase.Instance.GetTexture("CactEye/Icons/target", false);
            save = GameDatabase.Instance.GetTexture("CactEye/Icons/save", false);
            texRect = new Rect(10, 60, 480, 480 / FlightCamera.fetch.mainCamera.aspect);
            bodies = FlightGlobals.Bodies.OrderBy(x => x.bodyName).ToList();

            storedTime = Planetarium.GetUniversalTime();

            InvokeRepeating("AcquireOptics", 0.5f, 0.5f);
            InvokeRepeating("AcquireProcessors", 0.5f, 0.5f);
            InvokeRepeating("AcquireGyros", 0.5f, 0.5f);
        }

        public void Update()
        {
            if (opticsModule != null)
            {
                if (opticsModule.guiOn != guiOn)
                    Toggle();
            }

            timer += Planetarium.GetUniversalTime() - storedTime;
            storedTime = Planetarium.GetUniversalTime();
        }

        private void mainGUI(int windowID)
        {
            if (procModule == null && pMList.Count != 0)
                procModule = pMList[0];

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));

            if (procModule != null)
            {
                if (opticsModule != null && tex != null)
                {
                    if (!opticsModule.isDamaged)
                    {
                        if (!opticsModule.isSmallOptics ^ opticsModule.smallApertureOpen)
                        {
                            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));

                            GUI.skin.GetStyle("Label").alignment = TextAnchor.UpperLeft;
                            GUILayout.Label("Zoom");

                            //Formatting zoom amount
                            string zoom;
                            if (opticsModule.fov > 0.064)
                                zoom = "X " + string.Format("{0:##0.0}", 64 / opticsModule.fov);
                            else
                            {
                                zoom = "X " + string.Format("{0:0.00E+0}", (64 / opticsModule.fov));
                                int pow = int.Parse(zoom.Substring(zoom.Length - 1));
                                if (pow > 9)
                                    print("ERROR: Telescope zooms in more than 10^" + pow + "!!!");
                                zoom = zoom.Replace("E+", "x10");
                                zoom = zoom.Remove(zoom.Length - 1);
                                zoom += uPow[pow];
                            }

                            GUI.skin.GetStyle("Label").alignment = TextAnchor.UpperRight;
                            GUILayout.Label(zoom);

                            GUILayout.EndHorizontal();


                            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));

                            GUI.skin.GetStyle("Label").alignment = TextAnchor.UpperCenter;
                            fov = GUILayout.HorizontalSlider(fov, 0f, 1f);
                            //print("minFOV: " + procModule.minFOV + " || ^1/3: " + Mathf.Pow(procModule.minFOV, 1f / 3f));
                            opticsModule.fov = 0.5f * Mathf.Pow(4f - fov * (4f - Mathf.Pow(procModule.minFOV, 1f / 3f)), 3);

                            GUILayout.EndHorizontal();


                            GUILayout.Space(texRect.height);
                            tex = opticsModule.outputTex;
                            GUI.Box(new Rect(texRect.xMin - 2, texRect.yMin - 2, texRect.width + 4, texRect.height + 4), "");
                            GUI.DrawTexture(texRect, tex);
                            //GUILayout.Box("", GUILayout.Width(texRect.width), GUILayout.Height(texRect.height));
                            GUI.DrawTexture(new Rect(texRect.xMin, texRect.yMax - 32f, 128f, 32f), preview);
                            GUI.DrawTexture(new Rect(texRect.xMin + (0.5f * texRect.width) - 64, texRect.yMin + (0.5f * texRect.height) - 64, 128, 128), crosshair);
                            if (timer < 5f)
                            {
                                GUI.skin.GetStyle("Label").alignment = TextAnchor.UpperLeft;
                                GUI.Label(new Rect(texRect.xMin, texRect.yMin, 480, 50), new GUIContent(dispText));
                            }

                            if (FlightGlobals.fetch.VesselTarget != null)
                            {
                                string targetName = FlightGlobals.fetch.VesselTarget.GetName();
                                Vector2 vec = opticsModule.GetTargetPos(FlightGlobals.fetch.VesselTarget.GetTransform().position, texRect.width);

                                if (vec.x > 16 && vec.y > 16 && vec.x < texRect.width - 16 && vec.y < texRect.height - 16)
                                {
                                    GUI.DrawTexture(new Rect(vec.x + texRect.xMin - 16, vec.y + texRect.yMin - 16, 32, 32), targetPointer);
                                    Vector2 size = GUI.skin.GetStyle("Label").CalcSize(new GUIContent(targetName));
                                    if (vec.x > 0.5 * size.x && vec.x < texRect.width - (0.5 * size.x) && vec.y < texRect.height - 16 - size.y)
                                    {
                                        GUI.skin.GetStyle("Label").alignment = TextAnchor.UpperCenter;
                                        GUI.Label(new Rect(vec.x + texRect.xMin - (0.5f * size.x), vec.y + texRect.yMin + 20, size.x, size.y), targetName);
                                    }
                                }

                                if (procModule.techType == "Planetary" && procModule.isActive && procModule.isFunctional)
                                {
                                    if (GUI.Button(new Rect(texRect.xMax - 36, texRect.yMax - 36, 32, 32), save))
                                        DisplayText("Saved screenshot to " + opticsModule.GetTex(true, targetName));
                                }
                            }
                        }
                        else
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("FungEye optics aperture closed.");
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Note that you can never close the aperture again once you open them with these optics!");
                            GUILayout.EndHorizontal();
                        }
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Optics module has been damaged by the sun! Repair must be made to optics module during an EVA.");
                        //print("FONT SIZE: " + GUI.skin.GetStyle("Label").fontSize);
                        GUILayout.EndHorizontal();
                    }
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("No optics detected!");
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(10f);

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));

                GUI.skin.GetStyle("Label").alignment = TextAnchor.MiddleCenter;
                string targetstring;
                if (FlightGlobals.fetch.VesselTarget == null)
                    targetstring = "No target selected";
                else
                    targetstring = "Target: " + FlightGlobals.fetch.VesselTarget.GetName();

                GUILayout.Label(targetstring, GUILayout.Height(30f), GUILayout.Width(370f));

                if (GUILayout.Button("Select Target", GUILayout.Height(30f)))
                    targetOpen = !targetOpen;

                GUILayout.EndHorizontal();

                GUILayout.Space(10f);

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));

                GUI.skin.GetStyle("Label").alignment = TextAnchor.MiddleLeft;
                GUILayout.Label("Processor: ", GUILayout.Width(100f), GUILayout.Height(30f));

                GUI.skin.GetStyle("Label").alignment = TextAnchor.MiddleCenter;
                if (GUILayout.Button(procModule.pName, GUILayout.Height(30f)))
                {
                    int pI = pMList.IndexOf(procModule) + 1;
                    if (pI > pMList.Count - 1)
                        procModule = pMList[0];
                    else
                        procModule = pMList[pI];
                }

                GUILayout.Space(15);
                GUILayout.Label(procModule.status, GUILayout.Height(30f));
                GUILayout.FlexibleSpace();
                if (procModule.isFunctional)
                {
                    if (GUILayout.Button(procModule.isActive ? "Disable" : "Activate", GUILayout.Height(30f), GUILayout.Width(80f)))
                    {
                        procModule.toggle(null);
                    }
                }

                GUILayout.EndHorizontal();

                if (procModule.isActive && procModule.isFunctional && HighLogic.CurrentGame.Mode != Game.Modes.SANDBOX)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("", GUILayout.Width(100f), GUILayout.Height(30f));

                    if (procModule.GetScienceCount() == 0)
                    {
                        if (GUILayout.Button("Make Observation", GUILayout.Height(30f)))
                            procModule.observation();
                    }
                    else
                    {
                        if (GUILayout.Button("Review Data", GUILayout.Height(30f)))
                            procModule.eventReviewScience();
                        GUILayout.Space(80);
                        if (GUILayout.Button("Dump Data", GUILayout.Height(30f)))
                            procModule.eventReviewScience();
                    }

                    GUILayout.EndHorizontal();

                    if (procModule.techType == "Occultation")
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("", GUILayout.Width(100f), GUILayout.Height(30f));

                        CelestialBody planetBody = CactEyeVars.GetPlanetBody(FlightGlobals.ActiveVessel.mainBody);

                        if (planetBody != null)
                        {
                            if (!CactEyeVars.occultationExpTypes.ContainsKey(planetBody))
                                CactEyeVars.GenerateOccultationExp(planetBody);

                            if (CactEyeVars.occultationExpTypes[planetBody] == "Asteroid")
                                GUILayout.Label("Next occultation: " + CactEyeVars.occultationExpAsteroids[planetBody].vesselName + ", at " + CactEyeVars.GetOccultationExpTimeString(planetBody), GUILayout.Height(30f));
                            else
                                GUILayout.Label("Next occultation: " + CactEyeVars.occultationExpBodies[planetBody].bodyName + ", at " + CactEyeVars.GetOccultationExpTimeString(planetBody), GUILayout.Height(30f));

                            //I sure wish I could put a button here to auto create a KAC alarm. Hint hint TriggerAu - make a KAC wrapper!
                        }
                        else
                            GUILayout.Label("Occultation experiment not available in solar orbit!");

                        GUILayout.EndHorizontal();
                    }
                }
                
                if (HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Science experiments unavailable in sandbox mode!");
                    GUILayout.EndHorizontal();
                }

                if (gMList.Count != 0)
                {
                    GUILayout.FlexibleSpace();

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));

                    GUI.skin.GetStyle("Label").alignment = TextAnchor.MiddleLeft;
                    GUILayout.Label("Gyro Speed: ", GUILayout.Width(100f), GUILayout.Height(30f));

                    GUI.skin.GetStyle("Label").alignment = TextAnchor.MiddleCenter;
                    gSel = GUILayout.SelectionGrid(gSel, gModes, 3, GUILayout.Height(30f));

                    if (gSel != gSelSt)
                    {
                        if (gSel == 0)
                        {
                            foreach (CactEyeGyro gM in gMList)
                                gM.normScale(null);
                        }
                        if (gSel == 1)
                        {
                            foreach (CactEyeGyro gM in gMList)
                                gM.redScale(null);
                        }
                        if (gSel == 2)
                        {
                            foreach (CactEyeGyro gM in gMList)
                                gM.fineScale(null);
                        }

                        gSelSt = gSel;
                    }

                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("No processors detected!");
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();

            GUI.DragWindow();
            GUI.skin.GetStyle("Label").alignment = TextAnchor.UpperLeft;
        }

        private void targetGUI(int windowID)
        {
            GUI.skin.GetStyle("Label").alignment = TextAnchor.MiddleCenter;
            GUILayout.BeginVertical(GUILayout.ExpandHeight(false));

            GUILayout.BeginHorizontal();
            //GUILayout.FlexibleSpace();
            if (GUILayout.Button("Collapse", GUILayout.Height(30f)))
                targetOpen = false;
            //GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (procModule.techType == "Planetary" || procModule.techType == "Occultation")
            {
                GUILayout.BeginVertical();
                GUILayout.Box("Planetary Targets", GUILayout.Width(120), GUILayout.Height(25));
                GUILayout.EndVertical();
            }

            GUILayout.FlexibleSpace();

            if (procModule.techType == "Asteroid" || procModule.techType == "Occultation")
            {
                GUILayout.BeginVertical();
                GUILayout.Box("Asteroid Targets", GUILayout.Width(120), GUILayout.Height(25));
                GUILayout.EndVertical();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (procModule.techType == "Planetary" || procModule.techType == "Occultation")
            {
                targetScr = GUILayout.BeginScrollView(targetScr, GUILayout.Width(120), GUILayout.Height(windowPos.height - 110));

                GUILayout.BeginVertical();
                GUILayout.Space(10f);

                foreach (CelestialBody b in bodies)
                {
                    if (b.bodyName != "Sun" && b.bodyName != FlightGlobals.ActiveVessel.mainBody.bodyName)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(b.bodyName))
                            FlightGlobals.fetch.SetVesselTarget(b);
                        GUILayout.EndHorizontal();
                    }
                }

                GUILayout.EndVertical();

                GUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();

            if (procModule.techType == "Asteroid" || procModule.techType == "Occultation")
            {
                targetScr2 = GUILayout.BeginScrollView(targetScr2, GUILayout.Width(120), GUILayout.Height(windowPos.height - 110));

                GUILayout.BeginVertical();
                GUILayout.Space(10f);

                List<Vessel> astList = new List<Vessel>();
                foreach (Vessel v in FlightGlobals.Vessels)
                {   
                    if (v.vesselType == VesselType.SpaceObject)
                    {
                        if (v.DiscoveryInfo.trackingStatus.Value == "Tracking" && !astList.Contains(v))
                            astList.Add(v);
                    }
                }

                foreach (Vessel v in astList)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(v.vesselName + "\n(Class " + v.DiscoveryInfo.size.Value + ")"))
                        FlightGlobals.fetch.SetVesselTarget(v);
                    GUILayout.EndHorizontal();
                }

                GUILayout.EndVertical();

                GUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        protected void drawGUI()
        {
            if (guiOn && hasOptics && HighLogic.LoadedSceneIsFlight)
            {
                if (!PlanetariumCamera.fetch.enabled)
                {
                    windowPos = GUILayout.Window(-5234628, windowPos, mainGUI, "CactEye Orbital Telescope", GUILayout.Width(500), GUILayout.Height(540));
                    windowPosNode.SetValue("xMin", "" + windowPos.xMin);
                    windowPosNode.SetValue("yMin", "" + windowPos.yMin);
                    windowPosCFG.Save(path);

                    if (procModule != null && targetOpen)
                    {
                        GUILayout.Window(-5234627, new Rect(windowPos.xMin + 500, windowPos.yMin, procModule.techType == "Occultation" ? 300 : 150, windowPos.height), targetGUI, "Target Selection");
                    }
                }
            }
        }

        static public void DisplayText(string text)
        {
            dispText = text;
            timer = 0;
        }

        public void Toggle()
        {
            guiOn = !guiOn;
            if (ToolbarManager.ToolbarAvailable && hasOptics)
            {
                if (guiOn)
                    button.TexturePath = "CactEye/Icons/toolbar";
                else
                    button.TexturePath = "CactEye/Icons/toolbar_disabled";
            }

            if (hasOptics)
            {
                opticsModule.guiOn = guiOn;
            }
        }

        public void AcquireOptics()
        {
            int opticsPriority = 0;
            foreach (Part p in FlightGlobals.ActiveVessel.parts)
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
                            opticsModule = oM;
                        }
                        //Small and functional optics get second
                        else if (oM.isSmallOptics && oM.isFunctional && opticsPriority < 2)
                        {
                            opticsPriority = 2;
                            opticsModule = oM;
                        }
                        //Nonfunctional optics get last priority
                        else if (!oM.isFunctional && opticsPriority < 1)
                        {
                            opticsPriority = 1;
                            opticsModule = oM;
                        }
                    }
                }
            }
            if (opticsModule != null)
            {
                if (ToolbarManager.ToolbarAvailable)
                    button.Visible = true;
                
                hasOptics = true;

                if (tex == null)
                {
                    tex = new Texture2D(160, (int)(160 / FlightCamera.fetch.mainCamera.aspect));
                }
            }
            else
            {
                if (ToolbarManager.ToolbarAvailable)
                {
                    button.Visible = false;
                }

                hasOptics = false;
            }
        }

        private void AcquireProcessors()
        {
            pMList.Clear();

            foreach (Part p in FlightGlobals.ActiveVessel.Parts)
            {
                CactEyeProcessor cP = p.GetComponent<CactEyeProcessor>();

                if (cP != null)
                {
                    if (!pMList.Contains(cP))
                        pMList.Add(cP);
                }
            }
        }

        private void AcquireGyros()
        {
            foreach (Part p in FlightGlobals.ActiveVessel.Parts)
            {
                CactEyeGyro cG = p.GetComponent<CactEyeGyro>();

                if (cG != null)
                {
                    if (!gMList.Contains(cG))
                        gMList.Add(cG);
                }
            }
        }

        private void OnDestroy()
        {
            button.Destroy();
        }
    }
}
