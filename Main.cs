using System;
using System.Collections.Generic;
using Flow.Launcher.Plugin;
using System.Windows;

namespace Flow.Launcher.Plugin.Add2Path;

public class Add2Path : IPlugin
{
    // ReSharper disable once InconsistentNaming
    private PluginInitContext Context;

    public void Init(PluginInitContext context)
    {
        Context = context;
    }
    
    public List<Result> Query(Query query)
    {
        return new List<Result>
        {
            new Result
            {
                Title = "add",
                SubTitle = !(String.IsNullOrWhiteSpace(query.ActionKeyword)) ? $"{query.ActionKeyword} add <folder_path>" : "add <folder_path>",
                Action = c =>
                {
                    if (query.Search.Split("add").Length == 1 || String.IsNullOrWhiteSpace(query.Search.Split("add")[1]))
                    {
                        return false;
                    }
                    
                    // Get the folder path (first argument), and trim whitespace.
                    string folder_path = query.Search.Split("add")[1].Trim();
                    
                    if (Path.Contains(folder_path))
                    {
                        Context.API.ShowMsg($"Cannot add {folder_path} to PATH because it is already in PATH.");
                        // FIXME: What does return false do? Should this return true or false?
                        return true;
                    }
                    else
                    {
                        Path.Add(folder_path);
                        Context.API.ShowMsg("Successfully added to PATH",
                            $"Successfully added {folder_path} to PATH.");
                        return true;
                    }
                },
                IcoPath = "icon.png"
            },
            
            new Result
            {
                Title = "remove",
                SubTitle = !(String.IsNullOrWhiteSpace(query.ActionKeyword)) ? $"{query.ActionKeyword} remove <folder_path>" : "remove <folder_path>",
                Action = c =>
                {
                    if (query.Search.Split("remove").Length == 1 || String.IsNullOrWhiteSpace(query.Search.Split("remove")[1]))
                    {
                        return false;
                    }
                    
                    // Get the folder path (first argument), and trim whitespace.
                    string folder_path = query.Search.Split("remove")[1].Trim();
                    
                    if (!Path.Contains(folder_path))
                    {
                        Context.API.ShowMsg("Folder is not in PATH",
                                $"Cannot remove {folder_path} from PATH because it is not in" +
                                                       $" PATH. Verify that the path is correct. You can use" +
                                                       $" \"{query.ActionKeyword} list\" to list all items in PATH.");
                        // FIXME: What does return false do? Should this return true or false?
                        return true;
                    }
                    else
                    {
                        Path.Remove(folder_path);
                        Context.API.ShowMsg("Successfully removed from PATH", 
                            $"Successfully removed {folder_path} from PATH.");
                        return true;
                    }
                },
                IcoPath = "icon.png"
            },
            
            new Result
            {
                Title = "list",
                SubTitle = $"{query.ActionKeyword} list",
                Action = c =>
                {
                    string pathlistasstring = String.Join("\n", Path.Get());
                    pathlistasstring = pathlistasstring.Remove(pathlistasstring.Length - 1); // Remove trailing newline from String.Join()
                    Clipboard.SetText(pathlistasstring);
                    Context.API.ShowMsg("Copied to clipboard");
                    return true;
                },
                IcoPath = "icon.png"
            },
            
            new Result
            {
                Title = "get",
                SubTitle = $"{query.ActionKeyword} get",
                Action = c =>
                {
                    Clipboard.SetText(Path.GetFullString());
                    Context.API.ShowMsg("Copied to clipboard");
                    return true;
                },
                IcoPath = "icon.png"
            }
        };
    }
}