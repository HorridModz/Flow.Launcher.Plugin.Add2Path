<!-- This README file was generated by the FlowLauncher C# Dotnet Plugin template (https://github.com/Flow-Launcher/dotnet-template), then edited manually-->

Flow.Launcher.Plugin.Add2Path
==================

A [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) plugin
for managing your PATH environment variable.

> **Warning**: Dealing with environment variables is dangerous and can break things. Though I have tried to make this plugin as safe to use as I can, I cannot guarantee that it won't break or corrupt your PATH environment variable.
>
> **Use at your own risk. I am not responsible if this plugin breaks things for you.**

## Usage

- Add to PATH: `path add <folder_path>`
- Remove from PATH: `path remove <folder_path>`
- List all entries in PATH and copy to clipboard: `path list`
- Get current PATH value (semicolon delimited) and copy to clipboard: `path get`

> **Note**: There is no `set` command because I can't think of any practical use
> for it. Plus, completely overwriting your PATH can break a lot of things.
> If you really want to do it anyway, you can do it manually via the
> "Edit the system environment variables" screen.

# Credits

The [Plugin Icon](icon.png) is a screenshot of the icon Windows shows you when you go to the Start Menu and type `Edit the sytem environment variables`.