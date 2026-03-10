namespace IntegrationTest.Shared.Infrastructure;

public static class MessageSessionExtensions
{
    public static Task StartTestWithMessage<T>(this IMessageSession session, string testCaseName, T message)
        where T : class
    {
        var opts = new SendOptions();
        opts.SetHeader("TestCaseName", testCaseName);

        return session.Send(message, opts);
    }
}