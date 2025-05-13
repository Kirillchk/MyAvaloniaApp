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
    List<string> GetRequestLogs = new();
    List<string> PostRequestLogs = new();
    private bool showGetLogs = true;

    public MainWindow()
    {
        InitializeComponent();
        zeros = new double[60];
        Array.Fill(zeros, 0);
        hourZeros = new double[60];
        Array.Fill(hourZeros, 0);
        
        MyChart.Series = new LiveChartsCore.ISeries[]
        {
            new LineSeries<double>
            {
                Name = "Per Minute",
                Values = zeros,
                Stroke = new SolidColorPaint(SkiaSharp.SKColors.Red) { StrokeThickness = 3 },
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0 
            },
            new LineSeries<double>
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
            UpdateFilteredLogs();
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
        ToggleLogFilterButton.Click += (s, e) => {
            showGetLogs = !showGetLogs;
            ToggleLogFilterButton.Content = showGetLogs ? "Show POST Logs" : "Show GET Logs";
            UpdateFilteredLogs();
        };
        #endregion
    }

    private void UpdateFilteredLogs()
    {
        FilteredLogOutput.Text = string.Join(Environment.NewLine + Environment.NewLine, 
            showGetLogs ? GetRequestLogs : PostRequestLogs);
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
            
            var requestLog = new System.Text.StringBuilder();
            requestLog.AppendLine($"Incoming request #{requestId}");
            requestLog.AppendLine($"Method: {request.HttpMethod}");
            requestLog.AppendLine($"URL: {request.Url}");
            requestLog.AppendLine($"Headers:");
            foreach (string header in request.Headers)
            {
                requestLog.AppendLine($"  {header}: {request.Headers[header]}");
            }

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
                            requestLog.AppendLine($"Request body:");
                            requestLog.AppendLine(requestBody);
                        }
                    }
                } 
                catch (Exception ex) {
                    requestLog.AppendLine($"Error reading request body: {ex.Message}");
                }
            }

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentLength64 = buffer.Length;
            
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
            time.Stop();
            ElapsedList.Add(time.ElapsedMilliseconds);
            AvgResponseTimeText.Text = ElapsedList.Average().ToString() + "ms";

            requestLog.AppendLine($"Processing time: {time.ElapsedMilliseconds}ms");
            requestLog.AppendLine($"Response:");
            requestLog.AppendLine(responseString);

            LogMessage(requestLog.ToString(), "SERVER");
        }
        catch (Exception ex)
        {
            var errorLog = new System.Text.StringBuilder();
            errorLog.AppendLine($"Request processing error: {ex.Message}");
            errorLog.AppendLine($"Stack trace:");
            errorLog.AppendLine(ex.StackTrace);
            LogMessage(errorLog.ToString(), "SERVER", isError: true);
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
            var logBuilder = new System.Text.StringBuilder();
            logBuilder.AppendLine($"Sending GET request to: {URL}");
            
            HttpResponseMessage response = await client.GetAsync(URL);
            response.EnsureSuccessStatusCode();
            
            string result = await response.Content.ReadAsStringAsync();
            
            AppendToResponseOutput($"GET {URL}\nStatus: {response.StatusCode}\nResponse:\n{result}\n\n");
            
            logBuilder.AppendLine($"Status code: {(int)response.StatusCode} {response.StatusCode}");
            logBuilder.AppendLine("Response headers:");
            foreach (var header in response.Headers)
            {
                logBuilder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            logBuilder.AppendLine("Response content:");
            logBuilder.AppendLine(result);

            var logEntry = logBuilder.ToString();
            GetRequestLogs.Add(logEntry);
            UpdateFilteredLogs();
            
            LogMessage(logEntry, "CLIENT");
        } 
        catch (Exception ex) {
            var errorBuilder = new System.Text.StringBuilder();
            errorBuilder.AppendLine($"GET request failed to {URL}");
            errorBuilder.AppendLine($"Error: {ex.Message}");
            errorBuilder.AppendLine("Stack trace:");
            errorBuilder.AppendLine(ex.StackTrace);

            string errorMessage = errorBuilder.ToString();
            AppendToResponseOutput(errorMessage + "\n\n");
            
            GetRequestLogs.Add(errorMessage);
            UpdateFilteredLogs();
            
            LogMessage(errorMessage, "CLIENT", isError: true);
        }
    }
    
    async Task SendRequestPOST(string URL){
        try {
            var logBuilder = new System.Text.StringBuilder();
            var requestBody = RequestJsonInput.Text ?? "{}";
            logBuilder.AppendLine($"Sending POST request to: {URL}");
            logBuilder.AppendLine("Request body:");
            logBuilder.AppendLine(requestBody);
            
            var jsonDoc = JsonDocument.Parse(requestBody);
            HttpResponseMessage response = await client.PostAsJsonAsync(URL, jsonDoc);
            
            string responseContent = await response.Content.ReadAsStringAsync();
            
            AppendToResponseOutput($"POST {URL}\nStatus: {response.StatusCode}\nResponse:\n{responseContent}\n\n");
            
            logBuilder.AppendLine($"Status code: {(int)response.StatusCode} {response.StatusCode}");
            logBuilder.AppendLine("Response headers:");
            foreach (var header in response.Headers)
            {
                logBuilder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            logBuilder.AppendLine("Response content:");
            logBuilder.AppendLine(responseContent);

            var logEntry = logBuilder.ToString();
            PostRequestLogs.Add(logEntry);
            UpdateFilteredLogs();
            
            LogMessage(logEntry, "CLIENT");
        } 
        catch (Exception ex) {
            var errorBuilder = new System.Text.StringBuilder();
            errorBuilder.AppendLine($"POST request failed to {URL}");
            errorBuilder.AppendLine($"Error: {ex.Message}");
            errorBuilder.AppendLine("Stack trace:");
            errorBuilder.AppendLine(ex.StackTrace);

            string errorMessage = errorBuilder.ToString();
            AppendToResponseOutput(errorMessage + "\n\n");
            
            PostRequestLogs.Add(errorMessage);
            UpdateFilteredLogs();
            
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
        var logEntry = $"[{timestamp}] [{source}] {(isError ? "[ERROR] " : "")}\n{message}\n";

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
        
        chartValue = 0;
    }
    #endregion
}