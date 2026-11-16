using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

public static class InputRecordingChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("Input recording regression: " + message);
    }

#if ENABLE_INPUT_SYSTEM
    private static void QueueKeyboard(Keyboard keyboard, bool pressed, double time)
    {
        using (StateEvent.FromDefaultStateFor(keyboard, out var evt))
        {
            keyboard.aKey.WriteValueIntoEvent(pressed ? 1f : 0f, evt);
            keyboard.f9Key.WriteValueIntoEvent(pressed ? 1f : 0f, evt);
            evt.time = time;
            InputSystem.QueueEvent(evt);
        }
    }

    private static void QueueMouse(Mouse mouse, int button, bool pressed, double time)
    {
        var buttons = new[] { mouse.leftButton, mouse.rightButton, mouse.middleButton, mouse.forwardButton, mouse.backButton };
        using (StateEvent.FromDefaultStateFor(mouse, out var evt))
        {
            buttons[button].WriteValueIntoEvent(pressed ? 1f : 0f, evt);
            evt.time = time;
            InputSystem.QueueEvent(evt);
        }
    }
#endif

    public static void Run()
    {
#if ENABLE_INPUT_SYSTEM
        // Initialize the editor input timeline before assigning explicit event timestamps.
        InputSystem.Update();
#endif
        string directory = Environment.GetEnvironmentVariable("OVGL_INPUT_CHECK_DIR") ?? Path.GetTempPath();
        string path = Path.Combine(directory, "input_check_" + Guid.NewGuid().ToString("N") + ".csv");
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        using (var recorder = new InputTrialRecorder(path, 7, 0))
        {
            // A shared readable CSV must exist before the trial ends (crash recovery).
            for (int attempt = 0; attempt < 100 && !File.Exists(path); attempt++) Thread.Sleep(10);
            Thread.Sleep(50);
            using (var reading = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(reading))
                Check(reader.ReadToEnd().Contains("trial_start"), "start checkpoint was not flushed");
#if ENABLE_INPUT_SYSTEM
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            double now = Time.realtimeSinceStartupAsDouble;
            QueueKeyboard(keyboard, true, now);
            QueueKeyboard(keyboard, false, now + 0.010);
            InputSystem.Update();
            for (int button = 0; button < 5; button++)
            {
                QueueMouse(mouse, button, true, now + 0.020 + button * 0.020);
                QueueMouse(mouse, button, false, now + 0.030 + button * 0.020);
                InputSystem.Update();
            }
            using (StateEvent.FromDefaultStateFor(mouse, out var evt))
            {
                mouse.scroll.WriteValueIntoEvent(new Vector2(0, 120), evt);
                mouse.position.WriteValueIntoEvent(new Vector2(123, 456), evt);
                evt.time = now + 0.140;
                InputSystem.QueueEvent(evt);
            }
            InputSystem.Update();
            recorder.Tick(1.0);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
#endif
            recorder.End(1.25);
            recorder.End(1.25);
        }
        string[] lines = File.ReadAllLines(path);
        Check(lines[0] == InputTrialRecorder.Header.TrimEnd('\n'), "schema changed");
        var rows = lines.Skip(1).Select(line => line.Split(',')).ToArray();
        Check(rows.All(row => row.Length == 11 && row[0] == "7"), "invalid columns/trial number");
        Check(rows.Count(row => row[6] == "trial_start") == 1 && rows.Count(row => row[6] == "trial_end") == 1, "duplicate/missing boundary");
        Check(rows[0][1] == "0.000000000" && rows.Last()[1] == "1.250000000", "clock changed/reset at close");
        Check(double.Parse(rows[0][3], System.Globalization.CultureInfo.InvariantCulture) >= before - 5, "UTC origin incorrect");
#if ENABLE_INPUT_SYSTEM
        foreach (string control in new[] { "A", "F9", "leftButton", "rightButton", "middleButton", "forwardButton", "backButton" })
        {
            var pair = rows.Where(row => row[5] == control).ToArray();
            Check(pair.Length == 2 && pair[0][6] == "down" && pair[1][6] == "up", "missing pair for " + control);
            Check(pair.All(row => row[10] == "event"), "not event timestamps");
            double delta = double.Parse(pair[1][1], System.Globalization.CultureInfo.InvariantCulture) - double.Parse(pair[0][1], System.Globalization.CultureInfo.InvariantCulture);
            Check(Math.Abs(delta - 0.010) < 0.000001, "subframe timing lost");
            double wallDelta = double.Parse(pair[1][3], System.Globalization.CultureInfo.InvariantCulture) - double.Parse(pair[0][3], System.Globalization.CultureInfo.InvariantCulture);
            Check(Math.Abs(wallDelta - 10) < 0.01, "wall clock did not use event interval");
        }
        Check(rows.Any(row => row[6] == "scroll" && row[7] == "120" && row[8] == "123.000" && row[9] == "456.000"), "scroll/event coordinates missing");
#endif
        Debug.Log("INPUT_RECORDING_CHECKS_PASSED " + path);
    }
}
