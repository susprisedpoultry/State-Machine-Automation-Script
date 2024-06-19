using Sandbox.Game.EntityComponents;
using Sandbox.Game.GUI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Eventing.Reader;
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
        private OutputLevel _outputLevel;

        // State
        private State _currentState = null;
        private State _nextState = null;

        // Managing the display
        private class TraceMessage
        {
            public int TicksRemaining;
            public string Message;

            public TraceMessage(float delay, string message) 
            {
                 TicksRemaining = (int)Math.Abs(60f * delay); 
                 Message = message;
            }
        }

        private StringBuilder _displayText = new StringBuilder();

        private StringBuilder _displayStatus = new StringBuilder();

        private TraceMessage[] _displayTraces = { null, null };
        private int _lastdisplayTrace = -1;

        public StateMachine(MyGridProgram theProgram)
        {
            _theProgram = theProgram;
            _outputLevel = OutputLevel.TRACE;
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

        public enum OutputLevel {
            NONE = 0,
            LABEL = 1,
            ERROR = 2,
            STATUS = 3,
            TRACE = 4,
        } 

        public void SetOutputLevel(string outputLevel)
        {
            if (outputLevel == LCDOutputLevel.NONE)
                _outputLevel = OutputLevel.NONE;
            else if (outputLevel == LCDOutputLevel.LABEL)
                _outputLevel = OutputLevel.LABEL;
            else if (outputLevel == LCDOutputLevel.ERROR)
                _outputLevel = OutputLevel.ERROR;
            else if (outputLevel == LCDOutputLevel.STATUS)
                _outputLevel = OutputLevel.STATUS;
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
            // TODO: Validate that this is only entered when the machine is halted

            if (_currentState != null)
            {
                throw new Exception("SetStartState can only be called once");
            }

            if (!_states.TryGetValue(stateName, out _currentState))
            {
                throw new Exception(String.Format(Messages.STATE_NOT_FOUND, stateName));
            }            

            LogMessage(OutputLevel.TRACE, "Setting start state " + stateName);
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

        public void Start()
        {
            // Todo handle not binding in the configuration
            foreach(State state in _states.Values)
            {
                state.OnBindBlocks(this);
            }

            // Enter the start state
            _currentState.OnEnter();
        }

        public void Halt()
        {

        }

        public float TicksPerSecond { get { return 60f; } }

        public bool IsOutputting(OutputLevel level)
        {
            return ((_theSurface != null) && (_outputLevel >= level));
        }
        
        public void LogMessage(OutputLevel level, string message)
        {
            if (IsOutputting(level))
            {
                switch (level) 
                {
                    case OutputLevel.ERROR:
                    case OutputLevel.TRACE:
                        _lastdisplayTrace++;
                        if (_lastdisplayTrace >= _displayTraces.Length)
                        {
                            Array.Resize(ref _displayTraces, _displayTraces.Length + 10);
                        }
                        _displayTraces[_lastdisplayTrace] = new TraceMessage(4, message);
                        break;
                    case OutputLevel.STATUS:
                        _displayStatus.Append(message);
                        _displayStatus.Append('\n');
                        break;
                }
            }
        }

        public void TransitionTo(string newStateName)
        {
            if (_nextState != null)
            {
                LogMessage(OutputLevel.ERROR, "ERROR: Can't Transition from " + _currentState.Name + " to " + newStateName + " already transitionning to " + _nextState.Name);
                Halt();

                return;
            } 

            if (!_states.TryGetValue(newStateName, out _nextState))
            {
                if (IsOutputting(OutputLevel.ERROR)) 
                {
                    LogMessage(OutputLevel.ERROR, String.Format(Messages.STATE_NOT_FOUND,newStateName));
                    foreach(string state in _states.Keys)
                    {
                        LogMessage(OutputLevel.ERROR, "S: '" + state + "'");
                    }
                }
                Halt();
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
                    TransitionTo(transition.TargetState);

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
                    LogMessage(OutputLevel.TRACE, String.Format(Messages.TRC_SWITCHED_STATE, _currentState.Name));
                    _currentState.OnEnter();
                }            
                else if (!CheckTransitions())
                {                    
                    _currentState.OnTick();
                }
            }
            catch (Exception e)
            {
                LogMessage(OutputLevel.ERROR, e.Message + "currentState :" + _currentState?.Name);
            }

            // Update the display
            if ( (_theSurface != null) && (_outputLevel > OutputLevel.NONE)) 
            {
                _displayText.Clear();
                _displayText.Append(_currentState.Name);

                if (IsOutputting(OutputLevel.STATUS))
                {
                    _displayText.Append("\n---\n");
                    _displayText.Append(_displayStatus.ToString());                                    
                    _displayStatus.Clear();
                }

                if (IsOutputting(OutputLevel.ERROR))
                {
                    _displayText.Append("\n---\n");

                    int lastValidIndex = -1;

                    for(int i=0;i<=_lastdisplayTrace;i++)
                    {
                        if (_displayTraces[i].TicksRemaining > 0) 
                        {
                            _displayTraces[i].TicksRemaining--;
                            _displayText.Append(_displayTraces[i].Message).Append("\n");
                            lastValidIndex++;                         
                            _displayTraces[lastValidIndex] = _displayTraces[i];
                        }
                    }
                    _lastdisplayTrace = lastValidIndex;
                    _theSurface.WriteText(_displayText.ToString());
                }
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
                    this.TransitionTo(newState);
                }
            }

            // Trace out when we are not handling a command
            if (IsOutputting(OutputLevel.TRACE) && (_nextState == null)) {

                LogMessage(OutputLevel.TRACE, String.Format(Messages.TRC_COMMAND_NOTHANDLED, command, _currentState.Name));
            }
        }

        public IMyTerminalBlock[] FindBlockOrGroupbyName(string name)
        {
            IMyBlockGroup group = this._theProgram.GridTerminalSystem.GetBlockGroupWithName(name);
            List<IMyTerminalBlock> lights = new List<IMyTerminalBlock>();

            if (group != null) 
            {
                group.GetBlocks(lights);
            }
            else
            {
                IMyTerminalBlock theLight =  this._theProgram.GridTerminalSystem.GetBlockWithName(name);

                if (theLight != null)
                {
                    lights.Add(theLight);
                }
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

        public void OnBindBlocks(StateMachine theMachine) 
        {
            _condition1.OnBindBlocks(theMachine);
            _condition2.OnBindBlocks(theMachine);
        }

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
        private bool _actionsDone = false; 
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

        public void OnBindBlocks(StateMachine theMachine)
        {
            // Rebind the blocks on the actions and conditions
            foreach(IStateAction action in _actions)
            {
                action.OnBindBlocks(theMachine);
            }              

            foreach(TransitionWithCondition transition in _conditions)
            {
                transition.Condition.OnBindBlocks(theMachine);
            }
        }

        public void OnEnter()
        {
            _actionsDone = false;            
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
                    _machine.TransitionTo(transition.TargetState);

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
            // TODO: Maybe shortcut if we already know we're done

            bool areWeDone = true;
            foreach(IStateAction action in _actions)
            {
                action.OnTick();

                areWeDone &= action.IsDone();
            }       

            if (areWeDone)
            {
                if (_doneStateName != null)
                {
                    _machine.TransitionTo(_doneStateName);
                }
                else if (!_actionsDone)
                {
                    _machine.LogMessage(StateMachine.OutputLevel.TRACE, String.Format(Messages.TRC_ACTIONS_DONE_WAITING, Name));
                }
            }

            _actionsDone = areWeDone;
        }

        public void OnCommand(string command)
        {
            string newState;

            if (_commandTransitions.TryGetValue(command, out newState))
            {
                _machine.TransitionTo(newState);
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