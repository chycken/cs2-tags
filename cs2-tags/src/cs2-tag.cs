using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.NetMessages;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.ProtobufDefinitions;
using TagsApi;
using Tomlyn.Extensions.Configuration;
using static SwiftlyS2.Shared.Helper;
using static Tags.TagExtensions;
using static TagsApi.Tags;

namespace Tags;

[PluginMetadata(Id = "Tags", Version = "v1", Name = "Tags", Author = "schwarper")]
public sealed class Tags(ISwiftlyCore core) : BasePlugin(core)
{
    public static ISwiftlyCore Instance { get; set; } = null!;
    public static readonly Dictionary<ulong, Tag> PlayerTagsList = [];
    public static readonly Dictionary<ulong, DateTime> PlayerJoinUtc = [];
    public static readonly TagsAPI Api = new();
    public static Config Config { get; set; } = null!;

    // Warmup window for async permission loaders (ShopCore, etc.)
    private static readonly TimeSpan PermissionWarmupWindow = TimeSpan.FromSeconds(40);

    // ULTRA-LITE: only 3 attempts, fixed delays (no 200x recursion)
    private static readonly float[] ApplyDelaysSeconds = new float[] { 0.0f, 1.0f, 6.0f };

    // Prebuilt indexes to avoid scanning Config.Tags constantly
    private static readonly Dictionary<ulong, Tag> _steamIdIndex = new();
    private static readonly List<RoleTagEntry> _roleIndex = new();

    public readonly record struct RoleTagEntry(string Role, Tag Tag);

    public override void Load(bool hotReload)
    {
        Instance = Core;

        const string ConfigFileName = "config.toml";
        const string ConfigSection = "Tags";
        Core.Configuration
            .InitializeTomlWithModel<Config>(ConfigFileName, ConfigSection)
            .Configure(cfg => cfg.AddTomlFile(
                ConfigFileName,
                optional: false,
                reloadOnChange: true));

        ServiceCollection services = new();
        services.AddSwiftly(Core)
            .AddOptionsWithValidateOnStart<Config>()
            .BindConfiguration(ConfigSection);

        var provider = services.BuildServiceProvider();
        Config = provider.GetRequiredService<IOptions<Config>>().Value;

        foreach (var command in Config.Commands.TagsReload)
            Core.Command.RegisterCommand(command, Command_Tags_Reload, true, "tags.reload");

        foreach (var command in Config.Commands.Visibility)
            Core.Command.RegisterCommand(command, Command_Visibility, true, "tags.visibility");

        Tags.Config.Settings.Init();
        BuildTagIndexes();

        // Align with permission loaders (world update)
        Core.Scheduler.NextWorldUpdate(() => ReloadTags());

        if (hotReload)
            ReloadTags();
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
        => interfaceManager.AddSharedInterface<ITagApi, TagsAPI>("Tags.Api", Api);

    public override void Unload()
    {
        PlayerTagsList.Clear();
        PlayerJoinUtc.Clear();
        _steamIdIndex.Clear();
        _roleIndex.Clear();
    }

    // Build indexes (steamid -> tag, role -> tag)
    private static void BuildTagIndexes()
    {
        _steamIdIndex.Clear();
        _roleIndex.Clear();

        foreach (var t in Config.Tags)
        {
            if (string.IsNullOrWhiteSpace(t.Role))
                continue;

            // If role is numeric steamid -> direct index
            if (ulong.TryParse(t.Role, out var sid) && sid != 0)
            {
                _steamIdIndex[sid] = t.Clone();
                continue;
            }

            // Otherwise treat it as permission role string
            _roleIndex.Add(new RoleTagEntry(t.Role, t.Clone()));
        }
    }

    // Exposed to TagExtensions (ultra-lite lookup)
    public static bool TryGetSteamIdTag(ulong steamId, out Tag tag) => _steamIdIndex.TryGetValue(steamId, out tag!);
    public static IReadOnlyList<RoleTagEntry> GetRoleTagIndex() => _roleIndex;

    public static void Command_Tags_Reload(ICommandContext context)
    {
        ReloadConfig();
        BuildTagIndexes();
        ReloadTags();
    }

    public void Command_Visibility(ICommandContext context)
    {
        if (!context.IsSentByPlayer) return;

        var player = context.Sender!;
        var localizer = Core.Translation.GetPlayerLocalizer(player);

        if (player.GetVisibility())
        {
            player.SetVisibility(false);
            context.Reply(Config.Settings.Tag.Colored() + localizer["Tags are now hidden"]);
        }
        else
        {
            player.SetVisibility(true);
            context.Reply(Config.Settings.Tag.Colored() + localizer["Tags are now visible"]);
        }
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event)
    {
        if (@event.UserIdPlayer is not IPlayer player) return HookResult.Continue;
        if (player.IsFakeClient || player.SteamID == 0) return HookResult.Continue;

        PlayerJoinUtc[player.SteamID] = DateTime.UtcNow;
        PlayerTagsList.Remove(player.SteamID);

        ScheduleApplyLite(player, force: true);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event)
    {
        if (@event.UserIdPlayer is not IPlayer player) return HookResult.Continue;

        PlayerTagsList.Remove(player.SteamID);
        PlayerJoinUtc.Remove(player.SteamID);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        if (@event.UserIdPlayer is not { } player) return HookResult.Continue;
        if (player.IsFakeClient || player.SteamID == 0) return HookResult.Continue;

        ScheduleApplyLite(player, force: false);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerTeam(EventPlayerTeam @event)
    {
        if (@event.UserIdPlayer is not IPlayer player) return HookResult.Continue;
        if (player.IsFakeClient || player.SteamID == 0) return HookResult.Continue;

        ScheduleApplyLite(player, force: true);
        return HookResult.Continue;
    }

    // ULTRA-LITE apply: 3 attempts with fixed delays, no recursion storm
    private static void ScheduleApplyLite(IPlayer player, bool force)
    {
        for (int i = 0; i < ApplyDelaysSeconds.Length; i++)
        {
            float delay = ApplyDelaysSeconds[i];

            if (delay <= 0.001f)
            {
                Instance.Scheduler.NextWorldUpdate(() => TryApplyTag(player, force));
                continue;
            }

            Instance.Scheduler.DelayBySeconds(delay, () =>
                Instance.Scheduler.NextWorldUpdate(() => TryApplyTag(player, force: true))
            );
        }
    }

    private static bool TryApplyTag(IPlayer player, bool force)
    {
        if (player == null || !player.IsValid || player.IsFakeClient || player.SteamID == 0)
            return false;

        if (PlayerJoinUtc.TryGetValue(player.SteamID, out var joinedUtc))
        {
            if ((DateTime.UtcNow - joinedUtc) <= PermissionWarmupWindow)
                force = true;
        }

        var tag = GetOrCreatePlayerTag(player, force);
        player.SetScoreTag(player.GetVisibility() ? tag.ScoreTag : Tags.Config.Default.ScoreTag);
        return true;
    }

    [ServerNetMessageHandler]
    public HookResult OnMessageSayText2(CUserMessageSayText2 msg)
    {
        if (Core.PlayerManager.GetPlayer(msg.Entityindex - 1) is not { } player)
            return HookResult.Continue;

        if (player.IsFakeClient || player.SteamID == 0)
            return HookResult.Continue;

        if (string.IsNullOrEmpty(msg.Param2))
            return HookResult.Continue;

        bool force = false;
        if (PlayerJoinUtc.TryGetValue(player.SteamID, out var joinedUtc))
        {
            if ((DateTime.UtcNow - joinedUtc) <= PermissionWarmupWindow)
                force = true;
        }

        var tag = GetOrCreatePlayerTag(player, force);

        MessageProcess messageProcess = new()
        {
            Player = player,
            Tag = !player.GetVisibility() ? Config.Default.Clone() : tag.Clone(),
            Message = msg.Param2.RemoveCurlyBraceContent(),
            PlayerName = msg.Param1,
            ChatSound = msg.Chat,
            TeamMessage = !msg.Messagename.Contains("All")
        };

        if (string.IsNullOrEmpty(messageProcess.Message))
            return HookResult.Handled;

        var hookResult = Api.MessageProcessPre(messageProcess);
        if (hookResult >= HookResult.Stop)
            return hookResult;

        string prefixname =
            player.Controller.PawnIsAlive || player.Controller.Team == Team.Spectator
                ? player.Controller.Team.PrefixName()
                : Config.Settings.DeadName;

        string teamname = messageProcess.TeamMessage ? player.Controller.Team.Name() : string.Empty;

        var playerData = messageProcess.Tag;
        var team = player.Controller.Team;

        messageProcess.PlayerName = FormatMessage(
            team,
            prefixname,
            teamname,
            playerData.ChatTag ?? string.Empty,
            playerData.NameColor ?? string.Empty,
            messageProcess.PlayerName
        );

        messageProcess.Message = FormatMessage(team, playerData.ChatColor ?? string.Empty, messageProcess.Message);

        hookResult = Api.MessageProcess(messageProcess);
        if (hookResult >= HookResult.Stop)
            return hookResult;

        msg.Messagename = $" {messageProcess.PlayerName}\u0001: {messageProcess.Message}";
        msg.Chat = playerData.ChatSound;

        Api.MessageProcessPost(messageProcess);
        return HookResult.Continue;
    }
}
