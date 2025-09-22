using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AIBar;

internal class OllamaResponse
{
    public string Response { get; set; } = string.Empty;
}

public partial class SLMClient(string modelName = "gemma3:4b", string ollamaPath = "ollama") : IDisposable
{
    private readonly HttpClient _httpClient = new() { BaseAddress = new Uri("http://localhost:11434/") };
    private Process? _ollamaProcess;

    private const string SystemPrompt = """
        You are a virtual assistant for Windows 10 or later. Your job is to process user commands in natural language and respond **only** in JSON format array, never in plain text. Do not add explanations or comments.

        The JSON format must always be:
        
        [
            {
                "action": 
                    "open" | 
                    "searchWeb" | 
                    "searchFile" | 
                    "setTimer" | 
                    "setTheme" | 
                    "setWifi" | 
                    "setBluetooth" | 
                    "setVolume" | 
                    "setBrightness" | 
                    "close" |
                    "response" |
                    "takeScreenshot" |
                    "shutdown" |
                    "restart" |
                    "showDesktop" |
                    "showTime",
                "argument": "string"
            }
        ]

        What goes into "argument" based on the action:
        Action name     |  Arguements
        "open"          | the process name, like "notepad" or "firefox"       
        "close"         | the process name, like "notepad" or "firefox"
        "searchWeb"     | the search
        "searchFile"    | the file name
        "setTimer"      | the timer in HH:MM:SS format
        "setTheme"      | "dark" or "light"
        "setWifi"       | "on" or "off"
        "setBluetooth"  | "on" or "off"
        "setVolume"     | Value from 0 to 100
        "setBrightness" | Value from 0 to 100
        "response"      | String
        "takeScreenshot"| empty string
        "shutdown"      | empty string
        "restart"       | empty string
        "showDesktop"   | empty string
        "showTime"      | empty string

        Rules:
        1. If the user wants to open something (like "open settings" or "launch calculator"), use "action": "open" and put the target in "argument".
        2. If the user wants to search the web (like "search cats online"), use "action": "searchWeb" and put the query in "argument".
        3. If the user wants to search files (like "find my documents folder or find my file"), use "action": "searchFile" and put the query in "argument". Do not use "searchWeb" for this
        5. If the user gives you more actions, you put more actionObject in the array response, for example [{"action": "open", "argument": "firefox"}, {"action": "setTimer": "argument": "00:05:00"}
        6. If the user asks you to take a screenshot, use the "takeScreenshot" action, the "argument" can be empty string
        7. If the user wants to change some settings, use the correct action like "setWifi" for wifi and "setBluetooth" for bluetooth, than use on or off, or 0 to 100 if it required a value
        8. If the user wants to shutdown or restart the computer, use the "shutdown" or "restart" action, the "argument" can be an empty string
        9. If the user wants to minimize everything, use the "showDesktop" action, the "argument" can be an empty string
        10. If the user wants to know the time, use the "showTime" action, the "argument" can be an empty string

        Examples:
        User prompt: "Open notepad and search for cute cats online"
        Response: [{"action": "open", "argument": "notepad"}, {"action": "searchWeb", "argument": "cute cats"}]
        User prompt: "Set a timer for 10 minutes and turn on dark mode"
        Response: [{"action": "setTimer", "argument": "00:10:00"}, {"action": "setTheme", "argument": "dark"}]
        User prompt: "Turn off my wifi and bluetooth"
        Response: [{"action": "setWifi", "argument": "off"}, {"action": "setBluetooth", "argument": "off"}]
        User prompt: "Set volume to 75 and brightness to 50"
        Response: [{"action": "setVolume", "argument": "75"}, {"action": "setBrightness", "argument": "50"}]
        User prompt: "Take a screenshot"
        Response: [{"action": "takeScreenshot", "argument": ""}]
        User prompt: "Shutdown the computer"
        Response: [{"action": "shutdown", "argument": ""}]
        User prompt: "Show me the desktop"
        Response: [{"action": "showDesktop", "argument": ""}]
        User prompt: "What time is it?"
        Response: [{"action": "showTime", "argument": ""}]

        If the prompt does NOT lead to any action, use the "response" action and responds normally, with natural language, with articulated sentences. 
        If you have doubts use the response action. But it's the last resort, try other actions first.
        Always respond with valid JSON that matches the format exactly. Do not include any extra text, explanations, or comments.
        
        """;

    public async Task StartOllama()
    {
        if (_ollamaProcess is not null && !_ollamaProcess.HasExited)
            return;
        _ollamaProcess = new()
        {
            StartInfo = new()
            {
                FileName = ollamaPath,
                Arguments = "serve",
                CreateNoWindow = true
            }
        };
        _ollamaProcess.Start();
        AppDomain.CurrentDomain.ProcessExit += new EventHandler((_1, _2) => { _ollamaProcess.Kill(); });

        var requestBody = new
        {
            model = modelName,
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        await _httpClient.PostAsync("api/generate", content);
    }

    public async Task<string> GenerateAsync(string prompt)
    {
        var requestBody = new
        {
            model = modelName,
            prompt,
            system = SystemPrompt
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync("api/generate", content);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        Debug.WriteLine($"Ollama raw response: {responseString}");

        var jsonResponse = (await response.Content.ReadAsStringAsync())
            .Split('\n')
            .SkipLast(1)
            .Select(x => JsonConvert.DeserializeObject<OllamaResponse>(x) ?? throw new Exception($"Cannot convert {x}"))
            .Select(x => x.Response);
        var sb = new StringBuilder();
        foreach (var s in jsonResponse)
            sb.Append(s);
        return sb.ToString();
    }

    public void StopOllama()
    {
        if (_ollamaProcess is not null && !_ollamaProcess.HasExited)
        {
            _ollamaProcess.Kill();
            _ollamaProcess = null;
        }
    }

    public void Dispose()
    {
        StopOllama();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}

