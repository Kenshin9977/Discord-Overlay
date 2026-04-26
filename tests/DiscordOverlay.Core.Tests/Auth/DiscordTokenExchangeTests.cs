using System.Net;
using System.Text;
using DiscordOverlay.Core.Auth;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordOverlay.Core.Tests.Auth;

public class DiscordTokenExchangeTests
{
    private static readonly DiscordOAuthCredentials Credentials =
        new("CLIENT_ID", "CLIENT_SECRET", "http://localhost:3000/callback");

    [Fact]
    public async Task ExchangeAuthorizationCode_PostsFormEncodedRequest_AndReturnsToken()
    {
        var (httpClient, captured) = BuildClient(HttpStatusCode.OK,
            """{"access_token":"AT","token_type":"Bearer","expires_in":604800,"refresh_token":"RT","scope":"rpc identify"}""");
        var sut = new DiscordTokenExchange(httpClient, NullLogger<DiscordTokenExchange>.Instance);

        var token = await sut.ExchangeAuthorizationCodeAsync(Credentials, "CODE");

        Assert.Equal("AT", token.AccessToken);
        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal(604800, token.ExpiresIn);
        Assert.Equal("RT", token.RefreshToken);
        Assert.Equal("rpc identify", token.Scope);

        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal(DiscordTokenExchange.TokenEndpoint, captured.Uri?.ToString());
        Assert.Equal("application/x-www-form-urlencoded", captured.ContentType);

        Assert.Contains("grant_type=authorization_code", captured.Body);
        Assert.Contains("code=CODE", captured.Body);
        Assert.Contains("client_id=CLIENT_ID", captured.Body);
        Assert.Contains("client_secret=CLIENT_SECRET", captured.Body);
        Assert.Contains("redirect_uri=http%3A%2F%2Flocalhost%3A3000%2Fcallback", captured.Body);
    }

    [Fact]
    public async Task RefreshAsync_PostsRefreshGrant()
    {
        var (httpClient, captured) = BuildClient(HttpStatusCode.OK,
            """{"access_token":"AT2","token_type":"Bearer","expires_in":3600,"refresh_token":"RT2","scope":"rpc"}""");
        var sut = new DiscordTokenExchange(httpClient, NullLogger<DiscordTokenExchange>.Instance);

        var token = await sut.RefreshAsync(Credentials, "OLD_RT");

        Assert.Equal("AT2", token.AccessToken);
        Assert.Contains("grant_type=refresh_token", captured.Body);
        Assert.Contains("refresh_token=OLD_RT", captured.Body);
    }

    [Fact]
    public async Task ExchangeAuthorizationCode_OnError_ThrowsWithParsedFields()
    {
        var (httpClient, _) = BuildClient(HttpStatusCode.BadRequest,
            """{"error":"invalid_grant","error_description":"Invalid authorization code"}""");
        var sut = new DiscordTokenExchange(httpClient, NullLogger<DiscordTokenExchange>.Instance);

        var ex = await Assert.ThrowsAsync<DiscordOAuthException>(
            () => sut.ExchangeAuthorizationCodeAsync(Credentials, "BAD_CODE"));

        Assert.Equal("invalid_grant", ex.ErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("Invalid authorization code", ex.Message);
    }

    [Fact]
    public async Task ExchangeAuthorizationCode_OnNonJsonError_StillThrowsWithStatus()
    {
        var (httpClient, _) = BuildClient(HttpStatusCode.InternalServerError, "<html>oops</html>");
        var sut = new DiscordTokenExchange(httpClient, NullLogger<DiscordTokenExchange>.Instance);

        var ex = await Assert.ThrowsAsync<DiscordOAuthException>(
            () => sut.ExchangeAuthorizationCodeAsync(Credentials, "CODE"));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    private static (HttpClient Client, RequestCapture Capture) BuildClient(HttpStatusCode status, string responseBody)
    {
        var capture = new RequestCapture();
        var handler = new FakeHandler(async (req, _) =>
        {
            capture.Method = req.Method;
            capture.Uri = req.RequestUri;
            capture.ContentType = req.Content?.Headers.ContentType?.MediaType;
            capture.Body = req.Content is null ? string.Empty : await req.Content.ReadAsStringAsync();
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        });
        return (new HttpClient(handler), capture);
    }

    private sealed class RequestCapture
    {
        public HttpMethod? Method { get; set; }
        public Uri? Uri { get; set; }
        public string? ContentType { get; set; }
        public string Body { get; set; } = string.Empty;
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
