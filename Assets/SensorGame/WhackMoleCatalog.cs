using UnityEngine;
namespace WhackAMole.Sensors
{
    public sealed class WhackMoleCatalog : ScriptableObject
    {
        public GameObject normalRed,normalBlue,chargeRed,chargeBlue,flashRed,flashBlue;
        public GameObject helmetRed,helmetBlue,balanceRed,balanceBlue,sleepyRed,sleepyBlue,kingRed,kingBlue;
        public GameObject Prefab(MoleKind kind,string team)
        {
            bool red=team=="red";
            switch(kind)
            {
                case MoleKind.Charge:return red?chargeRed:chargeBlue;
                case MoleKind.Flash:return red?flashRed:flashBlue;
                case MoleKind.Helmet:return red?helmetRed:helmetBlue;
                case MoleKind.Balance:return red?balanceRed:balanceBlue;
                case MoleKind.Sleepy:return red?sleepyRed:sleepyBlue;
                case MoleKind.King:return red?kingRed:kingBlue;
                default:return red?normalRed:normalBlue;
            }
        }
        public MoleKind KindOf(GameObject prefab)
        {
            foreach(MoleKind kind in System.Enum.GetValues(typeof(MoleKind)))
                if(prefab!=null&&(prefab==Prefab(kind,"red")||prefab==Prefab(kind,"blue")))return kind;
            return MoleKind.Normal;
        }
    }
}
