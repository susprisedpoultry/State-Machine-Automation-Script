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
using SEStateMachine;
using System.CodeDom;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        private StateMachine _parkingStateMachine = null;

        private void CloseSubState(ref State parentState, ref State currentState)
        {
            if ( (currentState != null) && 
                 (parentState != null) )
            {
                parentState.AddSubState(currentState);
                currentState = null;
            }
        }

        private void CloseState(ref State parentState, ref State currentState)
        {
            CloseSubState(ref parentState, ref currentState);

            if (currentState != null)
            {
                _parkingStateMachine.AddState(currentState);
            }

            if (parentState != null)
            {
                _parkingStateMachine.AddState(parentState);
            }

            parentState = null;
            currentState = null;
        }

        private TransitionCondition ParseCondition(Parser.FunctionCommand whenCommand)
        {
            if (whenCommand.Function == Parser.FN_CONNECTORSTATE)
            {
                return new ConnectorStateCondition(_parkingStateMachine,
                                                    whenCommand.Parameters[0],
                                                    whenCommand.Parameters[1]);
            }
            else if (whenCommand.Function == Parser.FN_SENSORORSTATE)
            {
                return new SensorStateCondition(_parkingStateMachine,
                                                whenCommand.Parameters[0],
                                                whenCommand.Parameters[1]);
            }
            else
            {
                throw new Exception (" Unsupported condition function " + whenCommand.Function);
            }
        }

        public Program()
        {
            Parser configParser = new Parser(Me.CustomData);
            int errorCount = 0;
            _parkingStateMachine = null;

            Echo("Loading Config");

            foreach (Parser.ParsedCommand line in configParser.Commands)
            {
                Parser.ParseError error = line as Parser.ParseError;

                if (error != null ) {
                    Echo(line.ToString());
                    errorCount++;
                }
            }            

            // Abort loading on parse error
            if (errorCount > 0)
                return;

            string firstStateName = null;

            try
            {                
                State currentState = null;
                State parentState = null;
                int substateCount = 0;
                TransitionCondition activeCondition = null;
                Parser.FunctionCommand activeWhenCommand = null;

                foreach (Parser.ParsedCommand command in configParser.Commands)
                {
                    if (_parkingStateMachine == null)
                    {
                        if (command.Name == Parser.TK_MACHINE)
                        {
                            _parkingStateMachine = new StateMachine(this);
                            continue;
                        }
                        else
                        {
                            throw new Exception("State machine undefined");
                        }
                    }                      

                    if (activeWhenCommand != null)
                    {
                        // We are in a command block, the only option is a ThenGo:
                        if (command.Name == Parser.TK_THENGO)
                        {
                            Parser.SimpleCommand thenGoCommand = command as Parser.SimpleCommand;

                            if (currentState != null)
                            {
                                currentState.AddCommand(activeWhenCommand.Parameters[0], thenGoCommand.Parameter);                            
                            }
                            else
                            {
                                _parkingStateMachine.AddCommand(activeWhenCommand.Parameters[0], thenGoCommand.Parameter);
                            }

                            activeWhenCommand = null;                            
                            continue;                            
                        }
                        else
                        {
                            throw new Exception (" Unsupported command after a When:Command(*) " + command.Name + " Line " + command.Line);
                        }
                    }

                    
                    if (activeCondition != null) 
                    {
                        // We're in a When: block (not Command())
                        // Only possible actions are ThenGo and And:
                        if (command.Name == Parser.TK_THENGO)
                        {
                            Parser.SimpleCommand thenGoCommand = command as Parser.SimpleCommand;

                            if (currentState != null)
                            {
                                currentState.AddTransition(activeCondition, thenGoCommand.Parameter);                            
                            }
                            else
                            {                        
                                _parkingStateMachine.AddTransition(activeCondition, thenGoCommand.Parameter);
                            }

                            activeCondition = null;                            
                            continue;                            
                        }
                        else if (command.Name == Parser.TK_AND) 
                        {
                            Parser.FunctionCommand andCommand = command as Parser.FunctionCommand;

                            activeCondition = new TransitionAndCondition(_parkingStateMachine, activeCondition, ParseCondition(andCommand));
                            continue;
                        }
                        
                        throw new Exception (" Unsupported command after a When: " + command.Name + " Line " + command.Line);
                    }

                    // We're not in a When: block
                    if (command.Name == Parser.TK_WHEN)
                    {
                        Parser.FunctionCommand whenCommand = command as Parser.FunctionCommand;

                        if (whenCommand.Function == Parser.FN_COMMAND) 
                        {
                            activeWhenCommand = whenCommand;
                        }
                        else
                        {
                            activeCondition = ParseCondition(whenCommand);
                        }
                        continue;
                    }
                    else if (command.Name == Parser.TK_STATE)
                    {
                        Parser.SimpleCommand stateCommand = command as Parser.SimpleCommand;

                        CloseState(ref parentState, ref currentState);
                        substateCount = 0;
                        currentState = new State(_parkingStateMachine, stateCommand.Parameter);

                        if (firstStateName== null)
                        {
                            firstStateName = currentState.Name;
                        }
                        continue;
                    }
                    else if (command.Name == Parser.TK_STEP)
                    {
                        Parser.SimpleCommand stateCommand = command as Parser.SimpleCommand;
                        string subStateName = stateCommand.Parameter;

                        if (currentState == null) {
                            throw new Exception("Step: command only valid inside a state");
                        }

                        State previousState = currentState;

                        // If this is the first Step in the state
                        if (parentState == null)
                        {
                            parentState = currentState;
                            currentState = null;
                            substateCount = 1;
                        }
                        else
                        {
                            CloseSubState(ref parentState, ref currentState);
                            substateCount++;
                        }

                        if (subStateName.Length == 0)
                        {
                            subStateName = parentState.Name + StateMachine.SUBSTATE_NAME_SEPARATOR + substateCount;
                        }
                        else if (!subStateName.StartsWith(parentState.Name + StateMachine.SUBSTATE_NAME_SEPARATOR))
                        {
                            throw new Exception("Step: state name must start with " + parentState.Name + StateMachine.SUBSTATE_NAME_SEPARATOR);
                        }

                        if (previousState.TransitionWhenDone == null) 
                        {
                            previousState.TransitionWhenDone = subStateName;
                        }

                        currentState = new State(_parkingStateMachine, subStateName);

                        continue;
                    }
                    else if (command.Name == Parser.TK_GO)
                    {
                        Parser.SimpleCommand stateCommand = command as Parser.SimpleCommand;

                        if (currentState == null)
                        {
                            throw new Exception("Go: command only valid inside a state");
                        }

                        currentState.TransitionWhenDone = stateCommand.Parameter;
                        continue;
                    }
                    else if (command.Name == Parser.TK_ACTION)
                    {
                        Parser.FunctionCommand actionCommand = command as Parser.FunctionCommand;

                        if (currentState == null)
                        {
                            if (actionCommand.Function == Parser.FN_CONECTTERMINAL) 
                            {
                                int terminalIndex = 0;

                                if (actionCommand.Parameters.Length == 2) 
                                {
                                    Int32.TryParse(actionCommand.Parameters[1], out terminalIndex);
                                }

                                _parkingStateMachine.ConnectTerminal(actionCommand.Parameters[0], terminalIndex);

                                continue;
                            }

                            throw new Exception("Action: command only valid inside a state");
                        }

                        if (actionCommand.Function == Parser.FN_SETANGLE) 
                        {
                            float targetAngle = 0f;
                            float maxRPM = 0f;

                            Single.TryParse(actionCommand.Parameters[1], out targetAngle);
                            Single.TryParse(actionCommand.Parameters[2], out maxRPM);

                            currentState.AddAction(new TurnRotorAction(_parkingStateMachine, 
                                                                       actionCommand.Parameters[0],
                                                                       targetAngle,
                                                                       maxRPM,
                                                                       actionCommand.Parameters[3]));
                        }
                        else if (actionCommand.Function == Parser.FN_SETPOSITION) 
                        {
                            float targetPosition = 0f;
                            float maxVelocity = 0f;

                            Single.TryParse(actionCommand.Parameters[1], out targetPosition);
                            Single.TryParse(actionCommand.Parameters[2], out maxVelocity);

                            currentState.AddAction(new SetPositionAction(_parkingStateMachine, 
                                                                         actionCommand.Parameters[0],
                                                                         targetPosition,
                                                                         maxVelocity));
                        }
                        else if (actionCommand.Function == Parser.FN_SETVALUEFLOAT) 
                        {
                            float newValue = 0f;
                            float transitionDuration = 0f;

                            Single.TryParse(actionCommand.Parameters[2], out newValue);
                            Single.TryParse(actionCommand.Parameters[3], out transitionDuration);

                            currentState.AddAction(new SetValueFloatAction(_parkingStateMachine, 
                                                                         actionCommand.Parameters[0],
                                                                         actionCommand.Parameters[1],
                                                                         newValue,
                                                                         transitionDuration));
                        }
                        else if (actionCommand.Function == Parser.FN_TRIGGERTIMER) 
                        {
                            currentState.AddAction(new TriggerTimerAction(_parkingStateMachine, 
                                                                       actionCommand.Parameters[0],
                                                                       actionCommand.Parameters[1]));
                        }
                        else if (actionCommand.Function == Parser.FN_LOCKCONNECTOR) 
                        {
                            currentState.AddAction(new LockConnectorAction(_parkingStateMachine, 
                                                                       actionCommand.Parameters[0],
                                                                       actionCommand.Parameters[1]));
                        }
                        else
                        {
                            throw new Exception("Unexpected function for Action: " + actionCommand.Function + " Line " + command.Line);
                        }

                        continue;
                    }                    
                    else {
                        throw new Exception("Unexpected command " + command.Name + " Line " + command.Line);
                    }               
                }  

                // TODO: What if we have Conditions open

                // Close any state left open
                CloseState(ref parentState, ref currentState);
                
                // We're done, do we have a state machine?
                if (_parkingStateMachine != null)
                {
                    try 
                    {
                        _parkingStateMachine.DeserializeState(Storage);
                    }
                    catch (Exception e)
                    {
                        // The state didn't load from storage
                        _parkingStateMachine.SetStartState(firstStateName);                        
                    }

                    // Everything went well, start the updates
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                }
            }            
            catch (Exception e)
            {
                Echo("CONFIGURATION ERROR: " + e.Message);
                _parkingStateMachine = null;
            }
            
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.

            if (_parkingStateMachine != null) 
            {
                Storage = _parkingStateMachine.SerializeState();
            }
            else 
            {
                Storage = "";
            }
        }

        public void Main(string argument, UpdateType updateType)
        {
            // Skip all if we didn't initialize properly
            if (_parkingStateMachine == null)
                return;

            // Script run by other script/terminal/timer
            if ((updateType & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger)) > 0)
            {
                _parkingStateMachine.OnCommand(argument);

                // Not the timer update
                return;
            }

            // This means that now we're in the timer update
           _parkingStateMachine.OnTick();

        }
    }
}
