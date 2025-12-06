# BannerPigeon Crash Analysis - Town Entry Issues (FIXED)

## Summary

After analyzing the BannerPigeon mod source code, I identified **several potential crash-causing patterns** that could trigger when entering towns. The mod hooks into both the town and castle game menus, which means code executes whenever these menus are initialized.

---

## Findings

### 1. **Null Reference in Settings Access** ⚠️ HIGH RISK

**Location:** [`PigeonPostBehavior.cs`](file:///c:/Users/Travis/Documents/GitHub/BannerPigeon/PigeonPostBehavior.cs#L166-L172)

```csharp
private bool CanUsePigeonInTown(MenuCallbackArgs args)
{
    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
    return PigeonSettings.Instance.EnableInTowns && Settlement.CurrentSettlement?.IsTown == true;
}

private bool CanUsePigeonInCastle(MenuCallbackArgs args)
{
    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
    return PigeonSettings.Instance.EnableInCastles && Settlement.CurrentSettlement?.IsCastle == true;
}
```

**Problem:** If MCM (Mod Configuration Menu) is not installed or fails to initialize, `PigeonSettings.Instance` returns `null`. This causes a `NullReferenceException` when the game evaluates menu options during town/castle entry.

**Why this triggers on town entry:** These methods are called by the game engine when rendering the town menu options.

**Fix:** Add null-check for `PigeonSettings.Instance`:
```csharp
private bool CanUsePigeonInTown(MenuCallbackArgs args)
{
    if (PigeonSettings.Instance == null) return false;
    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
    return PigeonSettings.Instance.EnableInTowns && Settlement.CurrentSettlement?.IsTown == true;
}
```

---

### 2. **Conversation Queue Processing During Menu Transitions** ⚠️ MEDIUM RISK

**Location:** [`PigeonPostBehavior.cs`](file:///c:/Users/Travis/Documents/GitHub/BannerPigeon/PigeonPostBehavior.cs#L416-L432)

```csharp
private void ProcessConversationQueue()
{
    if (_conversationQueue.Count == 0 || Campaign.Current.ConversationManager.IsConversationInProgress)
        return;

    var letter = _conversationQueue.Peek();
    _currentProcessingLetter = letter;
    // ...
    StartConversationWithLord(letter.TargetLord);
}
```

**Problem:** `ProcessConversationQueue()` is called from `OnDailyTick()`. If a daily tick fires while the player is transitioning into a town (a common occurrence), and a pigeon response is ready, the mod attempts to start a conversation exactly when the game is loading the town scene. This can conflict with the game's current state and cause crashes.

**Why this triggers on town entry:** The daily tick event doesn't wait for menu/scene transitions to complete.

---

### 3. **Unsafe Access to Settlement Properties** ⚠️ MEDIUM RISK

**Location:** [`PigeonPostBehavior.cs`](file:///c:/Users/Travis/Documents/GitHub/BannerPigeon/PigeonPostBehavior.cs#L175-L194)

```csharp
private bool CanContactSettlementOwner(MenuCallbackArgs args)
{
    var owner = Settlement.CurrentSettlement?.OwnerClan?.Leader;
    if (owner == null || owner == Hero.MainHero)
    {
        args.IsEnabled = false;
        args.Tooltip = new TextObject("This settlement has no owner or you own it.");
        return false;
    }
```

**Problem:** While there is null-coalescing (`?.`), the method still accesses properties and sets tooltip even when `args` could theoretically be in an invalid state during rapid menu transitions or save loads.

---

### 4. **LINQ on MobileParty.All During Menu Rendering** ⚠️ MEDIUM RISK

**Location:** [`PigeonPostBehavior.cs`](file:///c:/Users/Travis/Documents/GitHub/BannerPigeon/PigeonPostBehavior.cs#L231-L243)

```csharp
private bool CanContactCaravan(MenuCallbackArgs args)
{
    var caravans = MobileParty.All.Where(p => p.IsCaravan && p.Owner == Hero.MainHero && p.LeaderHero != null).ToList();
    // ...
}
```

**Problem:** Iterating over `MobileParty.All` collection during menu callback can be dangerous if the collection is being modified by another thread or if parties are being loaded/unloaded during the town entry transition. This is especially risky when loading a save game or entering a town after extended map time.

---

### 5. **Missing Try-Catch in Critical Callbacks** ⚠️ LOW-MEDIUM RISK

**Location:** All menu callback methods

**Problem:** Unlike `SubModule.OnGameStart()` which has error handling, the menu callbacks (`CanUsePigeonInTown`, `CanContactSettlementOwner`, etc.) lack try-catch blocks. Any unhandled exception bubbles up to the game engine and causes a crash.

---

## Most Likely Crash Cause

**Finding #1 (Null MCM Settings)** is the most likely crash cause for town entry issues because:

1. It executes **every single time** the player enters a town
2. It happens **before** the player even sees the menu
3. If MCM is missing, outdated, or slow to initialize, the crash is guaranteed
4. Matches the symptom of crashes immediately when entering towns

---

## Recommended Fixes

### Immediate Fix (All issues):

Add defensive coding to all menu callbacks:

```csharp
private bool CanUsePigeonInTown(MenuCallbackArgs args)
{
    try
    {
        if (PigeonSettings.Instance == null) return false;
        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
        return PigeonSettings.Instance.EnableInTowns && Settlement.CurrentSettlement?.IsTown == true;
    }
    catch
    {
        return false;
    }
}
```

### Additional Recommendations:

1. **Add MCM as a soft dependency** - Check if MCM is available before accessing settings
2. **Guard conversation starts** - Don't start conversations if a mission/scene is loading
3. **Cache caravan queries** - Don't run LINQ on `MobileParty.All` in menu callbacks
4. **Add logging** - Help users diagnose issues by logging to Bannerlord's log system

---

## How to Confirm This Mod Is the Issue

Users can test by:
1. **Disable BannerPigeon** in the launcher → If crashes stop, the mod is responsible
2. **Check MCM installation** → Ensure MCM v5.10.2+ is installed and loads before BannerPigeon
3. **Check load order** → BannerPigeon should load AFTER MCM in the launcher

---

## Questions for Bug Reports

When users report town crashes, ask:
1. Is MCM installed and what version?
2. Does it happen on every town entry or only sometimes?
3. Does it happen immediately or after a few seconds?
4. Any error messages in `C:\Users\[Username]\Documents\Mount and Blade II Bannerlord\Crashed\`?

---

## ✅ Fixes Implemented

All issues identified above have been addressed in `PigeonPostBehavior.cs`:

### Menu Callback Protection
Added try-catch blocks and null checks to these methods:
- `CanUsePigeonInTown()` - Now checks `PigeonSettings.Instance == null`
- `CanUsePigeonInCastle()` - Now checks `PigeonSettings.Instance == null`
- `CanContactSettlementOwner()` - Wrapped in try-catch
- `CanContactKingdomLeader()` - Wrapped in try-catch
- `CanContactCaravan()` - Added null check for `MobileParty.All` and individual parties

### Daily Tick Protection
- `OnDailyTick()` - Wrapped entire method in try-catch to prevent game-wide crash

### Conversation Queue Protection
- `ProcessConversationQueue()` - Added multiple safety checks:
  - `Campaign.Current?.ConversationManager == null` check
  - `CampaignMission.Current != null` check (prevents starts during scene loading)
  - `GameStateManager.Current?.ActiveState == null` check
  - `letter?.TargetLord == null` check
  - `PigeonSettings.Instance?.ShowNotifications` null-safe access

**Build verified successful** - All changes compile without errors.
