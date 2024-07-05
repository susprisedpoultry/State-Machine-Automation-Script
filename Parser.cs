using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace IngameScript
{
    public class ConnectorStates {
        public static readonly string LOCKED = "LOCKED";
        public static readonly string UNLOCKED = "UNLOCKED";
        public static readonly string READY = "READY";
    }

    public class RotorDirection {
        public static readonly string ANY = "ANY";
        public static readonly string POSITIVE = "POS";
        public static readonly string NEGATIVE = "NEG";
    }

    public class EnabledStates {
        public static readonly string ENABLED = "ENABLED";
        public static readonly string DISABLED = "DISABLED";
    }

    public class SensorStates {
        public static readonly string DETECTED = "DETECTED";
        public static readonly string UNDETECTED = "UNDETECTED";
    }

    public class BlockFoundStates {
        public static readonly string FOUND = "FOUND";
        public static readonly string NOTFOUNT = "NOTFOUND";
    }


    public class TopAttachedStates {
        public static readonly string ATTACHED = "ATTACHED";
        public static readonly string DETACHED = "DETACHED";
    }


    public class TimerTriggerType {
        public static readonly string NOW = "NOW";
        public static readonly string START = "START";
    }

    public class Axis {
        public static readonly string X = "X";
        public static readonly string Y = "Y";
        public static readonly string Z = "Z";
    }
    
    public class LCDOutputLevel {

        public static readonly string NONE = "NONE";
        public static readonly string LABEL = "LABEL";
        public static readonly string ERROR = "ERROR";
        public static readonly string STATUS = "STATUS";
        public static readonly string TRACE = "TRACE";
    }

    public class Functions {
        public static readonly string COMMAND = "Command";
        public static readonly string CONNECTORSTATE = "ConnectorState";
        public static readonly string SENSORORSTATE = "SensorState";
        public static readonly string ENABLEDSTATE = "EnabledState";
        public static readonly string MERGEDSTATE = "MergedState";
        public static readonly string FOUNDBLOCKSTATE = "FoundBlockState";
        public static readonly string BLOCKTOPSTATE = "BlockTopState";
        public static readonly string BLOCKSALIGNED = "BlocksAligned";
        public static readonly string CONECTTERMINAL = "ConnectTerminal";
        public static readonly string OUTPUTLCD = "OutputLCD";        
        public static readonly string OUTPUTLEVEL = "OutputLevel";        
        public static readonly string TRIGGERTIMER = "TriggerTimer";
        public static readonly string SETENABLED = "SetEnabled";
        public static readonly string APPLYACTION = "ApplyAction";
        public static readonly string SETANGLE = "SetAngle";
        public static readonly string SETPOSITION = "SetPosition";
        public static readonly string LOCKCONNECTOR = "LockConnector";
        public static readonly string SETVALUEFLOAT = "SetValueFloat";
        public static readonly string SETLIGHTCOLOR = "SetLightColor";
        public static readonly string WRITETOLCD = "WriteToLCD";
        public static readonly string RGB = "RGB";        
    }

    public class Parser
    {
        // Parser Keywords
        public static readonly string TK_MACHINE = "Machine";
        public static readonly string TK_STATE = "State";
        public static readonly string TK_STEP = "Step";
        public static readonly string TK_ACTION = "Action";
        public static readonly string TK_OPTION = "Option";        
        public static readonly string TK_GO = "Go";
        public static readonly string TK_WHEN = "When";
        public static readonly string TK_THENGO = "ThenGo";
        public static readonly string TK_AND = "And";

        private static readonly char COMMENT_MARKER = '#';        
        private static readonly char COMMAND_MARKER = ':';        
        private static readonly char OPEN_PAR = '(';        
        private static readonly char CLOSE_PAR = ')';        
        private static readonly char PARAM_SEPARATOR = ',';        
        private static readonly char LINE_SEPARATOR = '\n';        
        private static readonly char[] PARSING_TOKENS = {OPEN_PAR, CLOSE_PAR,PARAM_SEPARATOR};


        public static readonly string[] ROTOR_DIRECTION_CUE = { RotorDirection.ANY, 
                                                                RotorDirection.POSITIVE, 
                                                                RotorDirection.NEGATIVE};

        public static readonly string[] ENABLED_STATES = { EnabledStates.ENABLED,
                                                           EnabledStates.DISABLED};

        public static readonly string[] CONNECTOR_STATES = { ConnectorStates.LOCKED,
                                                             ConnectorStates.UNLOCKED,
                                                             ConnectorStates.READY};
        public static readonly string[] FOUND_BLOCK_STATES = { BlockFoundStates.FOUND,
                                                               BlockFoundStates.NOTFOUNT };

        public static readonly string[] TOP_ATTACHED_STATES = { TopAttachedStates.ATTACHED, 
                                                                TopAttachedStates.DETACHED };                                                               
        public static readonly string[] SENSOR_STATES = { SensorStates.DETECTED,
                                                          SensorStates.UNDETECTED};
        public static readonly string[] TIMER_TRIGER_TYPE = { TimerTriggerType.NOW, 
                                                              TimerTriggerType.START};

        public static readonly string[] WORLD_AXISES = { Axis.X, Axis.Y, Axis.Z };

        public static readonly string[] OUTPUT_LEVELS = { LCDOutputLevel.NONE,
                                                          LCDOutputLevel.LABEL,
                                                          LCDOutputLevel.ERROR,
                                                          LCDOutputLevel.STATUS,
                                                          LCDOutputLevel.TRACE};

        private List<ParsedCommand> _commands;

        public List<ParsedCommand> Commands
        {
            get { return _commands; }
        }



        public struct Color {
            public readonly int Red;
            public readonly int Green;
            public readonly int Blue;

            public Color(int red, int green, int blue ) { Red = red; Green = green; Blue = blue;}
        }

        private static string[] Tokenize(string stringToTokenize)
        {
            List<string> tokenList = new List<string>();
            StringBuilder stringToken = new StringBuilder();

            for(int i=0;i<stringToTokenize.Length;i++)
            {
                if (PARSING_TOKENS.Contains(stringToTokenize[i]))
                {
                    if (stringToken.Length != 0) 
                    {
                        tokenList.Add(stringToken.ToString().Trim());
                        stringToken = new StringBuilder();
                    }

                    tokenList.Add(stringToTokenize[i].ToString());
                }
                else
                {
                    stringToken.Append(stringToTokenize[i]);
                }
            }            

            // Add the last token if we have it
            if (stringToken.Length != 0) 
            {
                tokenList.Add(stringToken.ToString().Trim());
                stringToken = new StringBuilder();
            }

            return tokenList.ToArray();
        }

        public class ParsedParameter
        {
            public virtual new String ToString() {
                return "";
            }
        }

        public class ParsedStringParameter : ParsedParameter
        {
            private string _value;

            public string Value { get { return _value;}}
            public ParsedStringParameter(string value) { _value = value ;}
            public override string ToString() { return _value; }
        }

        public class ParsedFunctionParameter : ParsedParameter
        {
            private readonly string _name;
            private List<ParsedParameter> _parameters = new List<ParsedParameter>();

            public string Name { get { return _name; }}
            //public ParsedParameter[] Parameters { get { return _parameters.ToArray(); }}
            public int ParamCount { get { return _parameters.Count; }}

            public ParsedFunctionParameter(string name)
            {
                _name = name;
            }

            public bool IsFunctionMatch(string functionName, int maxParams)
            {
                if (Name == functionName)
                {
                    if (ParamCount > maxParams) 
                    {
                        throw new Exception(String.Format(Messages.UNEXPECTED_PAR_IN_FUNC, Name));
                    }

                    return true;
                }

                return false;
            }

            public void AddParam(ParsedParameter param) 
            {
                _parameters.Add(param);
            }

            private ParsedParameter GetParam(int paramIndex, string paramName)
            {
                if (paramIndex >= _parameters.Count())
                {
                    throw new Exception(String.Format(Messages.MISSING_PARAMETER_IN_FUNC, paramName, Name));
                } 

                return _parameters[paramIndex];
            }

            public int GetIntParam(int paramIndex, string paramName)
            {
                string rawParameter = GetStringParam(paramIndex, paramName);
                int paramValue = 0;

                if (!Int32.TryParse(rawParameter, out paramValue))
                {
                    throw new Exception(String.Format(Messages.INVALID_PARAMETER, paramName, rawParameter));
                }

                return paramValue;
            }

            public float GetFloatParam(int paramIndex, string paramName)
            {
                string rawParameter = GetStringParam(paramIndex, paramName);
                float paramValue = 0;

                if (!Single.TryParse(rawParameter, out paramValue))
                {
                    throw new Exception(String.Format(Messages.INVALID_PARAMETER, paramName, rawParameter));
                }

                return paramValue;
            }

            public Parser.Color GetColorParam(int paramIndex, string paramName)
            {
                ParsedFunctionParameter param = GetParam(paramIndex, paramName) as ParsedFunctionParameter;

                if ( (param == null) || (param.Name != Functions.RGB) )
                {
                    throw new Exception(String.Format(Messages.INVALID_PARAMETER, paramName, _parameters[paramIndex].ToString()));
                }

                return new Color(param.GetIntParam(0, "red"),
                                 param.GetIntParam(1, "green"),
                                 param.GetIntParam(2, "blue"));
            }


            public string GetStringParam(int paramIndex, string paramName)
            {
                ParsedStringParameter param = GetParam(paramIndex, paramName) as ParsedStringParameter;

                if (param == null)
                {
                    throw new Exception(String.Format(Messages.INVALID_PARAMETER, paramName, _parameters[paramIndex].ToString()));
                }

                return param.Value;
            }

            public string GetValidatedStringParam(int paramIndex, string paramName, string[] validValues)
            {
                string paramValue = GetStringParam(paramIndex, paramName);

                if (!validValues.Contains(paramValue))
                {
                    throw new Exception(String.Format(Messages.INVALID_PARAMETER, paramName, paramValue));
                }

                return paramValue;
            }


            public override string ToString()
            {
                StringBuilder newString = new StringBuilder();
                bool firstParam = true;

                newString.Append(Name);
                newString.Append("( ");

                foreach(ParsedParameter parameter in _parameters)
                {
                    if (!firstParam)
                        newString.Append(", ");

                    newString.Append(parameter.ToString());
                    firstParam=false;
                }

                newString.Append(")");

                return newString.ToString();
            }
        }


        private static ParsedFunctionParameter ParseFunction(string[] tokenizedString)
        {
            int tokenCursor = 0;

            ParsedFunctionParameter newFunction = Parser.ParseFunction(tokenizedString, ref tokenCursor);

            if (tokenCursor != tokenizedString.Length)
            {
                throw new Exception(String.Format("Unexpected value {0}", tokenizedString[tokenCursor]));
            }

            return newFunction;        
        }

        private static ParsedFunctionParameter ParseFunction(string[] tokenizedString, ref int parseCursor)
        {

            int cursorPosition = parseCursor;
            string functionName;

            // We're expecting <function name>(
            if (PARSING_TOKENS.Contains(tokenizedString[cursorPosition][0]))
            {
                throw new Exception(String.Format("Expected function name, found {0}", tokenizedString[cursorPosition][0]));
            }
            functionName = tokenizedString[cursorPosition];
            cursorPosition++;
            if (OPEN_PAR != tokenizedString[cursorPosition][0])
            {
                throw new Exception(String.Format("Expected '(', found {0}", tokenizedString[cursorPosition][0]));
            }
            cursorPosition++;

            ParsedFunctionParameter newFunctionParam = new ParsedFunctionParameter(functionName);

            while (cursorPosition < tokenizedString.Length)
            {
                // If we encounter a closed parameter, we're successful, we parsed a function
                if (CLOSE_PAR == tokenizedString[cursorPosition][0])
                {
                    // Let the parent know where they need to continue                    
                    parseCursor = cursorPosition + 1;
                    return newFunctionParam;
                }

                // If we already parsed parameters, we are expecting a comma
                if ( (newFunctionParam.ParamCount > 0) && (PARAM_SEPARATOR != tokenizedString[cursorPosition++][0]))
                {
                    throw new Exception(String.Format("Expecting {0}", PARAM_SEPARATOR));
                }

                // we can try parsing an inner function
                try 
                {
                    newFunctionParam.AddParam(ParseFunction(tokenizedString, ref cursorPosition));
                    continue;
                }
                catch (Exception)
                {
                    // Ignore this exception, we just didn't find a function
                }

                if (PARSING_TOKENS.Contains(tokenizedString[cursorPosition][0]))
                {
                    throw new Exception(String.Format("Expecting a parameter value, got {0}", tokenizedString[cursorPosition][0]));
                }

                newFunctionParam.AddParam(new ParsedStringParameter(tokenizedString[cursorPosition]));
                cursorPosition++;                
            }

            // If we get here, we are missing a parenthesis
            throw new Exception(String.Format("Expecting {0}", CLOSE_PAR));
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

            public virtual new string ToString()
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
            private readonly ParsedFunctionParameter _function;
            public ParsedFunctionParameter Function { get { return _function; }}

            public FunctionCommand(int line, string name, string functionToParse) : base(line, name)
            {
                string[] tokenizedString = Parser.Tokenize(functionToParse);

                _function = Parser.ParseFunction(tokenizedString);
            }

            public override string ToString()
            {
                StringBuilder newString = new StringBuilder();

                newString.Append(base.ToString());
                newString.Append(" : ");
                newString.Append(_function.ToString());

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
                string strippedLine = line.Split(COMMENT_MARKER)[0].Trim();
                string[] parsedLine = strippedLine.Split(COMMAND_MARKER);

                try {

                    if (parsedLine.Length != 2)
                    {
                        if (strippedLine.Length != 0)
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
                        (parsedLine[0] == TK_OPTION) || 
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
