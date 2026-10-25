using System.Collections;
using Destock.App.Models;

namespace Destock.App.Controls;

public sealed class FlowChart : GraphicsView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(FlowChart),
        null,
        propertyChanged: (bindable, _, _) => ((FlowChart)bindable).Invalidate());

    public FlowChart()
    {
        Drawable = new FlowChartDrawable(this);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    internal IReadOnlyList<MonthFlowDto> Snapshot =>
        ItemsSource?.Cast<MonthFlowDto>().ToList() ?? [];
}

internal sealed class FlowChartDrawable : IDrawable
{
    private static readonly Color InColor = Color.FromArgb("#10B981");
    private static readonly Color OutColor = Color.FromArgb("#F59E0B");
    private static readonly Color GridColor = Color.FromArgb("#8CA39A");
    private static readonly Color TextColor = Color.FromArgb("#7C8F87");

    private readonly FlowChart _chart;

    public FlowChartDrawable(FlowChart chart)
    {
        _chart = chart;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();

        var items = _chart.Snapshot;
        var width = dirtyRect.Width;
        var height = dirtyRect.Height;

        if (items.Count == 0)
        {
            canvas.FontColor = TextColor;
            canvas.FontSize = 12;
            canvas.DrawString(
                "Aucun mouvement sur la période",
                0,
                0,
                width,
                height,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
            canvas.RestoreState();
            return;
        }

        const float bottom = 24f;
        const float top = 12f;
        var chartHeight = height - bottom - top;
        var max = items.Max(item => Math.Max(item.StockIn, item.StockOut));
        max = Math.Max(max, 1);
        max *= 1.12;

        // Lignes de repère
        canvas.StrokeSize = 1;
        canvas.StrokeColor = GridColor.WithAlpha(0.18f);
        for (var i = 0; i <= 3; i++)
        {
            var y = top + chartHeight * i / 3f;
            canvas.DrawLine(0, y, width, y);
        }

        var group = width / items.Count;
        var barWidth = Math.Min(14f, group * 0.25f);
        var gap = Math.Min(5f, group * 0.05f);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var center = group * index + group / 2f;

            var inHeight = (float)(item.StockIn / max * chartHeight);
            var outHeight = (float)(item.StockOut / max * chartHeight);

            var inRect = new RectF(
                center - barWidth - gap / 2f,
                top + chartHeight - inHeight,
                barWidth,
                Math.Max(inHeight, 2f));

            var outRect = new RectF(
                center + gap / 2f,
                top + chartHeight - outHeight,
                barWidth,
                Math.Max(outHeight, 2f));

            canvas.FillColor = InColor;
            canvas.FillRoundedRectangle(inRect, 4);
            canvas.FillColor = OutColor;
            canvas.FillRoundedRectangle(outRect, 4);

            canvas.FontColor = TextColor;
            canvas.FontSize = 9.5f;
            canvas.DrawString(
                item.Label,
                group * index,
                height - bottom + 6,
                group,
                bottom - 4,
                HorizontalAlignment.Center,
                VerticalAlignment.Top);
        }

        canvas.RestoreState();
    }
}
