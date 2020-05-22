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
        // state machine
        string _state;

        // permanent part lists used in sequencing
        List<IMyShipDrill> _drills = new List<IMyShipDrill>();
        List<IMyLandingGear> _gearsFront = new List<IMyLandingGear>();
        List<IMyLandingGear> _gearsRear = new List<IMyLandingGear>();
        List<IMyExtendedPistonBase> _pistonsAxial = new List<IMyExtendedPistonBase>();
        List<IMyExtendedPistonBase> _pistonsFront = new List<IMyExtendedPistonBase>();
        List<IMyExtendedPistonBase> _pistonsRear = new List<IMyExtendedPistonBase>();

        // churn vars used to exit edge cases
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
                blockGroupNames.ForEach(name => Echo($"* {name}"));
                Echo("Recompile when addressed. Exiting.");
                return;
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
            Echo($"State: {_state}");
            Echo($"LastRunTimeMs: {Runtime.LastRunTimeMs}");
            Echo($"InstructionCount: {Runtime.CurrentInstructionCount} / {Runtime.MaxInstructionCount}");
            Echo($"Tick internals: {_ticksSlept} / {_ticksToSleep}");

            // DEBUG
            //Echo("Previous piston positions:");
            //_prevPositions.ForEach(position => Echo($"{position}"));

            if (updateSource == UpdateType.Terminal)
            {
                _state = argument.ToUpper();
            }

            // In general, cases in this state machine are arranged as follows:
            // * check for exit condition, and return ASAP if not met;
            // * perform commands of _following_ step;
            // * set state variable.
            // However, FIDDLING states - used to exit in-game edge cases - are different:
            // * check for edge-case exit condition, and set "back" state if so;
            // * otherwise, perform actions with an ever-increasing delay.
            switch (_state)
            {
                case "DRILLING":
                    if (!ArePistonsInHighestPosition(_pistonsAxial))
                    {
                        if (!AreAnyGearsMoving(_gearsFront))
                        {
                            DrillsDisable(_drills);
                            PistonsReverse(_pistonsAxial);
                            _state = "PUMPING AXIAL D";
                        }

                        return; 
                    }

                    DrillsDisable(_drills);
                    GearsAutolock(_gearsFront);
                    PistonsExtend(_pistonsFront);
                    _state = "LOCKING FRONT";

                    break;

                case "PUMPING AXIAL D":
                    PumpGearsAndPistons(_gearsFront, _pistonsAxial, "DRILLING");
                    DrillsEnable(_drills);

                    break;

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

                    break;

                case "FIDDLING FRONT":
                    FiddleWithGearsAndPistons(_gearsFront, _pistonsFront, "LOCKING FRONT");

                    break;

                case "UNLOCKING REAR":
                    if (!ArePistonsInLowestPosition(_pistonsRear))
                    {
                        if (!AreAnyGearsMoving(_gearsRear))
                        {
                            PistonsReverse(_pistonsRear);
                            _state = "PUMPING REAR";
                        }
                        
                        return;
                    }

                    PistonsRetract(_pistonsAxial);
                    _state = "CONTRACTING";

                    break;

                case "PUMPING REAR":
                    PumpGearsAndPistons(_gearsRear, _pistonsRear, "UNLOCKING REAR");

                    break;

                case "CONTRACTING":
                    if (!ArePistonsInLowestPosition(_pistonsAxial))
                    {
                        if (!AreAnyGearsMoving(_gearsRear))
                        {
                            PistonsReverse(_pistonsAxial);
                            _state = "PUMPING AXIAL C";
                        }

                        return;
                    }

                    GearsAutolock(_gearsRear);
                    PistonsExtend(_pistonsRear);
                    _state = "LOCKING REAR";

                    break;

                case "PUMPING AXIAL C":
                    PumpGearsAndPistons(_gearsRear, _pistonsAxial, "CONTRACTING");

                    break;

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

                    break;

                case "FIDDLING REAR":
                    FiddleWithGearsAndPistons(_gearsRear, _pistonsRear, "LOCKING REAR");
                    
                    break;

                case "UNLOCKING FRONT":
                    if (!ArePistonsInLowestPosition(_pistonsFront))
                    {
                        if (!AreAnyGearsMoving(_gearsFront))
                        {
                            PistonsReverse(_pistonsFront);
                            _state = "PUMPING FRONT";
                        }

                        return;
                    }

                    DrillsEnable(_drills);
                    PistonsExtend(_pistonsAxial);
                    _state = "DRILLING";

                    break;

                case "PUMPING FRONT":
                    PumpGearsAndPistons(_gearsFront, _pistonsFront, "UNLOCKING FRONT");

                    break;

                case "RESET":
                    DrillsDisable(_drills);
                    GearsUnlock(_gearsFront);
                    GearsUnlock(_gearsRear);
                    PistonsRetract(_pistonsAxial);
                    PistonsRetract(_pistonsFront);
                    PistonsRetract(_pistonsRear);
                    _state = "HALT";

                    break;

                case "RUN":
                case "START":
                    GearsAutolock(_gearsRear);
                    PistonsExtend(_pistonsRear);
                    _state = "LOCKING REAR";

                    break;

                case "HALT":
                case "INVALID":
                case "STOP":
                    break;

                case "ERROR":
                    // TODO: error reporting via beacon
                    break;

                default:
                    _state = "INVALID";
                    break;
            }
        }

        private void FiddleWithGearsAndPistons(List<IMyLandingGear> gears, List<IMyExtendedPistonBase> lateralPistons, string returnToState)
        {
            if (AreAnyGearsLocked(gears))
            {
                _state = returnToState;
                return;
            }

            _ticksSlept++;

            if (_ticksSlept >= _ticksToSleep)
            {
                PistonsReverse(lateralPistons);
                _ticksSlept = 0;
                _ticksToSleep++;
            }

            if (ArePistonsInLowestPosition(_pistonsAxial) || ArePistonsInHighestPosition(_pistonsAxial))
            {
                PistonsReverse(_pistonsAxial);
            }
        }

        private void PumpGearsAndPistons(List<IMyLandingGear> gears, List<IMyExtendedPistonBase> pistons, string returnToState)
        {
            // this will update gear positions, but we don't need the result, so discard it
            AreAnyGearsMoving(gears);

            PistonsReverse(pistons);
            _state = returnToState;
        }

        #region drills
        private void DrillsEnable(List<IMyShipDrill> drills)
        {
            drills.ForEach(drill => drill.Enabled = true);
        }

        private void DrillsDisable(List<IMyShipDrill> drills)
        {
            drills.ForEach(drill => drill.Enabled = false);
        }
        #endregion

        #region pistons
        private bool ArePistonsInLowestPosition(List<IMyExtendedPistonBase> pistons)
        {
            foreach (var piston in pistons)
                if (piston.CurrentPosition != piston.LowestPosition)
                    return false;
            return true;
        }

        private bool ArePistonsInHighestPosition(List<IMyExtendedPistonBase> pistons)
        {
            foreach (var piston in pistons)
                if (piston.CurrentPosition != piston.HighestPosition)
                    return false;
            return true;
        }

        private void PistonsExtend(List<IMyExtendedPistonBase> pistons)
        {
            pistons.ForEach(piston => piston.Extend());
        }

        private void PistonsRetract(List<IMyExtendedPistonBase> pistons)
        {
            pistons.ForEach(piston => piston.Retract());
        }

        private void PistonsReverse(List<IMyExtendedPistonBase> pistons)
        {
            pistons.ForEach(piston => piston.Reverse());
        }
        #endregion

        #region gears
        private bool AreAnyGearsLocked(List<IMyLandingGear> gears)
        {
            foreach (var gear in gears)
                if (gear.IsLocked)
                    return true;
                          
            return false;
        }

        // FIXME: This uses a few of the instance's variables for comparison!
        // If different sets of gears are checked interchangeably, this will break.
        private bool AreAnyGearsMoving(List<IMyLandingGear> gears)
        {
            bool atLeastOneGearIsMoving = false;

            // re-populate current positions
            _currPositions.Clear();
            gears.ForEach(gear => _currPositions.Add(gear.GetPosition()));
            
            // compare to previous positions, marking if there's any notable difference
            var positionDistances = _currPositions.Zip(_prevPositions, (curr, prev) => Vector3D.DistanceSquared(curr, prev));
            foreach (var distance in positionDistances)
            {
                // MAGICNUM 0.01d: distance travelled threshold, should be lower than
                // slowest piston group travel speed, but higher than GetPosition() noise
                if (distance >= 0.01d)
                {
                    atLeastOneGearIsMoving = true;
                    break;
                }
            }

            // save positions for next iteration
            _prevPositions.Clear();
            _currPositions.ForEach(position => _prevPositions.Add(position));

            return atLeastOneGearIsMoving;
        }

        private void GearsAutolock(List<IMyLandingGear> gears)
        {
            gears.ForEach(gear => gear.AutoLock = true);
        }

        private void GearsUnlock(List<IMyLandingGear> gears)
        {
            gears.ForEach(gear => gear.AutoLock = false);
            gears.ForEach(gear => gear.Unlock());
        }
        #endregion

        #region utils
        private List<string> GetMissingBlockGroups(params string[] groupNames)
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
