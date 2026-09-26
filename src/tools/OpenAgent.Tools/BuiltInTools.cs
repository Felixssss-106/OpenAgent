using OpenAgent.Tools.Files;
using OpenAgent.Tools.Screen;
using OpenAgent.Tools.System;

namespace OpenAgent.Tools;

/// <summary>
/// The built-in Windows tool set in one list, so the composition root and the
/// tests cannot drift apart (spec sections 38, 126).
/// </summary>
public static class BuiltInTools
{
    public static IReadOnlyList<ITool> Create() => new ITool[]
    {
        new SystemGetInfoTool(),
        new ProcessListTool(),
        new ProcessTerminateTool(),
        new AppLaunchTool(),
        new AppCloseTool(),
        new FileListTool(),
        new FileReadTool(),
        new FileWriteTool(),
        new FileMoveTool(),
        new FileCopyTool(),
        new FileDeleteTool(),
        new ScreenCaptureTool(),
    };
}
