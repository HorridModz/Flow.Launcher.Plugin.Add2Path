using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Flow.Launcher.Plugin.Add2Path;

public class NotInPathException : Exception
{

    public NotInPathException(string message)
        : base(message)
    {
    }
    
}

public class AlreadyInPathException : Exception
{

    public AlreadyInPathException(string message)
        : base(message)
    {
    }
    
}

[SuppressMessage("ReSharper", "ArrangeStaticMemberQualifier")]
internal static class Path
{
    
    [CanBeNull]
    private static string _ExecuteCommand(string command, bool captureoutput = false)
    {
        // From https://www.grepper.com/answers/663405/c%23+run+cmd+command?ucard=1 (modified)
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string? output = captureoutput ? process.StandardOutput.ReadToEnd() : null;
        process.WaitForExit();
        return output;
    }

    public static string GetFullString()
    {
        //return Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine);
        //return _ExecuteCommand("echo %PATH%");
        return _ExecuteCommand("for /f \"tokens=2*\" %a in ('REG QUERY \"HKCU\\Environment\" /v \"Path\" ^| findstr \"Path\"') do @<nul set /p =%~b", 
            true);
    }
    
    public static List<string> Get()
    {
        return Path.GetFullString().Split(";").ToList();
    }
    
    
    public static void SetFullString(string value)
    {
        //Environment.SetEnvironmentVariable("Path", value, EnvironmentVariableTarget.Machine);
        //_ExecuteCommand($"setx Path {value}");
        _ExecuteCommand($"reg add HKCU\\Environment /v Path /d \"{value}\" /f");
    }
    
    public static void Set(List<string> folder_paths)
    {
        Path.SetFullString(String.Join(";", folder_paths));
    }

    public static bool Contains(string folder_path)
    {
        return Path.Get().Contains(folder_path);
    }
    public static void Add(string folder_path)
    {
        if (Path.Contains(folder_path))
        {
            throw new AlreadyInPathException($"{folder_path} is already in PATH");
        }
        
        List<string> folder_paths = Path.Get();
        folder_paths.Add(folder_path);
        Path.Set(folder_paths);
    }
    
    public static void Remove(string folder_path)
    {
        if (!Path.Contains(folder_path))
        {
            throw new NotInPathException($"{folder_path} is not in PATH");
        }

        List<string> folder_paths = Path.Get();
        folder_paths.RemoveAll(existing_folder_path => existing_folder_path == folder_path);
        Path.Set(folder_paths);
    }
}