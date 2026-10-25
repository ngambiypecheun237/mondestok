using CommunityToolkit.Mvvm.ComponentModel;

namespace Destock.App.Models;

/// <summary>Option simple valeur/libellé utilisée par les listes déroulantes.</summary>
public sealed class ChoiceOption
{
    public string Value { get; set; } = "";

    public string Label { get; set; } = "";

    public override string ToString() => Label;
}

/// <summary>Pastille de couleur sélectionnable pour les catégories.</summary>
public sealed partial class ColorOption : ObservableObject
{
    public ColorOption(string hex, string label)
    {
        Hex = hex;
        Label = label;
    }

    public string Hex { get; }

    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;
}
