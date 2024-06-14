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

namespace IngameScript
{

    public class ConnectorStateCondition : ITransitionCondition 
    {        
        public static readonly string STATE_LOCKED = "LOCKED";
        public static readonly string STATE_UNLOCKED = "UNLOCKED";
        public static readonly string STATE_READY = "READY";

        private IMyShipConnector _theConnector;
        private MyShipConnectorStatus _targetStatus;

        public ConnectorStateCondition(StateMachine theMachine, 
                                    string connectorName, 
                                    string targetStatus)
        {
            _theConnector = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(connectorName) as IMyShipConnector;

            if (_theConnector == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, connectorName));
            }  

            if (targetStatus == STATE_LOCKED)
            {
                _targetStatus = MyShipConnectorStatus.Connected;
            }
            else if (targetStatus == STATE_UNLOCKED)
            {
                _targetStatus = MyShipConnectorStatus.Unconnected;
            }
            else
            {
                _targetStatus = MyShipConnectorStatus.Connectable;
            }            
        }

        public bool IsMet()
        {
            return _theConnector.Status == _targetStatus;
        }         
    }

    public class SensorStateCondition : ITransitionCondition 
    {
        public static readonly string STATE_DETECTED = "DETECTED";
        public static readonly string STATE_UNDETECTED = "UNDETECTED";

        //private StateMachine _theMachine;

        private IMySensorBlock _theSensor;
        private bool _triggerOnEmpty;

        public SensorStateCondition(StateMachine theMachine, 
                                    string sensorName, 
                                    string triggerState)
        {
            _theSensor = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(sensorName) as IMySensorBlock;

            if (_theSensor == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, sensorName));
            }  

            _triggerOnEmpty = (triggerState == STATE_UNDETECTED);
        }

        public bool IsMet()
        {
            return _theSensor.LastDetectedEntity.IsEmpty() == _triggerOnEmpty;
        }         
    }
}