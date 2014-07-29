using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CactEye
{
    class CactEyeGyro : ModuleReactionWheel
    {
        [KSPField(isPersistant = false)]
        public float gyroScale = 0.1f;

        [KSPField(isPersistant = false)]
        public float gyroFineScale = 0.001f;

        [KSPField(isPersistant = false)]
        public float guiRate = 0.3f;

        [KSPField(isPersistant = false)]
        public int lifeSpan = 90; // in Earth days

        //[KSPField(isPersistant = true)]
        public double creationTime = -1f;

        [KSPField(isPersistant = true, guiActive = true, guiUnits = "Lifetime", guiActiveEditor = false, guiFormat = "P1")]
        [UI_ProgressBar(minValue = 0f, maxValue = 1f, controlEnabled = false)]
        public float lifetime = 1f;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Mode", guiActiveEditor = false)]
        public string torqueMode = "Normal";

        private bool dead = false;

        float pitchScale = 2.5f;
        float yawScale = 2.5f;
        float rollScale = 2.5f;

        [KSPEvent(guiActive = true, guiName = "Switch Mode", active = true)]
        public void cycleTorque()
        {
            if(torqueMode == "Normal")
            {
                redScale(null);
                return;
            }
            if(torqueMode == "Reduced")
            {
                fineScale(null);
                return;
            }
            if(torqueMode == "Fine")
            {
                normScale(null);
                return;
            }
        }

        [KSPAction("Normal Torque")]
        public void normScale(KSPActionParam param)
        {
            if (!dead)
            {
                torqueMode = "Normal";

                base.PitchTorque = pitchScale;
                base.YawTorque = yawScale;
                base.RollTorque = rollScale;

                ScreenMessages.PostScreenMessage("Torque Mode: " + torqueMode, 4, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        [KSPAction("Reduced Torque")]
        public void redScale(KSPActionParam param)
        {
            if (!dead)
            {
                torqueMode = "Reduced";

                base.PitchTorque = pitchScale * gyroScale;
                base.YawTorque = yawScale * gyroScale;
                base.RollTorque = rollScale * gyroScale;

                ScreenMessages.PostScreenMessage("Torque Mode: " + torqueMode, 4, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        [KSPAction("Fine Torque")]
        public void fineScale(KSPActionParam param)
        {
            if (!dead)
            {
                torqueMode = "Fine";

                base.PitchTorque = pitchScale * gyroFineScale;
                base.YawTorque = yawScale * gyroFineScale;
                base.RollTorque = rollScale * gyroFineScale;

                ScreenMessages.PostScreenMessage("Torque Mode: " + torqueMode, 4, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        [KSPAction("Switch Torque Mode")]
        public void switchMode(KSPActionParam param)
        {
            cycleTorque();
        }

        [KSPEvent(active = false, externalToEVAOnly = true, guiActiveUnfocused = true, guiName = "Repair Gyroscope", unfocusedRange = 2)]
        public void Restart()
        {
            dead = false;
            lifetime = 1f;
            creationTime = Planetarium.GetUniversalTime();
            wheelState = WheelState.Active;
            Events["cycleTorque"].active = true;
            Fields["torqueMode"].guiActive = true;
            normScale(null);
            Activate(null);
        }

        public void Die()
        {
            Events["cycleTorque"].active = false;
            Events["Restart"].active = true;
            //Events["OnToggle"].active = false; //this doesn't seem to stick
            Fields["torqueMode"].guiActive = false;
            base.PitchTorque = 0f;
            base.YawTorque = 0f;
            base.RollTorque = 0f;
            Deactivate(null);
            wheelState = WheelState.Broken;
            lifetime = 0;
            dead = true; //RIP <3
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            pitchScale = base.PitchTorque;
            yawScale = base.YawTorque;
            rollScale = base.RollTorque;

            creationTime = Planetarium.GetUniversalTime() + ((86400f * lifeSpan) * lifetime) - (86400f * lifeSpan);
            print("creationgTime: " + creationTime + " /// time: " + Planetarium.GetUniversalTime());
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (((86400f * lifeSpan) + creationTime) < Planetarium.GetUniversalTime() && !dead)
                Die();
            
            if (!dead)
            {
                lifetime = (float)((creationTime + (86400f * lifeSpan) - Planetarium.GetUniversalTime()) / (86400f * lifeSpan));
            }
        }

        public override string GetInfo()
        {
            var sb = new StringBuilder();

            sb.AppendLine("Lifespan: " + ((GameSettings.KERBIN_TIME) ? ((lifeSpan * 4) + " Kerbin Days") : (lifeSpan + " Earth Days")));

            sb.AppendLine();

            if (pitchScale == yawScale && pitchScale == rollScale)
            {
                sb.AppendLine("Normal Torque: " + string.Format("{0:0.0##}", pitchScale));
                sb.AppendLine("Reduced Torque: " + string.Format("{0:0.0##}", pitchScale * gyroScale));
                sb.AppendLine("Fine Torque: " + string.Format("{0:0.0###}", pitchScale * gyroFineScale));
            }
            else
            {
                sb.AppendLine("Pitch Torque: " + string.Format("{0:0.0##}", pitchScale));
                sb.AppendLine("Yaw Torque: " + string.Format("{0:0.0##}", yawScale));
                sb.AppendLine("Roll Torque: " + string.Format("{0:0.0##}", rollScale));
                sb.AppendLine("Reduced Scale: " + string.Format("{0:0.0##}", gyroScale));
                sb.AppendLine("Fine Scale: " + string.Format("{0:0.0###}", gyroFineScale));
            }

            if (guiRate != -1)
            {
                sb.AppendLine();
                sb.AppendLine("<color=#99ff00ff>Requires:</color>");
                sb.AppendLine("- ElectricCharge: " + ((guiRate < 1) ? (string.Format("{0:0.0##}", guiRate * 60) + "/min.") : (string.Format("{0:0.0##}", guiRate) + "/sec.")));
            }

            return sb.ToString();
        }
    }
}
