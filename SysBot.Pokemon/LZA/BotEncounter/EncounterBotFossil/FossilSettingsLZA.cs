using System.ComponentModel;

namespace SysBot.Pokemon;

public class FossilSettingsLZA
{
    private const string Fossil = nameof(Fossil);
    public override string ToString() => "Fossil Bot Settings";

    [Category(Fossil), Description("Species of fossil Pokémon to hunt for.")]
    public FossilSpeciesLZA Species { get; set; } = FossilSpeciesLZA.Any;

    /// <summary>
    /// Toggle for injecting fossil pieces.
    /// </summary>
    [Category(Fossil), Description("Toggle for injecting fossil pieces.")]
    public bool InjectWhenEmpty { get; set; }
}
