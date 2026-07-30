public enum AreaId
{
    None = 0,
    LabCorridor = 100,
    TreatmentRoom = 200,
    UtilityRoom = 300,
    ServerRoom = 400,
    ControlRoomExterior = 500,
    CCTVRoom = 600,
}

public enum ReportTargetId
{
    None = 0,
    Wheelchair = 100,
    Chair = 101,
    Door = 102,
    Light = 103,
    Portrait = 104,
    Curtain = 105,
    ToolBox = 106,
    PatientBed = 107,
    ExitSign = 108,
    Clock = 109,
    Phone = 110,
    Person = 200,
    Monitor = 202,
    ServerRack = 203,
    Vent = 204,
    Sound = 300,
    CCTVVideo = 400,
}

public enum AnomalyReportType
{
    None = 0,
    PositionChange = 100,
    Added = 101,
    Missing = 102,
    StateChange = 103,
    ShapeChange = 104,
    AbnormalBehavior = 105,
}

public enum AnomalyActionType
{
    None = 0,
    SetActive = 1,
    MoveToLocalPosition = 2,
    ChangeSprite = 3,
    ChangeColor = 4,
    SetLocalEulerAngles = 5,
    MoveByLocalPositionOffset = 6,
}

public enum AnomalyState
{
    Inactive = 0,
    Activating = 1,
    Active = 2,
    Normalizing = 3,
    Resolved = 4,
    Missed = 5,
}


