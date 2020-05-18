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
                    FiddleWithGearsAndPistons(_gearsFront, _pistonsFront, "LOCKING FRONT");

                    break; // case "FIDDLING FRONT"

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

                    break; // case "UNLOCKING REAR"

                case "PUMPING REAR":
                    PumpPistons(_pistonsRear, "UNLOCKING REAR");

                    break;

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
                    FiddleWithGearsAndPistons(_gearsRear, _pistonsRear, "LOCKING REAR");
                    
                    break; // case "FIDDLING REAR"

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

                    break; // case "UNLOCKING FRONT"

                case "PUMPING FRONT":
                    PumpPistons(_pistonsFront, "UNLOCKING FRONT");

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

        void FiddleWithGearsAndPistons(List<IMyLandingGear> gears, List<IMyExtendedPistonBase> pistons, string returnToState)
        {
            if (AreAnyGearsLocked(gears))
            {
                _state = returnToState;
                return;
            }

            _ticksSlept++;

            if (_ticksSlept >= _ticksToSleep)
            {
                PistonsReverse(pistons);
                _ticksSlept = 0;
                _ticksToSleep++;
            }
        }

        void PumpPistons(List<IMyExtendedPistonBase> pistons, string returnToState)
        {
            PistonsReverse(pistons);
            _state = returnToState;
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

        // FIXME: This uses a few of the instance's variables for comparison!
        // If different sets of gears are checked interchangeably, this will break.
        bool AreAnyGearsMoving(List<IMyLandingGear> gears)
        {
            bool atLeastOneGearIsMoving = false;

            // re-populate current positions
            _currPositions.Clear();
            gears.ForEach(gear => _currPositions.Add(gear.GetPosition()));
            
            // compare to previous positions, marking if there's any notable difference
            var positionDistances = _currPositions.Zip(_prevPositions, (curr, prev) => Vector3D.DistanceSquared(curr, prev));
            foreach (var distance in positionDistances)
            {
                // MAGICNUM 0.1d: distance travelled threshold, chosen arbitrarily
                if (distance >= 0.1d)
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
