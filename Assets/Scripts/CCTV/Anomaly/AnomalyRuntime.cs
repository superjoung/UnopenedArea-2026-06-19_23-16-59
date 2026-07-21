public sealed class AnomalyRuntime
{
    public AnomalyDefinition Definition { get; }
    public AnomalyState State { get; private set; }
    public float RemainingActiveTimeSec { get; private set; }

    public AnomalyRuntime(AnomalyDefinition definition)
    {
        Definition = definition;
        State = AnomalyState.Inactive;
        RemainingActiveTimeSec = definition != null ? definition.ActiveDurationSec : 0f;
    }

    public void ChangeState(AnomalyState state)
    {
        State = state;
    }

    public void ActivateTimer()
    {
        RemainingActiveTimeSec = Definition != null ? Definition.ActiveDurationSec : 0f;
        State = AnomalyState.Active;
    }

    public void Tick(float deltaTime)
    {
        if (State != AnomalyState.Active)
            return;

        RemainingActiveTimeSec -= deltaTime;
    }

    public bool IsTimedOut => State == AnomalyState.Active && RemainingActiveTimeSec <= 0f;
}
