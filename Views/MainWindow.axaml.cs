namespace MyAvaloniaApp.Views;
using System;
using System.Net;
using Avalonia.Controls;
using System.Text.RegularExpressions;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Json;
using System.Text.Json;
using LiveChartsCore.SkiaSharpView;
using System.Linq;
using System.Collections.Generic;
using LiveChartsCore.SkiaSharpView.Painting;
using Avalonia.Threading;

public partial class MainWindow : Window
{
	List<long> ElapsedList = new();
    private DispatcherTimer timer;
    static double[] zeros;
	static double[] hourZeros;
	int chartValue = 0;
    public MainWindow()
    {
		InitializeComponent();
		zeros = new double[60];  // 60 points for minutes
		Array.Fill(zeros, 0);
		hourZeros = new double[60];  // 24 points for hours
		Array.Fill(hourZeros, 0);
		
		MyChart.Series = new LiveChartsCore.ISeries[]
		{
			new LineSeries<double>  // Minute series (60 points)
			{
				Name = "Per Minute",
				Values = zeros,
				Stroke = new SolidColorPaint(SkiaSharp.SKColors.Red) { StrokeThickness = 3 },
				Fill = null,
				GeometrySize = 0,
				LineSmoothness = 0 
			},
			new LineSeries<double>  // Hour series (24 points)
			{
				Name = "Per Hour",
				Values = hourZeros,
				Stroke = new SolidColorPaint(SkiaSharp.SKColors.Blue) { StrokeThickness = 3 },
				Fill = null,
				GeometrySize = 0,
				LineSmoothness = 0 
			}
		};
        timer = new();
        timer.Interval = TimeSpan.FromMilliseconds(1000);
        timer.Tick += (sender, e) => {
            ChangeChart();
        };
        timer.Start();
        
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
        DownloadLogButton.Click += (s, e) => {
            File.WriteAllText("logs.txt", FullLogOutput.Text);
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
        LogMessage($"Server started on port {PortInput.Text}", "SERVER");
        serverStarted = DateTime.Now;
        try
        {
            while (listener.IsListening)
                ProcessRequest(await listener.GetContextAsync());
        }
        catch (Exception ex) {
            LogMessage($"Server error: {ex.Message}", "SERVER", isError: true);
        }
    }
    
    private async void ProcessRequest(HttpListenerContext context)
    {
		var time = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var request = context.Request;
            var requestId = zaprosov + 1;
			TotalRequestsText.Text = requestId.ToString();
            
            LogMessage($"Incoming request #{requestId}", "SERVER");
            LogMessage($"Method: {request.HttpMethod}", "SERVER");
            LogMessage($"URL: {request.Url}", "SERVER");

            string responseString = "Not supported";
            zaprosov++;
            chartValue++;
            
            if (context.Request.HttpMethod == "GET") {
                TimeSpan uptime = DateTime.Now - serverStarted; 
                responseString = $"Requests in total: {zaprosov}\n";
                responseString += $"Total uptime: {uptime:hh\\:mm\\:ss}";
            } 
            else if(context.Request.HttpMethod == "POST") {
                responseString = $"Unique ID: {zaprosov}";
            }
            
            if (context.Request.HasEntityBody)
            {
                try {
                    using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                    {
                        var requestBody = await reader.ReadToEndAsync();
                        if (!string.IsNullOrEmpty(requestBody))
                        {
                            LogMessage($"Request body: {requestBody}", "SERVER");
                        }
                    }
                } 
                catch (Exception ex) {
                    LogMessage($"Error reading request body: {ex.Message}", "SERVER", isError: true);
                }
            }

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentLength64 = buffer.Length;
            
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
            time.Stop();
			ElapsedList.Add(time.ElapsedMilliseconds);
			AvgResponseTimeText.Text = ElapsedList.Average().ToString() + "ms";

            LogMessage($"Request took {time.ElapsedMilliseconds}ms to process", "SERVER");
            LogMessage($"Response sent for request #{requestId}", "SERVER");
            LogMessage($"Response: {responseString}", "SERVER");
        }
        catch (Exception ex)
        {
            LogMessage($"Request processing error: {ex.Message}", "SERVER", isError: true);
            context.Response.StatusCode = 500;
            context.Response.Close();
        }
    }
    
    void stopServer() {
        try {
            listener?.Stop();
            listener?.Close();
            LogMessage("Server stopped", "SERVER");
        }
        catch (Exception ex) {
            LogMessage($"Error stopping server: {ex.Message}", "SERVER", isError: true);
        }
    }
    #endregion

    #region Client
    HttpClient client = new();
    
    async Task SendRequestGET(string URL){
        try {
            LogMessage($"Sending GET request to: {URL}", "CLIENT");
            
            HttpResponseMessage response = await client.GetAsync(URL);
            response.EnsureSuccessStatusCode();
            
            string result = await response.Content.ReadAsStringAsync();
            
            // Update ResponseOutput
            AppendToResponseOutput($"GET {URL}\nStatus: {response.StatusCode}\nResponse:\n{result}\n\n");
            
            LogMessage($"GET response from {URL}", "CLIENT");
            LogMessage($"Status code: {response.StatusCode}", "CLIENT");
            LogMessage($"Response: {result}", "CLIENT");
        } 
        catch (Exception ex) {
            string errorMessage = $"GET request failed: {ex.Message}";
            AppendToResponseOutput(errorMessage + "\n\n");
            LogMessage(errorMessage, "CLIENT", isError: true);
        }
    }
    
    async Task SendRequestPOST(string URL){
        try {
            var requestBody = RequestJsonInput.Text ?? "{}";
            LogMessage($"Sending POST request to: {URL}", "CLIENT");
            LogMessage($"Request body: {requestBody}", "CLIENT");
            
            var jsonDoc = JsonDocument.Parse(requestBody);
            HttpResponseMessage response = await client.PostAsJsonAsync(URL, jsonDoc);
            
            string responseContent = await response.Content.ReadAsStringAsync();
            
            // Update ResponseOutput
            AppendToResponseOutput($"POST {URL}\nStatus: {response.StatusCode}\nResponse:\n{responseContent}\n\n");
            
            LogMessage($"POST response from {URL}", "CLIENT");
            LogMessage($"Status code: {response.StatusCode}", "CLIENT");
            LogMessage($"Response: {responseContent}", "CLIENT");
        } 
        catch (Exception ex) {
            string errorMessage = $"POST request failed: {ex.Message}";
            AppendToResponseOutput(errorMessage + "\n\n");
            LogMessage(errorMessage, "CLIENT", isError: true);
        }
    }

    private void AppendToResponseOutput(string text)
    {
        Dispatcher.UIThread.Post(() => 
        {
            ResponseOutput.Text += text;
        });
    }
    #endregion

    #region Logs
    
    private void LogMessage(string message, string source, bool isError = false)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{source}] {(isError ? "[ERROR] " : "")}{message}\n";

		Dispatcher.UIThread.Post(() => 
		{
			RequestLogOutput.Text += logEntry;
			FullLogOutput.Text += logEntry;

		});	
    }
    
	void ChangeChart()
	{
		DateTime now = DateTime.Now;
		bool isNewMinute = now.Second == 0;
		
		var minuteSeries = (LineSeries<double>)MyChart.Series.First(s => s.Name == "Per Minute");
		var hourSeries = (LineSeries<double>)MyChart.Series.First(s => s.Name == "Per Hour");
		
		Queue<double> minuteQueue = new((double[])minuteSeries.Values);
		minuteQueue.Dequeue();
		minuteQueue.Enqueue(chartValue);
		double[] newMinuteArray = minuteQueue.ToArray();
		
		double[] hourValues = (double[])hourSeries.Values;
		
		if (isNewMinute)
		{
			double hourAverage = newMinuteArray.Sum();
			
			for (int i = 0; i < hourValues.Length - 1; i++)
				hourValues[i] = hourValues[i + 1];
			hourValues[^1] = hourAverage;
		}
		
		MyChart.Series = new LiveChartsCore.ISeries[]
		{
			new LineSeries<double>
			{
				Name = "Per Minute",
				Values = newMinuteArray,
				Stroke = new SolidColorPaint(SkiaSharp.SKColors.Red) { StrokeThickness = 3 },
				Fill = null,
				GeometryStroke = null,
				GeometryFill = null,
				GeometrySize = 0,
				LineSmoothness = 0
			},
			new LineSeries<double>
			{
				Name = "Per Hour",
				Values = hourValues,
				Stroke = new SolidColorPaint(SkiaSharp.SKColors.Blue) { StrokeThickness = 3 },
				Fill = null,
				GeometryStroke = null,
				GeometryFill = null,
				GeometrySize = 0,
				LineSmoothness = 0
			}
		};
		
		// Reset counter for next minute
		chartValue = 0;
	}
    #endregion
}