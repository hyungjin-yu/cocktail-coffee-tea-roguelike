namespace CCT
{
    /// <summary>
    /// 런 종료 시에도 초기화되지 않는 영구 데이터 (기획서 11장, 13장).
    /// 지금은 스페셜티(강화 보스 처치로 얻는 영구 재화) 하나뿐이지만, 나중에 항목이 늘어도
    /// JsonUtility로 그대로 직렬화되도록 필드만 추가하면 된다.
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        public int specialtyPoints;
    }
}
