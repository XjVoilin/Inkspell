namespace Game
{
    /// <summary>
    /// 主界面常驻状态显示数据。
    /// 字段可写，供 GM 构造显示测试数据。
    /// </summary>
    public sealed class MainStatusViewData
    {
        public int CurrentStageId { get; set; }
        public int MagicInk { get; set; }
        public int PendingSpellCount { get; set; }
        public float GenerationProgressSeconds { get; set; }
        public float GenerationIntervalSeconds { get; set; }
        public float BookHealth { get; set; }
        public float BookMaxHealth { get; set; }
        public float BookShield { get; set; }
    }
}
