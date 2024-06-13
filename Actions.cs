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

    public class SetPositionAction : StateAction
    {
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
                                 float maxVelocity) : base(theMachine)
        {
            _attachedPiston = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(pistonName) as IMyPistonBase;  

            if (_attachedPiston == null)
            {
                throw new Exception("Piston not found " + pistonName);
            }  

            _maxVelocity = Math.Abs(maxVelocity);
            _targetPosition = targetPosition;
        }

        override public bool IsDone()
        {
            return _isOnTarget;
        }

        override public void OnEnter()
        {
            _isOnTarget = false;
        }

        override public void OnExit()
        {
            _attachedPiston.Velocity = 0;            
        }

        public override void OnTick(int tickCount)
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

    public class TriggerTimerAction : StateAction
    {
        public static readonly string TRIGGER_METHOD_NOW = "NOW";
        public static readonly string TRIGGER_METHOD_START = "START";

        private IMyTimerBlock _attachedTimerBlock;
        private string _triggerMethod;

        public TriggerTimerAction(StateMachine theMachine, string timerBlockName, string triggerMethod) : base(theMachine)
        {
            _attachedTimerBlock = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(timerBlockName) as IMyTimerBlock;  

            if (_attachedTimerBlock == null)
            {
                throw new Exception("Timer Block not found " + _attachedTimerBlock);
            }  

            _triggerMethod = triggerMethod;
        }

        override public void OnEnter()
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
    } 

    public class TurnRotorAction : StateAction
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
                               string direction) : base(theMachine)
        {
            _attachedRotor = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(rotorName) as IMyMotorStator;

            if (_attachedRotor == null)
            {
                throw new Exception("Rotor not found " + rotorName);
            }  

            _maxRPM = Math.Abs(maxRPM);
            _targetAngle = targetAngle;
            _direction = direction;
            _isOnTarget = false;
        }

        override public bool IsDone()
        {
            return _isOnTarget;
        }

        override public void OnEnter()
        {
            _isOnTarget = false;
        }

        public override void OnTick(int tickCount)
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

        override public void OnExit()
        {
            _attachedRotor.TargetVelocityRPM = 0;            
        }

    } 

    public class SetValueFloatAction : StateAction
    {
        private static readonly int TICK_SKIP_COUNT = 6;

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
                                float transitionDuration) : base(theMachine)
        {
            _theBlock = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(blockName) as IMyTerminalBlock;

            if (_theBlock == null)
            {
                throw new Exception("Block not found " + blockName);
            }  

            if (_theBlock.GetProperty(propertyName) == null)
            {
                throw new Exception("Property '" + propertyName + "' not found for block " + blockName);
            }

            _propertyName = propertyName;
            _targetValue = targetValue;
            _delayInTicks = (int)Math.Ceiling(transitionDuration * 60f);
            if (_delayInTicks == 0)
                _delayInTicks = 1;
        }

        override public bool IsDone()
        {
            return (_currentTick >= _delayInTicks);
        }

        override public void OnEnter()
        {
            _startingValue = _theBlock.GetValueFloat(_propertyName);
            _currentTick = 0;
        }

        public override void OnTick(int tickCount)
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

    public class LockConnectorAction : StateAction
    {
        private IMyShipConnector _theConnector;
        private MyShipConnectorStatus _desiredState;

        public LockConnectorAction(StateMachine theMachine, 
                                string connectorName, 
                                string desiredState) : base(theMachine)
        {
            _theConnector = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(connectorName) as IMyShipConnector;  

            if (_theConnector == null)
            {
                throw new Exception("Connector not found " + connectorName);
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
                throw new Exception("Invalid parameter " + desiredState);
            }
        }

        override public bool IsDone()
        {
            _theMachine.stateStatus("C " + _theConnector.Status.ToString());
            return _theConnector.Status == _desiredState;
        }

        override public void OnEnter()
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