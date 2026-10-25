namespace Destock.App.Controls;

public sealed class FormEntry : ContentView
{
    public static readonly BindableProperty FieldLabelProperty = BindableProperty.Create(
        nameof(FieldLabel), typeof(string), typeof(FormEntry), string.Empty,
        propertyChanged: (bindable, _, value) =>
        {
            var control = (FormEntry)bindable;
            control._label.Text = value as string ?? string.Empty;
            control._label.IsVisible = !string.IsNullOrWhiteSpace(value as string);
        });

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(FormEntry), string.Empty,
        BindingMode.TwoWay,
        propertyChanged: (bindable, _, value) =>
        {
            var control = (FormEntry)bindable;
            var text = value as string ?? string.Empty;
            if (control._entry.Text != text)
            {
                control._entry.Text = text;
            }
        });

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(FormEntry), string.Empty,
        propertyChanged: (bindable, _, value) =>
            ((FormEntry)bindable)._entry.Placeholder = value as string ?? string.Empty);

    public static readonly BindableProperty KeyboardProperty = BindableProperty.Create(
        nameof(Keyboard), typeof(Keyboard), typeof(FormEntry), Keyboard.Default,
        propertyChanged: (bindable, _, value) =>
            ((FormEntry)bindable)._entry.Keyboard = value as Keyboard ?? Keyboard.Default);

    public static readonly BindableProperty IsPasswordProperty = BindableProperty.Create(
        nameof(IsPassword), typeof(bool), typeof(FormEntry), false,
        propertyChanged: (bindable, _, value) =>
            ((FormEntry)bindable)._entry.IsPassword = value is bool b && b);

    public static readonly BindableProperty MaxLengthProperty = BindableProperty.Create(
        nameof(MaxLength), typeof(int), typeof(FormEntry), int.MaxValue,
        propertyChanged: (bindable, _, value) =>
            ((FormEntry)bindable)._entry.MaxLength = value is int i ? i : int.MaxValue);

    private readonly Label _label;
    private readonly Entry _entry;
    private readonly Border _border;
    private bool _updating;

    public FormEntry()
    {
        _label = new Label { IsVisible = false };
        _label.SetDynamicResource(StyleProperty, "FieldLabel");

        _entry = new Entry();
        _entry.SetDynamicResource(StyleProperty, "FormEntryInput");
        _entry.TextChanged += (_, e) =>
        {
            if (_updating)
            {
                return;
            }
            _updating = true;
            Text = e.NewTextValue ?? string.Empty;
            _updating = false;
        };

        _border = new Border
        {
            Content = _entry
        };
        _border.SetDynamicResource(StyleProperty, "InputBorder");

        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Children = { _label, _border }
        };
    }

    public string FieldLabel
    {
        get => (string)GetValue(FieldLabelProperty);
        set => SetValue(FieldLabelProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public Keyboard Keyboard
    {
        get => (Keyboard)GetValue(KeyboardProperty);
        set => SetValue(KeyboardProperty, value);
    }

    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }
}
