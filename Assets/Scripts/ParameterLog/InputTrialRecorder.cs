using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
#endif

/// <summary>Observes input without consuming it. All disk I/O runs on a worker.</summary>
public sealed class InputTrialRecorder : IDisposable
{
    public const string Header = "trial_index,time,frame,unix_ms,device,control,phase,value,mouse_x,mouse_y,time_source\n";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private readonly BlockingCollection<string> batches = new BlockingCollection<string>();
    private readonly StringBuilder buffer = new StringBuilder(Header);
    private readonly Thread worker;
    private readonly int trialIndex;
    private readonly double realtimeOrigin;
    private readonly long unixOrigin;
    private double nextFlush;
    private bool ended;
    private Exception writeError;
    private double lastTime;
#if ENABLE_INPUT_SYSTEM
    private struct PendingInput
    {
        public double realtime;
        public int frame;
        public string device, control, phase, value;
        public Vector2 position;
    }
    private readonly List<PendingInput> pending = new List<PendingInput>();
#else
    private readonly List<KeyCode> keys = new List<KeyCode>();
    private int lastPolledFrame;
    private static readonly string[] MouseNames =
        { "leftButton", "rightButton", "middleButton", "backButton", "forwardButton", "button5", "button6" };
#endif

    public InputTrialRecorder(string path, int index, double time)
    {
        trialIndex = index;
        lastTime = time;
        double before = Time.realtimeSinceStartupAsDouble;
        unixOrigin = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        realtimeOrigin = (before + Time.realtimeSinceStartupAsDouble) * 0.5;
        nextFlush = realtimeOrigin + 0.5;
#if ENABLE_INPUT_SYSTEM
        InputSystem.onEvent += OnInputEvent;
#else
        var seen = new HashSet<int>();
        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            if (key > KeyCode.None && key < KeyCode.Mouse0 && seen.Add((int)key)) keys.Add(key);
        // The input that starts a trial belongs to the menu, before this boundary.
        lastPolledFrame = Time.frameCount;
#endif
        worker = new Thread(() => WriteBatches(path)) { IsBackground = true, Name = "Input CSV writer" };
        worker.Start();
        Boundary("trial_start", time);
        FlushBuffer();
    }

    public void Tick(double time, bool clockAdvancing = true)
    {
        if (ended) return;
        lastTime = time;
        double realtime = Time.realtimeSinceStartupAsDouble;
#if ENABLE_INPUT_SYSTEM
        // Bridge the unchanged stream clock to event time at this frame's sample.
        // Wall time always advances, including while the stream clock is paused.
        foreach (var item in pending)
        {
            double eventTime = clockAdvancing ? time + item.realtime - realtime : time;
            Append(Math.Max(0, eventTime), item.frame, item.realtime, item.device,
                item.control, item.phase, item.value, item.position, "event");
        }
        pending.Clear();
#else
        if (lastPolledFrame != Time.frameCount)
        {
            lastPolledFrame = Time.frameCount;
            Vector2 position = Input.mousePosition;
            foreach (KeyCode key in keys)
            {
                if (Input.GetKeyDown(key)) Append(time, Time.frameCount, realtime, "keyboard", key.ToString(), "down", "", position, "frame");
                if (Input.GetKeyUp(key)) Append(time, Time.frameCount, realtime, "keyboard", key.ToString(), "up", "", position, "frame");
            }
            for (int button = 0; button < MouseNames.Length; button++)
            {
                if (Input.GetMouseButtonDown(button)) Append(time, Time.frameCount, realtime, "mouse", MouseNames[button], "down", "", position, "frame");
                if (Input.GetMouseButtonUp(button)) Append(time, Time.frameCount, realtime, "mouse", MouseNames[button], "up", "", position, "frame");
            }
            Vector2 scroll = Input.mouseScrollDelta;
            if (scroll != Vector2.zero) Append(time, Time.frameCount, realtime, "mouse", "wheel", "scroll", ScrollValue(scroll), position, "frame");
        }
#endif
        if (realtime >= nextFlush)
        {
            FlushBuffer();
            nextFlush = realtime + 0.5;
        }
        ReportError();
    }

#if ENABLE_INPUT_SYSTEM
    private void OnInputEvent(InputEventPtr evt, InputDevice device)
    {
        if (ended || evt.time < realtimeOrigin || (!evt.IsA<StateEvent>() && !evt.IsA<DeltaStateEvent>())) return;
        var mouse = device as Mouse;
        Vector2 position = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        if (mouse != null && mouse.position.ReadValueFromEvent(evt, out Vector2 eventPosition)) position = eventPosition;
        var keyboard = device as Keyboard;
        if (keyboard != null)
        {
            foreach (var key in keyboard.allKeys) ObserveButton(evt, key, "keyboard", key.keyCode.ToString(), position);
        }
        else if (mouse != null)
        {
            foreach (var control in mouse.allControls)
                if (control is ButtonControl button && !button.synthetic)
                    ObserveButton(evt, button, "mouse", button.name, position);
            if (mouse.scroll.ReadValueFromEvent(evt, out Vector2 scroll) && scroll != Vector2.zero)
                AddPending(evt, "mouse", "wheel", "scroll", ScrollValue(scroll), position);
        }
    }

    private void ObserveButton(InputEventPtr evt, ButtonControl button, string device, string name, Vector2 position)
    {
        if (!button.ReadValueFromEvent(evt, out float value)) return;
        bool pressed = value >= button.pressPointOrDefault;
        if (pressed != button.isPressed) AddPending(evt, device, name, pressed ? "down" : "up", "", position);
    }

    private void AddPending(InputEventPtr evt, string device, string control, string phase, string value, Vector2 position)
    {
        pending.Add(new PendingInput { realtime = evt.time, frame = Time.frameCount,
            device = device, control = control, phase = phase, value = value, position = position });
    }
#endif

    // Vertical scroll is a number; horizontal/diagonal scroll preserves both axes as x;y.
    private static string ScrollValue(Vector2 scroll) => scroll.x == 0
        ? scroll.y.ToString("R", Invariant)
        : scroll.x.ToString("R", Invariant) + ";" + scroll.y.ToString("R", Invariant);

    private void Boundary(string phase, double time)
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 position = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        Vector2 position = Input.mousePosition;
#endif
        Append(time, Time.frameCount, Time.realtimeSinceStartupAsDouble, "", "", phase, "", position, "frame");
    }

    private void Append(double time, int frame, double realtime, string device, string control,
        string phase, string value, Vector2 position, string source)
    {
        buffer.Append(trialIndex).Append(',').Append(time.ToString("F9", Invariant)).Append(',')
            .Append(frame).Append(',').Append((unixOrigin + (realtime - realtimeOrigin) * 1000).ToString("F3", Invariant)).Append(',')
            .Append(device).Append(',').Append(control).Append(',').Append(phase).Append(',').Append(value).Append(',')
            .Append(position.x.ToString("F3", Invariant)).Append(',').Append(position.y.ToString("F3", Invariant)).Append(',')
            .Append(source).Append('\n');
    }

    private void FlushBuffer()
    {
        if (buffer.Length == 0) return;
        batches.Add(buffer.ToString());
        buffer.Length = 0;
    }

    private void WriteBatches(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
                foreach (string batch in batches.GetConsumingEnumerable())
                {
                    writer.Write(batch);
                    writer.Flush();
                    file.Flush(true);
                }
        }
        catch (Exception error) { Interlocked.Exchange(ref writeError, error); }
    }

    private void ReportError()
    {
        var error = Interlocked.Exchange(ref writeError, null);
        if (error != null) Debug.LogError("[InputTrialRecorder] Input log write failed: " + error);
    }

    public void End(double time, bool clockAdvancing = true)
    {
        if (ended) return;
        Tick(time, clockAdvancing);
        Boundary("trial_end", time);
        ended = true;
#if ENABLE_INPUT_SYSTEM
        InputSystem.onEvent -= OnInputEvent;
#endif
        FlushBuffer();
        batches.CompleteAdding();
        // Only at trial end/exit; never wait for disk during gameplay.
        worker.Join();
        ReportError();
        batches.Dispose();
    }

    public void Dispose() => End(lastTime);
}
