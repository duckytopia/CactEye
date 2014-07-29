using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using UnityEngine;

namespace CactEyeWrapper
{
    public class HullcamWrapper
    {
        public static bool CheckInstall()
        {
            foreach (AssemblyLoader.LoadedAssembly assembly in AssemblyLoader.loadedAssemblies)
            {
                if (assembly.name == "HullCamera")
                    return true;
            }
            return false;
        }

        public static Camera GetCamera(Part host)
        {
            Camera[] camList = host.GetComponentsInChildren<Camera>();
            if(camList.Count() != 0)
            {
                foreach (Camera c in camList)
                {
                    Debug.Log(c.name);
                }

                return camList[0];
            }
            
            return null;
        }

        public static PartModule GetCamModule(Part host)
        {
            foreach (PartModule m in host.Modules)
            {
                if (m.moduleName == "MuMechModuleHullCamera")
                    return m;
            }

            return null;
        }

        public static PartModule GetZoomModule(Part host)
        {
            foreach (PartModule m in host.Modules)
            {
                if (m.moduleName == "MuMechModuleHullCameraZoom")
                    return m;
            }

            return null;
        }
    }
}
