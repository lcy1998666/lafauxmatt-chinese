namespace StardewMods.BetterChests.Framework.Models.StorageOptions;

using StardewMods.Common.Services.Integrations.BetterChests.Interfaces;
using StardewValley.GameData.Buildings;
using StardewValley.TokenizableStrings;

/// <inheritdoc />
internal sealed class BuildingStorageOptions : ChildStorageOptions
{
    /// <summary>Initializes a new instance of the <see cref="BuildingStorageOptions" /> class.</summary>
    /// <param name="getDefault">Get the default storage options.</param>
    /// <param name="data">The building data.</param>
    public BuildingStorageOptions(Func<IStorageOptions> getDefault, BuildingData data)
        : base(getDefault, new CustomFieldsStorageOptions(data.CustomFields)) =>
        this.Data = data;

    /// <summary>Gets the building data.</summary>
    public BuildingData Data { get; }

    /// <inheritdoc />
    public override string GetDescription() => TokenParser.ParseText(this.Data.Description);

    /// <inheritdoc />
    public override string GetDisplayName() => TokenParser.ParseText(this.Data.Name);
}