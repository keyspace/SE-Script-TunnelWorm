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
using System.Security.Cryptography.X509Certificates;
using Sandbox.Game.GUI;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // state machine
        FiniteStateMachine _fsm = new FiniteStateMachine();

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

            SetupFSM();

            string[] storedData = Storage.Split(';');
            if (storedData.Length >= 1)
            {
                _fsm.SetCurrentStateName(storedData[0]);
                if (_fsm.GetCurrentStateName() != storedData[0])
                {
                    _fsm.SetCurrentStateName("HALT");
                }
            }
            else
            {
                _fsm.SetCurrentStateName("HALT");
            }
        }

        public void Save()
        {
            Storage = string.Join(";", _fsm.GetCurrentStateName() ?? "ERROR");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo($"State: {_fsm.GetCurrentStateName()}");
            Echo($"LastRunTimeMs: {Runtime.LastRunTimeMs}");
            Echo($"InstructionCount: {Runtime.CurrentInstructionCount} / {Runtime.MaxInstructionCount}");
            Echo($"Tick internals: {_ticksSlept} / {_ticksToSleep}");

            // DEBUG
            //Echo("Previous piston positions:");
            //_prevPositions.ForEach(position => Echo($"{position}"));

            if (updateSource == UpdateType.Terminal)
            {
                _fsm.SetCurrentStateName(GuessUserCommand(argument));
            }

            _fsm.DoFirstPossibleStateTransition();
        }

        private string GuessUserCommand(string userInput)
        {
            string userCommand = userInput.ToUpper();
            // aliases
            switch (userCommand)
            {
                case "BEGIN":
                case "LOCK REAR":
                case "RUN":
                case "START":
                    userCommand = "LOCKING REAR";
                    break;
                case "LOCK FRONT":
                    userCommand = "LOCKING FRONT";
                    break;
                case "END":
                case "STOP":
                    userCommand = "HALT";
                    break;
                default:
                    userCommand = "INVALID";
                    break;
            }
            return userCommand;
        }

        private void SetupFSM()
        {
            // main sequence states
            _fsm.AddState(
                "DRILLING",
                () => { DrillsEnable(_drills); PistonsExtend(_pistonsAxial); },
                () => { DrillsDisable(_drills); }
                );
            _fsm.AddState(
                "LOCKING FRONT",
                () => { GearsAutolock(_gearsFront); PistonsExtend(_pistonsFront); },
                NoOp
                );
            _fsm.AddState(
                "UNLOCKING REAR",
                () => { GearsUnlock(_gearsRear); PistonsRetract(_pistonsRear); },
                NoOp
                );
            _fsm.AddState(
                "CONTRACTING",
                () => { PistonsRetract(_pistonsAxial); },
                NoOp
                );
            _fsm.AddState(
                "LOCKING REAR",
                () => { GearsAutolock(_gearsRear); PistonsExtend(_pistonsRear); },
                NoOp
                );
            _fsm.AddState(
                "UNLOCKING FRONT",
                () => { GearsUnlock(_gearsFront); PistonsRetract(_pistonsFront); },
                NoOp
                );

            // main sequence transitions
            _fsm.AddStateTransition("DRILLING", "LOCKING FRONT", () => ArePistonsInHighestPosition(_pistonsAxial));
            _fsm.AddStateTransition("LOCKING FRONT", "UNLOCKING REAR", () => AreAnyGearsLocked(_gearsFront));
            _fsm.AddStateTransition("UNLOCKING REAR", "CONTRACTING", () => ArePistonsInLowestPosition(_pistonsRear));
            _fsm.AddStateTransition("CONTRACTING", "LOCKING REAR", () => ArePistonsInLowestPosition(_pistonsAxial));
            _fsm.AddStateTransition("LOCKING REAR", "UNLOCKING FRONT", () => AreAnyGearsLocked(_gearsRear));
            _fsm.AddStateTransition("UNLOCKING FRONT", "DRILLING", () => ArePistonsInLowestPosition(_pistonsFront));

            // states to exit in-game edge-cases, conditions to detect these states, and transitions in/out
            //
            // FIDDLING has to be done when LOCKING gears, but no gears happen to lock: most often because
            // the pistons can extend no further (the walls are too far away), or because the wall is too
            // curved. In either case, the fact that the gears won't be moving can be used to detect the
            // condition.
            _fsm.AddStateTransition("LOCKING FRONT", "FIDDLING FRONT", () => !AreAnyGearsMoving(_gearsFront));
            _fsm.AddState(
                "FIDDLING FRONT",
                () => { ResetTicks(); PistonsReverse(_pistonsAxial); PistonsReverse(_pistonsFront); },
                () => { ResetTicks(); }
                );
            _fsm.AddStateTransition("FIDDLING FRONT", "LOCKING FRONT", () => FiddleTick(_gearsFront, _pistonsFront));

            _fsm.AddStateTransition("LOCKING REAR", "FIDDLING REAR", () => !AreAnyGearsMoving(_gearsRear));
            _fsm.AddState(
                "FIDDLING REAR",
                () => { ResetTicks(); PistonsReverse(_pistonsAxial); PistonsReverse(_pistonsRear); },
                () => { ResetTicks(); }
                );
            _fsm.AddStateTransition("FIDDLING REAR", "LOCKING REAR", () => FiddleTick(_gearsRear, _pistonsRear));

            // PUMPING has to be done when pistons get "sticky", or gears clip walls and therefore get "stuck".
            // This can happen during any piston extension/contraction in the main sequence, but the case is
            // also inadvertently covered by the FIDDLING states, so there is no need to also describe it here.
            // The operation itself is as simple as reversing the pistons back and forth.
            _fsm.AddStateTransition("UNLOCKING FRONT", "PUMPING FRONT", () => !AreAnyGearsMoving(_gearsFront));
            _fsm.AddState(
                "PUMPING FRONT",
                () => PistonsReverse(_pistonsFront),
                () => PistonsReverse(_pistonsFront)
                );
            _fsm.AddStateTransition("PUMPING FRONT", "UNLOCKING FRONT", () => true);

            _fsm.AddStateTransition("UNLOCKING REAR", "PUMPING REAR", () => !AreAnyGearsMoving(_gearsRear));
            _fsm.AddState(
                "PUMPING REAR",
                () => PistonsReverse(_pistonsRear),
                () => PistonsReverse(_pistonsRear)
                );
            _fsm.AddStateTransition("PUMPING REAR", "UNLOCKING REAR", () => true);

            // TODO: determine which state to return to based on velocity (+/-), have 1 state, 2 exit conditions!
            _fsm.AddStateTransition("CONTRACTING", "PUMPING AXIAL C", () => !AreAnyGearsMoving(_gearsRear));
            _fsm.AddState(
                "PUMPING AXIAL C",
                () => PistonsReverse(_pistonsAxial),
                () => PistonsReverse(_pistonsAxial)
                );
            _fsm.AddStateTransition("PUMPING AXIAL C", "CONTRACTING", () => true);

            _fsm.AddStateTransition("DRILLING", "PUMPING AXIAL D", () => !AreAnyGearsMoving(_gearsFront));
            _fsm.AddState(
                "PUMPING AXIAL D",
                () => PistonsReverse(_pistonsAxial),
                () => PistonsReverse(_pistonsAxial)
                );
            _fsm.AddStateTransition("PUMPING AXIAL D", "DRILLING", () => true);

            // meta/helper states
            _fsm.AddState(
                "HALT",
                () => DrillsDisable(_drills),
                NoOp);

            _fsm.AddState(
                "RESET", 
                () =>
                {
                    DrillsDisable(_drills);
                    GearsUnlock(_gearsFront);
                    GearsUnlock(_gearsRear);
                    PistonsRetract(_pistonsAxial);
                    PistonsRetract(_pistonsFront);
                    PistonsRetract(_pistonsRear);
                },
                NoOp);

            _fsm.AddState("INVALID", NoOp, NoOp);
        }

        private void ResetTicks()
        {
            _ticksSlept = 0;
            _ticksToSleep = 1;
        }

        private bool FiddleTick(List<IMyLandingGear> gears, List<IMyExtendedPistonBase> lateralPistons)
        {
            if (AreAnyGearsLocked(gears))
            {
                return true;
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

            return false;
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

        private void NoOp()
        {
            return;
        }
        #endregion
    }
}
