namespace LarpStrap.Core.Tests;

public sealed class SingleInstanceTests
{
    private readonly string _name = $"LarpStrap-test-{Guid.NewGuid():N}";

    // The mutex is re-entrant for its owning thread, as for a second LarpStrap process: try from another thread.
    private SingleInstance? AcquireElsewhere()
    {
        SingleInstance? other = null;
        var thread = new Thread(() => other = SingleInstance.TryAcquire(_name));
        thread.Start();
        thread.Join();
        return other;
    }

    [Fact]
    public void Only_the_first_start_gets_the_instance()
    {
        using var first = SingleInstance.TryAcquire(_name);

        Assert.NotNull(first);
        Assert.Null(AcquireElsewhere());
    }

    [Fact]
    public async Task A_second_start_hands_its_message_to_the_first()
    {
        using var first = SingleInstance.TryAcquire(_name)!;
        var received = new TaskCompletionSource<string>();
        first.Listen(received.SetResult);

        Assert.True(SingleInstance.Send(_name, "roblox-player:1+launchmode:app", TimeSpan.FromSeconds(5)));

        Assert.Equal("roblox-player:1+launchmode:app", await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Oversized_messages_are_dropped_and_listening_continues()
    {
        using var first = SingleInstance.TryAcquire(_name)!;
        var received = new TaskCompletionSource<string>();
        first.Listen(received.SetResult);

        Assert.True(SingleInstance.Send(_name, new string('a', SingleInstance.MaxMessageBytes + 1), TimeSpan.FromSeconds(5)));
        Assert.True(SingleInstance.Send(_name, "ok", TimeSpan.FromSeconds(5)));

        Assert.Equal("ok", await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Sending_with_nobody_listening_fails_fast() =>
        Assert.False(SingleInstance.Send(_name, "x", TimeSpan.FromMilliseconds(200)));
}
