using UnityEngine;
using Vuforia;

public class ModelSwapper : MonoBehaviour
    
{
    public ObserverBehaviour theTrackable;
    public GameObject unity_chan_prefab;

    private CustomObserverEventHandler script1, script2, script3;

    private bool target1_flag = false;
    private bool target2_flag = false;
    private bool target3_flag = false;
    private bool change_flag = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        /*
        ImageTarget1と2のDefaultTrackableEventHandlerにアクセスできるように
        宣言する
        */
        GameObject object1 = GameObject.Find("ImageTarget1");
        GameObject object2 = GameObject.Find("ImageTarget2");
        GameObject object3 = GameObject.Find("ImageTarget3");
        script1 = object1.GetComponent<CustomObserverEventHandler>();
        script2 = object2.GetComponent<CustomObserverEventHandler>();
        script3 = object3.GetComponent<CustomObserverEventHandler>();
    }

    // Update is called once per frame
    void Update()
    {
        /*
       ImageTarget1と2の両方でマーカーが認識されている, かつ一度もキューブが
       unity_chanに変更されてなければSwapModel()関数を実行
       */
        target1_flag = script1.tracking_flag;
        target2_flag = script2.tracking_flag;
        target3_flag = script3.tracking_flag;
        if (target1_flag && target2_flag && target3_flag &!change_flag)
        {
            change_flag = true;
            SwapModel();
        }
    }
    private void SwapModel()
    {
        GameObject trackableGameObject = theTrackable.gameObject;
        // 既存の表示を無効にする
        Transform child = trackableGameObject.transform.GetChild(0);
        child.gameObject.SetActive(false);
        /*
        for (int i = 0; i < trackableGameObject.transform.childCount; i++)
        {
            Transform child = trackableGameObject.transform.GetChild(i);
            //child.gameObject.active = false;
            child.gameObject.SetActive(false);
        }
        */
    }
       
}
