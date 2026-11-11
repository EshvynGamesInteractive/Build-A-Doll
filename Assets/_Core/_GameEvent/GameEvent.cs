using System;

public class GameEvent
{
    private event Action Event;

    public void Register(Action listener)
    {
        Event += listener;
    }

    public void Unregister(Action listener)
    {
        Event -= listener;
    }

    public void Raise()
    {
        Event?.Invoke();
    }

    public void UnregisterAll()
    {
        Event = null;
    }
}

public class GameEvent<T>
{
    private event Action<T> Event;

    public void Register(Action<T> listener)
    {
        Event += listener;
    }

    public void Unregister(Action<T> listener)
    {
        Event -= listener;
    }

    public void Raise(T arg)
    {
        Event?.Invoke(arg);
    }

    public void UnregisterAll()
    {
        Event = null;
    }
}

public class GameEvent<T1, T2>
{
    private event Action<T1, T2> Event;

    public void Register(Action<T1, T2> listener)
    {
        Event += listener;
    }

    public void Unregister(Action<T1, T2> listener)
    {
        Event -= listener;
    }

    public void Raise(T1 arg1, T2 arg2)
    {
        Event?.Invoke(arg1, arg2);
    }

    public void UnregisterAll()
    {
        Event = null;
    }
}

public class GameEvent<T1, T2, T3>
{
    private event Action<T1, T2, T3> Event;

    public void Register(Action<T1, T2, T3> listener)
    {
        Event += listener;
    }

    public void Unregister(Action<T1, T2, T3> listener)
    {
        Event -= listener;
    }

    public void Raise(T1 arg1, T2 arg2, T3 arg3)
    {
        Event?.Invoke(arg1, arg2, arg3);
    }

    public void UnregisterAll()
    {
        Event = null;
    }
}