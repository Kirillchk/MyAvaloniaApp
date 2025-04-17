using System;
using System.Net;
using System.Threading;
using Avalonia.Controls;
using Tmds.DBus.Protocol;
namespace MyAvaloniaApp.Views;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Json;
using System.Text.Json;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MyAvaloniaApp.ViewModels;
public partial class MainWindow : Window
{
	public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
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
				if(validatePort() || isServerOnline){
					isServerOnline = !isServerOnline;
					ServerStatusText.Text = isServerOnline?"Online":"Offline";
					StartServerButton.Content = isServerOnline?"Stop server":"Start server";
					if (isServerOnline)
						startServer();
					else 
						stopServer();
				}
			};
			SendButtonGET.Click += async (s, e) => {
				await SendRequestGET(ClientUriInput.Text??"musor");
			};
			SendButtonPOST.Click += async (s, e) => {
				await SendRequestPOST(ClientUriInput.Text??"musor");
			};
		#endregion
    }
	#region Server
		HttpListener? listener;
		DateTime serverStarted;
		ulong zaprosov = 0;
		bool validatePort() 
		{
			string pattern = @"^(0|[1-9]\d{0,3}|[1-5]\d{4}|6[0-4]\d{3}|65[0-4]\d{2}|655[0-2]\d|6553[0-5])$";
			return Regex.IsMatch(PortInput.Text??"musor", pattern);
		}
		async void startServer() {
			listener = new HttpListener();
			listener.Prefixes.Add($"http://localhost:{PortInput.Text}/");
			listener?.Start();
			RequestLogOutput.Text += "Started server\n";
			serverStarted = DateTime.Now;
			try
			{
				while (listener.IsListening)
					ProcessRequest(await listener.GetContextAsync());
			}
			catch (Exception ex) {
				RequestLogOutput.Text += ex;
			}
		}
		private async void ProcessRequest(HttpListenerContext context)
		{
			try
			{
        		var request = context.Request;
				RequestLogOutput.Text += 
					$"\nMethod: {request.HttpMethod}\n" +
                    $"URL: {request.Url}\n";
				string responseString = "Not suported";
				zaprosov++;
				if (context.Request.HttpMethod == "GET") {
                	TimeSpan uptime = DateTime.Now - serverStarted; 
					responseString = "\nrequests in total:" + zaprosov;
					responseString += $"\ntotal time {uptime:hh\\:mm\\:ss}";
				} else if(context.Request.HttpMethod == "POST") {
					responseString = "\nunique id:" + zaprosov;
				}
				byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
				context.Response.ContentLength64 = buffer.Length;
				string jsonBody;
				try {
					using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
					{
						jsonBody = reader.ReadToEnd();
						RequestLogOutput.Text += jsonBody;
					}
				} catch (Exception ex) {
					RequestLogOutput.Text += ex;
				}
				await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
				context.Response.Close();
			}
			catch
			{
				context.Response.StatusCode = 500;
				context.Response.Close();
			}
		}
		void stopServer() {
			listener?.Stop();
			listener?.Close();
		}
	#endregion
	#region Client
	HttpClient client = new();
	async Task SendRequestGET(string URL){
		try {
			HttpResponseMessage responce = await client.GetAsync(URL);
			responce.EnsureSuccessStatusCode();
			string result = await responce.Content.ReadAsStringAsync();
			ResponseOutput.Text += result;
		} catch (Exception ex) {
			ResponseOutput.Text += ex;
		}
		ResponseOutput.Text += "\n";
	}
	async Task SendRequestPOST(string URL){
		try {
			var jsonDoc = JsonDocument.Parse(RequestJsonInput.Text??"musor");
			HttpResponseMessage response = await client.PostAsJsonAsync(URL, jsonDoc);
			ResponseOutput.Text += await response.Content.ReadAsStringAsync();;
		} catch (Exception ex) {
			ResponseOutput.Text += ex;
		}
		ResponseOutput.Text += "\n";
	}
	#endregion
	#region Logs
	
	#endregion
}