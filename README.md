# The update 0.7.0+ **REQUIRES** the latest version of ScarletCore, so make sure to update it as well.

# ScarletCarrier

**ScarletCarrier** is a V Rising mod that adds a servant carrier system to help transport your items. The mod provides a convenient way to summon a personal servant that can carry items, with automatic inventory persistence between summons.

---

## Support & Donations

<a href="https://www.patreon.com/bePatron?u=30093731" data-patreon-widget-type="become-patron-button"><img height='36' style='border:0px;height:36px;' src='https://i.imgur.com/o12xEqi.png' alt='Become a Patron' /></a>  <a href='https://ko-fi.com/F2F21EWEM7' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' alt='Buy Me a Coffee at ko-fi.com' /></a>

---

## How It Works

When you summon a carrier, your personal servant appears and can store items in its inventory. The carrier will remain active until you manually dismiss it or use emote controls. All items are automatically saved and will be restored when you summon your carrier again.

**Note:** Instanced items (items with unique properties like enchantments or durability) cannot be stored in the carrier for now. I'll be adding a system to handle these items in the future. *(Maybe)*

## Features

* Summon a servant that can carry items
* Manual dismiss and emote controls for carrier management
* Automatic inventory persistence between summons
* Interactive dialogue system
* Simple command interface
* Customizable carrier appearance with multiple options
* **Carrier following system** - Your carrier can follow you around or stay in place
* **Emote controls** - Use emotes for quick carrier management

## Upcoming Features

* Storage for instanced items

## Commands

* `.carrier call` - Summons your carrier servant
* `.carrier dismiss` - Dismisses your carrier early
* `.carrier follow` - Makes your carrier follow you around
* `.carrier stop` - Makes your carrier stay in place
* `.carrier list` - Lists all available carrier appearances
* `.carrier <number>` - Changes the appearance of your carrier servant to the specified number from the list

### Emote Controls

For quick and intuitive carrier management, you can use these emotes:

* 👍 **Yes** - Summon your carrier
* 👎 **No** - Dismiss your carrier
* 👉 **Point** - Toggle between follow/stay behavior

### Appearance System

Your carrier's appearance can be customized using various predefined options. Each player can have their own unique appearance setting that persists between summons. Use `.carrier list` to see all available appearances with their corresponding numbers, then use `.carrier <number>` to apply your preferred look.

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
