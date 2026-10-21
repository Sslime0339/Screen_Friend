using System;

internal readonly record struct WindowData(
    nint Handle,
    float Width,
    float Height,
    float CenterX,
    float CenterY);
