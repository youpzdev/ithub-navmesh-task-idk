using System.IO;
using UnityEditor;
using UnityEngine;

public static class WorkbenchMotionCapture
{
    private static string folder;
    private static int index;
    private static double next;
    private static int lastFrame;

    public static void Begin(string path)
    {
        EditorApplication.update -= Capture;
        folder = path; index = 0; lastFrame = -1; next = 0;
        Directory.CreateDirectory(folder);
        EditorApplication.update += Capture;
    }

    private static void Capture()
    {
        if (!EditorApplication.isPlaying || index >= 30) { EditorApplication.update -= Capture; return; }
        if (EditorApplication.timeSinceStartup < next || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount; next = EditorApplication.timeSinceStartup + 1.0 / 15;
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, index.ToString("D3") + ".png")); index++;
    }
}
