using Avalonia.Controls;

namespace MyAvaloniaApp.Views;

public partial class MainWindow : Window
{
	public MainWindow()
    {
        InitializeComponent();
        
        // Set initial view
        ShowClientView();
        
        // Setup tab button click handlers
        ClientTabButton.Click += (s, e) => ShowClientView();
        ServerTabButton.Click += (s, e) => ShowServerView();
        LogTabButton.Click += (s, e) => ShowLogView();
    }
    
    private void ShowClientView()
    {
        ClientView.IsVisible = true;
        ServerView.IsVisible = false;
        LogView.IsVisible = false;
    }
    
    private void ShowServerView()
    {
        ClientView.IsVisible = false;
        ServerView.IsVisible = true;
        LogView.IsVisible = false;
    }
    
    private void ShowLogView()
    {
        ClientView.IsVisible = false;
        ServerView.IsVisible = false;
        LogView.IsVisible = true;
    }
}