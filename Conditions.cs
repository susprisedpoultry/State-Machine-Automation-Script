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

        public ConnectorStateCondition(string connectorName, 
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

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theConnector = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(_blockName) as IMyShipConnector;

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

        public SensorStateCondition(string sensorName, 
                                    string triggerState)
        {
            _blockName = sensorName;
            _triggerOnEmpty = (triggerState == SensorStates.UNDETECTED);
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theSensor = theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(_blockName) as IMySensorBlock;

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

    public class BlockPresentCondition : ITransitionCondition 
    {

        // Configuration
        private string _blockName;        
        private bool _triggerOnPresent;
        private float _testDelay;


        // State data
        private StateMachine _theMachine;
        private int _delayInTicks;

        public BlockPresentCondition(string blockName, 
                                     string triggerState,
                                     float  testDelay)
        {
            _blockName = blockName;
            _triggerOnPresent = (triggerState == BlockFoundStates.FOUND);
            _testDelay = testDelay;
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theMachine = theMachine;
            _delayInTicks = 0;
        }

        public bool IsMet()
        {
            _delayInTicks--;
            if (_delayInTicks > 0)
            {
                return false;
            }

            // reset timer
            _delayInTicks = (int)Math.Ceiling(_testDelay * _theMachine.TicksPerSecond);

            IMyTerminalBlock block = _theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(_blockName);

            if (_theMachine.IsOutputting(StateMachine.OutputLevel.TRACE))
            {
                _theMachine.LogMessage(StateMachine.OutputLevel.TRACE, String.Format(Messages.TRC_FIND_BLOCK, _blockName, block != null));
            }

            if (_triggerOnPresent)
                return block != null;
            else
                return block == null;
        }         
    }

    public class BlockTopState : ITransitionCondition 
    {

        // Configuration
        private string _blockName;        
        private bool _triggerOnFound;

        // State data
        private StateMachine _theMachine;
        private IMyMechanicalConnectionBlock _theBlock;

        public BlockTopState(string blockName, 
                                     string triggerState)
        {
            _blockName = blockName;
            _triggerOnFound = (triggerState == TopAttachedStates.ATTACHED);
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theMachine = theMachine;

            _theBlock = _theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(_blockName) as IMyMechanicalConnectionBlock;

            if (_theBlock == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _blockName));
            }  
        }

        public bool IsMet()
        {
            return _theBlock.IsAttached == _triggerOnFound;
        }         
    }

    
    public class ConnectedBlocksAligned : ITransitionCondition 
    {

        // Configuration
        private string _connectorBlockName;        
        private string _alignmentBlockNAme;        
        private string _axis;
        private float _tolerance;

        // State data
        private StateMachine _theMachine;
        private IMyShipConnector _theConnector;
        private IMyTerminalBlock _theAlignementBlock;

        public ConnectedBlocksAligned(string connectorName, 
                             string alignmentBlockName,
                             String axis,
                             float tolerance)
        {
            _connectorBlockName = connectorName;
            _alignmentBlockNAme = alignmentBlockName;
            _axis = axis;
            _tolerance = tolerance;
        }

        public void OnBindBlocks(StateMachine theMachine)
        {
            _theMachine = theMachine;
            _theConnector = _theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(_connectorBlockName) as IMyShipConnector;
            _theAlignementBlock = _theMachine.TheProgram.GridTerminalSystem.GetBlockWithName(_alignmentBlockNAme);

            if (_theConnector == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _connectorBlockName));
            }  
            if (_theAlignementBlock == null)
            {
                throw new Exception(String.Format(Messages.BLOCK_NOT_FOUND, _alignmentBlockNAme));
            } 
        }

        public bool IsMet()        
        {
            // We need transforms to bring everything back to the world coordinates 
            // of the grid where the programmable block is
            IMyTerminalBlock referenceBlock = _theMachine.TheProgram.Me;
            Vector3D referenceWorldPosition = referenceBlock.WorldMatrix.Translation; 
            MatrixD mainGridTransfrorm = _theMachine.TheProgram.Me.WorldMatrix;

            IMyTerminalBlock connectedBlock = _theConnector.OtherConnector;

            if (connectedBlock == null)
                connectedBlock = _theConnector;


            // Get the Grid position of te connected block
            Vector3D worldPosition = connectedBlock.GetPosition();

            //Convert worldPosition into a world direction
            Vector3D worldDirection = worldPosition - referenceWorldPosition ; //this is a vector starting at the reference block pointing at your desired position

            //Convert worldDirection into a local direction
            Vector3D connectedBlockPostion = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(referenceBlock.WorldMatrix)); //note that we transpose to go from world -> body

            // Get the reference position
            worldPosition = _theAlignementBlock.GetPosition();

            //Convert worldPosition into a world direction
            worldDirection = worldPosition - referenceWorldPosition ; //this is a vector starting at the reference block pointing at your desired position

            //Convert worldDirection into a local direction
            Vector3D referenceBlockPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(referenceBlock.WorldMatrix)); //note that we transpose to go from world -> body

            double distance;

            if (_axis==Axis.X) 
            {
                distance = connectedBlockPostion.X - referenceBlockPosition.X;
            }
            else if (_axis==Axis.Y) 
            {
                distance = connectedBlockPostion.Y - referenceBlockPosition.Y;
            }
            else // (_axis==Axis.Z) 
            {
                distance = connectedBlockPostion.Z - referenceBlockPosition.Z;
            }

            distance = Math.Abs(distance);

            if (_theMachine.IsOutputting(StateMachine.OutputLevel.STATUS))
            {
                _theMachine.LogMessage(StateMachine.OutputLevel.STATUS, String.Format("Block1: ({0:F2}, {1:F2}, {2:F2})", connectedBlockPostion.X, connectedBlockPostion.Y, connectedBlockPostion.Z));
                _theMachine.LogMessage(StateMachine.OutputLevel.STATUS, String.Format("Block2: ({0:F2}, {1:F2}, {2:F2})", referenceBlockPosition.X, referenceBlockPosition.Y, referenceBlockPosition.Z));
                _theMachine.LogMessage(StateMachine.OutputLevel.STATUS, String.Format("distance: ({0:F2}", distance));
            }

            return distance < _tolerance;
        }         
    }

}