using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace IngameScript
{
    public class Parser
    {
        // Parser Keywords
        public static readonly string TK_MACHINE = "Machine";
        public static readonly string TK_STATE = "State";
        public static readonly string TK_STEP = "Step";
        public static readonly string TK_ACTION = "Action";
        public static readonly string TK_GO = "Go";
        public static readonly string TK_WHEN = "When";
        public static readonly string TK_THENGO = "ThenGo";
        public static readonly string TK_AND = "And";

        public static readonly string FN_COMMAND = "Command";
        public static readonly string FN_CONNECTORSTATE = "ConnectorState";
        public static readonly string FN_SENSORORSTATE = "SensorState";
        public static readonly string FN_CONECTTERMINAL = "ConnectTerminal";
        public static readonly string FN_TRIGGERTIMER = "TriggerTimer";
        public static readonly string FN_SETANGLE = "SetAngle";
        public static readonly string FN_SETPOSITION = "SetPosition";
        public static readonly string FN_LOCKCONNECTOR = "LockConnector";
        public static readonly string FN_SETVALUEFLOAT = "SetValueFloat";

        private static readonly char COMMENT_MARKER = '#';        
        private static readonly char COMMAND_MARKER = ':';        
        private static readonly char OPEN_PAR = '(';        
        private static readonly char CLOSE_PAR = ')';        
        private static readonly char PARAM_SEPARATOR = ',';        
        private static readonly char LINE_SEPARATOR = '\n';        

        public static readonly string[] ROTOR_DIRECTION_CUE = { TurnRotorAction.ROTOR_DIRECTION_ANY, 
                                                                TurnRotorAction.ROTOR_DIRECTION_POSITIVE, 
                                                                TurnRotorAction.ROTOR_DIRECTION_NEGATIVE};
        public static readonly string[] CONNECTOR_STATES = { ConnectorStateCondition.STATE_LOCKED,
                                                             ConnectorStateCondition.STATE_UNLOCKED,
                                                             ConnectorStateCondition.STATE_READY};
        public static readonly string[] SENSOR_STATES = { SensorStateCondition.STATE_DETECTED,
                                                          SensorStateCondition.STATE_UNDETECTED};
        public static readonly string[] TIMER_TRIGER_TYPE = { TriggerTimerAction.TRIGGER_METHOD_NOW, 
                                                              TriggerTimerAction.TRIGGER_METHOD_START};


        private List<ParsedCommand> _commands;

        public List<ParsedCommand> Commands
        {
            get { return _commands; }
        }

        public class ParsedCommand
        {
            private readonly string _name;
            private readonly int _line;

            public ParsedCommand(int line, string commandName)
            {
                _line = line;
                _name = commandName;
            }

            public string Name { get { return _name; }}
            public int Line { get { return _line; }}

            public override string ToString()
            {
                return _line + " : " + _name;
            }
    }

        public class ParseError : ParsedCommand
        {
            private readonly string _description;

            public ParseError(int line, string description) : base(line, "ERROR")
            {
                _description = description;
            }

            public override string ToString()
            {
                return base.ToString() + " : " + _description;
            }            
        }

        public class SimpleCommand : ParsedCommand
        {
            private readonly string _parameter;

            public SimpleCommand(int line, string name, string parameter) : base(line, name)
            {
                _parameter = parameter;
            }

            public string Parameter { get { return _parameter; }}

            public override string ToString()
            {
                return base.ToString() + " : " + _parameter;
            }            
        }

        public class FunctionCommand : ParsedCommand
        {
            private readonly string _function;
            private readonly string[] _parameters;

            public string Function { get { return _function; }}
            public string[] Parameters { get { return _parameters; }}

            public FunctionCommand(int line, string name, string functionToParse) : base(line, name)
            {
                functionToParse = functionToParse.Trim(); 

                int firstParenthese = functionToParse.IndexOf(OPEN_PAR);
                int lastParenthese = functionToParse.LastIndexOf(CLOSE_PAR);

                if (functionToParse.Length==0) 
                {
                    throw new Exception(Messages.MISSING_FUNCTION);
                }

                // Make sure we have functionname(param) formatting
                if ( (firstParenthese <= 0) || 
                    (lastParenthese == -1) || 
                    (lastParenthese != (functionToParse.Length - 1)) )
                {
                    throw new Exception(Messages.MISMATCHED_PAR);
                }
                
                // Parse the function name and parameters
                _function = functionToParse.Substring(0, firstParenthese);
                _parameters = functionToParse.Substring(firstParenthese + 1, (lastParenthese - firstParenthese) - 1).Split(PARAM_SEPARATOR);

                for(int i=0;i<_parameters.Length;i++)
                {
                    _parameters[i] = _parameters[i].Trim();
                }

                if (_function == FN_COMMAND)
                {
                    if ( (_parameters.Length != 1) ||  (_parameters[0].Length == 0))
                        throw new Exception(String.Format(Messages.UNEXPECTED_PAR_IN_FUNC, _function));
                }
                else if (_function == FN_CONECTTERMINAL)
                {
                    if ( (_parameters.Length < 1) ||
                         (_parameters.Length > 2) ||  
                         (_parameters[0].Length == 0) )
                        throw new Exception(String.Format(Messages.UNEXPECTED_PAR_IN_FUNC, _function));

                    int testValue;
                    if ( (_parameters.Length == 2) &&
                         (!Int32.TryParse(_parameters[1], out testValue)) )
                    {
                        throw new Exception("Invalid parameter for terminal index : " + _parameters[1]);
                    }
                }
                else if ( 
                    (_function == FN_CONNECTORSTATE) || 
                    (_function == FN_LOCKCONNECTOR) )
                {
                    if ( (_parameters.Length != 2) ||  
                        (_parameters[0].Length == 0) ||  
                        (_parameters[1].Length == 0))
                        throw new Exception(String.Format(Messages.UNEXPECTED_PAR_IN_FUNC, _function));

                    if (!CONNECTOR_STATES.Any(_parameters[1].Contains))
                    {
                        throw new Exception(String.Format(Messages.INVALID_PARAMETER, "connectorState", _parameters[1]));
                    }
                }
                else if (_function == FN_SENSORORSTATE)
                {
                    if ( (_parameters.Length != 2) ||  
                        (_parameters[0].Length == 0) ||  
                        (_parameters[1].Length == 0))
                        throw new Exception(String.Format(Messages.UNEXPECTED_PAR_IN_FUNC, _function));                    

                    if (!SENSOR_STATES.Any(_parameters[1].Contains))
                    {
                        throw new Exception(String.Format(Messages.INVALID_PARAMETER, "sensorState", _parameters[1]));
                    }
                }
                else if (_function == FN_TRIGGERTIMER)
                {
                    if ( (_parameters.Length != 2) ||  
                        (_parameters[0].Length == 0) ||  
                        (_parameters[1].Length == 0))
                        throw new Exception(String.Format(Messages.UNEXPECTED_PAR_IN_FUNC, _function));                    

                    if (!TIMER_TRIGER_TYPE.Any(_parameters[1].Contains))
                    {
                        throw new Exception(String.Format(Messages.INVALID_PARAMETER, "triggerType", _parameters[1]));
                    }
                }
                else if (_function == FN_SETANGLE)
                {
                    if ( (_parameters.Length != 4) ||  
                        (_parameters[0].Length == 0))
                        throw new Exception(String.Format(Messages.UNEXPECTED_PAR_IN_FUNC, _function));

                    float testValue;
                    if (!Single.TryParse(_parameters[1], out testValue))
                    {
                        throw new Exception(String.Format(Messages.INVALID_PARAMETER, "targetAngle", _parameters[1]));
                    }

                    if (!Single.TryParse(_parameters[2], out testValue))
                    {
                        throw new Exception(String.Format(Messages.INVALID_PARAMETER, "maxRPM", _parameters[2]));
                    }

                    if (!ROTOR_DIRECTION_CUE.Any(_parameters[3].Contains))
                    {
                        throw new Exception(String.Format(Messages.INVALID_PARAMETER, "direction", _parameters[3]));
                    }
                }
                else if (_function == FN_SETPOSITION)
                {
                    if ( (_parameters.Length != 3) ||  
                        (_parameters[0].Length == 0))
                        throw new Exception(String.Format(Messages.UNEXPECTED_PAR_IN_FUNC, _function));

                    float testValue;
                    if (!Single.TryParse(_parameters[1], out testValue))
                    {
                        throw new Exception(String.Format(Messages.INVALID_PARAMETER, "targetPosition", _parameters[1]));
                    }

                    if (!Single.TryParse(_parameters[2], out testValue))
                    {
                        throw new Exception(String.Format(Messages.INVALID_PARAMETER, "maxVelocity", _parameters[2]));
                    }
                }
                else if (_function == FN_SETVALUEFLOAT)
                {
                    if ( (_parameters.Length != 4) ||  
                        (_parameters[0].Length == 0) ||  
                        (_parameters[1].Length == 0))
                        throw new Exception(String.Format(Messages.UNEXPECTED_PAR_IN_FUNC, _function));

                    float testValue;
                    if (!Single.TryParse(_parameters[2], out testValue))
                    {
                        throw new Exception(String.Format(Messages.INVALID_PARAMETER, "value", _parameters[1]));
                    }
                    if (!Single.TryParse(_parameters[3], out testValue))
                    {
                        throw new Exception(String.Format(Messages.INVALID_PARAMETER, "delay", _parameters[2]));
                    }
                }
                else 
                {
                    throw new Exception(String.Format(Messages.UNKNOWN_FUNCTION, _function));
                }

            }

            public override string ToString()
            {
                StringBuilder newString = new StringBuilder();

                newString.Append(base.ToString() + " : " + _function + " ( ");
                
                for(int i = 0;i<_parameters.Length;i++) 
                {
                    if (i>0)
                        newString.Append(", ");

                    newString.Append(_parameters[i].ToString());
                }
                newString.Append(" )");

                return newString.ToString();
            }            
        }

        public Parser(string scriptToParse)
        {
            string[] lines = scriptToParse.Split(LINE_SEPARATOR);
            List<ParsedCommand> commands = new List<ParsedCommand>();
            
            // Breakdown each line into commands
            //int i = 0;
            for (int i=0;i<lines.Length;i++)
            {
                string line = lines[i].Trim();
                string strippedLine = line.Split(COMMENT_MARKER)[0];
                string[] parsedLine = strippedLine.Split(COMMAND_MARKER);

                try {

                    if (parsedLine.Length != 2)
                    {
                        if (line.Length != 0)
                        {
                            throw new Exception(Messages.NO_COMMAND);
                        }
                    }
                    else if ( 
                        (parsedLine[0] == TK_GO) || 
                        (parsedLine[0] == TK_THENGO) || 
                        (parsedLine[0] == TK_MACHINE) || 
                        (parsedLine[0] == TK_STATE) ||
                        (parsedLine[0] == TK_STEP)                         
                        )
                    {
                        commands.Add(new SimpleCommand(i, parsedLine[0].Trim(), parsedLine[1].Trim()));
                    }
                    else if (
                        (parsedLine[0] == TK_ACTION) || 
                        (parsedLine[0] == TK_WHEN) || 
                        (parsedLine[0] == TK_AND)
                        )
                    {
                        commands.Add(new FunctionCommand(i, parsedLine[0].Trim(), parsedLine[1].Trim()));
                    }
                    else
                    {
                        throw new Exception(String.Format(Messages.UNKNOWN_COMMAND, parsedLine[0]));
                    }
                }
                catch(Exception e)
                {
                    commands.Add(new ParseError(i, e.Message));
                }
            }

            _commands = commands;
        }
    }

}
