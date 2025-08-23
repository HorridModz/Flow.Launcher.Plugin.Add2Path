using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.ComponentModel;

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

public class CommandErrorException : Exception
{

    public CommandErrorException(string message)
        : base(message)
    {
    }
}


public class RegistryEntryNotFoundException : Exception
{

    public RegistryEntryNotFoundException(string message)
        : base(message)
    {
    }
}


internal static class PathUtils // Renamed from Path to PathUtils, to avoid naming conflict with System.IO.Path
{

    private static string _ExecuteCommand(string command, bool requestadmin = false, bool throwerrors = true)
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
        string stdout_file = Path.GetTempFileName();
        string stderr_file = Path.GetTempFileName();
        Process process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c ({command}) > \"{stdout_file}\" 2> \"{stderr_file}\"",
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
        string output = File.ReadAllText(stdout_file).TrimEnd();
        string stderr = File.ReadAllText(stderr_file).TrimEnd();
        File.Delete(stdout_file);
        File.Delete(stderr_file);

        if (throwerrors && !string.IsNullOrEmpty(stderr)) {
            throw new CommandErrorException(stderr);
        }
        return output;
    }

    private static string _GetFromRegistry(string registrykey, string entry, bool requestadmin = false)
    {
        string resulttext;
        try
        {
            resulttext = _ExecuteCommand($"for /f \"tokens=2*\" %a in ('REG QUERY {_EncodeParameterArgument(registrykey)} /v {_EncodeParameterArgument(entry)}" +
                                         $" ^| findstr {_EncodeParameterArgument(entry)}') do @if not \"%b\"==\"\" echo %b", requestadmin);
        }
        catch (Win32Exception)
        {
            throw new Exception($"Failed to get registry entry - admin access was denied, or a different Windows error occurred");
        }
        catch (CommandErrorException ex)
        {
            if (ex.Message.Contains("ERROR: The system was unable to find the specified registry key or value."))
            {
                throw new RegistryEntryNotFoundException($"Failed to get registry entry - this key or value does not exist");
            } else
            {
                throw;
            }
        }
        return resulttext;
    }

    private static string _SetRegistryEntry(string registrykey, string entry, string value, bool requestadmin = false)
    {
        try
        {
            return _ExecuteCommand($"reg add {_EncodeParameterArgument(registrykey)} /v {_EncodeParameterArgument(entry)} /d {_EncodeParameterArgument(value)} /f", requestadmin);
        }
        catch (Win32Exception)
        {
            throw new Exception($"Failed to set registry key - admin access was denied, or a different Windows error occurred");
        }
    }


    public static string _EncodeParameterArgument(string original)
    {
        // From https://stackoverflow.com/questions/5510343/escape-command-line-arguments-in-c-sharp
        /*
         * The code:
            if (string.IsNullOrEmpty(original))
                return original;
         * Causes a bug. It leads to a blank string being returned as a blank string, not "":
			   EncodeParameterArgument("") -> ""; not "\"\""
         * This behavior is not always wrong, but with the reg add command, passing nothing
         * instead of "" breaks the command.
        */
        string value = Regex.Replace(original, @"(\\*)" + "\"", @"$1\$0");
        value = Regex.Replace(value, @"^(.*\s.*?)(\\*)$", "\"$1$2$2\"");
        value = $"\"{value.Trim('"')}\"";
        return value;
    }

    private static string _GetPathRegistryKey(bool system = false)
    {
        return system ? """HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment""" : """HKEY_CURRENT_USER\Environment""";
    }

    public static string GetFullString(bool system = false)
    {
        try
        {
            return _GetFromRegistry(_GetPathRegistryKey(system), "Path", system);
        }
        catch (Win32Exception)
        {
            throw new Exception($"Failed to get PATH - admin access was denied, or a different Windows error occurred");
        }
    }

    public static void SetFullString(string value, bool system = false)
    {
        string resulttext;
        try
        {
            resulttext = _SetRegistryEntry(_GetPathRegistryKey(system), "Path", value, system);
        }
        catch (Win32Exception)
        {
            throw new Exception($"Failed to set PATH - admin access was denied, or a different Windows error occurred");
        }
    }


    private static string _GetBackup(bool last_manual = false, bool system = false)
    {
        try
        {
            return _GetFromRegistry(_GetPathRegistryKey(system), last_manual ? "PathBackupManual" : "PathBackup", system);
        }
        catch (RegistryEntryNotFoundException)
        {
            if (last_manual)
            {
                throw new Exception($"Failed to get backup - no manual backup has been created (perhaps you meant to restore the last automatic backup?)");
            }
            else
            {
                throw new Exception($"Failed to get backup - no automatic backups have been created (perhaps you meant to restore the last manual backup?)");
            }
        }
        catch (Win32Exception)
        {
            throw new Exception($"Failed to get backup - admin access was denied, or a different Windows error occurred");
        }
    }

    private static void _SetBackup(string value, bool manual, bool system = false)
    {
        string resulttext;
        try
        {
            resulttext = _SetRegistryEntry(_GetPathRegistryKey(system), manual ? "PathBackupManual" : "PathBackup", value, system);
        }
        catch (Win32Exception)
        {
            throw new Exception($"Failed to set backup - admin access was denied, or a different Windows error occurred");
        }
        if (!resulttext.Contains("The operation completed successfully."))
        {
            throw new Exception($"Failed to set backup - got output: {resulttext}");
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


    public static void Set(List<string> folderPaths, bool system = false)
    {
        // Wrap folder paths in quotes
        folderPaths = (from folderPath in folderPaths select $"\"{folderPath}\"").ToList();
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

    public static void Backup(bool manual = false, bool system = false)
    {
        _SetBackup(GetFullString(system), manual, system);
    }

    public static void Restore(bool last_manual = false, bool system = false)
    {
        SetFullString(_GetBackup(last_manual, system), system);
    }
}
