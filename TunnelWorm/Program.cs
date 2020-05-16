using Sandbox.Game.EntityComponents;
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

        List<IMyShipDrill> drills = new List<IMyShipDrill>();
        List<IMyLandingGear> gearsFront = new List<IMyLandingGear>();
        List<IMyLandingGear> gearsRear = new List<IMyLandingGear>();
        List<IMyExtendedPistonBase> pistonsAxial = new List<IMyExtendedPistonBase>();
        List<IMyExtendedPistonBase> pistonsFront = new List<IMyExtendedPistonBase>();
        List<IMyExtendedPistonBase> pistonsRear = new List<IMyExtendedPistonBase>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update100;

            string[] storedData = Storage.Split(';');
            if (storedData.Length >= 1)
            {
                state = storedData[0];
                //Me.CustomData = state;
            }

            GridTerminalSystem.GetBlockGroupWithName("Drills").GetBlocksOfType(drills);
            GridTerminalSystem.GetBlockGroupWithName("Landing Gears Front").GetBlocksOfType(gearsFront);
            GridTerminalSystem.GetBlockGroupWithName("Landing Gears Rear").GetBlocksOfType(gearsRear);
            GridTerminalSystem.GetBlockGroupWithName("Pistons Axial").GetBlocksOfType(pistonsAxial);
            GridTerminalSystem.GetBlockGroupWithName("Pistons Front").GetBlocksOfType(pistonsFront);
            GridTerminalSystem.GetBlockGroupWithName("Pistons Rear").GetBlocksOfType(pistonsRear);
        }

        public void Save()
        {
            Storage = string.Join(";", state ?? "ERROR");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo("State: " + state);

            // The cases for this state machine are arranged as follows:
            // * check for end condition, and exit ASAP if not met;
            // * perform commands of _following_ step;
            // * set state variable.
            switch (state)
            {
                case "DRILLING":
                    if (!PistonsInHighestPosition(pistonsAxial))
                            return;

                    drills.ForEach(drill => drill.Enabled = false);
                    // TODO: turn on front gear auto-lock
                    pistonsFront.ForEach(piston => piston.Extend());
                    state = "LOCKING FRONT";

                    break; // case "DRILLING"

                case "LOCKING FRONT":
                    // FIXME: wrong condition, need "front gear locked"
                    if (!PistonsInHighestPosition(pistonsFront))
                        return;

                    // TODO: unlock rear gears
                    pistonsRear.ForEach(piston => piston.Retract());
                    state = "UNLOCKING REAR";

                    break; // case "LOCKING FRONT"

                case "UNLOCKING REAR":
                    if (!PistonsInLowestPosition(pistonsRear))
                        return;

                    pistonsAxial.ForEach(piston => piston.Retract());
                    state = "CONTRACTING";

                    break; // case "UNLOCKING REAR"

                case "CONTRACTING":
                    if (!PistonsInLowestPosition(pistonsAxial))
                        return;

                    // TODO: turn on rear gear auto-lock
                    pistonsRear.ForEach(piston => piston.Extend());
                    state = "LOCKING REAR";

                    break; // case "CONTRACTING"

                case "LOCKING REAR":
                    // FIXME: wrong condition, need "gear locked"
                    if (!PistonsInHighestPosition(pistonsRear))
                        return;

                    // TODO: unlock front gears
                    pistonsFront.ForEach(piston => piston.Retract());
                    state = "UNLOCKING FRONT";

                    break; // case "LOCKING REAR"

                case "UNLOCKING FRONT":
                    if (!PistonsInLowestPosition(pistonsFront))
                        return;
                    
                    drills.ForEach(drill => drill.Enabled = true);
                    pistonsAxial.ForEach(piston => piston.Extend());
                    state = "DRILLING";

                    break; // case "UNLOCKING FRONT"

                case "ERROR":
                    break;

                default:
                    state = Me.CustomData;
                    break;
            }
        }

        bool PistonsInLowestPosition(List<IMyExtendedPistonBase> pistons)
        {
            foreach (var piston in pistons)
                if (piston.CurrentPosition != piston.LowestPosition)
                    return false;
            return true;
        }

        bool PistonsInHighestPosition(List<IMyExtendedPistonBase> pistons)
        {
            foreach (var piston in pistons)
                if (piston.CurrentPosition != piston.HighestPosition)
                    return false;
            return true;
        }
    }
}
