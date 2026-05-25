using System.Windows;
using System.Windows.Input;
using ComputerBuddy.App.Models;
using ComputerBuddy.App.Services;
using ComputerBuddy.App.Services.OpenAi;

namespace ComputerBuddy.App.Windows;

public partial class ChatWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly UserSettings _settings;
    private readonly OpenAiChatClient _client = new();

    private readonly List<(string role, string content)> _messages = new()
    {
        ("system", "You are Computer Buddy, a helpful assistant living on the user's desktop. Keep answers concise.")
    };

    public ChatWindow(SettingsService settingsService, UserSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;

        InitializeComponent();
    }

    private void InputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            return;
        }

        e.Handled = true;
        SendButton_Click(sender, new RoutedEventArgs());
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var apiKey = _settingsService.GetOpenAiApiKey(_settings);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            MessagesList.Items.Add("System: No OpenAI API key set. Open Settings and paste your key.");
            MessagesList.ScrollIntoView(MessagesList.Items[^1]);
            return;
        }

        InputBox.Text = "";
        SendButton.IsEnabled = false;

        MessagesList.Items.Add($"You: {text}");
        MessagesList.ScrollIntoView(MessagesList.Items[^1]);
        _messages.Add(("user", text));

        try
        {
            var reply = await _client.SendAsync(apiKey, _settings.OpenAiModel, _messages, CancellationToken.None);
            _messages.Add(("assistant", reply));
            MessagesList.Items.Add($"Buddy: {reply}");
            MessagesList.ScrollIntoView(MessagesList.Items[^1]);
        }
        catch (Exception ex)
        {
            MessagesList.Items.Add($"System: {ex.Message}");
            MessagesList.ScrollIntoView(MessagesList.Items[^1]);
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }
}

