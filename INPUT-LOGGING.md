# Experiment 3 input CSV

Automatic logs: `OVGL/<productName>/PlayLogs/run_<GUID>/input_trial1.csv` (UTF-8).
Each automatic play has a new folder and existing automatic stream/snapshot trial index 1; input uses the same index. Existing CSV filenames, columns and gameplay are unchanged.

Columns: `trial_index,time,frame,unix_ms,device,control,phase,value,mouse_x,mouse_y,time_source`.

- `time`: seconds from the existing RecordingClock.ElapsedSeconds / stream time. No changes to the clock or pause behavior.
- `frame`: Unity application frame number (`Time.frameCount`), including boundary rows.
- Input `time_source`: `event`. Boundaries use `frame`; device/control/value are empty.
- `unix_ms`: fractional UTC epoch milliseconds, anchored once at trial start to Unity realtime. It continues during a paused game clock. No per-row wall-clock reads.
- Keyboard: all keys exposed by the active Unity keyboard backend, down/up only; no held/repeat rows. Mouse: every exposed button, plus wheel. No synthetic release is created at trial end; a key held across the boundary can have only one edge in that trial.
- `value`: empty except scroll. Vertical scroll is the backend's raw signed amount; horizontal/diagonal scroll is `x;y`. Legacy and Input System wheel units may differ.
- Screen coordinates: pixels, Unity bottom-left origin, included for keyboard and boundary rows too.
- Separate worker writes and flushes to disk every 0.5 seconds while inputs arrive. Trial start is immediately queued; end/abort/normal exit drains the queue. Force termination preserves flushed rows but cannot add a trial_end marker; the most recent interval can be lost.
- Input remains limited to what Unity receives while the app has focus. OS-reserved/unexposed keys cannot be observed. Legacy frame polling can miss a complete tap within one frame and has frame timing uncertainty (about 17 ms at 60 fps).

Chameleon retains Both input backends for gameplay. The logger observes InputSystem.onEvent without consuming or changing input, compares state-event button values before application, and preserves InputEvent.time. Event timestamps are mapped to the unchanged stream clock at each sample; UTC uses the original event timestamp. Multiple edges within one frame retain their event intervals.
