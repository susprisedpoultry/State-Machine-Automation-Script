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
        // Configuration
        private string _blockName;
        private MyShipConnectorStatus _targetStatus;

        // State data
        private IMyShipConnector _theConnector;

        public ConnectorStateCondition(StateMachine theMachine, 
                                    string connectorName, 
                                    string targetStatus)
        {
            _blockName = connectorName;

            if (targetStatus == ConnectorStates.LOCKED)
            {
                _targetStatus = MyShipConnectorStatus.Connected;
            }
            else if (targetStatus == ConnectorStates.UNLOCKED)
            {
                _targetStatus = MyShipConnectorStatus.Unconnected;
            }
            else
            {
                _targetStatus = MyShipConnectorStatus.Connectable;
            }            
        }

        public void OnBindBlocks(IMyGridTerminalSystem theGrid)
        {
            _theConnector = theGrid.GetBlockWithName(_blockName) as IMyShipConnector;

            if (_theConnector == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _blockName));
            }  
        }
        public bool IsMet()
        {
            return _theConnector.Status == _targetStatus;
        }         
    }

    public class SensorStateCondition : ITransitionCondition 
    {
        // Configuration
        private string _blockName;        
        private bool _triggerOnEmpty;

        // State data
        private IMySensorBlock _theSensor;

        public SensorStateCondition(StateMachine theMachine, 
                                    string sensorName, 
                                    string triggerState)
        {
            _blockName = sensorName;
            _triggerOnEmpty = (triggerState == SensorStates.UNDETECTED);
        }

        public void OnBindBlocks(IMyGridTerminalSystem theGrid)
        {
            _theSensor = theGrid.GetBlockWithName(_blockName) as IMySensorBlock;

            if (_theSensor == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _blockName));
            }  
        }

        public bool IsMet()
        {
            return _theSensor.LastDetectedEntity.IsEmpty() == _triggerOnEmpty;
        }         
    }
}