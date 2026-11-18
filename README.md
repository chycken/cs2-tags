# cs2-tags (Updated version)

A tag plugin designed to enhance your CS2 experience with a dynamic tagging system. Customise and manage player tags effortlessly for a more interactive and engaging game environment.

If you want to donate or need help with the plugin, you can contact me via Discord DM or server.

Original plugin developer: **schwarper**

## Credits

- [Hextags plugin for CSGO](https://github.com/Hexer10/HexTags)
- [@daffyyyy](https://github.com/daffyyyy/)
- [@Yarukon](https://github.com/Yarukon)

## Colors

**Please do not use Default, use White instead.**

```text
White
TeamColor
DarkRed
Green
LightYellow
LightBlue
Olive
Lime
Red
LightPurple
Purple
Grey
Yellow
Gold
Silver
Blue
DarkBlue
BlueGrey
Magenta
LightRed
Orange
```

## Example Configuration

Example `TOML` configuration for team chat prefixes and permission-based tags. Replace the example roles with permission groups configured on your server.

```toml
[Tags.Settings]
Tag = "[red][Tags] "
DeadName = "☠"

NonePrefixName = ""
TPrefixName = ""
CTPrefixName = ""
SpecPrefixName = ""

NoneTeamChatName = ""
SpecTeamChatName = "[purple](SPEC) "
TTeamChatName = "[orange](T) "
CTTeamChatName = "[blue](CT) "

[Tags.Commands]
TagsReload = ["tags_reload"]
Visibility = ["tags_toggletags", "tags_visibility"]

[Tags.Default]
ScoreTag = ""
ChatTag = ""
ChatColor = "[white]"
NameColor = "[teamcolor]"
ChatSound = false

# Admin tag
[[Tags.Tags]]
Role = "admin"
ScoreTag = ""
ChatTag = "[white][ [lightred]Admin [white]] "
ChatColor = "[white]"
NameColor = "[olive]"
ChatSound = false

# SuperVIP tag
[[Tags.Tags]]
Role = "supervip"
ScoreTag = "[SuperVIP] "
ChatTag = "[white][ [gold]SuperVIP [white]] "
ChatColor = "[white]"
NameColor = "[olive]"
ChatSound = false

# VIP tag
[[Tags.Tags]]
Role = "vip"
ScoreTag = "[VIP] "
ChatTag = "[white][ [yellow]VIP [white]] "
ChatColor = "[white]"
NameColor = "[olive]"
ChatSound = false

# Supporter tag
[[Tags.Tags]]
Role = "supporter"
ScoreTag = "[SUPPORTER] "
ChatTag = "[white][ [darkblue]SUPPORTER [white]] "
ChatColor = "[white]"
NameColor = "[olive]"
ChatSound = false
```

Add more `[[Tags.Tags]]` sections to configure additional ranks.

## Screenshots

![tag1](https://github.com/user-attachments/assets/93a333b4-55e0-4582-8f09-8ec3010724d3)

![tag2](https://github.com/user-attachments/assets/9066cb2f-2b6d-4268-9db3-b824de28d05e)
