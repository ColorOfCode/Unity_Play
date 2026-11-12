//////////////////////////////////////////////////////
// MK Glow Rotate Object             				//
//					                                //
// Created by Michael Kremmel                       //
// www.michaelkremmel.de                            //
// Copyright © 2017 All rights reserved.            //
//////////////////////////////////////////////////////
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MK.Glow.Example
{
    public class RotateObject : MonoBehaviour
    {
        [SerializeField]
        private Vector3 _rotation = Vector3.zero;
        public float _heightMovement = 0.125f;
        public float _heightMovementSpeed = 1f;

        private Vector3 _startPosition;

        private void Awake()
        {
            _startPosition = transform.localPosition;
        }

        void Update()
        {

            transform.Rotate(_rotation * Time.smoothDeltaTime);
            transform.localPosition = _startPosition + Vector3.up * _heightMovement * Mathf.Sin(Time.time * _heightMovementSpeed);
        }
    }
}
