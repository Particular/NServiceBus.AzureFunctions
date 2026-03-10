namespace IntegrationTest.Contracts;

public record ExceptionInfo(string FunctionName, string Type, string Message, string StackTrace);
public record InfoResult(string Version, TimeSpan Uptime);
public record MessageReceived(string MessageType, int Order, string SendingEndpoint, string ReceivingEndpoint);
public record Payload(MessageReceived[] MessagesReceived);