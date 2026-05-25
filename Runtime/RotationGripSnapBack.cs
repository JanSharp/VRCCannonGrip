using MMMaellon;
using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [RequireComponent(typeof(SmartObjectSync))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)] // Manual because it's on the same object as the smart object sync script.
    public class RotationGripSnapBack : UdonSharpBehaviour
    {
        private bool distanceCheckLoopShouldBeRunning = false;
        private bool distanceCheckLoopIsRunning = false;
        private const float DistanceCheckLoopUpdateInterval = 0.5f;

        [SerializeField] private Transform toSnapTo;
        [Tooltip("When the pickup gets moved too far away from its snap position, drop it.")]
        [Min(0.1f)]
        [SerializeField] private float maxAllowedDistanceAway = 1f;
        /// <summary>
        /// <para>In <see cref="toSnapTo"/> local space, respecting its scaling too.</para>
        /// </summary>
        private Vector3 offsetVector;
        /// <inheritdoc cref="offsetVector"/>
        private Quaternion offsetRotation;
        private SmartObjectSync objSync;

        private void Start()
        {
            objSync = GetComponent<SmartObjectSync>();
            offsetVector = toSnapTo.InverseTransformPoint(transform.position);
            offsetRotation = Quaternion.Inverse(toSnapTo.rotation) * transform.rotation;
        }

        public override void OnPickup()
        {
            StartDistanceCheckLoop();
        }

        public override void OnDrop()
        {
            SnapBack();
            StopDistanceCheckLoop();
        }

        private Vector3 GetSnappedPosition() => toSnapTo.TransformPoint(offsetVector);
        private Quaternion GetSnappedRotation() => toSnapTo.rotation * offsetRotation;

        private void SnapBack()
        {
            // The SmartObjectSync ends up syncing whatever the current position and rotation is.
            // It even changes its state 1 frame delayed after dropping, so this is definitely safe, there's
            // no way for it to fetch and sync the wrong values.
            transform.SetPositionAndRotation(GetSnappedPosition(), GetSnappedRotation());
        }

        private void StartDistanceCheckLoop()
        {
            distanceCheckLoopShouldBeRunning = true;
            if (distanceCheckLoopIsRunning)
                return;
            distanceCheckLoopIsRunning = true;
            SendCustomEventDelayedSeconds(nameof(DistanceCheckLoop), DistanceCheckLoopUpdateInterval);
        }

        private void StopDistanceCheckLoop()
        {
            distanceCheckLoopShouldBeRunning = false;
        }

        public void DistanceCheckLoop()
        {
            if (!distanceCheckLoopShouldBeRunning)
            {
                distanceCheckLoopIsRunning = false;
                return;
            }
            float distance = Vector3.Distance(GetSnappedPosition(), transform.position);
            if (distance > maxAllowedDistanceAway)
                objSync.pickup.Drop();
            SendCustomEventDelayedSeconds(nameof(DistanceCheckLoop), DistanceCheckLoopUpdateInterval);
        }
    }
}
