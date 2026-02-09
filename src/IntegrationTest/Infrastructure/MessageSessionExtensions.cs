namespace IntegrationTest;

public static class MessageSessionExtensions
{
    public static Task StartTestWithMessage<T>(this IMessageSession session, string testCaseName, string destination, T message)
        where T : class
    {
        var opts = new SendOptions();
        opts.SetDestination(destination);
        opts.SetHeader("TestCaseName", testCaseName);

        return session.Send(message, opts);
    }
}