w# Publishing BannerPigeon to Steam Workshop

This guide explains how to publish the BannerPigeon mod to the Mount & Blade II: Bannerlord Steam Workshop.

## Prerequisites

- **Steam Cloud must be enabled** for Mount & Blade II: Bannerlord
  - Right-click on the game in Steam Library → Properties → General
  - Or enable Steam Cloud for all games via Steam Settings → Cloud tab

## Publishing Method

**Note**: BannerPigeon contains campaign-specific code that causes the Editor to crash. You'll publish directly using the command-line tool, **skipping the Editor export step entirely**.

## Step 1: Prepare Your Module Files

Your module is already set up correctly at:
```
G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\BannerPigeon\
```

Verify it contains:
- ✅ `SubModule.xml`
- ✅ `bin\Win64_Shipping_Client\BannerPigeon.dll`
- ✅ `ModuleData\` folder (can be empty)

## Step 2: Create WorkshopCreate.xml

Create a file called `WorkshopCreate.xml` anywhere on your computer with this content:

```xml
<Tasks>
    <CreateItem/>
    <UpdateItem>
        <ModuleFolder Value="G:\SteamLibrary\steamapps\common\Mount &amp; Blade II Bannerlord\Modules\BannerPigeon"/>
        <!-- Path to your PUBLISHED module folder from Step 1 -->
        
        <ItemDescription Value="BannerPigeon adds a carrier pigeon mail system to Bannerlord. Send letters to lords from settlements for a small fee and receive responses after a few days. All costs and timing are configurable via MCM."/>
        
        <Tags> 
            <!-- Type Tags -->
            <Tag Value="Utility" />
            
            <!-- Setting Tags -->
            <Tag Value="Medieval" />
            
            <!-- Game Mode Tags -->
            <Tag Value="Singleplayer" />
            
            <!-- Compatible Version (check Steam Workshop for current version) -->
            <Tag Value="v1.2.0" />
        </Tags>
        
        <Image Value="G:\SteamLibrary\steamapps\common\Mount &amp; Blade II Bannerlord\Modules\BannerPigeon\Image.png"/>
        <!-- Path to featured image (MUST be smaller than 1 MB) -->
        
        <Visibility Value="Public"/>
        <!-- Options: Public, FriendsOnly, Private -->
    </UpdateItem>
</Tasks>
```

### Available Tags:
- **Type**: Graphical Enhancement, Map Pack, Partial Conversion, Sound, Total Conversion, Troops, UI, Utility, Weapons and Armour
- **Setting**: Native, Antiquity, Dark Ages, Medieval, Musket Era, Modern, Sci-Fi, Fantasy, Oriental, Apocalypse, Other
- **Game Mode**: Singleplayer, Multiplayer
- **Compatible Version**: Check Steam Workshop "Browse by Tag" section for current versions (e1.9.0, v1.0.0, v1.2.0, etc.)

### Important Notes:
- The `ModuleFolder` path points directly to your development module - **no export needed**
- Update `Image` path to your featured image (create one if needed, max 1 MB)
- Update `Compatible Version` tag to match current Bannerlord version
- Use `&amp;` instead of `&` in XML paths

## Step 3: Create a Featured Image (Optional but Recommended)

Create an `Image.png` file for your mod:
- Size: Recommended 256x256 or 512x512
- Format: PNG
- Max file size: **1 MB**
- Place it in: `G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\BannerPigeon\`
- Should represent your mod (e.g., carrier pigeon icon)

## Step 4: Upload to Steam Workshop

1. **Navigate to Bannerlord's Workshop tool directory**:
   ```
   G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client
   ```

2. **Open Command Prompt in this folder**:
   - Type `cmd` in the address bar and press Enter

3. **Run the upload command**:
   ```
   TaleWorlds.MountAndBlade.SteamWorkshop.exe C:\path\to\WorkshopCreate.xml
   ```
   - Replace `C:\path\to\WorkshopCreate.xml` with the actual path to your XML file

4. **Wait for upload to complete**:
   - If no errors appear, your mod has been successfully uploaded!
   - Note: You might see endless `Status: k_EItemUpdateStatusInvalid 0/0` - this may mean upload succeeded, check Steam Workshop to confirm

5. **Find your mod**:
   - Go to Steam → Community → Workshop → Mount & Blade II: Bannerlord
   - Your mod should appear in your published items

## Step 5: Testing Your Published Mod

**IMPORTANT**: To test your mod from Steam Workshop:
1. Temporarily move your development module folder:
   ```
   FROM: G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\BannerPigeon
   TO: Somewhere else (e.g., Documents\BannerPigeon_Backup)
   ```
2. Subscribe to your mod on Steam Workshop
3. The game will now load from: `Steam\steamapps\workshop\content\261550\`
4. After testing, move your development folder back

## Updating Your Mod

### Step 1: Prepare WorkshopUpdate.xml

Create `WorkshopUpdate.xml`:

```xml
<Tasks>
    <GetItem>
        <ItemId Value="YOUR_WORKSHOP_ITEM_ID_HERE"/>
        <!-- Find this in your workshop item's URL -->
    </GetItem>
    <UpdateItem>
        <ModuleFolder Value="G:\SteamLibrary\steamapps\common\Mount &amp; Blade II Bannerlord\Modules\BannerPigeon"/>
        
        <ChangeNotes Value="- Fixed save system compatibility&#xA;- Added MCM settings support&#xA;- Improved pigeon response timing" />
        <!-- Use &#xA; for line breaks in patch notes -->
        
        <Tags> 
            <Tag Value="Utility" />
            <Tag Value="Medieval" />
            <Tag Value="Singleplayer" />
            <Tag Value="v1.2.0" />
        </Tags>
    </UpdateItem>
</Tasks>
```

### Step 2: Find Your Workshop Item ID

1. Go to your mod's Steam Workshop page
2. Right-click on blank space → Copy Page URL
3. Paste the URL - the ID is the number in the URL
   - Example: `https://steamcommunity.com/sharedfiles/filedetails/?id=1234567890`
   - ID is: `1234567890`

### Step 3: Run Update Command

```
TaleWorlds.MountAndBlade.SteamWorkshop.exe C:\path\to\WorkshopUpdate.xml
```

## Troubleshooting

### Upload Fails
- Verify Steam Cloud is enabled for Bannerlord
- Check that all paths in XML use `&amp;` instead of `&`
- Ensure image is under 1 MB
- Verify the published module folder exists and contains all necessary files

### Endless Status Print
- This might indicate success - check Steam Workshop to confirm
- If upload failed, try again

### Module Not Loading
- Check SubModule.xml syntax is correct
- Verify all DLLs are in the correct folder: `bin\Win64_Shipping_Client\`
- Check Bannerlord launcher for error messages

## Post-Publishing

After publishing:
1. Add a detailed description on Steam Workshop page (editable via Steam UI)
2. Add screenshots/videos showing the mod in action
3. Monitor comments for bug reports and feedback
4. Update regularly with bug fixes and improvements

## Current BannerPigeon Configuration

- **Type**: Utility
- **Setting**: Medieval
- **Game Mode**: Singleplayer
- **Features**: Carrier pigeon mail system, MCM integration, save game compatibility
- **Dependencies**: MCM (Mod Configuration Menu) - optional but recommended
