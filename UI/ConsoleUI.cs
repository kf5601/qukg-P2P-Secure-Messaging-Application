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
    private string? _currentRoom = null;
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
        string badge = BuildBadge(message);

        if (message.Type == MessageType.Heartbeat)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(message.TargetPeerId) && message.TargetPeerId.StartsWith("@", StringComparison.Ordinal))
        {
            Console.WriteLine($"[{formattedTimestamp}] [DM] {message.Sender}: {message.Content}{badge}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(message.TargetPeerId) && message.TargetPeerId.StartsWith("#", StringComparison.Ordinal))
        {
            Console.WriteLine($"[{formattedTimestamp}] [{message.TargetPeerId}] {message.Sender}: {message.Content}{badge}");
            return;
        }

        if (message.Content.EndsWith("has joined the conversation"))
        {
            Console.WriteLine($"[{formattedTimestamp}] {message.Sender} {message.Content}{badge}");
            return;
        }

        Console.WriteLine($"[{formattedTimestamp}] {message.Sender}: {message.Content}{badge}");
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

    // Event display
    public void DisplayRoomEvent(string roomName, string message)
    {
        Console.WriteLine($"[Room:{roomName}] {message}");
    }

    // Makes room name 
    public void SetCurrentRoom(string? roomName)
    {
        _currentRoom = roomName;
    }

    // Shows room name in prompt
    public void PrintPrompt()
    {
        string prefix = _currentRoom is not null ? $"[{_currentRoom}] " : "";
        Console.Write($"{prefix}> ");
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
        Console.WriteLine("  /listen <port>           - Start listening for peer connections");
        Console.WriteLine("  /connect <ip> <port>     - Connect to another peer");
        Console.WriteLine("  /create #room            - Create a room");
        Console.WriteLine("  /join #room              - Join a room");
        Console.WriteLine("  /leave #room             - Leave the current room");
        Console.WriteLine("  /rooms                   - List available rooms");
        Console.WriteLine("  /msg #room message       - Send a room message");
        Console.WriteLine("  /msg @peer message       - Send a direct message");
        Console.WriteLine("  /peers                   - Show discovered and connected peers");
        Console.WriteLine("  /history                 - View saved message history");
        Console.WriteLine("  /help                    - Show this help");
        Console.WriteLine("  /quit                    - Exit the application");
        Console.WriteLine("  <text>                   - Send a normal chat message");
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
        if (input.Length == 0)
        {
            return new CommandResult { IsCommand = false, Message = "Error: Empty input" };
        }
        // if input doesn't start with '/', it's a regular message
        if (input[0] != '/')
        {
            return new CommandResult { IsCommand = false, Message = input };
        }
        string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // switch case for different commands
        CommandType commandType = parts[0].ToLower() switch
        {
            "/connect" => CommandType.Connect,
            "/listen" => CommandType.Listen,
            "/create" => CommandType.RoomCreate,
            "/join" => CommandType.RoomJoin,
            "/leave" => CommandType.RoomLeave,
            "/rooms" => CommandType.RoomList,
            "/msg" => CommandType.SendMessage,
            "/peers" => CommandType.Peers,
            "/history" => CommandType.History,
            "/help" => CommandType.Help,
            "/quit" or "/exit" => CommandType.Quit,
            _ => CommandType.Unknown
        };

        if (commandType == CommandType.Connect && parts.Length != 3)
        {
            return new CommandResult { IsCommand = true, CommandType = CommandType.Unknown, Message = "Error: /connect requires 2 arguments (host and port)" };
        }

        if (commandType == CommandType.Listen && parts.Length != 2)
        {
            return new CommandResult { IsCommand = true, CommandType = CommandType.Unknown, Message = "Error: /listen requires 1 argument (port)" };
        }

        if ((commandType == CommandType.RoomCreate || commandType == CommandType.RoomJoin || commandType == CommandType.RoomLeave) && parts.Length != 2)
        {
            return new CommandResult { IsCommand = true, CommandType = CommandType.Unknown, Message = $"Error: {parts[0]} requires 1 argument (room name)" };
        }

        if ((commandType == CommandType.RoomCreate || commandType == CommandType.RoomJoin || commandType == CommandType.RoomLeave) &&
            !parts[1].StartsWith("#", StringComparison.Ordinal))
        {
            return new CommandResult { IsCommand = true, CommandType = CommandType.Unknown, Message = "Error: Room names must start with '#'" };
        }

        if (commandType == CommandType.SendMessage)
        {
            if (parts.Length < 3)
            {
                return new CommandResult { IsCommand = true, CommandType = CommandType.Unknown, Message = "Error: /msg requires a target and message text" };
            }

            string target = parts[1];
            if (!target.StartsWith("#", StringComparison.Ordinal) && !target.StartsWith("@", StringComparison.Ordinal))
            {
                return new CommandResult { IsCommand = true, CommandType = CommandType.Unknown, Message = "Error: /msg target must start with '#' or '@'" };
            }

            string message = input[(input.IndexOf(target, StringComparison.Ordinal) + target.Length)..].Trim();
            return new CommandResult
            {
                IsCommand = true,
                CommandType = CommandType.SendMessage,
                Args = new[] { target, message }
            };
        }

        return new CommandResult
        {
            IsCommand = true,
            CommandType = commandType,
            Args = parts.Skip(1).ToArray()
        };
    }

    // checks message fields for encryption badges
    private static string BuildBadge(Message message)
    {
        if (message.EncryptedContent is { Length: > 0 } && message.Signature is { Length: > 0 })
            return " [ENCRYPTED+SIGNED]";
        if (message.EncryptedContent is { Length: > 0 })
            return " [ENCRYPTED]";
        if (message.Signature is { Length: > 0 })
            return " [SIGNED]";
        return "";
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
    RoomCreate,
    RoomJoin,
    RoomLeave,
    RoomList,
    SendMessage,
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
