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

        // Constants
        private static readonly float EASE_IN_DISTANCE = 0.2f;
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
            _isOnTarget = false;
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
            _isOnTarget = false;
            if (distance < ON_TARGET_DISTANCE)
            {
                _attachedPiston.Velocity = 0;
                _isOnTarget = true;
            }
            else if (distance < EASE_IN_DISTANCE)
            {
                _attachedPiston.Velocity = directionMultiplier * Math.Min(EASE_IN_VELOCITY, _maxVelocity);
            }
            else 
            {
                _attachedPiston.Velocity = directionMultiplier * _maxVelocity;
            }

            _theMachine.stateStatus("P: " + _attachedPiston.CurrentPosition + " => " + _targetPosition + " V: " + _attachedPiston.Velocity);
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
        private float _targetAngle;
        private string _direction;

        // State Info
        private IMyMotorStator _attachedRotor; 
        private bool _isOnTarget;

        // Constants
        private static readonly float EASE_IN_ANGLE = 5f/ 57.2957795f;
        private static readonly float TARGET_ANGLE = 0.5f/ 57.2957795f;
        private static readonly float EASE_IN_MAX_RPM = 0.5f;


        public TurnRotorAction(string blockName, 
                               float targetAngle, 
                               float maxRPM,
                               string direction)
        {
            _blockName = blockName;
            _maxRPM = Math.Abs(maxRPM);
            _targetAngle = targetAngle;
            _direction = direction;
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
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
            _isOnTarget = false;
        }

        public void OnTick()
        {
            float targetAngleInRAD = _targetAngle / 57.2957795f;
            float distanceInRAD = targetAngleInRAD - _attachedRotor.Angle;
            float directionMultiplyer = 1f;

            // TODO: Look for overflow to get real shortest path
            if ( (_direction == RotorDirection.NEGATIVE) || (distanceInRAD < 0) )
            {
                directionMultiplyer = -1f;
            }

            distanceInRAD = Math.Abs(distanceInRAD);

            _isOnTarget = false;
            if (distanceInRAD < TARGET_ANGLE)
            {
                _attachedRotor.TargetVelocityRPM = 0;
                _isOnTarget = true;
            }
            else if (distanceInRAD < EASE_IN_ANGLE)
            {
                _attachedRotor.TargetVelocityRPM = directionMultiplyer * Math.Min(EASE_IN_MAX_RPM, _maxRPM);
            }
            else 
            {
                _attachedRotor.TargetVelocityRPM = directionMultiplyer * _maxRPM;
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

                _theMachine.stateStatus("Value " +  progress + " => " + newValue);

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
            return _theConnector.Status == _desiredState;
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

}