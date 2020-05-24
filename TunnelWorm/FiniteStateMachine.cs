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
    partial class Program
    {
        public class FiniteStateMachine
        {
            string _currentStateName;
            Dictionary<string, State> _states;

            public FiniteStateMachine()
            {
                _currentStateName = "";
                _states = new Dictionary<string, State>();
            }

            public void SetCurrentStateName(string newState)
            {
                if (_states.ContainsKey(newState))
                {
                    _currentStateName = newState;
                }
            }

            public string GetCurrentStateName()
            {
                return _currentStateName;
            }

            public void AddState(string newStateName, Action entryAction, Action exitAction)
            {
                _states.Add(newStateName, new State(newStateName, entryAction, exitAction));
            }

            public void AddState(State newState)
            {
                _states.Add(newState.Name, newState);
            }

            public void AddStateTransition(string originStateName, string targetStateName, Func<bool> conditionFunction)
            {
                AddStateTransition(originStateName, new Transition(targetStateName, conditionFunction));
            }

            public void AddStateTransition(string originStateName, Transition newTransition)
            {
                State state = _states[originStateName];
                state.AddTransition(newTransition);
            }

            public void DoFirstPossibleStateTransition()
            {
                List<Transition> possibleTransitions = _states[_currentStateName].Transitions;

                foreach (Transition transition in possibleTransitions)
                {
                    if (transition.Condition())
                    {
                        TransitionToState(transition.Target);
                        break;
                    }
                }
            }

            public bool IsValidState(string stateName)
            {
                return _states.ContainsKey(stateName);
            }

            public bool IsValidStateTransition(string originStateName, string targetStateName)
            {
                if (!IsValidState(originStateName) || !IsValidState(targetStateName))
                    return false;

                State originState = _states[originStateName];
                foreach (Transition transition in originState.Transitions)
                {
                    if (transition.Target == targetStateName)
                        return true;
                }

                return false;
            }

            public void TransitionToState(string newState)
            {
                if (IsValidState(_currentStateName) && IsValidState(newState))
                {
                    _states[_currentStateName].ExitAction();
                    _currentStateName = newState;
                    _states[_currentStateName].EntryAction();
                }
            }
        }

        public class State
        {
            internal Action EntryAction;
            internal Action ExitAction;
            internal string Name;
            internal List<Transition> Transitions = new List<Transition>();

            public State(string name, Action entryAction, Action exitAction)
            {
                Name = name;
                EntryAction = entryAction;
                ExitAction = exitAction;
            }

            public void AddTransition(string targetStateName, Func<bool> conditionFunction)
            {
                Transitions.Add(new Transition(targetStateName, conditionFunction));
            }

            public void AddTransition(Transition transition)
            {
                Transitions.Add(transition);
            }
        }

        public class Transition
        {
            internal Func<bool> Condition;
            internal string Target;

            public Transition(string targetStateName, Func<bool> conditionFunction)
            {
                Target = targetStateName;
                Condition = conditionFunction;
            }
        }
    }
}
