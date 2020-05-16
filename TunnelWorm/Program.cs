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
        string _state;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update100;

            string[] storedData = Storage.Split('\n');
            if (storedData.Length >= 1)
            {
                _state = storedData[0];
                Me.CustomData = _state;
            }
        }

        public void Save()
        {
            Storage = string.Join("\n",
                _state ?? "ERROR"
            );
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo("State: " + _state);

            IMyBlockGroup pistonsAxialGroup = GridTerminalSystem.GetBlockGroupWithName("Pistons Axial");
            List<IMyExtendedPistonBase> pistonsAxial = new List<IMyExtendedPistonBase>();
            pistonsAxialGroup.GetBlocksOfType(pistonsAxial);

            IMyBlockGroup drillsGroup = GridTerminalSystem.GetBlockGroupWithName("Drills");
            List<IMyShipDrill> drills = new List<IMyShipDrill>();
            drillsGroup.GetBlocksOfType(drills);

            switch (_state)
            {
                case "DRILLING":
                    foreach (var piston in pistonsAxial)
                    {
                        if (piston.CurrentPosition != piston.HighestPosition)
                            return;
                    }

                    drills.ForEach(drill => drill.Enabled = false);
                    pistonsAxial.ForEach(piston => piston.Retract());
                    //foreach (var drill in drills)
                    //{
                    //    drill.Enabled = false;
                    //}
                    //foreach (var piston in pistonsAxial)
                    //{
                    //    piston.Retract();
                    //}
                    _state = "CONTRACTING";
                    
                    break; // case "DRILLING"

                case "CONTRACTING":
                    foreach (var piston in pistonsAxial)
                    {
                        if (piston.CurrentPosition != piston.LowestPosition)
                            return;
                    }

                    drills.ForEach(drill => drill.Enabled = true);
                    pistonsAxial.ForEach(piston => piston.Extend());
                    //foreach (var drill in drills)
                    //{
                    //    drill.Enabled = true;
                    //}
                    //foreach (var piston in pistonsAxial)
                    //{
                    //    piston.Extend();
                    //}
                    _state = "DRILLING";
                    
                    break; // case "CONTRACTING"

                case "ERROR":
                    break;

                default:
                    _state = Me.CustomData;
                    break;
            }
        }
    }
}
