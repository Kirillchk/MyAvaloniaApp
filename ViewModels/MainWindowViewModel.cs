namespace MyAvaloniaApp.ViewModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

public class MainWindowViewModel : ViewModelBase
{
	public ISeries[] Series { get; set; } = [
        new LineSeries<double>
        {
            Values = [4, 7, 2, 9, 3, 8, 1, 5, 6, 10, 
				2, 4, 7, 3, 9, 5, 1, 6, 8, 4, 
				10, 2, 3, 7, 5, 9, 6, 1, 8, 4, 
				2, 7, 3, 5, 9, 10, 6, 1, 4, 8, 
				3, 2, 7, 5, 9, 6, 10, 1, 4, 8, 
				3, 2, 7, 5, 9, 6, 10, 1, 4, 8],
            Fill = null,
            GeometrySize = 0,
            // use the line smoothness property to control the curve
            // it goes from 0 to 1
            // where 0 is a straight line and 1 the most curved
            LineSmoothness = 0 
        }
    ];
}