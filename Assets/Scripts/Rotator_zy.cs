using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;//不运行时编译的  
//[ExecuteAlways]
//模型自转脚本

//[ExecuteInEditMode] //不运行时编译
public class Rotator_zy : MonoBehaviour
{
    public float Speed;//
    public Vector3 Direction;
    
    void Awake() //不运行时编译 Update
    {
        EditorApplication.update += Update; 
    }

    private void OnDestroy()  //由于里面的运算编辑到Editor中了，所以不用的时候需要清除执行
    {
        EditorApplication.update -= Update; 
    }

    void Update()
    {
        transform.Rotate(Direction * Speed * Time.deltaTime, Space.World);
    }
}
