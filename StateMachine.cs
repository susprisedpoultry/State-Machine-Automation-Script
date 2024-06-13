using Sandbox.Game.EntityComponents;
using Sandbox.Game.GUI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Dynamic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.GameServices;
using VRage.Generics;
using VRageMath;
using VRageRender;

namespace SEStateMachine
{
    public class StateMachine
    {
        public static readonly string SUBSTATE_NAME_SEPARATOR = ".";

        // Configuration
        private MyGridProgram _theProgram;
        private IMyTextSurface _theSurface = null;
        private Dictionary<String, State> _states = new Dictionary<String, State>();
        private Dictionary<string, string> _commandTransitions = new Dictionary<string, string>();
        private List<TransitionWithCondition> _conditions = new List<TransitionWithCondition>();

        // State
        private State _currentState = null;
        private State _nextState = null;
        private StringBuilder _displayText = new StringBuilder();

        public StateMachine(MyGridProgram theProgram)
        {
            _theProgram = theProgram;
        }

        // Properties
        public MyGridProgram TheProgram { get { return _theProgram; }
        }
        public State ActiveState { get { return _currentState; } }

        public void ConnectTerminal(string lcdName, int screenIndex = 0)
        {
            IMyTerminalBlock namedBlock = _theProgram.GridTerminalSystem.GetBlockWithName(lcdName);
            IMyTextSurfaceProvider cockpit = namedBlock as IMyTextSurfaceProvider;

            if (namedBlock == null)
                throw new Exception("Block not found " + lcdName);

            // If we didn't hit a surface right away, it is a cockpit or console
            if (_theSurface == null)
            {
                _theSurface = cockpit.GetSurface(screenIndex);

                if (_theSurface == null) 
                {
                    throw new Exception("Unable to get screen #" + screenIndex + " from block " + lcdName);
                }
            }

            _theSurface.ContentType = ContentType.TEXT_AND_IMAGE;
            _theSurface.WriteText("State Machine Connected");

        }

        public string SerializeState()
        {
            return _currentState.Name;
        }

        public void DeserializeState(String storage)
        {
            SetStartState(storage);
        } 

        public void SetStartState(string stateName)
        {
            if (_currentState != null)
            {
                throw new Exception("SetStartState can only be called once");
            }

            if (!_states.TryGetValue(stateName, out _currentState))
            {
                throw new Exception("ERROR: Can't Transition to '" + stateName + "' state not found");
            }            

            stateStatus("--");
        }

        public void AddState(State stateToAdd) {

            _states.Add(stateToAdd.Name, stateToAdd);
        }

        public void AddCommand(String command, String targetState)
        {            
            _commandTransitions.Add(command, targetState);
        }

        public void AddTransition(TransitionCondition condition, string targetState)
        {
            _conditions.Add(new TransitionWithCondition(condition, targetState));
        }        

        public void logError(string error)
        {
            _theProgram.Echo("ERROR: " + error);
        }

        public void stateStatus(string message)
        {
            if (_theSurface != null) {
                _displayText.Append(message);
                _displayText.Append('\n');
            }
        }

        public void transitionTo(string newStateName)
        {
            if (_nextState != null)
            {
                // This is an error state, it shouldn't happen

                _theProgram.Echo("ERROR: Can't Transition from " + _currentState.Name + " to " + newStateName + " already transitionning to " + _nextState.Name);
                return;
            } 

            if (!_states.TryGetValue(newStateName, out _nextState))
            {
                stateStatus("ERROR: Can't Transition from " + _currentState.Name + " to " + newStateName + " state not found");
                foreach(string state in _states.Keys)
                {
                    stateStatus("S: '" + state + "'");
                }
            }
        }

        public void OnTick()
        {
            try 
            {
                if (_nextState != null)
                {
                    _currentState.OnExit();
                    _currentState = _nextState;
                    _nextState = null;
                    stateStatus("Switched to state");
                    _currentState.OnEnter();
                }            
                else 
                {
                    _currentState.OnTick(0);

                    // If we didn't trigger a state transition inside the state, 
                    // Check the global transitions
                    if (_nextState == null)
                    {
                        foreach(TransitionWithCondition transition in _conditions)
                        {
                            if (transition.Condition.IsMet()) 
                            {
                                transitionTo(transition.TargetState);

                                // Shortcut the testing
                                return;
                            }
                        }          
                    }      
                }
            }
            catch (Exception e)
            {
                if (_theSurface != null) {
                    _theSurface.WriteText(e.Message);
                }
            }

            // Update the display
            if (_theSurface != null) {
                _theSurface.WriteText(_displayText.ToString());
                _displayText = new StringBuilder(_currentState.Name);
                _displayText.Append("\n---\n");
            }
        }

        public void OnCommand(string command)
        {
            // Check the current state for commands
            _currentState.OnCommand(command);

            // If no commands triggered, Check global commands
            if (_nextState == null)
            {
                string newState;

                if (_commandTransitions.TryGetValue(command, out newState))
                {
                    this.transitionTo(newState);
                }
            }
        }
    }

    public class StateAction
    {
        protected StateMachine _theMachine;

        public StateAction(StateMachine theMachine)
        {
            _theMachine = theMachine;
        }

        virtual public bool IsDone()
        {
            return true;
        }

        virtual public void OnEnter()
        {

        }

        virtual public void OnExit()
        {

        }

        virtual public void OnTick(int tickCount)
        {
        }

    }

    public class TransitionCondition
    {
        public TransitionCondition()
        {

        }

        virtual public bool IsMet()
        {
            return true;
        } 
    }

    public class TransitionAndCondition : TransitionCondition 
    {
        private StateMachine _theMachine;

        TransitionCondition _condition1;
        TransitionCondition _condition2;

        public TransitionAndCondition(StateMachine theMachine, TransitionCondition condition1, TransitionCondition condition2)
        {
            _theMachine = theMachine;
            _condition1 = condition1;
            _condition2 = condition2;
        }

        override public bool IsMet()
        {
            _theMachine.stateStatus("Condition " + _condition1.IsMet() + ", " + _condition2.IsMet());
            //return false;
            return _condition1.IsMet() && _condition2.IsMet();
        } 
    }

    public struct TransitionWithCondition
    {
        public readonly TransitionCondition Condition;
        public readonly string TargetState;

        public TransitionWithCondition(TransitionCondition condition, string targetState)
        {
            Condition = condition;
            TargetState = targetState;
        }
    }

    public class State 
    {    
        private StateMachine _machine;
        private readonly string _name;
        private string _doneStateName = null;
        private List<TransitionWithCondition> _conditions = new List<TransitionWithCondition>();
        private List<StateAction> _actions = new List<StateAction>();
        private Dictionary<string, string> _commandTransitions = new Dictionary<string, string>();

        public State(StateMachine stateMachine, String name) 
        {
            _machine = stateMachine;
            _name = name;
        }

        public string Name {
            get { return _name; }
        }

        virtual public void OnEnter()
        {
            foreach(StateAction action in _actions)
            {
                action.OnEnter();
            }       
        }

        virtual public void OnExit()
        {
            foreach(StateAction action in _actions)
            {
                action.OnExit();
            }       
        }

        virtual public void OnTick(int tickCount)
        {
            foreach(TransitionWithCondition transition in _conditions)
            {
                if (transition.Condition.IsMet()) 
                {
                    _machine.transitionTo(transition.TargetState);

                    // Shortcut the testing
                    return;
                }
            }

            bool areWeDone = true;
            foreach(StateAction action in _actions)
            {
                action.OnTick(tickCount);

                areWeDone &= action.IsDone();
            }       

            if ( (_doneStateName != null) && areWeDone)            
            {
                _machine.transitionTo(_doneStateName);
            }
            else if (areWeDone)
            {
                _machine.stateStatus("Waiting");
            }
        }

        virtual public void OnCommand(string command)
        {
            string newState;

            if (_commandTransitions.TryGetValue(command, out newState))
            {
                _machine.transitionTo(newState);
            }
            else
            {
                _machine.logError("Command " + command + " not valid in state " + Name);
            }
        }        

        public State AddSubState(State stateToAdd) 
        {
            // TODO: transfer commands to sub-state

            _machine.AddState(stateToAdd);

            return stateToAdd;
        }

        public State AddTransition(TransitionCondition condition, string targetState)
        {
            _conditions.Add(new TransitionWithCondition(condition, targetState));

            return this;
        }

        public State AddCommand(String command, String targetState)
        {
            _commandTransitions.Add(command, targetState);
            return this;
        }

        public string TransitionWhenDone { set {  _doneStateName = value; } get { return _doneStateName;} }

        public State AddAction(StateAction action)
        {
            _actions.Add(action);
            return this;
        }
    }

}