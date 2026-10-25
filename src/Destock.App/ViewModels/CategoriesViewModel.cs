using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;

namespace Destock.App.ViewModels;

public partial class CategoriesViewModel : BaseViewModel
{
    private readonly DestockClient _client;

    public CategoriesViewModel(DestockClient client)
    {
        _client = client;
        Title = "Catégories";

        Palette.Add(new ColorOption("#10B981", "Émeraude"));
        Palette.Add(new ColorOption("#0EA5E9", "Océan"));
        Palette.Add(new ColorOption("#8B5CF6", "Violet"));
        Palette.Add(new ColorOption("#F59E0B", "Ambre"));
        Palette.Add(new ColorOption("#EF4444", "Rouge"));
        Palette.Add(new ColorOption("#EC4899", "Rose"));
        Palette.Add(new ColorOption("#14B8A6", "Turquoise"));
        Palette.Add(new ColorOption("#6366F1", "Indigo"));
        Palette.Add(new ColorOption("#84CC16", "Lime"));
        Palette.Add(new ColorOption("#64748B", "Ardoise"));
        SelectedColor = Palette[0];
    }

    public ObservableCollection<CategoryDto> Categories { get; } = [];

    public ObservableCollection<ColorOption> Palette { get; } = [];

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private ColorOption? _selectedColor;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private long _editingId;

    [ObservableProperty]
    private string _editorTitle = "Nouvelle catégorie";

    [ObservableProperty]
    private string _saveLabel = "Ajouter";

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _countLabel = "0 catégorie";

    public bool HasItems => Categories.Count > 0;

    public bool ShowEmpty => Categories.Count == 0;

    public async Task LoadAsync()
    {
        await LoadAsync(async () =>
        {
            var categories = await _client.CallAsync<List<CategoryDto>>("categories.list");

            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }

            CountLabel = categories.Count == 1 ? "1 catégorie" : $"{categories.Count} catégories";
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(ShowEmpty));
        });
        IsRefreshing = false;
    }

    partial void OnSelectedColorChanged(ColorOption? value)
    {
        foreach (var color in Palette)
        {
            color.IsSelected = value is not null &&
                               string.Equals(color.Hex, value.Hex, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private void SelectColor(ColorOption color)
    {
        SelectedColor = color;
    }

    [RelayCommand]
    private void StartNew()
    {
        ResetEditor();
    }

    [RelayCommand]
    private void EditCategory(CategoryDto category)
    {
        EditingId = category.Id;
        IsEditing = true;
        EditorTitle = "Modifier la catégorie";
        SaveLabel = "Enregistrer";
        Name = category.Name;

        var color = Palette.FirstOrDefault(item =>
                        string.Equals(item.Hex, category.Color, StringComparison.OrdinalIgnoreCase))
                    ?? Palette[0];
        SelectedColor = color;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ResetEditor();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length < 2)
            {
                throw new DestockException("VALIDATION", "Le nom de la catégorie doit contenir au moins 2 caractères.");
            }

            await _client.CallAsync<IdDto>("categories.save", new
            {
                Id = IsEditing ? EditingId : (long?)null,
                Name = Name.Trim(),
                Color = SelectedColor?.Hex ?? "#10B981"
            });

            ResetEditor();
            await LoadAsync();
        }, "Enregistrement impossible");
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(CategoryDto category)
    {
        var confirmed = await Dialogs.ConfirmAsync(
            "Supprimer la catégorie",
            $"Supprimer « {category.Name} » ? Les produits associés passeront en « Sans catégorie ».",
            "Supprimer",
            "Annuler");
        if (!confirmed)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var message = await _client.CallAsync<MessageDto>("categories.delete", new { Id = category.Id });
            if (IsEditing && EditingId == category.Id)
            {
                ResetEditor();
            }
            await Dialogs.AlertAsync("Catégorie", message.Message);
            await LoadAsync();
        }, "Suppression impossible");
    }

    private void ResetEditor()
    {
        EditingId = 0;
        IsEditing = false;
        EditorTitle = "Nouvelle catégorie";
        SaveLabel = "Ajouter";
        Name = string.Empty;
        SelectedColor = Palette[0];
    }
}
