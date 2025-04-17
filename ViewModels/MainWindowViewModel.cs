namespace MyAvaloniaApp.ViewModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

public class MainWindowViewModel : ViewModelBase
{
    // Add these properties for the chart
    public ISeries[] Series { get; set; }
    public Axis[] XAxes { get; set; }
    public Axis[] YAxes { get; set; }

    public MainWindowViewModel()
    {
        // Initialize chart data
        Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = new double[] { 2, 1, 3, 5, 3, 4, 6 },
                Fill = null,
                Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 },
                GeometrySize = 10
            }
        };

        XAxes = new Axis[]
        {
            new Axis
            {
                TextSize = 12,
                Labels = new string[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul" }
            }
        };

        YAxes = new Axis[]
        {
            new Axis
            {
                TextSize = 12,
                Labeler = value => value.ToString("N")
            }
        };
    }
}