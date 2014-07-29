using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CactEye
{
    public class CactEyeOptics : PartModule
    {
        [KSPField(isPersistant = false)]
        public bool isSmallOptics = false;
        [KSPField(isPersistant = false)]
        public string camTransformName = "CactEyeCam";

        [KSPField(isPersistant = false)]
        public bool isFunctional = false;
        [KSPField(isPersistant = true)]
        public bool isDamaged = false;
        [KSPField(isPersistant = true)]
        public bool smallApertureOpen = false;

        public bool guiOn = false;
        //public float aspectRatio;
        public float fov = 64;

        private ModuleAnimateGeneric opticsAnimate;

        public Texture2D outputTex;
        private Texture2D fullTex;
        private RenderTexture rtLow;
        private RenderTexture rtFull;

        private List<Camera> cameras = new List<Camera>();
        private Transform camTrans = null;
        private Rect fullRect;
        private Rect smallRect;

        private ScaledSpaceFader[] scaledSpaceFaders;
        private Renderer[] skyboxRenderers;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor)
                return;

            opticsAnimate = GetComponent<ModuleAnimateGeneric>();
            if (opticsAnimate == null && !smallApertureOpen)
            {
                isFunctional = false;
                Events["OpenSmallAperture"].active = true;
            }

            if (isDamaged)
            {
                isFunctional = false;
                Events["FixScope"].active = true;
            }

            fullRect = new Rect(0, 0, Screen.width, Screen.height);
            smallRect = new Rect(0, 0, 160, 160 / FlightCamera.fetch.mainCamera.aspect);

            skyboxRenderers = (from Renderer r in (FindObjectsOfType(typeof(Renderer)) as IEnumerable<Renderer>) where (r.name == "XP" || r.name == "XN" || r.name == "YP" || r.name == "YN" || r.name == "ZP" || r.name == "ZN") select r).ToArray<Renderer>();
            scaledSpaceFaders = FindObjectsOfType(typeof(ScaledSpaceFader)) as ScaledSpaceFader[];

            print("CactEye: Creating camera modules...");
            camTrans = part.FindModelTransform(camTransformName);
            CreateCamera("Camera ScaledSpace");
            CreateCamera("Camera VE Underlay");
            CreateCamera("Camera VE Overlay");
            CreateCamera("Camera 01");
            CreateCamera("Camera 00");
            print("CactEye: Finished creating " + cameras.Count + " cameras");

            outputTex = new Texture2D(160, (int)(smallRect.height));
            outputTex.wrapMode = TextureWrapMode.Clamp;
            fullTex = new Texture2D(Screen.width, Screen.height);
            fullTex.wrapMode = TextureWrapMode.Clamp;
            rtLow = new RenderTexture(160, (int)(smallRect.height), 24, RenderTextureFormat.RGB565, RenderTextureReadWrite.sRGB);
            rtLow.Create();
            rtFull = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rtFull.Create();
        }

        private void CreateCamera(string name)
        {
            Camera origCam = null;
            List<Camera> camList = Camera.allCameras.Where(n => n.name == name).ToList();
            if (camList.Count == 0)
            {
                if (!name.Contains("VE"))
                    print("ERROR: Original camera for " + name + " not found!");
                else
                    print(name + " for Visual Enhancements mod not found");

                return;
            }
            else
            {
                origCam = camList.First();
                print("Successfully found " + name);
            }

            GameObject gO = new GameObject();
            gO.name = "CactEye " + name;
            Camera cam = gO.AddComponent<Camera>();
            cam.CopyFrom(origCam);
            cam.targetTexture = rtLow;
            if (!cam.name.Contains("0"))
            {
                cam.farClipPlane = 3e15f;
                if (cam.name.Contains("Scaled"))
                    cam.clearFlags = CameraClearFlags.Depth;
            }
            cam.enabled = false;
            cameras.Add(cam);

            print("Successfully copied " + name);
        }

        public override void OnUpdate()
        {
            if(opticsAnimate != null && !isDamaged)
            {
                if (opticsAnimate.animTime < 0.5 && isFunctional)
                    isFunctional = false;
                if (opticsAnimate.animTime > 0.5 && !isFunctional)
                    isFunctional = true;
            }
            else if (smallApertureOpen && !isDamaged && !isFunctional)
                isFunctional = true;

            if (isDamaged && isFunctional)
                isFunctional = false;

            if (guiOn)
                RefreshTex();
        }

        private void RefreshTex()
        {
            GetTex(false, "");
        }

        public string GetTex(bool isFull, string targetName = "Photo")
        {
            RenderTexture currentRT = RenderTexture.active;
            if (isFull)
                RenderTexture.active = rtFull;
            else
                RenderTexture.active = rtLow;

            foreach (Camera c in cameras)
            {
                if (isFull)
                {
                    c.pixelRect = fullRect;
                    c.targetTexture = rtFull;
                }
                else
                {
                    c.pixelRect = smallRect;
                    c.targetTexture = rtLow;
                }

                if (c.name.Contains("0"))
                    c.transform.position = camTrans.position;
                c.transform.forward = camTrans.forward;
                c.transform.rotation = camTrans.rotation;
                c.fieldOfView = fov;

                c.Render();

                if (!c.name.Contains("0"))
                {
                    foreach (Renderer r in skyboxRenderers)
                        r.enabled = false;
                    foreach (ScaledSpaceFader s in scaledSpaceFaders)
                        s.r.enabled = true;
                    c.Render();
                    foreach (Renderer r in skyboxRenderers)
                        r.enabled = true;
                }
            }

            if (isFull)
            {
                fullTex.ReadPixels(fullRect, 0, 0);
                fullTex.Apply();

                byte[] bytes = fullTex.EncodeToPNG();

                if (!System.IO.Directory.Exists(CactEyeVars.root + "Screenshots/CactEye"))
                    System.IO.Directory.CreateDirectory(CactEyeVars.root + "Screenshots/CactEye");
                string filename = "Screenshots/CactEye/" + HighLogic.SaveFolder + "_" + targetName + "_" + CactEyeVars.Time() + ".png";

                System.IO.File.WriteAllBytes(CactEyeVars.root + filename, bytes);

                print("CactEye: saved screenshot to " + filename);

                RenderTexture.active = currentRT;
                return filename;
            }
            else
            {
                outputTex.ReadPixels(smallRect, 0, 0);

                //grayscale!
                Color[] texColors = outputTex.GetPixels();
                for (int i = 0; i < texColors.Length; i++)
                {
                    float grayValue = texColors[i].grayscale;
                    texColors[i] = new Color(grayValue, grayValue, grayValue, texColors[i].a);
                }
                outputTex.SetPixels(texColors);
                outputTex.Apply();

                RenderTexture.active = currentRT;
                return "";
            }

        }

        [KSPEvent(guiActive = true, guiName = "Control from Here", active = true)]
        public void controlFromHere()
        {
            vessel.SetReferenceTransform(part);
        }

        [KSPEvent(guiActive = true, guiName = "Toggle GUI", active = true)]
        public void ToggleGUI()
        {
            guiOn = !guiOn; //this value is read by the CactEyeGUI module
        }

        [KSPEvent(active = false, externalToEVAOnly = true, guiActiveUnfocused = true, guiName = "Repair Optics", unfocusedRange = 5)]
        public void FixScope()
        {
            isDamaged = false;
            Events["FixScope"].active = false;
        }

        [KSPEvent(active = false, guiActive = true, guiActiveUnfocused = true, guiName = "Open Aperture (permanent!)", unfocusedRange = 2)]
        public void OpenSmallAperture()
        {
            smallApertureOpen = true;
            Events["OpenSmallAperture"].active = false;
        }

        public void BreakScope()
        {
            isDamaged = true;
            isFunctional = false;
            Events["FixScope"].active = true;
        }

        public Vector3 GetTargetPos(Vector3 worldPos, float width)
        {
            Camera c = cameras.Find(n => n.name.Contains("00"));
            Vector3 vec = c.WorldToScreenPoint(worldPos);

            if (Vector3.Dot(camTrans.forward, worldPos) > 0)
            {
                if (vec.x > 0 && vec.y > 0 && vec.x < c.pixelWidth && vec.y < c.pixelHeight)
                {
                    vec.y = c.pixelHeight - vec.y;
                    vec *= (width / c.pixelWidth);
                    return vec;
                }
            }
            
            return new Vector3(-1, -1, 0);
        }
    }
}
