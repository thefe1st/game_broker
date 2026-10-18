using System;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonOfficeController : MonoBehaviour
    {
        public Action<OfficeInteractable> OnInteraction;
        public Action OnFocusLost;
        public Camera Eye { get; private set; }
        public OfficeInteractable Target { get; private set; }
        public bool ControlEnabled { get; private set; }
        public bool Captured => Cursor.lockState == CursorLockMode.Locked;
        private CharacterController body;
        private float pitch, verticalSpeed;
        private int suppressLook;
        private Vector3 safeWorldPosition;

        private void Awake()
        {
            safeWorldPosition = transform.position;
            body = GetComponent<CharacterController>(); body.height = 1.8f; body.radius = 0.3f;
            body.center = new Vector3(0, 0.9f, 0); body.stepOffset = 0.2f; body.slopeLimit = 45;
            gameObject.layer = 2; // Ignore Raycast: own capsule never blocks the aim ray.
            var eyes = new GameObject("Player eyes"); eyes.transform.SetParent(transform, false);
            eyes.transform.localPosition = new Vector3(0, 1.65f, 0);
            Eye = eyes.AddComponent<Camera>(); Eye.depth = 10; Eye.fieldOfView = 72; Eye.nearClipPlane = 0.05f; Eye.farClipPlane = 180;
            Eye.clearFlags = CameraClearFlags.SolidColor; Eye.backgroundColor = new Color(0.09f, 0.14f, 0.22f);
            eyes.AddComponent<AudioListener>();
        }
        public void SetControl(bool enabled)
        {
            if (ControlEnabled == enabled) return;
            ControlEnabled = enabled;
            if (!enabled) Release();
        }
        public void Capture()
        {
            if (!ControlEnabled || !Application.isFocused || !OfficeInput.Available) return;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; suppressLook = 2;
        }
        public void Release() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Target = null; }
        public void ResetAt(Vector3 position)
        {
            safeWorldPosition = position;
            body.enabled = false; transform.SetPositionAndRotation(position, Quaternion.identity); body.enabled = true;
            pitch = 0; verticalSpeed = 0; Eye.transform.localRotation = Quaternion.identity; Release();
        }
        private void Update()
        {
            Target = null;
            if (!ControlEnabled || !Application.isFocused) return;
            if (!Captured) { if (OfficeInput.Clicked()) Capture(); return; }
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (suppressLook > 0) suppressLook--;
            else
            {
                Vector2 look = OfficeInput.Look();
                transform.Rotate(0, look.x, 0, Space.Self);
                pitch = Mathf.Clamp(pitch - look.y, -75, 75); Eye.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            Vector2 move = OfficeInput.Move();
            float speed = OfficeInput.Sprinting() ? 3.8f : 2.4f;
            if (body.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += -18f * delta;
            body.Move((transform.right * move.x * speed + transform.forward * move.y * speed + Vector3.up * verticalSpeed) * delta);
            // Recovery for invalid spawns / unexpected external scene changes.
            if (transform.localPosition.y < CityBlockPlan.GroundY - 6) ResetAt(safeWorldPosition);
            if (Physics.Raycast(Eye.transform.position, Eye.transform.forward, out RaycastHit hit, 2.7f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                Target = hit.collider.GetComponentInParent<OfficeInteractable>();
            if (Target != null && OfficeInput.InteractPressed()) OnInteraction?.Invoke(Target);
        }
        private void OnApplicationFocus(bool focus)
        {
            if (!focus) { Release(); if (ControlEnabled) OnFocusLost?.Invoke(); }
        }
        private void OnDisable() { Release(); }
        private void OnDestroy() { Release(); }
    }
}
