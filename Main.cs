using System;
using System.Collections.Generic;
using Flow.Launcher.Plugin;
using System.Windows;
using System.IO;
using System.Linq;
using System.ComponentModel;

namespace Flow.Launcher.Plugin.Add2Path;

public class Add2Path : IPlugin
{
    // ReSharper disable once InconsistentNaming
    private PluginInitContext Context;

    private struct Add2PathCommand
    {
        public string Name;
        public string Description;
        public Func<ActionContext, bool> Action { get; set; }

        public string GetDefaultSubtitle()
        {
            string buildSubtitle = Name;
            if (Description.Contains("<manual_or_automatic>"))
            {
                buildSubtitle += " <!manual or leave blank>";
            }
            if (Description.Contains("<system_or_user>"))
            {
                buildSubtitle += " <!system or leave blank>";
            }
            if (Description.Contains("<folder_path>"))
            {
                buildSubtitle += " <folder_path>";
            }
            return buildSubtitle;
        }
    }

    public enum Flag
    {
        System,
        Manual
    }

    public void Init(PluginInitContext context)
    {
        Context = context;
    }

    private static bool _IsValidPath(string path)
    {
        // From https://stackoverflow.com/questions/2435894/how-do-i-check-for-illegal-characters-in-a-path (modified)
        for (var i = 0; i < path.Length; i++)
        {
            int c = path[i];

            if (c == '<' || c == '>' || c == '|' || c == '*' || c == '?' || c < 32)
                return false;
        }

        return true;
    }

    private static bool _IsDirectory(string path)
    {
        // From https://learn.microsoft.com/en-us/answers/questions/1128930/c-filedirectory-check and https://stackoverflow.com/a/1395226/22081657
        try
        {
            // Fails if path does not exist
            FileAttributes attr = File.GetAttributes(path);
            return attr.HasFlag(FileAttributes.Directory);
        }
        catch
        {
            // Fallback if path does not exist: Check if it has an extension (if it has an extension it's a file, if not it's a directory)
            return !Path.HasExtension(path);
        }
    }

    private static void _ParseValuePart(string valuepart, out List<Flag> flags, out string? folderPath)
    {
        flags = new List<Flag>();
        Dictionary<string, Flag> potentialFlags = new Dictionary<string, Flag> { { "system", Flag.System }, { "manual", Flag.Manual } };
        while (potentialFlags.Keys.Any(flagstring => valuepart.StartsWith($"!{flagstring}"))) {
            foreach ((string flagstring, Flag flag)  in potentialFlags)
            {
                if (valuepart.StartsWith($"!{flagstring}"))
                {
                    flags.Add(flag);
                    valuepart = valuepart[$"!{flagstring}".Length..].TrimStart();
                    break;
                }
            }
        }

        folderPath = valuepart.Trim(new char[3] { '"', ' ', '\t' }); // trim whitespace and quotes
        if (folderPath == "")
        {
            folderPath = null;
        }
    }

    private bool _ValidateFolderPath(string? folderPath)
    {
        if (String.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }
        if (!_IsValidPath(folderPath) || folderPath.Contains("\""))
        {
            Context.API.ShowMsgError("Invalid PATH Value", $"\"{folderPath}\" is not a valid folder path - verify that the path is correct and valid");
            return false;
        }
        if (!_IsDirectory(folderPath))
        {
            Context.API.ShowMsg("WARNING: This is a file path", $"PATH is for folder paths, but \"{folderPath}\" is a file path. File paths are ignored in PATH." +
                                                                " To add this file to PATH, add the folder it is in rather than the file itself.");
        }
        if (!Directory.Exists(folderPath))
        {
            Context.API.ShowMsg("WARNING: Directory does not exist", $"The directory \"{folderPath}\" does not exist. Did you mistype it?");
        }
        return true;
    }

    private static string _getResultSubtitle(Add2PathCommand command, Query query)
    {
        // When blank, list all args: Like "add <!system or leave blank> <folder path>" or get <!system or leave blank>
        string blankSubtitle = !String.IsNullOrEmpty(query.ActionKeyword) ? $"{query.ActionKeyword} {command.GetDefaultSubtitle()}" : command.GetDefaultSubtitle();
        if (!query.Search.TrimStart().StartsWith(command.Name))
        {
            return blankSubtitle;
        }
        string valuepart = query.Search.TrimStart()[command.Name.Length..].TrimStart();
        if (String.IsNullOrEmpty(valuepart) && command.Description.Contains("<folder_path>"))
        {
            return blankSubtitle;
        }
        _ParseValuePart(valuepart, out List<Flag> flags, out string ? folderPath);
        

        string subtitle = command.Description;
        subtitle = folderPath is not null ? subtitle.Replace("<folder_path>", $"\"{folderPath}\"") : subtitle;
        subtitle = flags.Contains(Flag.Manual) ? subtitle.Replace("<manual_or_automatic>", "last manual") : subtitle.Replace("<manual_or_automatic>", "last automatic");
        subtitle = flags.Contains(Flag.System) ? subtitle.Replace("<system_or_user>", "system") : subtitle.Replace("<system_or_user>", "user");
        return subtitle;
    }

    private static List<Result> _GetMatchedResults(List<Result> allResults, string searchQuery)
    {
        /* Finds all matched results based on the user's query
         * A result is considered a match if its title is contained in the user's input
         * For example, using a result with title "add":
         *   "add" - match
         *   " add" - match
         *   "add foo" - match
         *   "a" - match
         *   "ad" - match
         *   ". add" - not a match
         *   "addx" - not a match
         *   "adx" - not a match
         *   
         *   If the query is empty or whitespace, all results will be considered matches
        */
        List<Result> results = new List<Result>();
        foreach (Result result in allResults)
        {
            // Add a space to make sure the user has not typed the start of the command but not the command itself
            // For example, `addx` instead of just `add`
            string title = result.Title + " ";
            var zippedTitleAndQuery = title.TrimStart().Zip(searchQuery);
            bool match = true;
            foreach (List<char> chars in zippedTitleAndQuery)
            {
                if (chars[0] != chars[1])
                {
                    match = false;
                    break;
                }
            }
            if (match) { results.Add(result); }
        }
        return results;
    }

    public List<Result> Query(Query query)
    {
        // Note: Commands are in order of appearance (if they are reordered, the order FlowLauncher displays them in will also change)
        List<Add2PathCommand> commands = new List<Add2PathCommand>
        {
            new Add2PathCommand
            {
                Name = "add",
                Description = "add <folder_path> to <system_or_user> PATH",
                Action = c =>
                {
                    if (!query.Search.TrimStart().StartsWith("add"))
                    {
                        return false;
                    }
                    string valuepart = query.Search.TrimStart()["add".Length..].TrimStart();
                    _ParseValuePart(valuepart, out List<Flag> flags, out string ? folderPath);
                    if (folderPath is null || !_ValidateFolderPath(folderPath))
                    {
                        return false;
                    }
                    if (flags.Contains(Flag.Manual))
                    {
                        Context.API.ShowMsg("Invalid flag", "This command does not accept the !manual flag");
                        return true;
                    }

                    bool contains;
                    try {
                        contains = PathUtils.Contains(folderPath, flags.Contains(Flag.System));
                    }
                    catch (Exception ex) {
                        Context.API.ShowMsgError("Failed to add to PATH", $"Failed to add {$"\"{folderPath}\""} to {(flags.Contains(Flag.System) ? "system" : "user")} PATH - {ex.Message}");
                        return true;
                    }
                    if (contains)
                    {
                        Context.API.ShowMsg($"Cannot add {$"\"{folderPath}\""} to {(flags.Contains(Flag.System) ? "system" : "user")} PATH because it is already in {(flags.Contains(Flag.System) ? "system" : "user")} PATH.");
                        return true;
                    }
                    try
                    {
                        PathUtils.Backup(false, flags.Contains(Flag.System));
                    }
                    catch (Exception ex)
                    {
                        Context.API.ShowMsg("Warning: Failed to create backup of PATH", $"Failed to create a backup of {(flags.Contains(Flag.System) ? "system" : "user")} PATH - {ex.Message}");
                    }
                    try {
                        PathUtils.Add(folderPath, flags.Contains(Flag.System));
                        if (!PathUtils.Contains(folderPath, flags.Contains(Flag.System)))
                        {
                            throw new Exception("Sanity check: Though no errors were detected, value is still not in PATH.");
                        }
                    } catch (Exception ex){
                        Context.API.ShowMsgError("Failed to add to PATH", $"Failed to add {$"\"{folderPath}\""} to {(flags.Contains(Flag.System) ? "system" : "user")} PATH - {ex.Message}");
                        return true;
                    }
                    Context.API.ShowMsg("Successfully added to PATH", $"Successfully added {$"\"{folderPath}\""} to {(flags.Contains(Flag.System) ? "system" : "user")} PATH.");
                    return true;
                },
            },
            new Add2PathCommand
            {
                Name = "remove",
                Description = "remove <folder_path> from <system_or_user> PATH",
                Action = c =>
                {
                    if (!query.Search.TrimStart().StartsWith("remove"))
                    {
                        return false;
                    }
                    string valuepart = query.Search.TrimStart()["remove".Length..].TrimStart();
                    _ParseValuePart(valuepart, out List<Flag> flags, out string ? folderPath);
                    if (folderPath is null || !_ValidateFolderPath(folderPath))
                    {
                        return false;
                    }
                    if (flags.Contains(Flag.Manual))
                    {
                        Context.API.ShowMsg("Invalid flag", "This command does not accept the !manual flag");
                        return true;
                    }

                    bool contains;
                    try {
                        contains = PathUtils.Contains(folderPath, flags.Contains(Flag.System));
                    }
                    catch (Exception ex) {
                        Context.API.ShowMsgError("Failed to remove from PATH", $"Failed to remove {$"\"{folderPath}\""} from {(flags.Contains(Flag.System) ? "system" : "user")} PATH - {ex.Message}");
                        return true;
                    }
                    if (!contains)
                    {
                        Context.API.ShowMsg("Folder is not in PATH",
                                            $"Cannot remove {$"\"{folderPath}\""} from {(flags.Contains(Flag.System) ? "system" : "user")} PATH because it is not in PATH." +
                                            $" Double-check the file path and whether it is in system or user PATH." +
                                            $" Use the \"{query.ActionKeyword} list\" command to list all items in PATH.");
                        return true;
                    }
                    try
                    {
                        PathUtils.Backup(false, flags.Contains(Flag.System));
                    }
                    catch (Exception ex)
                    {
                        Context.API.ShowMsg("Warning: Failed to create backup of PATH", $"Failed to create a backup of {(flags.Contains(Flag.System) ? "system" : "user")} PATH - {ex.Message}");
                    }
                    try {
                        PathUtils.Remove(folderPath, flags.Contains(Flag.System));
                        if (PathUtils.Contains(folderPath, flags.Contains(Flag.System)))
                        {
                            throw new Exception("Sanity check: Though no errors were detected, value is still in PATH.");
                        }
                    } catch (Exception ex){
                        Context.API.ShowMsgError($"Failed to remove from PATH",
                            $"Failed to remove {$"\"{folderPath}\""} from {(flags.Contains(Flag.System) ? "system" : "user")} PATH - {ex.Message}");
                        return true;
                    }
                    Context.API.ShowMsg($"Successfully removed from PATH",
                        $"Successfully removed {$"\"{folderPath}\""} from {(flags.Contains(Flag.System) ? "system" : "user")} PATH.");
                    return true;
                },
            },

            new Add2PathCommand
            {
                Name = "list",
                Description = "list items in <system_or_user> PATH",
                Action = c =>
                {
                    if (!query.Search.TrimStart().StartsWith("list"))
                    {
                        return false;
                    }
                    string valuepart = query.Search.TrimStart()["list".Length..].TrimStart();
                    _ParseValuePart(valuepart, out List<Flag> flags, out string ? folderPath);
                    if (flags.Contains(Flag.Manual))
                    {
                        Context.API.ShowMsg("Invalid flag", "This command does not accept the !manual flag");
                        return true;
                    }

                    string pathliststring;
                    try
                    {
                        pathliststring = String.Join("\n", PathUtils.Get(flags.Contains(Flag.System)));
                    } catch (Exception ex)
                    {
                        Context.API.ShowMsgError("Failed to get PATH list", $"Failed to get {(flags.Contains(Flag.System) ? "system" : "user")} PATH list - {ex.Message}");
                        return true;
                    }
                    Clipboard.SetText(pathliststring);
                    Context.API.ShowMsg($"Copied {(flags.Contains(Flag.System) ? "system" : "user")} PATH to clipboard");
                    return true;
                },
            },

            new Add2PathCommand
            {
                Name = "get",
                Description = "get raw string of <system_or_user> PATH",
                Action = c =>
                {
                    if (!query.Search.TrimStart().StartsWith("get"))
                    {
                        return false;
                    }
                    string valuepart = query.Search.TrimStart()["get".Length..].TrimStart();
                    _ParseValuePart(valuepart, out List<Flag> flags, out string ? folderPath);
                    if (flags.Contains(Flag.Manual))
                    {
                        Context.API.ShowMsg("Invalid flag", "This command does not accept the !manual flag");
                        return true;
                    }

                    string pathstring;
                    try
                    {
                        pathstring = PathUtils.GetFullString(flags.Contains(Flag.System));
                    } catch (Exception ex)
                    {
                        Context.API.ShowMsgError("Failed to get PATH", $"Failed to get {(flags.Contains(Flag.System) ? "system" : "user")} PATH - {ex.Message}");
                        return true;
                    }
                    Clipboard.SetText(pathstring);
                    Context.API.ShowMsg($"Copied {(flags.Contains(Flag.System) ? "system" : "user")} PATH to clipboard");
                    return true;
                },
            },

            new Add2PathCommand
            {
                Name = "backup",
                Description = "Create backup of <system_or_user> PATH",
                Action = c =>
                {
                    if (!query.Search.TrimStart().StartsWith("backup"))
                    {
                        return false;
                    }
                    string valuepart = query.Search.TrimStart()["backup".Length..].TrimStart();
                    _ParseValuePart(valuepart, out List<Flag> flags, out _);
                    if (flags.Contains(Flag.Manual))
                    {
                        Context.API.ShowMsg("Invalid flag", "This command does not accept the !manual flag");
                        return true;
                    }

                    try
                    {
                        PathUtils.Backup(true, flags.Contains(Flag.System));
                    }
                    catch (Exception ex)
                    {
                        Context.API.ShowMsgError("Failed to create backup of PATH", $"Failed to backup {(flags.Contains(Flag.System) ? "system" : "user")} PATH - {ex.Message}");
                        return true;
                    }
                    Context.API.ShowMsg($"Successfully backed up {(flags.Contains(Flag.System) ? "system" : "user")} PATH");
                    return true;
                },
            },

            new Add2PathCommand
            {
                Name = "restore",
                Description = "Restore <system_or_user> PATH from <manual_or_automatic> backup",
                Action = c =>
                {
                    if (!query.Search.TrimStart().StartsWith("restore"))
                    {
                        return false;
                    }
                    string valuepart = query.Search.TrimStart()["restore".Length..].TrimStart();
                    _ParseValuePart(valuepart, out List<Flag> flags, out _);

                    try
                    {
                        PathUtils.Restore(flags.Contains(Flag.Manual), flags.Contains(Flag.System));
                    } catch (Exception ex)
                    {
                        Context.API.ShowMsgError("Failed to restore PATH from backup", $"Failed to restore {(flags.Contains(Flag.System) ? "system" : "user")} PATH - {ex.Message}");
                        return true;
                    }
                    Context.API.ShowMsg($"Successfully restored {(flags.Contains(Flag.System) ? "system" : "user")} PATH from backup");
                    return true;
                },
            }
        };
        List<Result> allResults = commands.Select((command, index) => new Result
        {
            Title = command.Name,
            SubTitle = _getResultSubtitle(command, query),
            Action = command.Action,
            IcoPath = "icon.png",
            Score = index
        }).ToList();
        return _GetMatchedResults(allResults, query.Search);
    }
}