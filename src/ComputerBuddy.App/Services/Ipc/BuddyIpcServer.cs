using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ComputerBuddy.App.Models;
using ComputerBuddy.App.Services.Reminders;
using ComputerBuddy.Contracts;

namespace ComputerBuddy.App.Services.Ipc;

public sealed class BuddyIpcServer : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;

    private readonly Func<UserSettings> _getSettings;
    private readonly Action<UserSettingsDto, bool> _applySettings;
    private readonly Action _toggleBuddy;
    private readonly Action _showBuddy;
    private readonly Action _hideBuddy;
    private readonly Action _exitBuddy;

    public BuddyIpcServer(
        Func<UserSettings> getSettings,
        Action<UserSettingsDto, bool> applySettings,
        Action toggleBuddy,
        Action showBuddy,
        Action hideBuddy,
        Action exitBuddy)
    {
        _getSettings = getSettings;
        _applySettings = applySettings;
        _toggleBuddy = toggleBuddy;
        _showBuddy = showBuddy;
        _hideBuddy = hideBuddy;
        _exitBuddy = exitBuddy;
    }

    public void Start()
    {
        _loopTask ??= Task.Run(LoopAsync);
    }

    public void Dispose()
    {
        _cts.Cancel();
    }

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    BuddyIpc.PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(_cts.Token);
                await HandleClientAsync(server, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
            }
        }
    }

    private async Task HandleClientAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true)
        {
            AutoFlush = true
        };

        var line = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        IpcRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<IpcRequest>(line, BuddyIpcJson.Options);
        }
        catch
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new IpcResponse("error", null, "Invalid request JSON"), BuddyIpcJson.Options));
            return;
        }

        if (req is null || string.IsNullOrWhiteSpace(req.Type))
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new IpcResponse("error", null, "Missing request type"), BuddyIpcJson.Options));
            return;
        }

        try
        {
            var resp = HandleRequest(req);
            await writer.WriteLineAsync(JsonSerializer.Serialize(resp, BuddyIpcJson.Options));
        }
        catch (Exception ex)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new IpcResponse("error", null, ex.Message), BuddyIpcJson.Options));
        }
    }

    private IpcResponse HandleRequest(IpcRequest request)
    {
        return request.Type switch
        {
            "get_settings" => new IpcResponse("get_settings", new GetSettingsResponse(ToDto(_getSettings()))),
            "set_settings" => HandleSetSettings(request),
            "toggle" => HandleSimple(() => _toggleBuddy()),
            "show" => HandleSimple(() => _showBuddy()),
            "hide" => HandleSimple(() => _hideBuddy()),
            "exit" => HandleSimple(() => _exitBuddy()),
            _ => new IpcResponse("error", null, $"Unknown request type '{request.Type}'")
        };
    }

    private IpcResponse HandleSetSettings(IpcRequest request)
    {
        var data = request.Data is null
            ? null
            : JsonSerializer.Deserialize<SetSettingsRequest>(JsonSerializer.Serialize(request.Data, BuddyIpcJson.Options), BuddyIpcJson.Options);

        if (data is null)
        {
            return new IpcResponse("error", null, "Invalid set_settings payload");
        }

        _applySettings(data.Settings, data.Save);
        return new IpcResponse("ok", new SimpleOkResponse(true));
    }

    private static IpcResponse HandleSimple(Action action)
    {
        action();
        return new IpcResponse("ok", new SimpleOkResponse(true));
    }

    private static UserSettingsDto ToDto(UserSettings s)
    {
        return new UserSettingsDto
        {
            Scale = s.Scale,
            SpeedDipPerSecond = s.SpeedDipPerSecond,
            ToggleHotkeyEnabled = s.ToggleHotkeyEnabled,
            ToggleHotkeyModifiers = s.ToggleHotkeyModifiers,
            ToggleHotkeyVirtualKey = s.ToggleHotkeyVirtualKey,
            CharacterMode = s.CharacterMode,
            HeadId = s.HeadId,
            BodyId = s.BodyId,
            HatId = s.HatId,
            AccessoryId = s.AccessoryId,
            SkinColor = s.SkinColor,
            ShirtColor = s.ShirtColor,
            PantsColor = s.PantsColor,
            HatColor = s.HatColor,
            AccentColor = s.AccentColor,
            SpriteSheetPngPath = s.SpriteSheetPngPath,
            SpriteSheetManifestPath = s.SpriteSheetManifestPath,
            OpenAiModel = s.OpenAiModel,
            RemindersEnabled = s.RemindersEnabled,
            ReminderMinutes = s.ReminderMinutes
        };
    }
}

