# BannerPigeon Internal Review & Planned Improvements

This document tracks potential improvements and limitations of the current BannerPigeon mod. These changes have not been applied to the codebase yet to maintain current stability.

## 1. Lord Availability Checks
**Current Behavior:**
- The mod checks if the lord is the Main Hero (player).
- It checks if the lord is alive before starting a conversation.
- It requires `lord.PartyBelongedTo != null` to start a conversation.

**Proposed Change:**
- **Prisoner Check:** Add a check to ensure the target lord is not currently a prisoner. It doesn't make sense for a prisoner to freely correspond or command a pigeon response.
- **Settlement Residents:** The `lord.PartyBelongedTo` check excludes lords who are currently staying in a settlement but not leading a party (e.g., governors, or lords resting in a keep). We should add logic to handle `lord.CurrentSettlement` to allow contacting these lords.

## 2. Response Time Logic
**Current Behavior:**
- Response time is a fixed configurable value (default 3 days).

**Proposed Change:**
- **Distance-Based Delay:** Calculate the distance between the player's current location and the target lord's location. Use this distance to calculate a dynamic response time (e.g., `Distance / Speed + ProcessingTime`). This would add realism.

## 3. Conversation Handling
**Current Behavior:**
- If multiple pigeons arrive on the same day, the code iterates through them and attempts to open a conversation for each.

**Proposed Change:**
- **Conversation Queue:** `CampaignMapConversation.OpenConversation` might conflict if called multiple times in one frame. A queue system should be implemented to trigger the next conversation only after the previous one finishes, or to consolidate messages.

## 4. Configuration Updates (v1.1.0)
- **Pigeon Speed Setting:** Added a new MCM setting `Pigeon Speed` (default 50) to control how fast pigeons travel when "Realistic Travel Time" is enabled.
- **Toggle Visibility:** Confirmed "Realistic Travel Time" toggle is present in MCM.

## 4. Economy
**Current Behavior:**
- Gold is removed from the player and disappears (`GiveGoldAction` with `null` receiver).

**Proposed Change:**
- **Settlement Economy:** Consider giving the gold to the settlement owner or the town's stash to simulate the local economy, rather than destroying the gold.

## 5. User Interface
**Current Behavior:**
- Uses standard Game Menus.

**Proposed Change:**
- **Custom UI:** A dedicated Gauntlet UI window for selecting lords (with portraits, clan banners, etc.) would be more immersive than a text list.
