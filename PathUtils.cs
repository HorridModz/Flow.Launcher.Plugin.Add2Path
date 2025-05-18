using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Win32;

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


internal static class PathUtils // Renamed from Path to PathUtils, to avoid naming conflict with System.IO.Path
{

    private static string _ExecuteCommand(string command, bool requestadmin = false)
    {
        /*
         * Oh boy, this took me ages. Let me explain...
         * So the issue I have is that, when requestadmin is true, I need to run as admin. But I also need to capture output.
         * Apparently C# doesn't want you to do that and makes it the 9th level of Hell (damn, why can't this shit just be easy like in python),
         * so when I google my problem the first result is an obscure StackOverflow post: https://stackoverflow.com/questions/15746716/run-new-process-as-admin-and-read-standard-output
         * That post discusses the crazy lengths you have to go to in order for this to work. I decided to just go with the OP's hacky solution of redirecting to a temp file.
         * Apparently the issue stems from funky issues with C# use of Windows functions to run commands:
         * RedirectStandardOutput (for capturing output) and UseShellExecute (for running as admin) are mutually exclusive by design.
         * Here's a post that makes the problem clear: https://www.codeproject.com/Questions/772905/run-process-as-admin-and-redirect-the-process-outp
        */

        /*
        // Debug
        Process process2 = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c echo {command} & pause & {command} & pause",
                RedirectStandardOutput = false,
                UseShellExecute = true,
                CreateNoWindow = false,
            }
        };
        process2.Start();
        process2.WaitForExit();
        */
        string tempfile = Path.GetTempFileName();
        Process process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command} > \"{tempfile}\"",
                RedirectStandardOutput = false,
                UseShellExecute = true,
                CreateNoWindow = true,
            }
        };
        if (requestadmin)
        {
            process.StartInfo.Verb = "runas";
        }

        process.Start();
        process.WaitForExit();
        string output = File.ReadAllText(tempfile).TrimEnd();
        File.Delete(tempfile);
        return output;
    }

    public static string _EncodeParameterArgument(string original)
    {
        // From https://stackoverflow.com/questions/5510343/escape-command-line-arguments-in-c-sharp
        if (string.IsNullOrEmpty(original))
            return original;
        string value = Regex.Replace(original, @"(\\*)" + "\"", @"$1\$0");
        return Regex.Replace(value, @"^(.*\s.*?)(\\*)$", "\"$1$2$2\"");
    }

    private static string _GetRegistryKey(bool system = false)
    {
        return system ? """HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment""" : """HKEY_CURRENT_USER\Environment""";
    }

    public static string GetFullString(bool system = false)
    {
        string registrykey = _GetRegistryKey(system);
        try
        {
            return _ExecuteCommand($"for /f \"tokens=2*\" %a in ('REG QUERY \"{registrykey}\" /v \"Path\" ^| findstr \"Path\"') do @echo %b", system);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new Exception($"Failed to set path - admin access was denied, or a different Windows error occurred");
        }
    }
    
    public static List<string> Get(bool system = false)
    {
        string pathstring = PathUtils.GetFullString(system);
        // Split by semicolon, but only for items not in quotes (because semicolon can appear literally in a folder path)
        Regex splitregex = new Regex(";(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))"); // Regex from https://stackoverflow.com/a/48275050/22081657
        List<string> folderPaths = splitregex.Split(pathstring).ToList();
        // Trim whitespace and quotes from all paths
        folderPaths = (from folderPath in folderPaths select folderPath.Trim(new char[3] { '"', ' ', '\t' })).ToList();
        // Remove all items that are empty or whitespace - path will get corrupted and not work, otherwise (Don't know if this program could possibly cause this corruption, but just in case)
        folderPaths.RemoveAll(String.IsNullOrWhiteSpace);
        return folderPaths;
    }
    
    
    public static void SetFullString(string value, bool system = false)
    {
        string registrykey = _GetRegistryKey(system);
        string resulttext;
        try
        {
            resulttext = _ExecuteCommand($"reg add \"{registrykey}\" /v Path /d {_EncodeParameterArgument(value)} /f", system);
        } catch (System.ComponentModel.Win32Exception)
        {
            throw new Exception($"Failed to set path - admin access was denied, or a different Windows error occurred");
        }
        if (!resulttext.Contains("The operation completed successfully."))
        {
            throw new Exception($"Failed to set path - got output: {resulttext}");
        }
    }
    
    public static void Set(List<string> folderPaths, bool system = false)
    {
        // ?: Windows wraps paths with semicolon (;) as default, 
        // ?: this way CMD can read path value by discarding path seperator character 
        // ?: (which is semicolon for in this case) which is between double quotes 
        // !: But in the other hand path with semicolons won't be working on Powershell because Powershell
        // !: doesn't trims any double quotes and seperates paths only by checking path seperator character
        // ?: You can check all the details from https://github.com/HorridModz/Flow.Launcher.Plugin.Add2Path/pull/8 
        folderPaths = (from folderPath in folderPaths select folderPath.Contains(';') ? $"\"{folderPath}\"" : folderPath).ToList();
        PathUtils.SetFullString(String.Join(";", folderPaths), system);
    }

    public static bool Contains(string folderPath, bool system = false)
    {
        List<string> folderPaths = PathUtils.Get(system);
        return folderPaths.Contains(folderPath);
    }

    public static void Add(string folderPath, bool system = false)
    {
        if (PathUtils.Contains(folderPath, system))
        {
            throw new AlreadyInPathException($"{folderPath} is already in PATH");
        }
        
        List<string> folderPaths = PathUtils.Get(system);

        folderPaths.Add(folderPath);
        PathUtils.Set(folderPaths, system);
    }
    
    public static void Remove(string folderPath, bool system = false)
    {
        if (!PathUtils.Contains(folderPath, system))
        {
            throw new NotInPathException($"{folderPath} is not in PATH");
        }

        List<string> folderPaths = PathUtils.Get(system);
        folderPaths.RemoveAll(existing_folder_path => existing_folder_path == folderPath);
        PathUtils.Set(folderPaths, system);
    }
}
