namespace FocusLock.Core.Websites;

/// <summary>
/// The built-in site presets. The hosts file has no wildcards, so every host is listed by its exact name.
/// Known gaps: video and image CDN hosts that change per user or per request (for example Netflix's
/// nflxvideo.net servers) cannot be listed, so a preset blocks the site, not every byte it streams.
/// Shared hosts (t.co, fbcdn.net, connect.facebook.net, googleapis.com in general) are left out on purpose.
/// </summary>
public sealed class SitePresetCatalog : ISitePresetCatalog
{
    public IReadOnlyList<SitePreset> All { get; } =
    [
        new("instagram", "Instagram",
        [
            "instagram.com",
            "www.instagram.com",
            "m.instagram.com",
            "i.instagram.com", //mobile app API host, may not be needed for the web feed
        ]),
        new("youtube", "YouTube",
        [
            // music.youtube.com is left out on purpose: some people study with music.
            "youtube.com",
            "www.youtube.com",
            "m.youtube.com",
            "youtu.be",
            "i.ytimg.com", //thumbnail host, YouTube-owned
            "youtubei.googleapis.com", //YouTube's own API host; listed by exact name only, not googleapis.com
        ]),
        new("tiktok", "TikTok",
        [
            "tiktok.com",
            "www.tiktok.com",
            "m.tiktok.com",
            "vm.tiktok.com", //short share links
            "api.tiktok.com", //app API host
            "tiktokv.com", //app video API host
        ]),
        new("reddit", "Reddit",
        [
            "reddit.com",
            "www.reddit.com",
            "old.reddit.com",
            "new.reddit.com",
            "m.reddit.com",
            "redd.it", // short links
            "i.redd.it", //image host
            "v.redd.it", //video host
            "oauth.reddit.com", //app API host
        ]),
        new("x", "X (Twitter)",
        [
            "x.com",
            "www.x.com",
            "mobile.x.com", //mobile host
            "api.x.com", //API host
            "twitter.com",
            "www.twitter.com",
            "mobile.twitter.com", //mobile host
            "api.twitter.com", //API host
            "abs.twimg.com", //static assets, X-owned
            "pbs.twimg.com", //image host, X-owned
            "video.twimg.com", //video host, X-owned
        ]),
        new("facebook", "Facebook",
        [
            "facebook.com",
            "www.facebook.com",
            "m.facebook.com",
            "web.facebook.com",
            "fb.com", //redirect domain
            "fb.watch", //short video links
        ]),
        new("snapchat", "Snapchat",
        [
            "snapchat.com",
            "www.snapchat.com",
            "web.snapchat.com", //web app host
            "accounts.snapchat.com", //login host, may break sign-in only
            "story.snapchat.com", //stories host
        ]),
        new("twitch", "Twitch",
        [
            "twitch.tv",
            "www.twitch.tv",
            "m.twitch.tv",
            "player.twitch.tv", //embedded player host
            "api.twitch.tv", //API host
            "gql.twitch.tv", //GraphQL API host used by the site
            "usher.ttvnw.net", //stream playlist host, Twitch-owned
            "passport.twitch.tv", //login host
        ]),
        new("netflix", "Netflix",
        [
            "netflix.com",
            "www.netflix.com",
            "assets.nflxext.com", //site assets, Netflix-owned
            "api-global.netflix.com", //API host
        ]),
        new("discord", "Discord",
        [
            "discord.com",
            "www.discord.com",
            "discordapp.com",
            "www.discordapp.com",
            "discord.gg", // invite links
            "gateway.discord.gg", //chat gateway host
            "cdn.discordapp.com", //attachments and avatars
        ]),
    ];
}
