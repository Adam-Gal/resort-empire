using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace ResortEmpire.Presentation
{
    // Moves the Cinemachine follow target by finger drag and zooms the lens by pinch.
    // The target is clamped to the same area the CinemachineConfiner2D uses, so the
    // view never leaves the island and dragging never gets "stuck" past the edge.
    [DisallowMultipleComponent]
    public sealed class IslandCameraController : MonoBehaviour
    {
        public CinemachineCamera VirtualCamera;
        public Transform Target;
        public Collider2D Bounds;
        [Min(.5f)] public float MinZoom = 3.5f;
        [Min(.5f)] public float MaxZoom = 8.5f;
        [Min(0f)] public float ScrollZoomStep = .1f;
        [Range(0f, 20f)] public float Inertia = 6f;

        private CinemachineConfiner2D confiner;
        private Vector2 velocity;
        private Vector2 lastSinglePosition;
        private float lastPinchDistance;
        private Vector2 lastPinchCenter;
        private int activeFingers;
        private bool pointerOverUi;

        private float Zoom
        {
            get => VirtualCamera.Lens.OrthographicSize;
            set
            {
                var lens = VirtualCamera.Lens;
                float size = Mathf.Clamp(value, MinZoom, EffectiveMaxZoom());
                if (Mathf.Approximately(lens.OrthographicSize, size))
                    return;
                lens.OrthographicSize = size;
                VirtualCamera.Lens = lens;
                // The confiner caches its area per lens size; without this a zoomed-in view stays centred.
                if (confiner)
                    confiner.InvalidateLensCache();
            }
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void Start()
        {
            if (!VirtualCamera || !Target || !Bounds)
            {
                Debug.LogError("IslandCamera: nastav VirtualCamera, Target a Bounds.", this);
                enabled = false;
                return;
            }

            confiner = VirtualCamera.GetComponent<CinemachineConfiner2D>();
            Zoom = Zoom;
            Target.position = Clamp(Target.position);
        }

        private void LateUpdate()
        {
            if (Touch.activeTouches.Count > 0 || activeFingers > 0)
                HandleTouches();
            else
                HandleMouse();

            if (activeFingers == 0 && velocity.sqrMagnitude > .0001f)
            {
                Move(velocity * Time.unscaledDeltaTime);
                velocity = Vector2.Lerp(velocity, Vector2.zero, Inertia * Time.unscaledDeltaTime);
            }
        }

        private void HandleTouches()
        {
            var touches = Touch.activeTouches;
            if (touches.Count == 0)
            {
                activeFingers = 0;
                return;
            }

            if (touches.Count != activeFingers)
            {
                // Finger count changed: restart the gesture so the map does not jump.
                activeFingers = touches.Count;
                pointerOverUi = IsOverUi(touches[0].touchId);
                lastSinglePosition = touches[0].screenPosition;
                if (touches.Count >= 2)
                {
                    lastPinchCenter = (touches[0].screenPosition + touches[1].screenPosition) * .5f;
                    lastPinchDistance = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
                }
                velocity = Vector2.zero;
                return;
            }

            if (pointerOverUi)
                return;

            if (touches.Count == 1)
            {
                Drag(lastSinglePosition, touches[0].screenPosition);
                lastSinglePosition = touches[0].screenPosition;
                return;
            }

            var center = (touches[0].screenPosition + touches[1].screenPosition) * .5f;
            float distance = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
            if (lastPinchDistance > 1f && distance > 1f)
                ZoomAround(center, Zoom * lastPinchDistance / distance);
            Drag(lastPinchCenter, center);
            lastPinchCenter = center;
            lastPinchDistance = distance;
            velocity = Vector2.zero;
        }

        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
            {
                pointerOverUi = IsOverUi(PointerInputModule.kMouseLeftId);
                lastSinglePosition = position;
                velocity = Vector2.zero;
            }
            else if (!pointerOverUi && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
            {
                Drag(lastSinglePosition, position);
                lastSinglePosition = position;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > .01f && !IsOverUi(PointerInputModule.kMouseLeftId))
                ZoomAround(position, Zoom * (1f - Mathf.Sign(scroll) * ScrollZoomStep));
        }

        private void Drag(Vector2 fromScreen, Vector2 toScreen)
        {
            Vector2 delta = (fromScreen - toScreen) * WorldUnitsPerPixel();
            Move(delta);
            float dt = Mathf.Max(Time.unscaledDeltaTime, .0001f);
            velocity = Vector2.Lerp(velocity, delta / dt, .5f);
        }

        // Keeps the world point under the fingers in place while the lens size changes.
        private void ZoomAround(Vector2 screenPoint, float newZoom)
        {
            Vector2 before = ScreenToWorld(screenPoint);
            Zoom = newZoom;
            Vector2 after = ScreenToWorld(screenPoint);
            Move(before - after);
        }

        private void Move(Vector2 delta)
        {
            Target.position = Clamp(Target.position + (Vector3)delta);
        }

        private Vector3 Clamp(Vector3 position)
        {
            var area = Bounds.bounds;
            float halfHeight = Zoom;
            float halfWidth = halfHeight * Aspect();
            float minX = area.min.x + halfWidth, maxX = area.max.x - halfWidth;
            float minY = area.min.y + halfHeight, maxY = area.max.y - halfHeight;
            position.x = minX > maxX ? area.center.x : Mathf.Clamp(position.x, minX, maxX);
            position.y = minY > maxY ? area.center.y : Mathf.Clamp(position.y, minY, maxY);
            return position;
        }

        private float EffectiveMaxZoom()
        {
            if (!Bounds)
                return MaxZoom;
            var size = Bounds.bounds.size;
            return Mathf.Max(MinZoom, Mathf.Min(MaxZoom, size.y * .5f, size.x * .5f / Aspect()));
        }

        private Vector2 ScreenToWorld(Vector2 screenPoint)
        {
            Vector2 offset = screenPoint - new Vector2(Screen.width, Screen.height) * .5f;
            return (Vector2)Target.position + offset * WorldUnitsPerPixel();
        }

        private float WorldUnitsPerPixel() => Zoom * 2f / Mathf.Max(1, Screen.height);

        private static float Aspect() => Screen.width / (float)Mathf.Max(1, Screen.height);

        private static bool IsOverUi(int pointerId) =>
            EventSystem.current && EventSystem.current.IsPointerOverGameObject(pointerId);
    }
}
