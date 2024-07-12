namespace Antelcat.Media.Abstractions.Interfaces;

public interface IStateMachine<TState> where TState : struct, Enum
{
    TState CurrentState { get; }

    public delegate void StateChangeHandler(TState oldState, TState newState);

    event StateChangeHandler? StateChanging;
}