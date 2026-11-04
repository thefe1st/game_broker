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
        // E in the driver's seat: the game decides whether the driver may get out here.
        public Action OnLeaveRequest;
        public Camera Eye { get; private set; }
        public OfficeInteractable Target { get; private set; }
        public bool ControlEnabled { get; private set; }
        public bool Captured => Cursor.lockState == CursorLockMode.Locked;
        // In the driver's seat the body stands still without its capsule, the keys go to the car (Drive) and the mouse
        // turns the chase camera round it (OrbitYaw, OrbitPitch, degrees), which settles back behind the car when idle.
        public bool Seated { get; private set; }
        public DriveInput Drive { get; private set; }
        public float OrbitYaw { get; private set; }
        public float OrbitPitch { get; private set; }
        private const float EyeHeight = 1.65f, OrbitRelaxDelay = 1.5f, OrbitRelaxRate = 2.5f, OrbitMinPitch = -12, OrbitMaxPitch = 36;
        private CharacterController body;
        private float pitch, verticalSpeed, orbitIdle;
        private int suppressLook;
        private Vector3 safeWorldPosition;

        private void Awake()
        {
            safeWorldPosition = transform.position;
            body = GetComponent<CharacterController>(); body.height = 1.8f; body.radius = 0.3f;
            body.center = new Vector3(0, 0.9f, 0); body.stepOffset = 0.2f; body.slopeLimit = 45;
            gameObject.layer = 2; // Ignore Raycast: own capsule never blocks the aim ray.
            var eyes = new GameObject("Player eyes"); eyes.transform.SetParent(transform, false);
            eyes.transform.localPosition = new Vector3(0, EyeHeight, 0);
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
        public void Release() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Target = null; Drive = DriveInput.None; }
        public void ResetAt(Vector3 position)
        {
            LeaveSeat(); PlaceBody(position, Quaternion.identity); Release();
        }
        // Into the driver's seat: the scene moves the eye behind the car from now on.
        public void Seat()
        {
            Seated = true; Drive = DriveInput.None; OrbitYaw = OrbitPitch = 0; orbitIdle = 0; Target = null;
            verticalSpeed = 0; body.enabled = false;
        }
        // Out of the car onto the given spot, turned as given (a yaw); the cursor stays captured.
        public void Unseat(Vector3 position, Quaternion facing)
        {
            LeaveSeat(); PlaceBody(position, facing);
        }
        private void LeaveSeat() { Seated = false; Drive = DriveInput.None; OrbitYaw = OrbitPitch = 0; orbitIdle = 0; }
        private void PlaceBody(Vector3 position, Quaternion rotation)
        {
            safeWorldPosition = position;
            body.enabled = false; transform.SetPositionAndRotation(position, rotation); body.enabled = true;
            pitch = 0; verticalSpeed = 0;
            Eye.transform.localPosition = new Vector3(0, EyeHeight, 0); Eye.transform.localRotation = Quaternion.identity;
        }
        private void Update()
        {
            Target = null;
            if (!ControlEnabled || !Application.isFocused) { Drive = DriveInput.None; return; }
            if (!Captured) { Drive = DriveInput.None; if (OfficeInput.Clicked()) Capture(); return; }
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Vector2 look = Vector2.zero;
            if (suppressLook > 0) suppressLook--;
            else look = OfficeInput.Look();
            if (Seated) { DriveCar(look, delta); return; }
            transform.Rotate(0, look.x, 0, Space.Self);
            pitch = Mathf.Clamp(pitch - look.y, -75, 75); Eye.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
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
        // Behind the wheel: full keys for the car (a diagonal still means full gas and full lock), the mouse orbits the
        // camera, and E asks to get out.
        private void DriveCar(Vector2 look, float delta)
        {
            Vector2 keys = OfficeInput.Move();
            Drive = new DriveInput(Digital(keys.y), Digital(keys.x), OfficeInput.Handbrake());
            if (look.sqrMagnitude > 1e-4f)
            {
                orbitIdle = 0;
                OrbitYaw = Mathf.Repeat(OrbitYaw + look.x + 180, 360) - 180;
                OrbitPitch = Mathf.Clamp(OrbitPitch - look.y, OrbitMinPitch, OrbitMaxPitch);
            }
            else if ((orbitIdle += delta) > OrbitRelaxDelay)
            {
                float settle = 1 - Mathf.Exp(-OrbitRelaxRate * delta);
                OrbitYaw -= OrbitYaw * settle; OrbitPitch -= OrbitPitch * settle;
            }
            if (OfficeInput.InteractPressed()) OnLeaveRequest?.Invoke();
        }
        private static float Digital(float axis) => axis > 0.1f ? 1 : axis < -0.1f ? -1 : 0;
        private void OnApplicationFocus(bool focus)
        {
            if (!focus) { Release(); if (ControlEnabled) OnFocusLost?.Invoke(); }
        }
        private void OnDisable() { Release(); }
        private void OnDestroy() { Release(); }
    }
}
