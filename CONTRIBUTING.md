# Contributing to Musait

Thanks for your interest in contributing! Here's everything you need to get started.

This repository is the Musait Free/source-available tree. Do not add private Musait Pro RFA writer code, dormant generator implementations, patchable license gates, or code paths that create, save, open, export, download, or stage `.rfa` output.

## Prerequisites

- **Visual Studio 2022** (or later) with the **.NET desktop development** workload
- **.NET SDK 8.0+** (install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download))
- **Autodesk Revit 2022–2027** (only needed to test the plugin at runtime)
- **Microsoft WebView2 Runtime** (usually pre-installed on Windows 10/11)

## Building

```powershell
# Clone the repo
git clone https://github.com/mashyo/musait.git
cd musait

# Restore and build all target frameworks
dotnet build src\Musait\Musait.csproj
```

The project multi-targets `net48`, `net8.0-windows`, and `net10.0-windows`. The `net48` target requires Revit 2023 API DLLs at `C:\Program Files\Autodesk\Revit 2023\`. If you don't have Revit installed, you can still build the .NET 8+ targets:

```powershell
dotnet build src\Musait\Musait.csproj -f net8.0-windows
```

## Building the Installer

The installer requires [Inno Setup 6.7+](https://jrsoftware.org/isinfo.php):

```powershell
.\installer\build-inno.ps1 -ProductVersion 0.1.0
```

## Pull Request Guidelines

1. **One feature or fix per PR.** Keep changes focused.
2. **Test with at least one Revit version** before submitting, if your change touches Revit API code.
3. **Follow existing code style.** The project uses C# 12, nullable reference types, and implicit usings.
4. **Write clear commit messages.** Describe *what* and *why*, not just *how*.
5. **No generated files.** Don't commit `bin/`, `obj/`, or `dist/` contents.
6. **Keep Free preview-only.** Family JSON validation, normalization, diagnostics, and 3D preview code are welcome; RFA conversion belongs in Musait Pro.

## Reporting Issues

Use [GitHub Issues](https://github.com/mashyo/musait/issues). Include your Revit version, Windows version, and steps to reproduce.

## License

By contributing, you agree that your contributions will be subject to the [Musait End User License Agreement (EULA)](LICENSE). You grant the Author a perpetual, irrevocable, worldwide, royalty-free license to use, modify, and distribute your contributions as part of Musait.
