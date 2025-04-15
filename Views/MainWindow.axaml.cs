using System;
using System.Net;
using System.Threading;
using Avalonia.Controls;
namespace MyAvaloniaApp.Views;

public partial class MainWindow : Window
{
	public MainWindow()
    {
        InitializeComponent();
		#region AvaloniaBinds
			ClientView.IsVisible = true;
			ClientTabButton.Click += (s, e) => {
				ClientView.IsVisible = true;
				ServerView.IsVisible = false;
				LogView.IsVisible = false;
			};
			ServerTabButton.Click += (s, e) => {
				ClientView.IsVisible = false;
				ServerView.IsVisible = true;
				LogView.IsVisible = false;
			};
			LogTabButton.Click += (s, e) => {
				ClientView.IsVisible = false;
				ServerView.IsVisible = false;
				LogView.IsVisible = true;
			};
			bool isServerOnline = false;
			StartServerButton.Click += (s, e) => {
				isServerOnline = !isServerOnline;
				ServerStatusText.Text = isServerOnline?"Online":"Offline";
				StartServerButton.Content = isServerOnline?"Stop server":"Start server";
				if (isServerOnline)
					startServer();
				else 
					stopServer();
			};
		#endregion
    }
	#region Server
		volatile bool runServer = true;
		Thread? serverThread;
		HttpListener? listener;

		void startServer() {
			runServer = true;
			listener = new HttpListener();
			listener.Prefixes.Add("http://localhost:8080/");
			listener.Start();
			
			serverThread = new Thread(() => {
				while (runServer)
				{
					try {
						HttpListenerContext context = listener.GetContext();
						string response = context.Request.HttpMethod == "GET" 
							? "GET received" 
							: "SET received";
						byte[] buffer = System.Text.Encoding.UTF8.GetBytes(response);
						context.Response.ContentLength64 = buffer.Length;
						context.Response.OutputStream.Write(buffer, 0, buffer.Length);
						context.Response.Close();
					}
					catch {
						if (!runServer) break;
					}
				}
			}) { IsBackground = true };
			
			serverThread.Start();
		}

		void stopServer() {
			runServer = false;
			try {
				listener?.Stop();
			}
			catch (ObjectDisposedException){}
			serverThread?.Join();
			listener?.Close();
			listener = null;
			serverThread = null;
		}
	#endregion
}