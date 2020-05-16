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
        List<IMyExtendedPistonBase> pistonsAxial = new List<IMyExtendedPistonBase>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update100;

            string[] storedData = Storage.Split(';');
            if (storedData.Length >= 1)
            {
                state = storedData[0];
                Me.CustomData = state;
            }

            IMyBlockGroup drillsGroup = GridTerminalSystem.GetBlockGroupWithName("Drills");
            drillsGroup.GetBlocksOfType(drills);

            IMyBlockGroup pistonsAxialGroup = GridTerminalSystem.GetBlockGroupWithName("Pistons Axial");
            pistonsAxialGroup.GetBlocksOfType(pistonsAxial);
        }

        public void Save()
        {
            Storage = string.Join(";", state ?? "ERROR");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo("State: " + state);

            switch (state)
            {
                case "DRILLING":
                    foreach (var piston in pistonsAxial)
                    {
                        if (piston.CurrentPosition != piston.HighestPosition)
                            return;
                    }

                    drills.ForEach(drill => drill.Enabled = false);
                    pistonsAxial.ForEach(piston => piston.Retract());
                    
                    state = "CONTRACTING";
                    
                    break; // case "DRILLING"

                case "CONTRACTING":
                    foreach (var piston in pistonsAxial)
                    {
                        if (piston.CurrentPosition != piston.LowestPosition)
                            return;
                    }

                    drills.ForEach(drill => drill.Enabled = true);
                    pistonsAxial.ForEach(piston => piston.Extend());
                    
                    state = "DRILLING";
                    
                    break; // case "CONTRACTING"

                case "ERROR":
                    break;

                default:
                    state = Me.CustomData;
                    break;
            }
        }
    }
}
