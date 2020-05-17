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
        // state machine
        string _state;

        // permanent part lists used in sequencing
        List<IMyShipDrill> _drills = new List<IMyShipDrill>();
        List<IMyLandingGear> _gearsFront = new List<IMyLandingGear>();
        List<IMyLandingGear> _gearsRear = new List<IMyLandingGear>();
        List<IMyExtendedPistonBase> _pistonsAxial = new List<IMyExtendedPistonBase>();
        List<IMyExtendedPistonBase> _pistonsFront = new List<IMyExtendedPistonBase>();
        List<IMyExtendedPistonBase> _pistonsRear = new List<IMyExtendedPistonBase>();

        // churn vars for exiting edge cases
        List<Vector3D> _currPositions = new List<Vector3D>();
        List<Vector3D> _prevPositions = new List<Vector3D>();
        int _ticksToSleep = 0;
        int _ticksSlept = 0;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update100;

            string[] storedData = Storage.Split(';');
            if (storedData.Length >= 1)
            {
                _state = storedData[0];
            }

            List<string> blockGroupNames = GetMissingBlockGroups(
                "Drills",
                "Landing Gears Front",
                "Landing Gears Rear",
                "Pistons Axial",
                "Pistons Front",
                "Pistons Rear"
                );
            if (blockGroupNames.Count != 0)
            {
                Echo("Missing block groups! Not present:");
                blockGroupNames.ForEach(name => Echo($"{name}"));
            }

            GridTerminalSystem.GetBlockGroupWithName("Drills").GetBlocksOfType(_drills);
            GridTerminalSystem.GetBlockGroupWithName("Landing Gears Front").GetBlocksOfType(_gearsFront);
            GridTerminalSystem.GetBlockGroupWithName("Landing Gears Rear").GetBlocksOfType(_gearsRear);
            GridTerminalSystem.GetBlockGroupWithName("Pistons Axial").GetBlocksOfType(_pistonsAxial);
            GridTerminalSystem.GetBlockGroupWithName("Pistons Front").GetBlocksOfType(_pistonsFront);
            GridTerminalSystem.GetBlockGroupWithName("Pistons Rear").GetBlocksOfType(_pistonsRear);
        }

        public void Save()
        {
            Storage = string.Join(";", _state ?? "ERROR");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo("State: " + _state);
            Echo("LastRunTimeMs: " + Runtime.LastRunTimeMs);
            Echo("CurrentInstructionCount: " + Runtime.CurrentInstructionCount);
            Echo("MaxInstructionCount: " + Runtime.MaxInstructionCount);

            if (updateSource == UpdateType.Terminal)
            {
                _state = argument.ToUpper();
            }

            // The cases for this state machine are arranged as follows:
            // * check for exit condition, and return ASAP if not met;
            // * perform commands of _following_ step;
            // * set state variable.
            switch (_state)
            {
                case "DRILLING":
                    if (!ArePistonsInHighestPosition(_pistonsAxial))
                            return;

                    DrillsDisable(_drills);
                    GearsAutolock(_gearsFront);
                    PistonsExtend(_pistonsFront);
                    _state = "LOCKING FRONT";

                    break; // case "DRILLING"

                case "LOCKING FRONT":
                    if (!AreAnyGearsLocked(_gearsFront))
                    {
                        if (!AreAnyGearsMoving(_gearsFront))
                        {
                            PistonsReverse(_pistonsAxial);
                            PistonsReverse(_pistonsFront);
                            _ticksSlept = 0;
                            _ticksToSleep = 1;
                            _state = "FIDDLING FRONT";
                        }

                        return;
                    }

                    GearsUnlock(_gearsRear);
                    PistonsRetract(_pistonsRear);
                    _state = "UNLOCKING REAR";

                    break; // case "LOCKING FRONT"

                case "FIDDLING FRONT":
                    if (AreAnyGearsLocked(_gearsFront))
                    {
                        _state = "LOCKING FRONT";
                        return;
                    }

                    _ticksSlept++;

                    if (AreAnyGearsMoving(_gearsFront))
                        break;

                    if (_ticksSlept >= _ticksToSleep)
                    {
                        PistonsReverse(_pistonsFront);
                        _ticksSlept = 0;
                        _ticksToSleep++;
                    }

                    break;

                case "UNLOCKING REAR":
                    if (!ArePistonsInLowestPosition(_pistonsRear))
                        return;

                    PistonsRetract(_pistonsAxial);
                    _state = "CONTRACTING";

                    break; // case "UNLOCKING REAR"

                case "CONTRACTING":
                    if (!ArePistonsInLowestPosition(_pistonsAxial))
                        return;

                    GearsAutolock(_gearsRear);
                    PistonsExtend(_pistonsRear);
                    _state = "LOCKING REAR";

                    break; // case "CONTRACTING"

                case "LOCKING REAR":
                    if (!AreAnyGearsLocked(_gearsRear))
                    {
                        if (!AreAnyGearsMoving(_gearsRear))
                        {
                            PistonsReverse(_pistonsAxial);
                            PistonsReverse(_pistonsRear);
                            _ticksSlept = 0;
                            _ticksToSleep = 1;
                            _state = "FIDDLING REAR";
                        }

                        return;
                    }

                    GearsUnlock(_gearsFront);
                    PistonsRetract(_pistonsFront);
                    _state = "UNLOCKING FRONT";

                    break; // case "LOCKING REAR"

                case "FIDDLING REAR":
                    if (AreAnyGearsLocked(_gearsRear))
                    {
                        _state = "LOCKING REAR";
                        return;
                    }

                    _ticksSlept++;

                    if (AreAnyGearsMoving(_gearsRear))
                        break;

                    if (_ticksSlept >= _ticksToSleep)
                    {
                        PistonsReverse(_pistonsRear);
                        _ticksSlept = 0;
                        _ticksToSleep++;
                    }

                    break;

                case "UNLOCKING FRONT":
                    if (!ArePistonsInLowestPosition(_pistonsFront))
                        return;

                    DrillsEnable(_drills);
                    PistonsExtend(_pistonsAxial);
                    _state = "DRILLING";

                    break; // case "UNLOCKING FRONT"

                case "RESET":
                    DrillsDisable(_drills);
                    GearsUnlock(_gearsFront);
                    GearsUnlock(_gearsRear);
                    PistonsRetract(_pistonsAxial);
                    PistonsRetract(_pistonsFront);
                    PistonsRetract(_pistonsRear);
                    _state = "HALT";

                    break;

                case "START":
                    GearsAutolock(_gearsRear);
                    PistonsExtend(_pistonsRear);
                    _state = "LOCKING REAR";

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

        void PistonsReverse(List<IMyExtendedPistonBase> pistons)
        {
            pistons.ForEach(piston => piston.Reverse());
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

        bool AreAnyGearsMoving(List<IMyLandingGear> gears)
        {
            bool atLeastOneGearIsMoving = false;

            _currPositions.Clear();
            gears.ForEach(gear => _currPositions.Add(gear.GetPosition()));

            var gearsNotMoving = _currPositions.Zip(_prevPositions, (curr, prev) => curr.Equals(prev));
            foreach (var gearIsNotMoving in gearsNotMoving)
            {
                if (!gearIsNotMoving)
                {
                    atLeastOneGearIsMoving = true;
                    break;
                }
            }

            _prevPositions.Clear();
            _prevPositions = _currPositions;
            //_currPositions.ForEach(position => _prevPositions.Add(position));

            return atLeastOneGearIsMoving;
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

        #region utils
        List<string> GetMissingBlockGroups(params string[] groupNames)
        {
            List<string> missingGroupNames = new List<string>();
            for (int i = 0; i < groupNames.Length; i++)
            {
                string groupName = groupNames[i];
                IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(groupName);
                if (group == null)
                {
                    missingGroupNames.Add(groupName);
                }
            }
            return missingGroupNames;
        }
        #endregion
    }
}
