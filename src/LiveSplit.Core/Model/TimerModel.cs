﻿using System;
using System.Collections.Generic;
using System.Linq;

using LiveSplit.Model.Input;

namespace LiveSplit.Model;

public class TimerModel : ITimerModel
{
    public LiveSplitState CurrentState
    {
        get => _CurrentState;
        set
        {
            _CurrentState = value;
            value?.RegisterTimerModel(this);
        }
    }

    private LiveSplitState _CurrentState;

    public event EventHandler OnSplit;
    public event EventHandler OnUndoSplit;
    public event EventHandler OnSkipSplit;
    public event EventHandler OnStart;
    public event EventHandlerT<TimerPhase> OnReset;
    public event EventHandler OnPause;
    public event EventHandler OnUndoAllPauses;
    public event EventHandler OnResume;
    public event EventHandler OnScrollUp;
    public event EventHandler OnScrollDown;
    public event EventHandler OnSwitchComparisonPrevious;
    public event EventHandler OnSwitchComparisonNext;

    public void Start()
    {
        if (CurrentState.CurrentPhase == TimerPhase.NotRunning)
        {
            CurrentState.CurrentPhase = TimerPhase.Running;
            CurrentState.CurrentSplitIndex = 0;
            CurrentState.AttemptStarted = TimeStamp.CurrentDateTime;
            CurrentState.AdjustedStartTime = CurrentState.StartTimeWithOffset = TimeStamp.Now - CurrentState.Run.Offset;
            CurrentState.StartTime = TimeStamp.Now;
            CurrentState.TimePausedAt = CurrentState.Run.Offset;
            CurrentState.IsGameTimeInitialized = false;
            CurrentState.Run.AttemptCount++;
            CurrentState.Run.HasChanged = true;

            OnStart?.Invoke(this, null);
        }
    }

    public void InitializeGameTime()
    {
        CurrentState.IsGameTimeInitialized = true;
    }

    public void Split()
    {
        if (CurrentState.CurrentPhase == TimerPhase.Running && CurrentState.CurrentTime.RealTime > TimeSpan.Zero)
        {
            CurrentState.CurrentSplit.SplitTime = CurrentState.CurrentTime;
            foreach (KeyValuePair<string, CustomVariable> kv in CurrentState.Run.Metadata.CustomVariables)
            {
                CurrentState.CurrentSplit.CustomVariableValues[kv.Key] = kv.Value.Value;
            }

            CurrentState.CurrentSplitIndex++;
            if (CurrentState.Run.Count == CurrentState.CurrentSplitIndex)
            {
                CurrentState.CurrentPhase = TimerPhase.Ended;
                CurrentState.AttemptEnded = TimeStamp.CurrentDateTime;
            }

            CurrentState.Run.HasChanged = true;

            OnSplit?.Invoke(this, null);
        }
    }

    public void SkipSplit()
    {
        if ((CurrentState.CurrentPhase == TimerPhase.Running
            || CurrentState.CurrentPhase == TimerPhase.Paused)
            && CurrentState.CurrentSplitIndex < CurrentState.Run.Count - 1)
        {
            CurrentState.CurrentSplit.SplitTime = default;
            CurrentState.CurrentSplit.CustomVariableValues.Clear();
            CurrentState.CurrentSplitIndex++;
            CurrentState.Run.HasChanged = true;

            OnSkipSplit?.Invoke(this, null);
        }
    }

    public void UndoSplit()
    {
        if (CurrentState.CurrentPhase != TimerPhase.NotRunning
            && CurrentState.CurrentSplitIndex > 0)
        {
            if (CurrentState.CurrentPhase == TimerPhase.Ended)
            {
                CurrentState.CurrentPhase = TimerPhase.Running;
            }

            CurrentState.CurrentSplitIndex--;
            CurrentState.CurrentSplit.SplitTime = default;
            CurrentState.CurrentSplit.CustomVariableValues.Clear();
            CurrentState.Run.HasChanged = true;

            OnUndoSplit?.Invoke(this, null);
        }
    }

    public void Reset()
    {
        Reset(true);
    }

    public void Reset(bool updateSplits)
    {
        if (CurrentState.CurrentPhase != TimerPhase.NotRunning)
        {
            ResetState(updateSplits);
            ResetSplits();
        }
    }

    private void ResetState(bool updateTimes)
    {
        if (CurrentState.CurrentPhase != TimerPhase.Ended)
        {
            CurrentState.AttemptEnded = TimeStamp.CurrentDateTime;
        }

        CurrentState.IsGameTimePaused = false;
        CurrentState.LoadingTimes = TimeSpan.Zero;

        if (updateTimes)
        {
            UpdateTimes();
        }
    }

    private void ResetSplits()
    {
        TimerPhase oldPhase = CurrentState.CurrentPhase;
        CurrentState.CurrentPhase = TimerPhase.NotRunning;
        CurrentState.CurrentSplitIndex = -1;

        //Reset Splits
        foreach (ISegment split in CurrentState.Run)
        {
            split.SplitTime = default;
            split.CustomVariableValues.Clear();
        }

        OnReset?.Invoke(this, oldPhase);

        CurrentState.Run.FixSplits();
    }

    public void LoadPaused(IRun run, LiveSplitState state)
    {
        Time pausedAt = run.PausedRun.InProgressTimes.Last();
        Attempt pausedAttempt = run.PausedRun.InProgressAttempt.Value;

        state.AttemptStarted = pausedAttempt.Started.Value;
        state.StartTime = TimeStamp.Now - pausedAt.RealTime.Value;
        state.StartTimeWithOffset = state.StartTime - run.Offset;
        state.LoadingTimes = pausedAt.RealTime.Value - (pausedAt.GameTime ?? pausedAt.RealTime.Value);
        state.AdjustedStartTime = state.StartTimeWithOffset + pausedAttempt.PauseTime.Value;
        state.IsGameTimePaused = false;
        state.CurrentSplitIndex = run.PausedRun.InProgressTimes.Count - 1;

        run.HasChanged = false;
        state.TimePausedAt = TimeStamp.Now - state.AdjustedStartTime;
        state.CurrentPhase = TimerPhase.Paused;
        
        for (int i = 0; i < run.Count; i++)
        {
            run[i].SplitTime = i < run.PausedRun.InProgressTimes.Count ? run.PausedRun.InProgressTimes[i] : default;
        }

        OnPause?.Invoke(this, null);
    }

    public void Pause()
    {
        if (CurrentState.CurrentPhase == TimerPhase.Running)
        {
            CurrentState.TimePausedAt = CurrentState.CurrentTime.RealTime.Value;
            CurrentState.CurrentPhase = TimerPhase.Paused;
            CurrentState.Run.PausedRun.InProgressAttempt = new Attempt(
                CurrentState.Run.AttemptCount,
                new Time(),
                CurrentState.AttemptStarted,
                null,
                CurrentState.PauseTime
            );
            CurrentState.Run.PausedRun.InProgressTimes = [.. CurrentState.Run.Select(x => x.SplitTime)];
            CurrentState.Run.PausedRun.InProgressTimes[CurrentState.CurrentSplitIndex] = CurrentState.CurrentTime;
            CurrentState.Run.HasChanged = true;
            OnPause?.Invoke(this, null);
        }
        else if (CurrentState.CurrentPhase == TimerPhase.Paused)
        {
            CurrentState.AdjustedStartTime = TimeStamp.Now - CurrentState.TimePausedAt;
            CurrentState.CurrentPhase = TimerPhase.Running;
            OnResume?.Invoke(this, null);
        }
        else if (CurrentState.CurrentPhase == TimerPhase.NotRunning)
        {
            Start(); //fuck abahbob                
        }
    }

    public void UndoAllPauses()
    {
        if (CurrentState.CurrentPhase == TimerPhase.Paused)
        {
            Pause();
        }

        TimeSpan pauseTime = CurrentState.PauseTime ?? TimeSpan.Zero;
        if (CurrentState.CurrentPhase == TimerPhase.Ended)
        {
            CurrentState.Run.Last().SplitTime += new Time(pauseTime, pauseTime);
        }

        CurrentState.AdjustedStartTime = CurrentState.StartTimeWithOffset;
        OnUndoAllPauses?.Invoke(this, null);
    }

    public void SwitchComparisonNext()
    {
        var comparisons = CurrentState.Run.Comparisons.ToList();
        CurrentState.CurrentComparison =
            comparisons.ElementAt((comparisons.IndexOf(CurrentState.CurrentComparison) + 1)
            % comparisons.Count);
        OnSwitchComparisonNext?.Invoke(this, null);
    }

    public void SwitchComparisonPrevious()
    {
        var comparisons = CurrentState.Run.Comparisons.ToList();
        CurrentState.CurrentComparison =
            comparisons.ElementAt((comparisons.IndexOf(CurrentState.CurrentComparison) - 1 + comparisons.Count())
            % comparisons.Count);
        OnSwitchComparisonPrevious?.Invoke(this, null);
    }

    public void ScrollUp()
    {
        OnScrollUp?.Invoke(this, null);
    }

    public void ScrollDown()
    {
        OnScrollDown?.Invoke(this, null);
    }

    private void UpdateAttemptHistory()
    {
        var time = new Time();
        if (CurrentState.CurrentPhase == TimerPhase.Ended)
        {
            time = CurrentState.CurrentTime;
        }

        int maxIndex = CurrentState.Run.AttemptHistory.DefaultIfEmpty().Max(x => x.Index);
        int newIndex = Math.Max(0, maxIndex + 1);
        var newAttempt = new Attempt(newIndex, time, CurrentState.AttemptStarted, CurrentState.AttemptEnded, CurrentState.PauseTime);
        CurrentState.Run.AttemptHistory.Add(newAttempt);
    }

    private void UpdateBestSegments()
    {
        TimeSpan? currentSegmentRTA = TimeSpan.Zero;
        TimeSpan? previousSplitTimeRTA = TimeSpan.Zero;
        TimeSpan? currentSegmentGameTime = TimeSpan.Zero;
        TimeSpan? previousSplitTimeGameTime = TimeSpan.Zero;
        foreach (ISegment split in CurrentState.Run)
        {
            var newBestSegment = new Time(split.BestSegmentTime);
            if (split.SplitTime.RealTime != null)
            {
                currentSegmentRTA = split.SplitTime.RealTime - previousSplitTimeRTA;
                previousSplitTimeRTA = split.SplitTime.RealTime;
                if (split.BestSegmentTime.RealTime == null || currentSegmentRTA < split.BestSegmentTime.RealTime)
                {
                    newBestSegment.RealTime = currentSegmentRTA;
                }
            }

            if (split.SplitTime.GameTime != null)
            {
                currentSegmentGameTime = split.SplitTime.GameTime - previousSplitTimeGameTime;
                previousSplitTimeGameTime = split.SplitTime.GameTime;
                if (split.BestSegmentTime.GameTime == null || currentSegmentGameTime < split.BestSegmentTime.GameTime)
                {
                    newBestSegment.GameTime = currentSegmentGameTime;
                }
            }

            split.BestSegmentTime = newBestSegment;
        }
    }

    private void UpdatePBSplits()
    {
        TimingMethod curMethod = CurrentState.CurrentTimingMethod;
        if ((CurrentState.Run.Last().SplitTime[curMethod] != null && CurrentState.Run.Last().PersonalBestSplitTime[curMethod] == null) || CurrentState.Run.Last().SplitTime[curMethod] < CurrentState.Run.Last().PersonalBestSplitTime[curMethod])
        {
            SetRunAsPB();
        }
    }

    private void UpdateSegmentHistory()
    {
        TimeSpan? splitTimeRTA = TimeSpan.Zero;
        TimeSpan? splitTimeGameTime = TimeSpan.Zero;
        foreach (ISegment split in CurrentState.Run.Take(CurrentState.CurrentSplitIndex))
        {
            var newTime = new Time
            {
                RealTime = split.SplitTime.RealTime - splitTimeRTA,
                GameTime = split.SplitTime.GameTime - splitTimeGameTime
            };
            split.SegmentHistory.Add(CurrentState.Run.AttemptHistory.Last().Index, newTime);
            if (split.SplitTime.RealTime.HasValue)
            {
                splitTimeRTA = split.SplitTime.RealTime;
            }

            if (split.SplitTime.GameTime.HasValue)
            {
                splitTimeGameTime = split.SplitTime.GameTime;
            }
        }
    }

    public void UpdateTimes()
    {
        UpdateAttemptHistory();
        UpdateBestSegments();
        UpdatePBSplits();
        UpdateSegmentHistory();
    }

    public void ResetAndSetAttemptAsPB()
    {
        if (CurrentState.CurrentPhase != TimerPhase.NotRunning)
        {
            ResetState(true);
            SetRunAsPB();
            ResetSplits();
        }
    }

    private void SetRunAsPB()
    {
        CurrentState.Run.ImportSegmentHistory();
        CurrentState.Run.FixSplits();
        foreach (ISegment current in CurrentState.Run)
        {
            current.PersonalBestSplitTime = current.SplitTime;
        }

        CurrentState.Run.Metadata.RunID = null;
    }
}
