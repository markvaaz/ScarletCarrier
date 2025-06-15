# ScarletCarrier

A modular .NET template for creating V Rising mods with optional dependencies.

## 🚀 Quick Start

### Installation

Install the template from NuGet:

```bash
dotnet new install ScarletCarrier
```

### Usage

Create a new V Rising mod project:

```bash
# Basic mod with ScarletCore only
dotnet new scarletcarrier -n MyMod

# With VampireCommandFramework support
dotnet new scarletcarrier -n MyMod --use_vcf

# With ScarletRCON soft integration
dotnet new scarletcarrier -n MyMod --use_vcf --use_rcon_soft

# With ScarletRCON full integration
dotnet new scarletcarrier -n MyMod --use_vcf --use_rcon_hard
```

## 📦 What's Included

### Core Dependencies (Always Included)
- **ScarletCore**: Core framework for V Rising mods
- **BepInEx**: Mod framework for Unity games
- **VRising.Unhollowed.Client**: V Rising game bindings

### Optional Dependencies
- **VampireCommandFramework** (`--use_vcf`): Command system for in-game commands
- **ScarletRCON** (`--use_rcon_soft` / `--use_rcon_hard`): Remote console integration

## 🛠️ Template Options

| Option | Description | Default |
|--------|-------------|---------|
| `--use_vcf` | Include VampireCommandFramework dependency | `false` |
| `--use_rcon_soft` | Include ScarletRCON with soft integration | `false` |
| `--use_rcon_hard` | Include ScarletRCON with full integration | `false` |

## 📁 Generated Project Structure

```
MyMod/
├── MyMod.csproj           # Project file with conditional dependencies
├── MyMod.sln              # Solution file
├── Plugin.cs              # Main plugin class with conditional code
├── MyPluginInfo.cs        # Auto-generated plugin metadata
├── nuget.config           # NuGet sources (BepInEx, Samboy Feed)
└── LICENSE                # MIT License
```

## 💡 Examples

### Basic Plugin
```csharp
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("markvaaz.ScarletCore")]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} loaded!");
    }
}
```

### With VCF Commands (when `--use_vcf` is used)
```csharp
[CommandGroup("mymod")]
public class MyCommands
{
    [Command("hello", "Say hello")]
    public static void HelloCommand(CommandContext context)
    {
        context.Reply("Hello from my mod!");
    }
}
```

### With RCON Support (when `--use_rcon_*` is used)
```csharp
[RconCommandCategory("mymod")]
public class MyRconCommands
{
    [RconCommand("status", "Get mod status")]
    public static string StatusCommand()
    {
        return "Mod is running!";
    }
}
```

## 🔧 Development

### Prerequisites
- .NET 6.0 SDK or later
- V Rising Dedicated Server (for testing)

### Building
```bash
dotnet build
```

### Installation Path
The template automatically copies built mods to:
```
C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer\BepInEx\plugins
```

## 📋 Requirements

- .NET 6.0+
- V Rising Dedicated Server
- BepInEx 6.0+

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## 🔗 Links

- [V Rising](https://store.steampowered.com/app/1604030/V_Rising/)
- [BepInEx](https://github.com/BepInEx/BepInEx)
- [VampireCommandFramework](https://github.com/decaprime/VampireCommandFramework)
- [ScarletCore](https://github.com/markvaaz/ScarletCore)
