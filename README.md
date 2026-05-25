# Computer Buddy

Windows WPF desktop “buddy” overlay + Control Center.

## Projects

- `src/ComputerBuddy.App` — the buddy overlay app
- `src/ComputerBuddy.Manager` — Control Center (settings + commands via IPC)
- `src/ComputerBuddy.Contracts` — shared DTOs/contracts

## Build

Open `ComputerBuddy.sln` in Visual Studio, or run:

```powershell
dotnet build .\ComputerBuddy.sln -c Debug
```
