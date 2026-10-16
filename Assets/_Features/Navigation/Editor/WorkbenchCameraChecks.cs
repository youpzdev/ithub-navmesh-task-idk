using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class WorkbenchCameraChecks
{
    public static string Check()
    {
        var bench = Object.FindFirstObjectByType<NavMeshWorkbench>();
        var camera = Camera.main;
        var rig = camera.transform.parent;
        Vector3 saved = rig.position;
        var method = typeof(NavMeshWorkbench).GetMethod("HandleCamera", BindingFlags.Instance | BindingFlags.NonPublic);
        bench.SetHelpOpen(false);
        rig.position = new Vector3(0, 24, -18);
        Vector3 before = rig.position;
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(300, 300), scroll = new Vector2(0, 1) });
        InputSystem.Update();
        method.Invoke(bench, null);
        float distance = Vector3.Distance(before, rig.position);
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(300, 300), scroll = new Vector2(0, -1) });
        InputSystem.Update();
        method.Invoke(bench, null);
        float returnError = Vector3.Distance(before, rig.position);
        rig.position = saved;
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(300, 300) });
        InputSystem.Update();

        return JsonUtility.ToJson(new Report { passed = distance >= 2 && distance <= 3 && returnError < 0.01f, distance = distance, returnError = returnError });
    }

    [System.Serializable]
    private class Report { public bool passed; public float distance; public float returnError; }
}
