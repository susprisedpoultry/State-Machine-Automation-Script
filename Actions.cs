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
        private StateMachine _theMachine;
        private IMyPistonBase _attachedPiston;
        private float _maxVelocity;
        private float _targetPosition;
        private bool _isOnTarget;

        private static readonly float EASE_IN_DISTANCE = 0.2f;
        private static readonly float ON_TARGET_DISTANCE = 0.01f;
        private static readonly float EASE_IN_VELOCITY = 0.5f;

        public SetPositionAction(StateMachine theMachine, 
                                 string pistonName, 
                                 float targetPosition,
                                 float maxVelocity)
        {
            _theMachine = theMachine;
            _attachedPiston = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(pistonName) as IMyPistonBase;  

            if (_attachedPiston == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, pistonName));
            }  

            _maxVelocity = Math.Abs(maxVelocity);
            _targetPosition = targetPosition;
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
        public static readonly string TRIGGER_METHOD_NOW = "NOW";
        public static readonly string TRIGGER_METHOD_START = "START";

        private IMyTimerBlock _attachedTimerBlock;
        private string _triggerMethod;

        public TriggerTimerAction(StateMachine theMachine, string timerBlockName, string triggerMethod)
        {
            _attachedTimerBlock = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(timerBlockName) as IMyTimerBlock;  

            if (_attachedTimerBlock == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, timerBlockName));
            }  

            _triggerMethod = triggerMethod;
        }

        public void OnEnter()
        {
            if (_triggerMethod == TRIGGER_METHOD_NOW)
            {
                _attachedTimerBlock.Trigger();
            }
            else
            {
                _attachedTimerBlock.StartCountdown();
            }
        }

        public void OnExit()
        {

        }

        public void OnTick()
        {
            
        }

        public bool IsDone()
        {
            return true;
        }
    } 

    public class TurnRotorAction : IStateAction
    {
        public static readonly string ROTOR_DIRECTION_ANY = "ANY";
        public static readonly string ROTOR_DIRECTION_POSITIVE = "POS";
        public static readonly string ROTOR_DIRECTION_NEGATIVE = "NEG";

        private static readonly float EASE_IN_ANGLE = 5f/ 57.2957795f;
        private static readonly float TARGET_ANGLE = 0.5f/ 57.2957795f;
        private static readonly float EASE_IN_MAX_RPM = 0.5f;

        private IMyMotorStator _attachedRotor; 
        private float _maxRPM;
        private float _targetAngle;
        private string _direction;
        //private float _maxRPMWithDirection;
        private bool _isOnTarget;

        public TurnRotorAction(StateMachine theMachine, 
                               string rotorName, 
                               float targetAngle, 
                               float maxRPM,
                               string direction)
        {
            _attachedRotor = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(rotorName) as IMyMotorStator;

            if (_attachedRotor == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, rotorName));
            }  

            _maxRPM = Math.Abs(maxRPM);
            _targetAngle = targetAngle;
            _direction = direction;
            _isOnTarget = false;
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
            if ( (_direction == ROTOR_DIRECTION_NEGATIVE) || (distanceInRAD < 0) )
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
        private static readonly int TICK_SKIP_COUNT = 6;

        private StateMachine _theMachine;
        private IMyTerminalBlock _theBlock;
        private string _propertyName;
        private float _targetValue;
        private float _startingValue;        
        private int _delayInTicks;
        private int _currentTick;

        public SetValueFloatAction(StateMachine theMachine, 
                                string blockName, 
                                string propertyName, 
                                float targetValue, 
                                float transitionDuration)
        {
            _theMachine = theMachine;
            _theBlock = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(blockName) as IMyTerminalBlock;

            if (_theBlock == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, blockName));
            }  

            if (_theBlock.GetProperty(propertyName) == null)
            {
                throw new Exception(String.Format(Messages.PROP_NOT_FOUND, propertyName, blockName));
            }

            _propertyName = propertyName;
            _targetValue = targetValue;
            _delayInTicks = (int)Math.Ceiling(transitionDuration * 60f);
            if (_delayInTicks == 0)
                _delayInTicks = 1;
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

    public class LockConnectorAction : IStateAction
    {
        StateMachine _theMachine;
        private IMyShipConnector _theConnector;
        private MyShipConnectorStatus _desiredState;

        public LockConnectorAction(StateMachine theMachine, 
                                string connectorName, 
                                string desiredState)
        {
            _theMachine = theMachine;
            _theConnector = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(connectorName) as IMyShipConnector;  

            if (_theConnector == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, connectorName));
            }  

            if (desiredState == ConnectorStateCondition.STATE_LOCKED)
            {
                _desiredState = MyShipConnectorStatus.Connected;
            }
            else if (desiredState == ConnectorStateCondition.STATE_UNLOCKED)
            {
                _desiredState = MyShipConnectorStatus.Unconnected;
            }
            else
            {
                throw new Exception(String.Format(Messages.INVALID_PARAMETER, "desiredState", desiredState));
            }
        }

        public bool IsDone()
        {
            _theMachine.stateStatus("C " + _theConnector.Status.ToString());
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