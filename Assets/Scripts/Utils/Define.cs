namespace define
{
    public enum CameraViewType
    {
        Left,
        Center,
        Right
    }

    public enum StageID
    {
        Stage1 = 1,
        Stage2 = 2,
        Stage3 = 3,
        Stage4 = 4,
        Stage5 = 5,
    }

    public enum EffectType
    {
        None = 0,
        Shake = 1,      // 화면 흔들림
        Zoom = 2,       // 화면 줌인
    }

    public enum DialogueState
    {
        Idle,       // 출력 이전
        Typing,     // 출력 중
        Completed,  // 출력 완료
        Finished    // 마지막 문장 출력 완료
    }

    public enum DialogueIdentity
    {
        None = 0,           // 미등록
        Stage1 = 1,         // 스토리 모드 - 1
        Stage2 = 2,         // 스토리 모드 - 2
        Stage3 = 3,         // 스토리 모드 - 3
        Stage4 = 4,         // 스토리 모드 - 4
        Stage5 = 5,         // 스토리 모드 - 5
    }
}