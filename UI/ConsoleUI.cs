// Kai Fan
// CSCI 251 - Secure Distributed Messenger
//
// SPRINT 1: Threading & Basic Networking
// Due: Week 5 | Work on: Weeks 3-4
//

using SecureMessenger.Core;

namespace SecureMessenger.UI;

/// <summary>
/// Console-based user interface.
/// Handles user input parsing and message display.
///
/// Supported Commands:
/// - /connect host port name  - Connect to another messenger
/// - /listen port        - Start listening for connections
/// - /peers              - Show connection status
/// - /history            - View message history (Sprint 3)
/// - /quit or /exit      - Exit the application
/// - Any other text      - Send as a message
/// </summary>
public class ConsoleUI
{
    /// <summary>
    /// Display a received message to the console.
    ///
    /// Implement the following:
    /// 1. Format the message nicely, e.g.: "[14:30:25] Alice: Hello!"
    /// 2. Use message.Timestamp.ToString("HH:mm:ss") for time format
    /// 3. Print to console
    /// </summary>
    public void DisplayMessage(Message message)
    {
        string formattedTimestamp = message.Timestamp.ToString("HH:mm:ss");
        if (message.Content == "has joined the conversation")
        {
            Console.WriteLine($"[{formattedTimestamp}] {message.Sender} {message.Content}");
            return;
        }

        Console.WriteLine($"[{formattedTimestamp}] {message.Sender}: {message.Content}");
    }

    /// <summary>
    /// Display a system message to the console.
    /// Implement the following:
    /// 1. Print in a distinct format, e.g.: "[System] Server started on port 5000"
    /// </summary>
    public void DisplaySystem(string message)
    {
        Console.WriteLine($"[System] {message}");
    }

    /// <summary>
    /// Show available commands to the user.
    ///
    /// Implement the following:
    /// 1. Print a formatted help message showing all available commands
    /// 2. Include: /connect, /listen, /peers, /history, /quit
    /// </summary>
    public void ShowHelp()
    {
        Console.WriteLine("\nAvailable Commands:");
        Console.WriteLine("  /connect <ip> <port> <name>  - Connect to another messenger");
        Console.WriteLine("  /listen <port>        - Start listening for connections");
        Console.WriteLine("  /peers                - Show connection status");
        Console.WriteLine("  /history              - View message history (Sprint 3)");
        Console.WriteLine("  /quit or /exit        - Exit the application");
    }

    /// <summary>
    /// Parse user input and return a CommandResult.
    ///
    /// TODO: Implement the following:
    /// 1. Check if input starts with "/" - if not, it's a regular message:
    ///    - Return CommandResult with IsCommand = false, Message = input
    ///
    /// 2. If it's a command, split by spaces and parse:
    ///    - "/connect host port" -> CommandType.Connect with Args = [host, port]
    ///    - "/listen port" -> CommandType.Listen with Args = [port]
    ///    - "/peers" -> CommandType.Peers
    ///    - "/history" -> CommandType.History
    ///    - "/quit" or "/exit" -> CommandType.Quit
    ///    - "/help" -> CommandType.Help
    ///    - Unknown command -> CommandType.Unknown with error message
    ///
    /// 3. Validate arguments:
    ///    - /connect requires 2 args (host and port)
    ///    - /listen requires 1 arg (port)
    ///
    /// Hint: Use input.Split(' ', StringSplitOptions.RemoveEmptyEntries)
    /// Hint: Use a switch expression for clean command matching
    /// </summary>
    public CommandResult ParseCommand(string input)
    {
        // if empty input, return not a command with error message
        if(input.Length == 0)
        {
            return new CommandResult { IsCommand = false, Message = "Error: Empty input"};
        }
        // if input doesn't start with '/', it's a regular message
        if(input[0] != '/')
        {
            return new CommandResult { IsCommand = false, Message = input };
        }
        string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // switch case for different commands
        CommandType commandType = parts[0].ToLower() switch
        {
            "/connect" => CommandType.Connect,
            "/listen" => CommandType.Listen,
            "/peers" => CommandType.Peers,
            "/history" => CommandType.History,
            "/help" => CommandType.Help,
            "/quit" or "/exit" => CommandType.Quit,
            _ => CommandType.Unknown
        };

        // validate arguments for connect and listen commands
        if (commandType == CommandType.Connect && parts.Length != 4)
        {
            return new CommandResult { IsCommand = true, CommandType = CommandType.Unknown, Message = "Error: /connect requires 3 arguments (host, port, and name)" };
        }
        
        if (commandType == CommandType.Listen && parts.Length != 2)
        {
            return new CommandResult { IsCommand = true, CommandType = CommandType.Unknown, Message = "Error: /listen requires 1 argument (port)" };
        }

        return new CommandResult
        {
            IsCommand = true,
            CommandType = commandType,
            Args = parts.Skip(1).ToArray() 
        };
    }
}

/// <summary>
/// Types of commands the user can enter
/// bruh why is this here??? shouldn't it be at the top
/// </summary>
public enum CommandType
{
    Unknown,
    Connect,
    Listen,
    Peers,
    History,
    Help,
    Quit
}

/// <summary>
/// Result of parsing a user input line
/// </summary>
public class CommandResult
{
    /// <summary>True if the input was a command (started with /)</summary>
    public bool IsCommand { get; set; }

    /// <summary>The type of command parsed</summary>
    public CommandType CommandType { get; set; }

    /// <summary>Arguments for the command (e.g., host and port for /connect)</summary>
    public string[]? Args { get; set; }

    /// <summary>The message content (for non-commands or error messages)</summary>
    public string? Message { get; set; }
}
