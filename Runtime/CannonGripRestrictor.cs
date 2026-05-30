using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;

namespace JanSharp
{
    // Not forcing sync mode since this script lives on the same object as other synced scripts, even though
    // this script does no syncing.
    // If this is used in conjunction with VRC Object Sync, it should be continuous.
    // If this is used in conjunction with Smart Object Sync, it should be manual.
    // Otherwise, if there is no other scripts on the object, it should be None.
    // [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [RequireComponent(typeof(VRCPickup))] // Must not use VRC.SDKBase.VRC_Pickup here, that is abstract.
    public class CannonGripRestrictor : UdonSharpBehaviour
    {
        /// <summary>
        /// <para>Having this as a separate variable from <see cref="distanceCheckLoopIsRunning"/> prevents
        /// multiple update loops from running effectively simultaneously. Imagine a
        /// <see cref="StopDistanceCheckLoop"/> call immediately followed by a
        /// <see cref="StartDistanceCheckLoop"/> call.</para>
        /// </summary>
        private bool distanceCheckLoopShouldBeRunning = false;
        private bool distanceCheckLoopIsRunning = false;
        private const float TimeBetweenDistanceChecks = 0.25f;

        [SerializeField] private Transform toSnapTo;
        [Tooltip("When the pickup gets moved too far away from its snap position, drop it.")]
        [Min(0.1f)]
        [SerializeField] private float maxAllowedDistanceAway = 1f;
        /// <summary>
        /// <para>In <see cref="toSnapTo"/> local space, respecting its scaling too.</para>
        /// </summary>
        private Vector3 offsetVector;
        /// <summary>
        /// <para>In <see cref="toSnapTo"/> local space.</para>
        /// </summary>
        private Quaternion offsetRotation;
        private VRCPickup pickup;

        private void Start()
        {
            pickup = GetComponent<VRCPickup>();
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
            SendCustomEventDelayedSeconds(nameof(DistanceCheckLoop), TimeBetweenDistanceChecks);
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
                pickup.Drop(); // Raises OnDrop.
            SendCustomEventDelayedSeconds(nameof(DistanceCheckLoop), TimeBetweenDistanceChecks);
        }
    }
}
