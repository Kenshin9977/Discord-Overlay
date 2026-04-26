using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordOverlay.Core.Discord;

internal sealed class DiscordIpcMessage
{
    [JsonPropertyName("cmd")] public string? Cmd { get; init; }
    [JsonPropertyName("nonce")] public string? Nonce { get; init; }
    [JsonPropertyName("evt")] public string? Evt { get; init; }
    [JsonPropertyName("data")] public JsonElement? Data { get; init; }
}

internal sealed class DiscordIpcRequest
{
    [JsonPropertyName("cmd")] public required string Cmd { get; init; }
    [JsonPropertyName("nonce")] public required string Nonce { get; init; }
    [JsonPropertyName("evt")] public string? Evt { get; init; }
    [JsonPropertyName("args")] public object? Args { get; init; }
}

internal sealed class DiscordIpcHandshake
{
    [JsonPropertyName("v")] public required int Version { get; init; }
    [JsonPropertyName("client_id")] public required string ClientId { get; init; }
}

internal sealed class DiscordAuthorizeArgs
{
    [JsonPropertyName("client_id")] public required string ClientId { get; init; }
    [JsonPropertyName("scopes")] public required IReadOnlyList<string> Scopes { get; init; }
    [JsonPropertyName("prompt")] public string? Prompt { get; init; }
}

internal sealed class DiscordAuthenticateArgs
{
    [JsonPropertyName("access_token")] public required string AccessToken { get; init; }
}

public sealed class DiscordRpcUser
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("username")] public required string Username { get; init; }
    [JsonPropertyName("discriminator")] public string? Discriminator { get; init; }
    [JsonPropertyName("global_name")] public string? GlobalName { get; init; }
    [JsonPropertyName("avatar")] public string? Avatar { get; init; }
}

public sealed class DiscordAuthenticateResult
{
    [JsonPropertyName("user")] public required DiscordRpcUser User { get; init; }
    [JsonPropertyName("scopes")] public IReadOnlyList<string>? Scopes { get; init; }
    [JsonPropertyName("expires")] public string? Expires { get; init; }
}

public sealed class DiscordVoiceChannelSelectedEventArgs : EventArgs
{
    public required string? ChannelId { get; init; }
    public required string? GuildId { get; init; }
}

public sealed class DiscordRpcVoiceChannel
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("guild_id")] public string? GuildId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("type")] public int Type { get; init; }
}
