using System;
using SensorBridge;
namespace WhackAMole.Sensors
{
    public enum MoleKind { Normal, Charge, Flash, Helmet, Balance, Sleepy, King }
    public static class SensorGamePolicy
    {
        public static bool CanDefeat(SensorArenaModel model,MoleKind kind,string team,double now)
            => !model.MatchBusy && (Cost(model,kind)<=0||model.Spend(team,now,Cost(model,kind)));
        public static double Cost(SensorArenaModel model,MoleKind kind) => kind==MoleKind.King?model.KingChargeCost:kind==MoleKind.Charge?model.ChargeCost:0;
        public static int ScoreMultiplier(MoleKind kind) => kind==MoleKind.King?5:kind==MoleKind.Helmet?2:kind==MoleKind.Normal?1:3;
        public static float MinimumLifetime(MoleKind kind) => kind==MoleKind.King?7:kind==MoleKind.Helmet?5:kind==MoleKind.Charge||kind==MoleKind.Balance||kind==MoleKind.Sleepy?6:0;
        public static string Name(MoleKind kind)
        {
            switch(kind){case MoleKind.Charge:return "ぜんまい";case MoleKind.Flash:return "スピード";case MoleKind.Helmet:return "ヘルメット";case MoleKind.Balance:return "バランス";case MoleKind.Sleepy:return "おねむ";case MoleKind.King:return "王様";default:return "通常";}
        }
        public static void RewardWinner(SensorArenaModel model)
        {
            model.ApplyContestReward();
        }
    }
}
