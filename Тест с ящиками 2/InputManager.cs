using SharpHook;
using SharpHook.Data;
//using SharpHook.Native;
using System;
using System.Collections.Concurrent;

public class InputManager : IDisposable
{
    private readonly SimpleGlobalHook _hook;
    private readonly Task _hookTask;
    private readonly ConcurrentQueue<InputEvent> _eventQueue = new ConcurrentQueue<InputEvent>();

    public int MouseX { get; private set; }
    public int MouseY { get; private set; }
    public int LastMouseX { get; private set; }
    public int LastMouseY { get; private set; }

    // Блокируем ТОЛЬКО клики мыши для ОС
    public bool SuppressMouseClicks { get; set; } = false;

    public InputManager()
    {
        _hook = new SimpleGlobalHook();

        _hook.KeyPressed += OnKeyPressed;
        _hook.MouseMoved += OnMouseMoved;
        _hook.MouseDragged += OnMouseMoved; // Движение с зажатой кнопкой
        _hook.MousePressed += OnMousePressed;
        _hook.MouseReleased += OnMouseReleased;

        _hookTask = _hook.RunAsync();
    }

    public bool PollEvent(out InputEvent ev)
    {
        return _eventQueue.TryDequeue(out ev);
    }

    public void UpdateMouseHistory()
    {
        LastMouseX = MouseX;
        LastMouseY = MouseY;
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        _eventQueue.Enqueue(new InputEvent { Type = InputEventType.KeyPressed, KeyCode = e.Data.KeyCode });
    }

    private void OnMousePressed(object? sender, MouseHookEventArgs e)
    {
        // Если флаг активен, это нажатие НЕ пропустит клик в ОС
        if (SuppressMouseClicks)
        {
            e.SuppressEvent = true;
        }

        _eventQueue.Enqueue(new InputEvent
        {
            Type = InputEventType.MousePressed,
            Button = e.Data.Button,
            X = e.Data.X,
            Y = e.Data.Y
        });
    }

    private void OnMouseReleased(object? sender, MouseHookEventArgs e)
    {
        // Если флаг активен, ОС не узнает, что кнопка была отпущена
        if (SuppressMouseClicks)
        {
            e.SuppressEvent = true;
        }

        _eventQueue.Enqueue(new InputEvent
        {
            Type = InputEventType.MouseReleased,
            Button = e.Data.Button,
            X = e.Data.X,
            Y = e.Data.Y
        });
    }

    private void OnMouseMoved(object? sender, MouseHookEventArgs e)
    {
        // МЫШЬ БОЛЬШЕ НЕ БЛОКИРУЕТСЯ. Курсор перемещается свободно по всему экрану ОС.
        MouseX = e.Data.X;
        MouseY = e.Data.Y;
    }

    public void Dispose()
    {
        _hook.Dispose();
        _hookTask.Wait();
    }
}

public enum InputEventType { KeyPressed, MousePressed, MouseReleased }

public struct InputEvent
{
    public InputEventType Type;
    public KeyCode KeyCode;
    public MouseButton Button;
    public int X;
    public int Y;
}
