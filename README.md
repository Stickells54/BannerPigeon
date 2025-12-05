# BannerPigeon

A Mount & Blade II: Bannerlord mod that allows you to send carrier pigeons to lords from settlements to initiate conversations remotely.

## Features

- **Send Carrier Pigeons**: Contact lords without traveling to find them on the map
- **Settlement Owner Contact**: Send a pigeon to the lord who owns the current settlement
- **Kingdom Leader Contact**: Send a pigeon to the ruler of the kingdom that owns the settlement
- **Realistic Travel Time**: Optional distance-based response delays - pigeons fly across the map!
- **Conversation Queue**: Never miss a response - letters queue up if you're busy
- **Settlement Support**: Contact lords whether they're in a party or resting in a settlement
- **Prisoner Checks**: Smart validation prevents wasting pigeons on imprisoned lords
- **Economy System**: Gold cost is paid to the local settlement owner (if applicable)
- **Cost System**: Pay gold to send each pigeon (default: 50 gold)
- **MCM Integration**: Full configuration via Mod Configuration Menu
- **Save Compatible**: Uses proper save system serialization

## How to Use

1. Visit any town or castle
2. Select "Send a carrier pigeon" from the menu
3. Choose who to contact:
   - Settlement Owner
   - Kingdom Leader
4. Pay the pigeon cost
5. Wait for the response (configurable days)
6. When ready, you'll automatically enter conversation with the lord

## MCM Settings

Access settings via **ESC → Mod Options → BannerPigeon**:

- **Pigeon Cost**: Gold required to send a pigeon (10-1000, default: 50)
- **Response Time**: Days to wait for a response (1-14, default: 3)
- **Use Realistic Travel Time**: Calculate response time based on distance to the lord (default: enabled)
- **Pigeon Speed**: Fine-tune how fast pigeons fly when using realistic travel time (10-200, default: 50)
- **Enable in Towns**: Allow pigeon use in towns (default: enabled)
- **Enable in Castles**: Allow pigeon use in castles (default: enabled)
- **Show Notifications**: Display message when response arrives (default: enabled)

## Requirements

- Mount & Blade II: Bannerlord v1.2.0 or later
- Mod Configuration Menu (MCM) v5.10.2 or higher

## Installation

1. Extract to `Mount & Blade II Bannerlord\Modules\BannerPigeon\`
2. Enable "BannerPigeon" in Bannerlord Launcher
3. Load or start a campaign

## Compatibility

- Compatible with most mods
- No conflicts with diplomacy or conversation mods
- Safe to add to existing saves

## Version History

### v1.2.0 (2025-12-04)
- **Realistic Travel Time**: Optional distance-based response delays
- **Pigeon Speed Setting**: Fine-tune pigeon flight speed (10-200 map units/day)
- **Conversation Queue System**: Multiple responses queue up instead of conflicting
- **Settlement Support**: Contact lords resting in settlements (not just mobile parties)
- **Prisoner Validation**: Prevents sending pigeons to imprisoned lords
- **Economy Improvement**: Pigeon cost now goes to the settlement owner
- **Bug Fixes**: Improved conversation handling and state management

### v1.1.0 (2025-12-04)
- Added realistic travel time calculations
- Added conversation queue system
- Added settlement party support
- Added prisoner checks
- Improved economy system

### v1.0.0 (2025-12-03)
- Initial release
- Carrier pigeon system
- MCM integration
- Configurable costs and response times

## License

Free to use and modify for personal use.
