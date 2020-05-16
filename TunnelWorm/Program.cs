﻿using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        string state;

        List<IMyShipDrill> _drills = new List<IMyShipDrill>();
        List<IMyLandingGear> _gearsFront = new List<IMyLandingGear>();
        List<IMyLandingGear> _gearsRear = new List<IMyLandingGear>();
        List<IMyExtendedPistonBase> _pistonsAxial = new List<IMyExtendedPistonBase>();
        List<IMyExtendedPistonBase> _pistonsFront = new List<IMyExtendedPistonBase>();
        List<IMyExtendedPistonBase> _pistonsRear = new List<IMyExtendedPistonBase>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update100;

            string[] storedData = Storage.Split(';');
            if (storedData.Length >= 1)
            {
                state = storedData[0];
                //Me.CustomData = state;
            }

            // TODO: catch exception when missing group(s)
            GridTerminalSystem.GetBlockGroupWithName("Drills").GetBlocksOfType(_drills);
            GridTerminalSystem.GetBlockGroupWithName("Landing Gears Front").GetBlocksOfType(_gearsFront);
            GridTerminalSystem.GetBlockGroupWithName("Landing Gears Rear").GetBlocksOfType(_gearsRear);
            GridTerminalSystem.GetBlockGroupWithName("Pistons Axial").GetBlocksOfType(_pistonsAxial);
            GridTerminalSystem.GetBlockGroupWithName("Pistons Front").GetBlocksOfType(_pistonsFront);
            GridTerminalSystem.GetBlockGroupWithName("Pistons Rear").GetBlocksOfType(_pistonsRear);
        }

        public void Save()
        {
            Storage = string.Join(";", state ?? "ERROR");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo("State: " + state);
            Echo("LastRunTimeMs: " + Runtime.LastRunTimeMs);
            Echo("CurrentInstructionCount: " + Runtime.CurrentInstructionCount);
            Echo("MaxInstructionCount: " + Runtime.MaxInstructionCount);

            if (updateSource == UpdateType.Terminal)
            {
                // TODO: allow any case
                state = argument;
            }

            // The cases for this state machine are arranged as follows:
            // * check for end condition, and exit ASAP if not met;
            // * perform commands of _following_ step;
            // * set state variable.
            switch (state)
            {
                case "DRILLING":
                    if (!ArePistonsInHighestPosition(_pistonsAxial))
                            return;

                    DrillsDisable(_drills);
                    GearsAutolock(_gearsFront);
                    PistonsExtend(_pistonsFront);
                    state = "LOCKING FRONT";

                    break; // case "DRILLING"

                case "LOCKING FRONT":
                    if (!AreAnyGearsLocked(_gearsFront))
                        return;

                    GearsUnlock(_gearsRear);
                    PistonsRetract(_pistonsRear);
                    state = "UNLOCKING REAR";

                    break; // case "LOCKING FRONT"

                case "UNLOCKING REAR":
                    if (!ArePistonsInLowestPosition(_pistonsRear))
                        return;

                    PistonsRetract(_pistonsAxial);
                    state = "CONTRACTING";

                    break; // case "UNLOCKING REAR"

                case "CONTRACTING":
                    if (!ArePistonsInLowestPosition(_pistonsAxial))
                        return;

                    GearsAutolock(_gearsRear);
                    PistonsExtend(_pistonsRear);
                    state = "LOCKING REAR";

                    break; // case "CONTRACTING"

                case "LOCKING REAR":
                    if (!AreAnyGearsLocked(_gearsRear))
                        return;

                    GearsUnlock(_gearsFront);
                    PistonsRetract(_pistonsFront);
                    state = "UNLOCKING FRONT";

                    break; // case "LOCKING REAR"

                case "UNLOCKING FRONT":
                    if (!ArePistonsInLowestPosition(_pistonsFront))
                        return;

                    DrillsEnable(_drills);
                    PistonsExtend(_pistonsAxial);
                    state = "DRILLING";

                    break; // case "UNLOCKING FRONT"

                case "RESET":
                    DrillsDisable(_drills);
                    GearsUnlock(_gearsFront);
                    GearsUnlock(_gearsRear);
                    PistonsRetract(_pistonsAxial);
                    PistonsRetract(_pistonsFront);
                    PistonsRetract(_pistonsRear);
                    state = "HALT";

                    break;

                case "START":
                    GearsAutolock(_gearsRear);
                    PistonsExtend(_pistonsRear);
                    state = "LOCKING REAR";

                    break;

                case "ERROR":
                    // TODO: error reporting
                    break;

                default:
                    break;
            }
        }

        #region drills
        void DrillsEnable(List<IMyShipDrill> drills)
        {
            drills.ForEach(drill => drill.Enabled = true);
        }

        void DrillsDisable(List<IMyShipDrill> drills)
        {
            drills.ForEach(drill => drill.Enabled = false);
        }
        #endregion

        #region pistons
        bool ArePistonsInLowestPosition(List<IMyExtendedPistonBase> pistons)
        {
            foreach (var piston in pistons)
                if (piston.CurrentPosition != piston.LowestPosition)
                    return false;
            return true;
        }

        bool ArePistonsInHighestPosition(List<IMyExtendedPistonBase> pistons)
        {
            foreach (var piston in pistons)
                if (piston.CurrentPosition != piston.HighestPosition)
                    return false;
            return true;
        }

        void PistonsExtend(List<IMyExtendedPistonBase> pistons)
        {
            pistons.ForEach(piston => piston.Extend());
        }

        void PistonsRetract(List<IMyExtendedPistonBase> pistons)
        {
            pistons.ForEach(piston => piston.Retract());
        }
        #endregion

        #region gears
        bool AreAnyGearsLocked(List<IMyLandingGear> gears)
        {
            foreach (var gear in gears)
                if (gear.IsLocked)
                    return true;
                          
            return false;
        }
        void GearsAutolock(List<IMyLandingGear> gears)
        {
            gears.ForEach(gear => gear.AutoLock = true);
        }
        void GearsUnlock(List<IMyLandingGear> gears)
        {
            gears.ForEach(gear => gear.AutoLock = false);
            gears.ForEach(gear => gear.Unlock());
        }
        #endregion
    }
}
