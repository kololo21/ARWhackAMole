#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WhackAMole.Sensors;

// Runs in the saved-scene integration verifier, with synthetic imagery, never
// captures a user's camera feed or modifies their saved scene.
public static class CameraDisplayChecks
{
    private static void Check(bool condition,string message)
    {if(!condition)throw new InvalidOperationException("Camera display: "+message);}

    public static void Run(string output)
    {
        var settings=CameraDisplaySettings.Instance;
        Check(settings!=null&&!settings.IsFlipped,"each Play session starts without a flip");
        var horizontal=WhackSensorGame.SceneObject("FlipHorizontal").GetComponent<Button>();
        var vertical=WhackSensorGame.SceneObject("FlipVertical").GetComponent<Button>();
        var reset=WhackSensorGame.SceneObject("ResetCameraFlip").GetComponent<Button>();
        horizontal.onClick.Invoke();Check(settings.FlipHorizontal&&!settings.FlipVertical,"horizontal control is independent");
        vertical.onClick.Invoke();Check(settings.FlipHorizontal&&settings.FlipVertical,"both controls combine");
        horizontal.onClick.Invoke();Check(!settings.FlipHorizontal&&settings.FlipVertical,"vertical control is independent");
        reset.onClick.Invoke();Check(!settings.IsFlipped,"reset control");

        var camera=Camera.main;var canvas=WhackSensorGame.SceneObject("Canvas").GetComponent<Canvas>();
        var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
        int mask=camera.cullingMask;bool canvasEnabled=canvas.enabled;var previousRect=camera.rect;
        var target=new RenderTexture(640,360,24);var texture=new Texture2D(640,360,TextureFormat.RGB24,false);
        var quads=new GameObject[4];var materials=new Material[4];
        var colors=new[]{Color.red,Color.green,Color.blue,Color.yellow};
        try
        {
            camera.targetTexture=target;camera.cullingMask=1<<31;canvas.enabled=false;
            for(int i=0;i<4;i++)
            {
                var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quads[i]=quad;quad.layer=31;
                quad.transform.position=camera.ViewportToWorldPoint(new Vector3(i%2==0?.25f:.75f,i<2?.25f:.75f,1));
                quad.transform.rotation=camera.transform.rotation;quad.transform.localScale=Vector3.one*.2f;
                materials[i]=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                materials[i].SetColor("_BaseColor",colors[i]);quad.GetComponent<Renderer>().sharedMaterial=materials[i];
            }
            for(int mode=0;mode<4;mode++)
            {
                bool h=(mode&1)!=0,v=(mode&2)!=0;settings.Configure(h,v);
                camera.Render();RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,640,360),0,0);texture.Apply();
                for(int i=0;i<4;i++)
                {
                    int expected=i^(h?1:0)^(v?2:0);
                    Color actual=texture.GetPixel(i%2==0?160:480,i<2?90:270);
                    Check(Vector3.Distance(new Vector3(actual.r,actual.g,actual.b),new Vector3(colors[expected].r,colors[expected].g,colors[expected].b))<.08f,
                        "rendered quadrant matches mode "+mode+", corner "+i+" ("+actual+")");
                }
                var point=CameraDisplaySettings.ToDisplayPoint(new Vector3(160,90,2),camera);
                Check(Math.Abs(point.x-(h?480:160))<.01&&Math.Abs(point.y-(v?270:90))<.01&&point.z==2,"overlay anchor follows mode "+mode);
                File.WriteAllBytes(Path.Combine(output,"camera-flip-"+mode+".png"),texture.EncodeToPNG());
            }
            // A displaced viewport must mirror about its own centre, not Screen.width.
            camera.targetTexture=null;camera.pixelRect=new Rect(40,30,400,240);settings.Configure(true,true);
            var offset=CameraDisplaySettings.ToDisplayPoint(new Vector3(140,90,3),camera);
            Check(Vector3.Distance(offset,new Vector3(340,210,3))<.05f,"offset viewport projection "+offset);
        }
        finally
        {
            settings.Configure(false,false);camera.rect=previousRect;camera.targetTexture=previousTarget;
            camera.cullingMask=mask;canvas.enabled=canvasEnabled;RenderTexture.active=previousActive;
            for(int i=0;i<4;i++){if(quads[i]!=null)UnityEngine.Object.DestroyImmediate(quads[i]);if(materials[i]!=null)UnityEngine.Object.DestroyImmediate(materials[i]);}
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);
        }
        Check(horizontal.GetComponentInChildren<TMP_Text>().text.Contains("OFF"),"readable UI state");
        Debug.Log("CAMERA_DISPLAY_CHECKS_PASSED: rendered four orientations, independent controls, reset, overlay anchors and viewport");
    }
}

#endif
