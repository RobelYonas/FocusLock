namespace FocusLock.Core.Websites;

/// <summary>
/// The built-in site presets. The hosts file has no wildcards, so every host is listed by its exact name.
/// Hosts marked "unsure" still need checking against the live site before release.
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
            "i.instagram.com", // unsure: mobile app API host, may not be needed for the web feed
        ]),
        new("youtube", "YouTube",
        [
            // music.youtube.com is left out on purpose: some people study with music.
            "youtube.com",
            "www.youtube.com",
            "m.youtube.com",
            "youtu.be",
            "i.ytimg.com", // unsure: thumbnail host, YouTube-owned
            "youtubei.googleapis.com", // unsure: YouTube's own API host; listed by exact name only, not googleapis.com
        ]),
        new("tiktok", "TikTok",
        [
            "tiktok.com",
            "www.tiktok.com",
            "m.tiktok.com",
            "vm.tiktok.com", // unsure: short share links
            "api.tiktok.com", // unsure: app API host
            "tiktokv.com", // unsure: app video API host
        ]),
        new("reddit", "Reddit",
        [
            "reddit.com",
            "www.reddit.com",
            "old.reddit.com",
            "new.reddit.com",
            "m.reddit.com",
            "redd.it", // short links
            "i.redd.it", // unsure: image host
            "v.redd.it", // unsure: video host
            "oauth.reddit.com", // unsure: app API host
        ]),
        new("x", "X (Twitter)",
        [
            "x.com",
            "www.x.com",
            "mobile.x.com", // unsure: mobile host
            "api.x.com", // unsure: API host
            "twitter.com",
            "www.twitter.com",
            "mobile.twitter.com", // unsure: mobile host
            "api.twitter.com", // unsure: API host
            "abs.twimg.com", // unsure: static assets, X-owned
            "pbs.twimg.com", // unsure: image host, X-owned
            "video.twimg.com", // unsure: video host, X-owned
        ]),
        new("facebook", "Facebook",
        [
            "facebook.com",
            "www.facebook.com",
            "m.facebook.com",
            "web.facebook.com",
            "fb.com", // unsure: redirect domain
            "fb.watch", // unsure: short video links
        ]),
        new("snapchat", "Snapchat",
        [
            "snapchat.com",
            "www.snapchat.com",
            "web.snapchat.com", // unsure: web app host
            "accounts.snapchat.com", // unsure: login host, may break sign-in only
            "story.snapchat.com", // unsure: stories host
        ]),
        new("twitch", "Twitch",
        [
            "twitch.tv",
            "www.twitch.tv",
            "m.twitch.tv",
            "player.twitch.tv", // unsure: embedded player host
            "api.twitch.tv", // unsure: API host
            "gql.twitch.tv", // unsure: GraphQL API host used by the site
            "usher.ttvnw.net", // unsure: stream playlist host, Twitch-owned
            "passport.twitch.tv", // unsure: login host
        ]),
        new("netflix", "Netflix",
        [
            "netflix.com",
            "www.netflix.com",
            "assets.nflxext.com", // unsure: site assets, Netflix-owned
            "api-global.netflix.com", // unsure: API host
        ]),
        new("discord", "Discord",
        [
            "discord.com",
            "www.discord.com",
            "discordapp.com",
            "www.discordapp.com",
            "discord.gg", // invite links
            "gateway.discord.gg", // unsure: chat gateway host
            "cdn.discordapp.com", // unsure: attachments and avatars
        ]),
    ];
}
