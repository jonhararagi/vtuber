using System.Diagnostics;

namespace VtuberBot.Core;

/// <summary>
/// Invokes each event subscriber independently. An exception in a view callback must not be
/// interpreted by a network supervisor as a transport failure or prevent other listeners running.
/// </summary>
public static class SafeEventDispatch
{
    public static void Invoke<T>(Action<T>? subscribers, T argument)
    {
        if (subscribers is null)
            return;

        foreach (var subscriber in subscribers.GetInvocationList())
        {
            try
            {
                ((Action<T>)subscriber)(argument);
            }
            catch (Exception exception)
            {
                Trace.TraceError("Event subscriber failed ({0}).", exception.GetType().Name);
            }
        }
    }

    public static void Invoke<TFirst, TSecond>(
        Action<TFirst, TSecond>? subscribers,
        TFirst firstArgument,
        TSecond secondArgument)
    {
        if (subscribers is null)
            return;

        foreach (var subscriber in subscribers.GetInvocationList())
        {
            try
            {
                ((Action<TFirst, TSecond>)subscriber)(firstArgument, secondArgument);
            }
            catch (Exception exception)
            {
                Trace.TraceError("Event subscriber failed ({0}).", exception.GetType().Name);
            }
        }
    }
}
