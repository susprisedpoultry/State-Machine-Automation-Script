namespace IngameScript
{
    class Messages {

        // Parsing Errors
        public static readonly string NO_COMMAND = "No Command specified";
        public static readonly string UNKNOWN_COMMAND = "Unknown command {0}";
        public static readonly string UNEXPECTED_PAR_IN_FUNC = "Unexpected # of parameters for function : {0}";
        public static readonly string MISSING_PARAMETER_IN_FUNC = "Missing Parameter '{0}' for function '{1}'";
        public static readonly string INVALID_PARAMETER = "Invalid parameter for {0} : {1}";
        public static readonly string UNEXPECTED_FUNCTION = "Unexpected function for Action: {0} at Line {1}";

        // Configuration errors

        public static readonly string WHEN_NOT_CLOSED = "When: command missing ThenGo: {0}";
        public static readonly string BLOCK_NOT_FOUND = "Block not found command {0}";
        public static readonly string SCREEN_NOT_FOUND = "Unable to get screen #{0} from block {1}";
        public static readonly string STATE_NOT_FOUND = "Can't Transition to '{0}' state not found";
        public static readonly string PROP_NOT_FOUND = "Property '{0}' not found for block {1}";
        public static readonly string OPTION_ONLY_VALID_AFTER_MACHINE = "Option: is only valid after Machine: and before State:";
        public static readonly string DEPRECATED_FUNCTION = "Function '{0}' is deprecated, use '{1}' instead.";
        public static readonly string ACTION_ONLY_VALID_IN_STATE = "Action '{0}' only valid inside a state";

        // Trace messages
        public static readonly string TRC_SWITCHED_STATE = "Switching to state '{0}'";
        public static readonly string TRC_ACTIONS_DONE_WAITING = "Actions done for '{0}' waiting for transition.";
        public static readonly string TRC_COMMAND_NOTHANDLED = "Command not handled : {0} in state {1}";
    }        
}