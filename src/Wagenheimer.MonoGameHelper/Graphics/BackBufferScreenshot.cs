using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Wagenheimer.MonoGameHelper.Graphics;

/// <summary>
/// Captures the **final frame** (the back buffer) to a PNG. Useful for fidelity gates in CI,
/// visual regression checks, and bug reports.
///
/// It captures the back buffer rather than an off-screen render target, so everything that was
/// drawn for the frame is included — including UI and any "above UI" overlay. Engine-provided
/// screenshot helpers usually capture an intermediate render target, which is taken *before* the
/// UI layer is drawn and therefore misses it.
///
/// ## CI / fidelity gate
///
/// <code>
/// // Startup
/// _screenshotRequest = ScreenshotRequest.FromEnvironment("MYGAME_SCREENSHOT", "MYGAME_SCREENSHOT_FRAME");
///
/// // End of Draw(), after every layer has been rendered
/// if (_screenshotRequest != null &amp;&amp; _screenshotRequest.TryCapture(GraphicsDevice))
///     Exit();
/// </code>
///
/// <code>
/// $env:MYGAME_SCREENSHOT = "C:\temp\gate.png"
/// $env:MYGAME_SCREENSHOT_FRAME = "45"
/// dotnet run
/// </code>
/// </summary>
public static class BackBufferScreenshot
{
    /// <summary>Default frame at which a request fires, giving the scene time to settle.</summary>
    public const int DefaultFrame = 30;

    /// <summary>Reads the back buffer into a new texture. The caller owns and must dispose it.</summary>
    public static Texture2D Capture(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        int width = graphicsDevice.PresentationParameters.BackBufferWidth;
        int height = graphicsDevice.PresentationParameters.BackBufferHeight;

        var pixels = new Color[width * height];
        graphicsDevice.GetBackBufferData(pixels);

        var texture = new Texture2D(graphicsDevice, width, height);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Saves the current back buffer to a PNG, creating the target directory if needed.</summary>
    public static void SaveToFile(GraphicsDevice graphicsDevice, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var texture = Capture(graphicsDevice);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        using var stream = File.Create(filePath);
        texture.SaveAsPng(stream, texture.Width, texture.Height);
    }
}

/// <summary>
/// A pending screenshot request, usually created from environment variables so a build can be
/// asked to render a frame, save it and quit — handy for automated fidelity gates.
/// </summary>
public sealed class ScreenshotRequest
{
    private int _frame;

    private ScreenshotRequest(string outputPath, int targetFrame)
    {
        OutputPath = outputPath;
        TargetFrame = targetFrame;
    }

    /// <summary>Where the PNG will be written.</summary>
    public string OutputPath { get; }

    /// <summary>Frame at which the capture happens.</summary>
    public int TargetFrame { get; }

    /// <summary>True when the requested frame has already been captured.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Builds a request from environment variables.
    /// Returns <c>null</c> when the path variable is not set.
    /// </summary>
    /// <param name="pathVariable">Variable holding the output PNG path.</param>
    /// <param name="frameVariable">Optional variable holding the target frame number.</param>
    /// <param name="defaultFrame">Frame to use when <paramref name="frameVariable"/> is absent.</param>
    public static ScreenshotRequest? FromEnvironment(
        string pathVariable,
        string? frameVariable = null,
        int defaultFrame = BackBufferScreenshot.DefaultFrame)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathVariable);

        var path = Environment.GetEnvironmentVariable(pathVariable);
        if (string.IsNullOrWhiteSpace(path))
            return null;

        int targetFrame = defaultFrame;

        if (!string.IsNullOrWhiteSpace(frameVariable))
        {
            var rawFrame = Environment.GetEnvironmentVariable(frameVariable);
            if (int.TryParse(rawFrame, out var parsed) && parsed > 0)
                targetFrame = parsed;
        }

        Console.WriteLine($"[Screenshot] Requested -> '{path}' at frame {targetFrame}.");
        return new ScreenshotRequest(path, targetFrame);
    }

    /// <summary>
    /// Advances the frame counter and captures when the target frame is reached.
    /// Call once per frame, at the end of the draw pass.
    /// </summary>
    /// <returns><c>true</c> when the screenshot was just written.</returns>
    public bool TryCapture(GraphicsDevice graphicsDevice)
    {
        if (IsCompleted)
            return false;

        _frame++;
        if (_frame < TargetFrame)
            return false;

        BackBufferScreenshot.SaveToFile(graphicsDevice, OutputPath);
        IsCompleted = true;

        Console.WriteLine($"[Screenshot] Saved: {OutputPath}");
        return true;
    }
}
