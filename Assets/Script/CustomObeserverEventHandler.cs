using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;

public class CustomObserverEventHandler : DefaultObserverEventHandler
{

    public bool tracking_flag = false;
    public Animator FieldOfAnimator;
    public float startTime;
    public bool timeout_flag = false;

    protected override void Start()
    {
        base.Start();
        
        FieldOfAnimator = GetComponentInChildren<Animator>(); 
    }

    void Update()
    {

        //Timeout 処理
        if (timeout_flag == false && tracking_flag == true && Time.time - startTime > 30f)
        {
            // 30秒経過した場合の処理
            Debug.Log("Timeout: Hide the object");
            FieldOfAnimator.SetTrigger("Hide");
            timeout_flag = true;
        }
    }

    protected override void OnTrackingFound()
    {
        Debug.Log("found");
        base.OnTrackingFound(); //隠れていたキャラを表示
        if(!tracking_flag) startTime = Time.time;
        tracking_flag = true;
        timeout_flag = false;
        // ここにマーカーが認識された時の初期化内容を記述
    }


    protected override void OnTrackingLost()
    {
        Debug.Log("lost");
        // ここにマーカーが見えなくなったときの処理を記述
        if (tracking_flag)
        {
            //ふさがれた:AnimatorにDefeatを送る(baseは呼ばない)
            FieldOfAnimator.SetTrigger("Defeat");
        }
        else
        {
            //まだ一度も見つかっていない:base.OnTrackingLost()で隠す
            base.OnTrackingLost();
        }

    }
}