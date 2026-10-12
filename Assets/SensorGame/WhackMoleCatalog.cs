using UnityEngine;
namespace WhackAMole.Sensors
{
    public sealed class WhackMoleCatalog : ScriptableObject
    {
        public GameObject normalRed,normalBlue,chargeRed,chargeBlue,flashRed,flashBlue;
        public GameObject Prefab(MoleKind kind,string team)
        {
            bool red=team=="red";
            return kind==MoleKind.Charge?(red?chargeRed:chargeBlue):kind==MoleKind.Flash?(red?flashRed:flashBlue):(red?normalRed:normalBlue);
        }
    }
}
