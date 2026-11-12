using System.IO.Compression;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
 
public class depthBuffer : MonoBehaviour {
 
    Camera cam = null;
    public RenderTexture colorRT;
    public RenderTexture depthRT;
    // Use this for initialization
    void Start () {
        cam = this.GetComponent<Camera>();
        //cam.depthTextureMode = DepthTextureMode.Depth;
        colorRT = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Default);
        depthRT = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth);
    }
	
    // Update is called once per frame
    void Update () {
 
    }
    private void OnPreRender() {
        cam.SetTargetBuffers(colorRT.colorBuffer,depthRT.depthBuffer);
    }
 
    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(depthRT,dest);
    }
 
}