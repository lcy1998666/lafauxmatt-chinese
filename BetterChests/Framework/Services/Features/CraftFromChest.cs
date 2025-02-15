namespace StardewMods.BetterChests.Framework.Services.Features;

using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewMods.BetterChests.Framework.Interfaces;
using StardewMods.BetterChests.Framework.Models.Containers;
using StardewMods.BetterChests.Framework.Services.Factory;
using StardewMods.Common.Interfaces;
using StardewMods.Common.Services.Integrations.BetterChests.Enums;
using StardewMods.Common.Services.Integrations.BetterChests.Interfaces;
using StardewMods.Common.Services.Integrations.BetterCrafting;
using StardewMods.Common.Services.Integrations.FauxCore;
using StardewMods.Common.Services.Integrations.ToolbarIcons;
using StardewValley.Locations;
using StardewValley.Objects;

/// <summary>Craft using items from placed chests and chests in the farmer's inventory.</summary>
internal sealed class CraftFromChest : BaseFeature<CraftFromChest>
{
    private static CraftFromChest instance = null!;

    private readonly AssetHandler assetHandler;
    private readonly BetterCraftingIntegration betterCraftingIntegration;
    private readonly BetterCraftingInventoryProvider betterCraftingInventoryProvider;
    private readonly ContainerFactory containerFactory;
    private readonly IInputHelper inputHelper;
    private readonly ToolbarIconsIntegration toolbarIconsIntegration;

    /// <summary>Initializes a new instance of the <see cref="CraftFromChest" /> class.</summary>
    /// <param name="assetHandler">Dependency used for handling assets.</param>
    /// <param name="betterCraftingIntegration">Dependency for Better Crafting integration.</param>
    /// <param name="betterCraftingInventoryProvider">Dependency used for providing inventories to Better Crafting.</param>
    /// <param name="containerFactory">Dependency used for accessing containers.</param>
    /// <param name="eventManager">Dependency used for managing events.</param>
    /// <param name="inputHelper">Dependency used for checking and changing input state.</param>
    /// <param name="log">Dependency used for logging debug information to the console.</param>
    /// <param name="manifest">Dependency for accessing mod manifest.</param>
    /// <param name="modConfig">Dependency used for accessing config data.</param>
    /// <param name="toolbarIconsIntegration">Dependency for Toolbar Icons integration.</param>
    public CraftFromChest(
        AssetHandler assetHandler,
        BetterCraftingIntegration betterCraftingIntegration,
        BetterCraftingInventoryProvider betterCraftingInventoryProvider,
        ContainerFactory containerFactory,
        IEventManager eventManager,
        IInputHelper inputHelper,
        ILog log,
        IManifest manifest,
        IModConfig modConfig,
        ToolbarIconsIntegration toolbarIconsIntegration)
        : base(eventManager, log, manifest, modConfig)
    {
        CraftFromChest.instance = this;
        this.assetHandler = assetHandler;
        this.betterCraftingIntegration = betterCraftingIntegration;
        this.betterCraftingInventoryProvider = betterCraftingInventoryProvider;
        this.containerFactory = containerFactory;
        this.inputHelper = inputHelper;
        this.toolbarIconsIntegration = toolbarIconsIntegration;

        if (!this.betterCraftingIntegration.IsLoaded)
        {
            this.Log.Warn("Better Crafting is not loaded. CraftFromChest will not be active.");
        }
    }

    /// <inheritdoc />
    public override bool ShouldBeActive =>
        this.Config.DefaultOptions.CraftFromChest != RangeOption.Disabled && this.betterCraftingIntegration.IsLoaded;

    /// <inheritdoc />
    protected override void Activate()
    {
        // Events
        this.Events.Subscribe<ButtonsChangedEventArgs>(this.OnButtonsChanged);
        this.Events.Subscribe<ButtonPressedEventArgs>(this.OnButtonPressed);

        // Integrations
        if (this.betterCraftingIntegration.IsLoaded)
        {
            this.betterCraftingIntegration.Api.RegisterInventoryProvider(
                typeof(BuildingContainer),
                this.betterCraftingInventoryProvider);

            this.betterCraftingIntegration.Api.RegisterInventoryProvider(
                typeof(ChestContainer),
                this.betterCraftingInventoryProvider);

            this.betterCraftingIntegration.Api.RegisterInventoryProvider(
                typeof(ChildContainer),
                this.betterCraftingInventoryProvider);

            this.betterCraftingIntegration.Api.RegisterInventoryProvider(
                typeof(FarmerContainer),
                this.betterCraftingInventoryProvider);

            this.betterCraftingIntegration.Api.RegisterInventoryProvider(
                typeof(FridgeContainer),
                this.betterCraftingInventoryProvider);

            this.betterCraftingIntegration.Api.RegisterInventoryProvider(
                typeof(ObjectContainer),
                this.betterCraftingInventoryProvider);

            this.betterCraftingIntegration.Api.MenuPopulateContainers += this.OnMenuPopulateContainers;
        }

        if (this.toolbarIconsIntegration.IsLoaded)
        {
            this.toolbarIconsIntegration.Api.AddToolbarIcon(
                this.Id,
                this.assetHandler.Icons.Name.BaseName,
                new Rectangle(32, 0, 16, 16),
                I18n.Button_CraftFromChest_Name());

            this.toolbarIconsIntegration.Api.Subscribe(this.OnIconPressed);
        }
    }

    /// <inheritdoc />
    protected override void Deactivate()
    {
        // Events
        this.Events.Unsubscribe<ButtonsChangedEventArgs>(this.OnButtonsChanged);
        this.Events.Unsubscribe<ButtonPressedEventArgs>(this.OnButtonPressed);

        // Integrations
        if (this.betterCraftingIntegration.IsLoaded)
        {
            this.betterCraftingIntegration.Api.UnregisterInventoryProvider(typeof(BuildingContainer));
            this.betterCraftingIntegration.Api.UnregisterInventoryProvider(typeof(ChestContainer));
            this.betterCraftingIntegration.Api.UnregisterInventoryProvider(typeof(ChildContainer));
            this.betterCraftingIntegration.Api.UnregisterInventoryProvider(typeof(FarmerContainer));
            this.betterCraftingIntegration.Api.UnregisterInventoryProvider(typeof(FridgeContainer));
            this.betterCraftingIntegration.Api.UnregisterInventoryProvider(typeof(ObjectContainer));
            this.betterCraftingIntegration.Api.MenuPopulateContainers -= this.OnMenuPopulateContainers;
        }

        if (this.toolbarIconsIntegration.IsLoaded)
        {
            this.toolbarIconsIntegration.Api.RemoveToolbarIcon(this.Id);
            this.toolbarIconsIntegration.Api.Unsubscribe(this.OnIconPressed);
        }
    }

    private static bool DefaultPredicate(IStorageContainer container) =>
        container is not FarmerContainer
        && container.Options.CraftFromChest is not (RangeOption.Disabled or RangeOption.Default)
        && container.Items.Count > 0
        && !CraftFromChest.instance.Config.CraftFromChestDisableLocations.Contains(Game1.player.currentLocation.Name)
        && !(CraftFromChest.instance.Config.CraftFromChestDisableLocations.Contains("UndergroundMine")
            && Game1.player.currentLocation is MineShaft)
        && container.Options.CraftFromChest.WithinRange(
            container.Options.CraftFromChestDistance,
            container.Location,
            container.TileLocation);

    private static bool WorkbenchPredicate(IStorageContainer container) =>
        container is not FarmerContainer
        && container.Options.CraftFromChest is not RangeOption.Disabled
        && container.Items.Count > 0
        && !CraftFromChest.instance.Config.CraftFromChestDisableLocations.Contains(Game1.player.currentLocation.Name)
        && !(CraftFromChest.instance.Config.CraftFromChestDisableLocations.Contains("UndergroundMine")
            && Game1.player.currentLocation is MineShaft)
        && CraftFromChest.instance.Config.CraftFromWorkbench.WithinRange(
            CraftFromChest.instance.Config.CraftFromWorkbenchDistance,
            container.Location,
            container.TileLocation);

    private void OnButtonPressed(ButtonPressedEventArgs e)
    {
        if (this.Config.CraftFromWorkbench is RangeOption.Disabled or RangeOption.Default
            || !Context.IsPlayerFree
            || Game1.player.CurrentItem is Tool
            || !e.Button.IsUseToolButton()
            || this.inputHelper.IsSuppressed(e.Button))
        {
            return;
        }

        if (!Game1.currentLocation.Objects.TryGetValue(e.Cursor.GrabTile, out var obj) || obj is not Workbench)
        {
            return;
        }

        this.betterCraftingIntegration.Api!.OpenCraftingMenu(false, true, obj.Location, obj.TileLocation, null, false);
    }

    private void OnButtonsChanged(ButtonsChangedEventArgs e)
    {
        if (!Context.IsPlayerFree || !this.Config.Controls.OpenCrafting.JustPressed())
        {
            return;
        }

        this.inputHelper.SuppressActiveKeybinds(this.Config.Controls.OpenCrafting);
        this.betterCraftingIntegration.Api!.OpenCraftingMenu(
            false,
            true,
            Game1.player.currentLocation,
            Game1.player.Tile,
            null,
            false);
    }

    private void OnIconPressed(IIconPressedEventArgs e)
    {
        if (e.Id == this.Id)
        {
            this.betterCraftingIntegration.Api!.OpenCraftingMenu(
                false,
                true,
                Game1.player.currentLocation,
                Game1.player.Tile,
                null,
                false);
        }
    }

    private void OnMenuPopulateContainers(IPopulateContainersEvent e)
    {
        e.DisableDiscovery = true;
        var location = e.Menu.Location ?? Game1.player.currentLocation;
        var position = e.Menu.Position ?? Game1.player.Tile;

        Func<IStorageContainer, bool> predicate =
            location.Objects.TryGetValue(position, out var obj) && obj is Workbench
                ? CraftFromChest.WorkbenchPredicate
                : CraftFromChest.DefaultPredicate;

        var containers = this.containerFactory.GetAll(predicate).ToList();
        foreach (var container in containers)
        {
            e.Containers.Add(new Tuple<object, GameLocation?>(container, container.Location));
        }
    }
}