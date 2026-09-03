using System;

namespace Game
{
    [Serializable]
    public readonly struct BattleOutcome
    {
        public BattleOutcome(long battleRunId, int stageId, bool victory)
        {
            BattleRunId = battleRunId;
            StageId = stageId;
            Victory = victory;
        }

        /// <summary>本次战斗尝试的唯一运行标识。</summary>
        public long BattleRunId { get; }

        /// <summary>本次尝试所使用的稳定关卡配置标识。</summary>
        public int StageId { get; }

        public bool Victory { get; }
    }

}
