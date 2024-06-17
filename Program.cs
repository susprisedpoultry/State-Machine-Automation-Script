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

        private ITransitionCondition ParseCondition(Parser.FunctionCommand whenCommand)
        {
            if (whenCommand.Function.IsFunctionMatch(Functions.CONNECTORSTATE, 2))
            {
                return new ConnectorStateCondition(whenCommand.Function.GetStringParam(0, "blockName"),
                                                   whenCommand.Function.GetValidatedStringParam(1, "blockName", Parser.CONNECTOR_STATES));
            }
            else if (whenCommand.Function.IsFunctionMatch(Functions.SENSORORSTATE, 2))
            {
                return new SensorStateCondition(whenCommand.Function.GetStringParam(0, "blockName"),
                                                whenCommand.Function.GetValidatedStringParam(1, "blockName", Parser.SENSOR_STATES));
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
                ITransitionCondition activeCondition = null;
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
                                currentState.AddCommand(activeWhenCommand.Function.GetStringParam(0, "commandName"), 
                                                        thenGoCommand.Parameter);                            
                            }
                            else
                            {
                                _parkingStateMachine.AddCommand(activeWhenCommand.Function.GetStringParam(0, "commandName"), 
                                                        thenGoCommand.Parameter);
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

                            activeCondition = new TransitionAndCondition(activeCondition, ParseCondition(andCommand));
                            continue;
                        }
                        
                        throw new Exception (" Unsupported command after a When: " + command.Name + " Line " + command.Line);
                    }

                    // We're not in a When: block
                    if (command.Name == Parser.TK_WHEN)
                    {
                        Parser.FunctionCommand whenCommand = command as Parser.FunctionCommand;

                        if (whenCommand.Function.IsFunctionMatch(Functions.COMMAND,1))
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

                        ChecksBeforeStateClose(activeCondition, activeWhenCommand);

                        CloseState(ref parentState, ref currentState);
                        substateCount = 0;
                        currentState = new State(_parkingStateMachine, stateCommand.Parameter);

                        if (firstStateName == null)
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

                            ChecksBeforeStateClose(activeCondition, activeWhenCommand);

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
                            if (actionCommand.Function.IsFunctionMatch(Functions.CONECTTERMINAL,2)) 
                            {
                                int terminalIndex = 0;

                                if (actionCommand.Function.ParamCount == 2) 
                                {
                                    terminalIndex = actionCommand.Function.GetIntParam(1, "terminalIndex");
                                }

                                _parkingStateMachine.ConnectTerminal(actionCommand.Function.GetStringParam(0, "blockName"), terminalIndex);

                                continue;
                            }

                            throw new Exception("Action: command only valid inside a state");
                        }

                        if (actionCommand.Function.IsFunctionMatch(Functions.SETANGLE, 4)) 
                        {
                            currentState.AddAction(new TurnRotorAction(actionCommand.Function.GetStringParam(0, "blockName"),
                                                                       actionCommand.Function.GetFloatParam(1, "targetAngle"),
                                                                       actionCommand.Function.GetFloatParam(2, "maxRPM"),
                                                                       actionCommand.Function.GetValidatedStringParam(3, "direction", Parser.ROTOR_DIRECTION_CUE)));
                        }
                        else if (actionCommand.Function.IsFunctionMatch(Functions.SETPOSITION, 3)) 
                        {
                            currentState.AddAction(new SetPositionAction(actionCommand.Function.GetStringParam(0, "blockName"),
                                                                         actionCommand.Function.GetFloatParam(1, "targetPosition"),
                                                                         actionCommand.Function.GetFloatParam(2, "maxVelocity")));
                        }
                        else if (actionCommand.Function.IsFunctionMatch(Functions.SETVALUEFLOAT, 4))
                        {
                            currentState.AddAction(new SetValueFloatAction(actionCommand.Function.GetStringParam(0, "blockName"),
                                                                         actionCommand.Function.GetStringParam(1, "property"),
                                                                         actionCommand.Function.GetFloatParam(2, "targetValue"),
                                                                         actionCommand.Function.GetFloatParam(3, "delay")));
                        }
                        else if (actionCommand.Function.IsFunctionMatch(Functions.TRIGGERTIMER, 2)) 
                        {
                            currentState.AddAction(new TriggerTimerAction(actionCommand.Function.GetStringParam(0, "blockName"),
                                                                       actionCommand.Function.GetValidatedStringParam(1, "timerStartType", Parser.TIMER_TRIGER_TYPE)));
                        }
                        else if (actionCommand.Function.IsFunctionMatch(Functions.LOCKCONNECTOR, 2)) 
                        {
                            currentState.AddAction(new LockConnectorAction(actionCommand.Function.GetStringParam(0, "blockName"),
                                                                       actionCommand.Function.GetValidatedStringParam(1, "timerStartType", Parser.CONNECTOR_STATES)));
                        }
                        else if (actionCommand.Function.IsFunctionMatch(Functions.SETENABLED, 2)) 
                        {
                            currentState.AddAction(new SetEnabledAction(actionCommand.Function.GetStringParam(0, "blockName"),
                                                                       actionCommand.Function.GetValidatedStringParam(1, "desiredState", Parser.ENABLED_STATES)));
                        }
                        else if (actionCommand.Function.IsFunctionMatch(Functions.SETLIGHTCOLOR, 3))
                        {
                            Parser.Color color = actionCommand.Function.GetColorParam(1, "targetValue");

                            currentState.AddAction(new SetLightColorAction(actionCommand.Function.GetStringParam(0, "blockName"),
                                                                           new Color(color.Red, color.Green, color.Blue),
                                                                           actionCommand.Function.GetFloatParam(2, "transitionDuration")));
                        }
                        else
                        {
                            throw new Exception(String.Format(Messages.UNEXPECTED_FUNCTION, actionCommand.Function, command.Line));
                        }

                        continue;
                    }                    
                    else {
                        throw new Exception("Unexpected command " + command.Name + " Line " + command.Line);
                    }               
                }  

                // Close any state left open
                ChecksBeforeStateClose(activeCondition, activeWhenCommand);
                CloseState(ref parentState, ref currentState);
                
                // We're done, do we have a state machine?
                if (_parkingStateMachine != null)
                {
                    try 
                    {
                        _parkingStateMachine.DeserializeState(Storage);
                    }
                    catch (Exception)
                    {
                        // The state didn't load from storage
                        _parkingStateMachine.SetStartState(firstStateName);                        
                    }

                    // Start-it-up (might bind blocks and cause errors)
                    _parkingStateMachine.Start();

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

        private static void ChecksBeforeStateClose(ITransitionCondition activeCondition, Parser.FunctionCommand activeWhenCommand)
        {
            // Check if we have Conditions open when the state closes
            if (activeCondition != null)
            {
                throw new Exception(String.Format(Messages.WHEN_NOT_CLOSED, activeCondition.GetType().ToString()));
            }
            
            if (activeWhenCommand != null)
            {
                throw new Exception(String.Format(Messages.WHEN_NOT_CLOSED, activeWhenCommand.Function.ToString()));
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
