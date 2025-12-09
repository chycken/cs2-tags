using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using static TagsApi.Tags;

namespace TagsApi;

public interface ITagApi
{
    event Func<MessageProcess, HookResult>? OnMessageProcessPre;
    event Func<MessageProcess, HookResult>? OnMessageProcess;
    event Action<MessageProcess>? OnMessageProcessPost;
    event Action<IPlayer, Tag>? OnTagsUpdatedPre;
    event Action<IPlayer, Tag>? OnTagsUpdatedPost;
    void AddAttribute(IPlayer player, TagType types, TagPrePost prePost, string newValue);
    void SetAttribute(IPlayer player, TagType types, string newValue);
    string? GetAttribute(IPlayer player, TagType type);
    void ResetAttribute(IPlayer player, TagType types);
    bool GetPlayerChatSound(IPlayer player);
    void SetPlayerChatSound(IPlayer player, bool value);
    bool GetPlayerVisibility(IPlayer player);
    void SetPlayerVisibility(IPlayer player, bool value);
    void ReloadTags();
}