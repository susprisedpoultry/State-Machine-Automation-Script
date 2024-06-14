namespace IngameScript
{
    class Messages {

        // Parsing Errors
        public static readonly string MISSING_FUNCTION = "Missing function";
        public static readonly string NO_COMMAND = "No Command specified";
        public static readonly string UNKNOWN_COMMAND = "Unknown command {0}";
        public static readonly string UNKNOWN_FUNCTION = "Unknown function {0}";
        public static readonly string MISMATCHED_PAR = "Mismatched or missing parentheses in statement";
        public static readonly string UNEXPECTED_PAR_IN_FUNC = "Unexpected # of parameters for function : {0}";
        public static readonly string MISSING_PARAMETER_IN_FUNC = "Missing Parameter '{0}' for function '{1}'";
        public static readonly string INVALID_PARAMETER = "Invalid parameter for {0} : {1}";
        public static readonly string UNEXPECTED_FUNCTION = "Unexpected function for Action: {0} at Line {1}";

        // Configuration errors
        public static readonly string BLOCK_NOT_FOUND = "Block not found command {0}";
        public static readonly string SCREEN_NOT_FOUND = "Unable to get screen #{0} from block {1}";
        public static readonly string STATE_NOT_FOUND = "Can't Transition to '{0}' state not found";
        public static readonly string CMD_NOT_VALID_IN_STATE = "Command {0} not valid in state {1}";
        public static readonly string PROP_NOT_FOUND ="Property '{0}' not found for block {1}";


    }        
}