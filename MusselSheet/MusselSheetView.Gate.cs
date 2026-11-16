namespace MusselSheet;

using System.Runtime.CompilerServices;

using Microsoft.AspNetCore.Components;

public sealed partial class MusselSheetView
{
    private static readonly ConditionalWeakTable<Sheet, SemaphoreSlim> Gates = [];

    private static readonly AsyncLocal<GateTurn?> CurrentTurn = new();

    private SemaphoreSlim Gate => Gates.GetValue(Sheet, static _ => new SemaphoreSlim(1, 1));

    internal async Task<T> RunInTurnAsync<T>(Func<Task<T>> operation)
    {
        var turn = await EnterGateAsync().ConfigureAwait(true);
        turn?.Begin();
        try
        {
            return await operation().ConfigureAwait(true);
        }
        finally
        {
            turn?.End();
        }
    }

    internal async Task RunInTurnAsync(Func<Task> operation)
    {
        var turn = await EnterGateAsync().ConfigureAwait(true);
        turn?.Begin();
        try
        {
            await operation().ConfigureAwait(true);
        }
        finally
        {
            turn?.End();
        }
    }

    private static async Task HandleAsync<T>(EventCallback<T> callback, T args)
    {
        var turn = CurrentTurn.Value;
        if (turn is null)
        {
            await callback.InvokeAsync(args).ConfigureAwait(true);
            return;
        }

        turn.Enter();
        try
        {
            await callback.InvokeAsync(args).ConfigureAwait(true);
        }
        finally
        {
            turn.Exit();
        }
    }

    private async Task<GateTurn?> EnterGateAsync()
    {
        var gate = Gate;
        if ((CurrentTurn.Value is { Handling: true } turn) && ReferenceEquals(turn.Gate, gate))
        {
            return null;
        }

        await gate.WaitAsync().ConfigureAwait(true);
        return new GateTurn(gate);
    }

    private sealed class GateTurn(SemaphoreSlim gate)
    {
        private int handlers;

        public SemaphoreSlim Gate => gate;

        public bool Handling => handlers > 0;

        public void Begin() => CurrentTurn.Value = this;

        public void Enter() => handlers++;

        public void Exit() => handlers--;

        public void End()
        {
            handlers = 0;
            CurrentTurn.Value = null;
            gate.Release();
        }
    }
}
