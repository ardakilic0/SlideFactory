using System;
using UnityEngine;

// Raises an event so that one failing listener cannot stop the others,
// and logs the full exception (with stack trace) of the failing one.
public static class SafeEvents
{
    public static void Raise(Action handlers)
    {
        if (handlers == null) return;
        foreach (Delegate d in handlers.GetInvocationList())
        {
            try { ((Action)d)(); }
            catch (Exception e) { Report(d, e); }
        }
    }

    public static void Raise<T>(Action<T> handlers, T arg)
    {
        if (handlers == null) return;
        foreach (Delegate d in handlers.GetInvocationList())
        {
            try { ((Action<T>)d)(arg); }
            catch (Exception e) { Report(d, e); }
        }
    }

    public static void Raise<T1, T2>(Action<T1, T2> handlers, T1 a, T2 b)
    {
        if (handlers == null) return;
        foreach (Delegate d in handlers.GetInvocationList())
        {
            try { ((Action<T1, T2>)d)(a, b); }
            catch (Exception e) { Report(d, e); }
        }
    }

    public static void Run(string context, Action action)
    {
        try { action?.Invoke(); }
        catch (Exception e) { Debug.LogError(context + " failed:\n" + e); }
    }

    private static void Report(Delegate d, Exception e)
    {
        Debug.LogError("Event listener " + d.Method.DeclaringType + "." + d.Method.Name +
                       " failed:\n" + e);
    }
}
