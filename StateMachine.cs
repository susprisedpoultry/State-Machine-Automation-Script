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
using VRage.Scripting;
using VRageMath;
using VRageRender;

namespace IngameScript
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
        public MyGridProgram TheProgram { get { return _theProgram; } }
        public State ActiveState { get { return _currentState; } }

        public void ConnectTerminal(string lcdName, int screenIndex = 0)
        {
            IMyTerminalBlock namedBlock = _theProgram.GridTerminalSystem.GetBlockWithName(lcdName);
            IMyTextSurfaceProvider cockpit = namedBlock as IMyTextSurfaceProvider;

            if (namedBlock == null)
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, lcdName));

            // If we didn't hit a surface right away, it is a cockpit or console
            if (_theSurface == null)
            {
                _theSurface = cockpit.GetSurface(screenIndex);

                if (_theSurface == null) 
                {
                    throw new Exception(String.Format(Messages.SCREEN_NOT_FOUND, screenIndex, lcdName));
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
                throw new Exception(String.Format(Messages.STATE_NOT_FOUND, stateName));
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

        public void AddTransition(ITransitionCondition condition, string targetState)
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
                stateStatus(String.Format(Messages.STATE_NOT_FOUND,newStateName));
                foreach(string state in _states.Keys)
                {
                    stateStatus("S: '" + state + "'");
                }
            }
        }

        private bool CheckTransitions()
        {
            // Otherwise, test for transitions in-state first
            if (_currentState.CheckTransitions()) 
                return true;

            // Then check global transitions
            foreach(TransitionWithCondition transition in _conditions)
            {
                if (transition.Condition.IsMet()) 
                {
                    transitionTo(transition.TargetState);

                    return true;
                }
            }   

            return false;
        }

        public void OnTick()
        {
            try 
            {
                // Are we in a transition change, do that, otherwise, do a transition check and then 
                // the OnTick() method for the state
                if (_nextState != null)
                {
                    _currentState.OnExit();
                    _currentState = _nextState;
                    _nextState = null;
                    stateStatus("Switched to state");
                    _currentState.OnEnter();
                }            
                else if (!CheckTransitions())
                {                    
                    _currentState.OnTick();
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

        public T[] FindBlockOrGroupbyName<T>(string name) where T : class, IMyTerminalBlock
        {
            IMyBlockGroup group = this._theProgram.GridTerminalSystem.GetBlockGroupWithName(name);
            List<T> lights = new List<T>();

            if (group != null) 
            {
                group.GetBlocksOfType<T>(lights);                
            }
            else
            {
                T theLight =  this._theProgram.GridTerminalSystem.GetBlockWithName(name) as T;

                if (theLight != null)
                {
                    lights.Add(theLight);
                }
            }

            if (lights.Count == 0)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, name));
            }      
            return lights.ToArray();    
        }
    
    }

    public interface IStateAction
    {
        bool IsDone();
        void OnBindBlocks(StateMachine theMachine);
        void OnEnter();
        void OnExit();        
        void OnTick();
    }

    public interface ITransitionCondition
    {
        void OnBindBlocks(StateMachine theMachine);
        bool IsMet();
    }

    public class TransitionAndCondition : ITransitionCondition 
    {
        ITransitionCondition _condition1;
        ITransitionCondition _condition2;

        public TransitionAndCondition(ITransitionCondition condition1, ITransitionCondition condition2)
        {
            _condition1 = condition1;
            _condition2 = condition2;
        }

        public void OnBindBlocks(StateMachine theMachine) {}

        public bool IsMet()
        {
            return _condition1.IsMet() && _condition2.IsMet();
        } 
    }

    public struct TransitionWithCondition
    {
        public readonly ITransitionCondition Condition;
        public readonly string TargetState;

        public TransitionWithCondition(ITransitionCondition condition, string targetState)
        {
            Condition = condition;
            TargetState = targetState;
        }
    }

    public class State 
    {    
        private StateMachine _machine;
        private State _parentState = null;
        private readonly string _name;
        private string _doneStateName = null;
        private List<TransitionWithCondition> _conditions = new List<TransitionWithCondition>();
        private List<IStateAction> _actions = new List<IStateAction>();
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
            // Rebind the blocks on the actions and conditions
            // TODO: decide what to do with the exceptions.
            foreach(IStateAction action in _actions)
            {
                action.OnBindBlocks(_machine);
            }              

            foreach(TransitionWithCondition transition in _conditions)
            {
                transition.Condition.OnBindBlocks(_machine);
            }

            foreach(IStateAction action in _actions)
            {
                action.OnEnter();
            }       
        }

        virtual public void OnExit()
        {
            foreach(IStateAction action in _actions)
            {
                action.OnExit();
            }       
        }

        public bool CheckTransitions()
        {
            foreach(TransitionWithCondition transition in _conditions)
            {
                if (transition.Condition.IsMet()) 
                {
                    _machine.transitionTo(transition.TargetState);

                    // Shortcut the testing
                    return true;
                }
            }

            if (_parentState != null) 
            {
                return _parentState.CheckTransitions();
            }

            return false;
        }

        public void OnTick()
        {
            bool areWeDone = true;
            foreach(IStateAction action in _actions)
            {
                action.OnTick();

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

        public void OnCommand(string command)
        {
            string newState;

            if (_commandTransitions.TryGetValue(command, out newState))
            {
                _machine.transitionTo(newState);
            }
            else 
            {
                _parentState?.OnCommand(command);
            }
        }        

        public State AddSubState(State stateToAdd) 
        {
            stateToAdd._parentState = this;
            _machine.AddState(stateToAdd);

            return stateToAdd;
        }

        public State AddTransition(ITransitionCondition condition, string targetState)
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

        public State AddAction(IStateAction action)
        {
            _actions.Add(action);
            return this;
        }
    }

}