using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
作者：琦玉老师的二弟子
*/
namespace JumpAgent{

    public class AutoRotate2 : MonoBehaviour
    {
        public float Movespeed = 1f;
        public Vector3 MoveDir ;
        public float Rotatespeed = 10f;
        public Vector3 RotateDir;

        private void Update()
        {
            transform.Rotate(RotateDir, Rotatespeed * Time.deltaTime, Space.Self);
            transform.Translate(MoveDir * Movespeed * Time.deltaTime);
        }


    }//类

}//命名空间