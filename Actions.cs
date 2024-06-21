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
using Sandbox.Game.Entities.Cube;
using System.Numerics;

namespace IngameScript
{

    public class SetPositionAction : IStateAction
    {
        // Configuration
        private string _blockName;
        private float _maxVelocity;
        private float _targetPosition;
        private bool _isOnTarget;

        // State Info
        private StateMachine _theMachine;
        private IMyPistonBase _attachedPiston = null;
        private float[] _ease_in_distances = { 0, 0, 0, 0 };        

        // Constants
        private static readonly float ON_TARGET_DISTANCE = 0.01f;
        private static readonly float EASE_IN_VELOCITY = 0.5f;

        public SetPositionAction(string blockName, 
                                 float targetPosition,
                                 float maxVelocity)
        {
            _blockName = blockName;
            _maxVelocity = Math.Abs(maxVelocity);
            _targetPosition = targetPosition;
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theMachine = theMachine;
            _attachedPiston = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(_blockName) as IMyPistonBase;  

            if (_attachedPiston == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _blockName));
            }  
        }

        public bool IsDone()
        {
            return _isOnTarget;
        }

        public void OnEnter()
        {
            float distance = _targetPosition - _attachedPiston.CurrentPosition;
            float directionMultiplier = 1f;

            if (distance < 0) 
                directionMultiplier = -1f;

            distance = Math.Abs(distance);


            // Figure-out the ease-in distance
            float distancePer10Ticks = _maxVelocity / 6f;


            if (_theMachine.IsOutputting(StateMachine.OutputLevel.TRACE))
            {

                _theMachine.LogMessage(StateMachine.OutputLevel.TRACE, "Piston pos : " + _attachedPiston.CurrentPosition + " tar: " + _targetPosition );
                _theMachine.LogMessage(StateMachine.OutputLevel.TRACE, "       dir : " + directionMultiplier + " dis: " + distance + " easein: " + distancePer10Ticks);
            }

            if (distance < ON_TARGET_DISTANCE)
            {
                _attachedPiston.Velocity = 0;
                _isOnTarget = true;
            }
            else
            {
                _ease_in_distances[0]=distancePer10Ticks * 2f;
                _ease_in_distances[1]=distancePer10Ticks * 1f;
                _ease_in_distances[2]=distancePer10Ticks * .5f;
                _ease_in_distances[3]=distancePer10Ticks * 0.25f;

                _attachedPiston.Velocity = directionMultiplier * _maxVelocity;
                _isOnTarget = false;
            }            
        }

        public void OnExit()
        {
            _attachedPiston.Velocity = 0;            
        }

        public void OnTick()
        {
            float distance = _targetPosition - _attachedPiston.CurrentPosition;
            float directionMultiplier = 1f;

            if (distance < 0) 
                directionMultiplier = -1f;

            distance = Math.Abs(distance);

            // TODO: Possible other conditions to be done (max reached, etc.)
            // TODO: Make sure we're not constantly setting the velocity when we're at the destination            
            _isOnTarget = false;
            if (distance < ON_TARGET_DISTANCE)
            {
                _attachedPiston.Velocity = 0;
                _isOnTarget = true;
            }
            else 
            {
                float velocity = _maxVelocity;
                int easein = 0;

                while ( (easein < _ease_in_distances.Length) && (distance < _ease_in_distances[easein]) )
                {
                    velocity*=0.5f;
                    easein++;
                }

                _attachedPiston.Velocity = directionMultiplier * Math.Max(EASE_IN_VELOCITY, velocity);

                if (_theMachine.IsOutputting(StateMachine.OutputLevel.STATUS))
                    _theMachine.LogMessage(StateMachine.OutputLevel.STATUS, "P: " + _attachedPiston.CurrentPosition + " => " + _targetPosition + " V: " + _attachedPiston.Velocity);
            }
        }
    } 

    public class TriggerTimerAction : IStateAction
    {
        // Configuration
        private string _blockName;
        private string _triggerMethod;

        // State Info
        private IMyTimerBlock _attachedTimerBlock = null;


        public TriggerTimerAction(string blockName, string triggerMethod)
        {
            _blockName = blockName;
            _triggerMethod = triggerMethod;
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _attachedTimerBlock = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(_blockName) as IMyTimerBlock;  

            if (_attachedTimerBlock == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _blockName));
            }  
        }

        public void OnEnter()
        {
            if (_triggerMethod == TimerTriggerType.NOW)
            {
                _attachedTimerBlock.Trigger();
            }
            else
            {
                _attachedTimerBlock.StartCountdown();
            }
        }

        public void OnExit() { }

        public void OnTick() { }

        public bool IsDone() { return true; }
    } 

    public class TurnRotorAction : IStateAction
    {
        // Configuration
        private string _blockName;
        private float _maxRPM;
        private float _targetAngleInRAD;
        private string _direction;

        // State Info
        private IMyMotorStator _attachedRotor; 
        StateMachine _theMachine;
        private bool _isOnTarget;
        private float _directionMultiplier;
        private float[] _ease_in_distances = { 0, 0, 0, 0 };


        // Constants
        private static readonly float DEGREES_TO_RAD = 0.01745329252f;
        private static readonly float TARGET_ANGLE = 0.5f * DEGREES_TO_RAD; // We're precise to half a degree
        private static readonly float EASE_IN_MIN_RPM = 0.5f;
        private static readonly float FULL_CIRCLE = 360f * DEGREES_TO_RAD;
        private static readonly float RPM_TO_RAD_PER_SEC = 0.10472f;


        public TurnRotorAction(string blockName, 
                               float targetAngle, 
                               float maxRPM,
                               string direction)
        {
            _blockName = blockName;
            _maxRPM = Math.Abs(maxRPM);
            _targetAngleInRAD = targetAngle * DEGREES_TO_RAD;
            _direction = direction;
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theMachine = theMachine;
            _attachedRotor = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(_blockName) as IMyMotorStator;  

            if (_attachedRotor == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _blockName));
            }  
        }

        public bool IsDone()
        {
            return _isOnTarget;
        }

        public void OnEnter()
        {
            if (_direction == RotorDirection.NEGATIVE)
            {
                _directionMultiplier = -1f;
            }
            else if (_direction == RotorDirection.POSITIVE)
            {
                _directionMultiplier = 1f;
            }
            else if (GetDistance(-1f) < GetDistance(1f))
            {
                _directionMultiplier = -1f;
            }
            else
            {
                _directionMultiplier = 1f;
            }

            // Figure-out the ease-in distance
            float distanceInRAD = GetDistance(_directionMultiplier);
            float radPer10Ticks = _maxRPM * RPM_TO_RAD_PER_SEC / 6f;


            if (_theMachine.IsOutputting(StateMachine.OutputLevel.TRACE))
            {

                _theMachine.LogMessage(StateMachine.OutputLevel.TRACE, "Rotor pos : " + _attachedRotor.Angle + " tar: " + _targetAngleInRAD );
                _theMachine.LogMessage(StateMachine.OutputLevel.TRACE, "      dir : " + _directionMultiplier + " dis: " + distanceInRAD );
            }



            if (distanceInRAD < TARGET_ANGLE)
            {
                _attachedRotor.TargetVelocityRPM = 0;
                _isOnTarget = true;
            }
            else
            {
                _ease_in_distances[0]=radPer10Ticks * 2f;
                _ease_in_distances[1]=radPer10Ticks * 1f;
                _ease_in_distances[2]=radPer10Ticks * 0.5f;
                _ease_in_distances[3]=radPer10Ticks * 0.25f;

                _attachedRotor.TargetVelocityRPM = _directionMultiplier * _maxRPM;
                _isOnTarget = false;
            }
        }

        public void OnTick()
        {
            float distanceInRAD = GetDistance(_directionMultiplier);

            _isOnTarget = false;
            if (distanceInRAD < TARGET_ANGLE)
            {
                _attachedRotor.TargetVelocityRPM = 0;
                _isOnTarget = true;
            }
            else 
            {
                float velocity = _maxRPM;
                int easein = 0;

                while ( (easein < _ease_in_distances.Length) && (distanceInRAD < _ease_in_distances[easein]) )
                {
                    velocity*=0.5f;
                    easein++;
                }

                if (_theMachine.IsOutputting(StateMachine.OutputLevel.STATUS))
                    _theMachine.LogMessage(StateMachine.OutputLevel.STATUS, "R: " + distanceInRAD + " velocity: " + velocity );


                _attachedRotor.TargetVelocityRPM = _directionMultiplier * Math.Max(EASE_IN_MIN_RPM, velocity);
            }
        }

        private float GetDistance(float direction)
        {
            if (direction >= 0f)
            {
                float distanceInRAD = _targetAngleInRAD - _attachedRotor.Angle;

                if (distanceInRAD < 0) {
                    distanceInRAD += FULL_CIRCLE;
                }                

                return distanceInRAD;
            }
            else 
            {
                float distanceInRAD = _attachedRotor.Angle - _targetAngleInRAD;

                if (distanceInRAD < 0) {
                    distanceInRAD += FULL_CIRCLE;
                }

                return distanceInRAD;                
            }        
        }

        public void OnExit()
        {
            _attachedRotor.TargetVelocityRPM = 0;            
        }

    } 

    public class SetValueFloatAction : IStateAction
    {
        // Configuration
        private string _blockName;
        private string _propertyName;
        private float _targetValue;
        private int _delayInTicks;

        // State Info
        private StateMachine _theMachine;
        private IMyTerminalBlock _theBlock;
        private float _startingValue;        
        private int _currentTick;

        // Constants
        private static readonly int TICK_SKIP_COUNT = 6;


        public SetValueFloatAction(string blockName, 
                                string propertyName, 
                                float targetValue, 
                                float transitionDuration)
        {
            _blockName = blockName;
            _propertyName = propertyName;
            _targetValue = targetValue;
            _delayInTicks = (int)Math.Ceiling(transitionDuration * 60f);
            if (_delayInTicks == 0)
                _delayInTicks = 1;
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theMachine = theMachine;
            _theBlock = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(_blockName) as IMyTerminalBlock;

            if (_theBlock == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _blockName));
            }  

            if (_theBlock.GetProperty(_propertyName) == null)
            {
                throw new Exception(String.Format(Messages.PROP_NOT_FOUND, _propertyName, _blockName));
            }
        }

        public bool IsDone()
        {
            return (_currentTick >= _delayInTicks);
        }

        public void OnExit() {}

        public void OnEnter()
        {
            _startingValue = _theBlock.GetValueFloat(_propertyName);
            _currentTick = 0;
        }

        public void OnTick()
        {
            int ticksRemaining = _delayInTicks - (++_currentTick);

            if ( (ticksRemaining >= 0) && ((ticksRemaining % TICK_SKIP_COUNT) == 0))
            {
                float progress = (float)_currentTick/(float)_delayInTicks;
                float newValue = _startingValue + ( (_targetValue -  _startingValue) * progress);

                if (_theMachine.IsOutputting(StateMachine.OutputLevel.STATUS))
                    _theMachine.LogMessage(StateMachine.OutputLevel.STATUS, "Value " +  progress + " => " + newValue);

                _theBlock.SetValueFloat(_propertyName, newValue);
            }
        }
    }

    public class SetLightColorAction : IStateAction
    {
        // Configuration
        private string _blockName;
        private Color _targetValue;
        private int _delayInTicks;

        // State Info
        private StateMachine _theMachine;
        private IMyLightingBlock[] _theBlocks;
        private Color[] _startingValue;        
        private int _currentTick;

        // Constants
        private static readonly int TICK_SKIP_COUNT = 6;

        public SetLightColorAction(string blockName, 
                                Color targetValue, 
                                float transitionDuration)
        {
            _blockName = blockName;
            _targetValue = targetValue;
            _delayInTicks = (int)Math.Ceiling(transitionDuration * 60f);

            if (_delayInTicks == 0)
                _delayInTicks = 1;
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theMachine = theMachine;
            _theBlocks = Array.ConvertAll<IMyTerminalBlock, IMyLightingBlock>(_theMachine.FindBlockOrGroupbyName(_blockName), item => item as IMyLightingBlock);
            _theBlocks = Array.FindAll<IMyLightingBlock>(_theBlocks, block => block != null);

            if (_theBlocks.Length == 0)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _blockName));
            } 
        }

        public bool IsDone()
        {
            return (_currentTick >= _delayInTicks);
        }

        public void OnExit() {}

        public void OnEnter()
        {
            _startingValue = new Color[_theBlocks.Length];
            for (int i=0;i<_theBlocks.Length;i++)
            {
                _startingValue[i] = _theBlocks[i].Color;
            }
            _currentTick = 0;
        }

        public void OnTick()
        {
            int ticksRemaining = _delayInTicks - (++_currentTick);

            if ( (ticksRemaining >= 0) && ((ticksRemaining % TICK_SKIP_COUNT) == 0))
            {
                float progress = (float)_currentTick/(float)_delayInTicks;

                for (int i=0;i<_theBlocks.Length;i++)
                {
                    Color newValue = Color.Lerp(_startingValue[i], _targetValue, progress);

                    _theBlocks[i].Color = newValue;
                }
            }
        }
    }

    public class ApplyAction : IStateAction
    {
        // Configuration
        private string _blockName;
        private string _action;

        // State Info
        private StateMachine _theMachine;
        private IMyTerminalBlock[] _theBlocks;

        public ApplyAction(string blockName, 
                                    string action)
        {
            _blockName = blockName;
            _action =  action;
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theMachine = theMachine;
            _theBlocks =_theMachine.FindBlockOrGroupbyName(_blockName);
            _theBlocks = Array.FindAll<IMyTerminalBlock>(_theBlocks, block => block != null);

            if (_theBlocks.Length == 0)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _blockName));
            }                  
        }

        public bool IsDone() { return true; }
        public void OnExit() {}
        public void OnTick() {}

        public void OnEnter()
        {
            for (int i=0;i<_theBlocks.Length;i++)
            {
                ((IMyTerminalBlock)_theBlocks[i]).ApplyAction(_action);
            }
        }
    } 

    public class SetEnabledAction : IStateAction
    {
        // Configuration
        private string _blockName;
        private bool _desiredState;

        // State Info
        private StateMachine _theMachine;
        private IMyFunctionalBlock[] _theBlocks;

        public SetEnabledAction(string blockName, 
                                string desiredState)
        {
            _blockName = blockName;
            _desiredState =  (desiredState == EnabledStates.ENABLED);
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theMachine = theMachine;
            _theBlocks = Array.ConvertAll<IMyTerminalBlock, IMyFunctionalBlock>(_theMachine.FindBlockOrGroupbyName(_blockName), item => item as IMyFunctionalBlock);
            _theBlocks = Array.FindAll<IMyFunctionalBlock>(_theBlocks, block => block != null);

            if (_theBlocks.Length == 0)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _blockName));
            }                  
        }

        public bool IsDone() { return true; }
        public void OnExit() {}
        public void OnTick() {}

        public void OnEnter()
        {
            for (int i=0;i<_theBlocks.Length;i++)
            {
                if (_theBlocks[i].Enabled != _desiredState)
                {
                    _theBlocks[i].Enabled = _desiredState;
                }
            }
        }
    } 

    public class LockConnectorAction : IStateAction
    {
        // Configuration
        private string _blockName;
        private MyShipConnectorStatus _desiredState;
        
        // State Info
        private StateMachine _theMachine;
        private IMyShipConnector _theConnector;


        public LockConnectorAction(string blockName, 
                                   string desiredState)
        {
            _blockName = blockName;

            if (desiredState == ConnectorStates.LOCKED)
            {
                _desiredState = MyShipConnectorStatus.Connected;
            }
            else if (desiredState == ConnectorStates.UNLOCKED)
            {
                _desiredState = MyShipConnectorStatus.Unconnected;
            }
            else
            {
                throw new Exception(String.Format(Messages.INVALID_PARAMETER, "desiredState", desiredState));
            }
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theMachine = theMachine;
            _theConnector = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(_blockName) as IMyShipConnector;  

            if (_theConnector == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _blockName));
            }  
        }

        public bool IsDone()
        {
            if (_desiredState == MyShipConnectorStatus.Connected) 
            {
                return _theConnector.Status == _desiredState;
            }

            return _theConnector.Status != MyShipConnectorStatus.Connected;
        }

        public void OnExit() {}
        public void OnTick() {}

        public void OnEnter()
        {
            if (_desiredState == MyShipConnectorStatus.Connected)
            {
                _theConnector.Connect();
            }
            else
            {
                _theConnector.Disconnect();
            }
        }
    } 

    public class SetLCDTextAction : IStateAction
    {
        private string _blockName;
        private string _message;
        private int _screenIndex;
        private Color? _textColor;
        private Color? _backColor;

        // State data
        private StateMachine _theMachine;
        private IMyTextSurface _theSurface;

        public SetLCDTextAction(string blockName, string message, int screenIndex, Color? textColor, Color? BackColor)
        {
            _blockName = blockName;
            _message = message;
            _screenIndex = screenIndex;
            _textColor = textColor;
            _backColor = BackColor;
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theMachine = theMachine;
            _theSurface = theMachine.FindLCD(_blockName, _screenIndex);
        }

        public void OnEnter() 
        {
            _theSurface.WriteText(_message);       

            if (_textColor != null)
            {
                _theSurface.FontColor = (Color)_textColor;
            }     

            if (_backColor != null)
            {
                _theSurface.BackgroundColor = (Color)_backColor;
            }
        }

        public void OnExit() {}
        public void OnTick() {}
        public bool IsDone()
        {
            return true;
        }

    }

}