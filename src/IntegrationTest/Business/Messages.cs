namespace IntegrationTest.Business;

public record SubmitOrder : ICommand;

public record OrderSubmitted : IEvent;

public record PaymentCleared : IEvent;