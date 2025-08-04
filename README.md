# The update 0.7.0+ **REQUIRES** the latest version of ScarletCore, so make sure to update it as well.

# ScarletCarrier

**ScarletCarrier** is a V Rising mod that adds a servant carrier system to help transport your items. The mod provides a convenient way to summon a personal servant that can carry items, with automatic inventory persistence between summons.

## Upcoming Features

* Additional customization options for carrier behavior and appearance

---

## Support & Donations

<a href="https://www.patreon.com/bePatron?u=30093731" data-patreon-widget-type="become-patron-button"><img height='36' style='border:0px;height:36px;' src='https://i.imgur.com/o12xEqi.png' alt='Become a Patron' /></a>  <a href='https://ko-fi.com/F2F21EWEM7' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' alt='Buy Me a Coffee at ko-fi.com' /></a>

---

## How It Works

When you summon a carrier, your personal servant appears and can store **any items** in its inventory. The carrier will remain active until you manually dismiss it, or the server restarts.

**Carrier Lifetime System:** Your carrier has a 15-day lifetime that only counts when the server is online. Every time you interact with your carrier (summon, dismiss, or use it), the timer resets. The carrier will only be destroyed if you don't interact with it for a total of 15 days of server uptime. This lifetime can be configured in the mod settings.

**⚠️ Important Warning:** When a carrier expires due to inactivity, **all items stored in its inventory will be permanently lost**. Make sure to regularly interact with your carrier to reset the timer and prevent item loss.

## Features

* **Universal item storage** - Store any items in your carrier's inventory, including weapons, armor, and instanced items
* **Smart lifetime system** - 15-day lifetime that only counts server uptime and resets with each interaction (configurable)
* **Enhanced animations** - Faster and more fluid spawn/despawn animations for better gameplay flow
* Summon a servant that can carry items
* Manual dismiss and emote controls for carrier management
* Automatic inventory persistence between summons
* Simple command interface
* Customizable carrier appearance with multiple options
* **Carrier following system** - Your carrier can follow you around or stay in place
* **Emote controls** - Use emotes for quick carrier management with seamless animation integration

## Commands

### Player Commands
* `.carrier call` / `.car c` - Summons your carrier servant
* `.carrier dismiss` / `.car d` - Dismisses your carrier early
* `.carrier follow` / `.car f` - Makes your carrier follow you around
* `.carrier stop` / `.car s` - Makes your carrier stay in place
* `.carrier list` / `.car l` - Lists all available carrier appearances
* `.carrier appearance <number>` / `.car a <number>` - Changes the appearance of your carrier servant to the specified number from the list
* `.carrier toggle emotes` / `.car te` - Toggles emote controls on/off for your carrier

### Admin Commands
* `.carrier move` / `.car m` - Allows admins to move any carrier by aiming at it and clicking to place
* `.carrier forcedismiss` / `.car fd` - Allows admins to forcefully dismiss any carrier by aiming at it
* `.carrier forcedismiss <playername>` / `.car fd <playername>` - Allows admins to forcefully dismiss a specific player's carrier by name

### Emote Controls

For quick and intuitive carrier management, you can use these emotes with seamless animation integration:

* 👍 **Yes** - Summon your carrier (original player animation disabled when emote controls are enabled)
* 👎 **No** - Dismiss your carrier (original player animation disabled when emote controls are enabled)
* 👉 **Point** - Toggle between follow/stay behavior (original player animation disabled when emote controls are enabled)

**Note:** When emote controls are enabled via `.carrier toggle emotes`, the original player animations for these specific emotes are disabled to provide a smoother experience. If you disable emote controls, the original animations will work normally.

### Appearance System

Your carrier's appearance can be customized using various predefined options. Each player can have their own unique appearance setting that persists between summons. Use `.carrier list` to see all available appearances with their corresponding numbers, then use `.carrier appearance <number>` to apply your preferred look.

## Installation

### Requirements

This mod requires the following dependencies:

* **[BepInEx](https://wiki.vrisingmods.com/user/bepinex_install.html)**
* **[ScarletCore](https://thunderstore.io/c/v-rising/p/ScarletMods/ScarletCore/)**
* **[VampireCommandFramework](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/)**

Make sure BepInEx is installed and loaded **before** installing ScarletCarrier.

### Manual Installation

1. Download the latest release of **ScarletCarrier**.

2. Extract the contents into your `BepInEx/plugins` folder:

   `<V Rising Server Directory>/BepInEx/plugins/`

   Your folder should now include:

   `BepInEx/plugins/ScarletCarrier.dll`

3. Ensure **ScarletCore** and **VampireCommandFramework** are also installed in the `plugins` folder.
4. Start or restart your server.

## This project is made possible by the contributions of the following individuals:

- **cheesasaurus, EduardoG, Helskog, Mitch, SirSaia, Odjit** & the [V Rising Mod Community on Discord](https://vrisingmods.com/discord)
