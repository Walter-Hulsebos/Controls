using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Samples
{
    [RequireComponent(requiredComponent: typeof(Rigidbody))]
    internal sealed class WalterTest : MonoBehaviour
    {
        #region Fields
        
        [SerializeField] private float moveSpeed = 5;
        
        [SerializeField] private InputActionReference moveInput;

        [HideInInspector, SerializeField] private Rigidbody rb;

        private float3 _moveVector = float3.zero;
        
        #endregion

        #region Methods

        private void Reset()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void OnEnable() => moveInput.Enable();

        private void OnDisable() => moveInput.Disable();

        private void Update()
        {
            _moveVector.xz = moveInput;
        }

        private void FixedUpdate()
        {
            rb.AddForce(_moveVector * moveSpeed);
        }
        
        #endregion
    }
}
