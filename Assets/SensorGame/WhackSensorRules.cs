using System;
using SensorBridge;
namespace WhackAMole.Sensors
{
    public enum MoleKind { Normal, Charge, Flash }
    public static class SensorGamePolicy
    {
        public static bool CanDefeat(SensorArenaModel model,MoleKind kind,string team,double now)
            => !model.MatchBusy && (kind!=MoleKind.Charge||model.Spend(team,now));
        public static void RewardWinner(SensorArenaModel model)
        {
            var winner=model.Result=="赤チームの勝ち！"?model.Red:model.Result=="青チームの勝ち！"?model.Blue:null;
            if(winner!=null)winner.Charge=Math.Min(model.ChargeLimit,winner.Charge+25);
        }
    }
}
