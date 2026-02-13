namespace IntegrationTest.Contracts;

public record MessageReceived(string MessageType, int Order, string SendingEndpoint, string ReceivingEndpoint);