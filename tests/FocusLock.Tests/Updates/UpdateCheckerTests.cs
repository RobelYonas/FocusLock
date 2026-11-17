using System.Net;
using FocusLock.Core.Updates;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusLock.Tests.Updates;

public class UpdateCheckerTests
{
    private static readonly Version Current = new(0, 1, 0, 0);

    [Theory]
    [InlineData("v0.2.0", "0.2.0")]
    [InlineData("0.1.1", "0.1.1")]
    [InlineData("V1.0", "1.0.0")]
    public async Task Reports_a_newer_release(string tag, string expected)
    {
        var update = await Check(HttpStatusCode.OK, $$"""{"tag_name":"{{tag}}","name":"x"}""");

        Assert.NotNull(update);
        Assert.Equal(Version.Parse(expected), update.Version);
        Assert.Equal(UpdateChecker.DownloadUrl, update.DownloadUrl);
    }

    [Theory]
    [InlineData("v0.1.0")]
    [InlineData("0.1.0.0")]
    [InlineData("v0.0.9")]
    [InlineData("nightly")]
    [InlineData("")]
    public async Task Stays_quiet_for_the_same_an_older_or_an_unreadable_version(string tag)
    {
        Assert.Null(await Check(HttpStatusCode.OK, $$"""{"tag_name":"{{tag}}"}"""));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"message":"Not Found"}""")]
    [InlineData(HttpStatusCode.Forbidden, """{"message":"rate limit"}""")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, """{"tag_name":5}""")]
    public async Task Treats_errors_and_odd_answers_as_no_update(HttpStatusCode status, string body)
    {
        Assert.Null(await Check(status, body));
    }

    [Fact]
    public async Task Treats_being_offline_as_no_update()
    {
        var checker = new UpdateChecker(new HttpClient(new StubHandler(_ => throw new HttpRequestException("offline"))), NullLogger<UpdateChecker>.Instance);

        Assert.Null(await checker.CheckAsync(Current));
    }

    [Fact]
    public async Task Sends_a_User_Agent_to_GitHub()
    {
        HttpRequestMessage? sent = null;
        var checker = new UpdateChecker(
            new HttpClient(new StubHandler(request =>
            {
                sent = request;
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            })),
            NullLogger<UpdateChecker>.Instance);

        await checker.CheckAsync(Current);

        Assert.NotNull(sent);
        Assert.NotEmpty(sent.Headers.UserAgent);
        Assert.Equal("api.github.com", sent.RequestUri!.Host);
    }

    private static Task<UpdateInfo?> Check(HttpStatusCode status, string body) =>
        new UpdateChecker(
                new HttpClient(new StubHandler(_ => new HttpResponseMessage(status) { Content = new StringContent(body) })),
                NullLogger<UpdateChecker>.Instance)
            .CheckAsync(Current);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
