using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CannonSpecializedConstraint : CannonSmartObjectSyncListenerBase
    {
#if CANNON_GRIP_DEBUG
        [HideInInspector][SerializeField][SingletonReference] private QuickDebugUI qd;
#endif

        private Transform pickupTransform;
        [Tooltip("The rotation of this object must be clean.\n"
            + "The up direction defines the axis the mount will spin around.\n"
            + "The right direction defines the plane the cannon will spin on.\n"
            + "The forward direction should point away from the grip, the grip being behind the mount.")]
        [SerializeField] private Transform mount;
        [Tooltip("Must be a child of Mount. Can be a nested child. The rotation of this object can be anything. "
            + "The position of this object defines the point it will spin around.")]
        [SerializeField] private Transform cannon;

        [SerializeField] private bool limitMountRotation;
        [SerializeField][Range(0f, 180f)] private float maxLeftRotation;
        [SerializeField][Range(0f, 180f)] private float maxRightRotation;
        private float maxHorizontalRadians;
        [SerializeField] private bool limitCannonRotation;
        [SerializeField][Range(0f, 180f)] private float maxDownRotation;
        [SerializeField][Range(0f, 180f)] private float maxUpRotation;
        private float maxVerticalRadians;

        /// <summary>
        /// <para>Relative to the <see cref="mount"/>'s parent rotation.</para>
        /// <para>Sits right in the middle between the maximum left and right rotated vectors.</para>
        /// </summary>
        private Vector3 middleDirectionFromMountToPickup;
        /// <summary>
        /// <para>Relative to the <see cref="cannon"/>'s parent rotation.</para>
        /// <para>Sits right in the middle between the maximum down and up rotated vectors.</para>
        /// </summary>
        private Vector3 middleDirectionFromCannonToPickup;

        /// <summary>
        /// <para>The rotation to get from <see cref="GetMountLookingAtPickup"/> to <see cref="mount"/>'s
        /// actual rotation, based on how <see cref="mount"/> was rotated in the scene.</para>
        /// </summary>
        private Quaternion mountRotationOffset;
        /// <summary>
        /// <para>The rotation to get from <see cref="GetCannonLookingAtPickup"/> to <see cref="cannon"/>'s
        /// actual rotation, based on how <see cref="cannon"/> was rotated in the scene.</para>
        /// </summary>
        private Quaternion cannonRotationOffset;

        protected override void Start()
        {
            pickupTransform = objSync.transform;

            if (limitMountRotation)
                InitializeMountLimits();
            if (limitCannonRotation)
                InitializeCannonLimits();

            // Calculate offsets to get from calculated rotations based on the pickup position to actual desired rotations.
            mountRotationOffset = Quaternion.Inverse(GetMountLookingAtPickup()) * mount.rotation;
            cannonRotationOffset = Quaternion.Inverse(GetCannonLookingAtPickup()) * cannon.rotation;

            base.Start();
        }

        private void InitializeMountLimits()
        {
            // This would be significantly easier if left and right limits were defined as just one value,
            // then this entire rotation shift logic would not be needed. Especially applicable to the cannon,
            // where it is notably more complicated than the mount. Which is actually only the case because
            // the cannon's rotation is allowed to be whatever the map creator wishes. If it was forced to
            // have a clean right direction it'd be the same logic for both the mount as well as the cannon.

            // If left and right rotation limits differ, the reference vector used for enforcing the rotation
            // limits must be rotated a bit such that it sits right between the left and right limits.
            float maxHorizontalRotation = (maxLeftRotation + maxRightRotation) / 2f;
            maxHorizontalRadians = maxHorizontalRotation * Mathf.Deg2Rad;
            // Determined order of operands for subtraction (so direction to rotate) via trial and error.
            Quaternion shift = Quaternion.AngleAxis(maxHorizontalRotation - maxLeftRotation, Vector3.up);
            Quaternion middleRotation = GetParentRotation(mount) * shift;
            // The projected direction is in world space, make it live in the mount's parent local space.
            // This allows the rotation of a cannon as a whole to change, without the rotation limits breaking.
            middleDirectionFromMountToPickup = Quaternion.Inverse(middleRotation) * GetProjectedVectorFromMountToPickup().normalized;
        }

        private void InitializeCannonLimits()
        {
            // Copy paste, except for how shift gets applied to middleRotation.
            float maxVerticalRotation = (maxDownRotation + maxUpRotation) / 2f;
            maxVerticalRadians = maxVerticalRotation * Mathf.Deg2Rad;
            // Determined order of operands for subtraction (so direction to rotate) via trial and error.
            Quaternion shift = Quaternion.AngleAxis(maxDownRotation - maxVerticalRotation, Vector3.right);
            // Must do some odd math here as the shift must be applied relative to the mount's rotation,
            // however the final vector must be rotated based on the cannon's parent rotation.
            Quaternion cannonParentRelativeToMount = Quaternion.Inverse(mount.rotation) * GetParentRotation(cannon);
            Quaternion middleRotation = mount.rotation * shift * cannonParentRelativeToMount;
            middleDirectionFromCannonToPickup = Quaternion.Inverse(middleRotation) * GetProjectedVectorFromCannonToPickup().normalized;
        }

        // Calculate the vector which points from the mount to the pickup first, then project that onto the desired plane.
        // It'd also be possible tp project the 2 positions individually first and then doing the subtraction, but that'd be a bit redundant.
        private Vector3 GetProjectedVectorFromMountToPickup() => Vector3.ProjectOnPlane(pickupTransform.position - mount.position, mount.up);
        // LookRotation handles non normalized vectors, but it is more technically correct.
        private Quaternion GetMountLookingAtPickup() => Quaternion.LookRotation(GetProjectedVectorFromMountToPickup().normalized, mount.up);

        // Copy paste.
        private Vector3 GetProjectedVectorFromCannonToPickup() => Vector3.ProjectOnPlane(pickupTransform.position - cannon.position, mount.right);
        private Quaternion GetCannonLookingAtPickup() => Quaternion.LookRotation(GetProjectedVectorFromCannonToPickup().normalized, mount.right);

        private Quaternion GetMountLookingAtPickupLimited()
        {
            Vector3 projected = GetProjectedVectorFromMountToPickup().normalized;
            // Using the precomputed direction which sits right in the middle of maximum allowed rotation
            // as well as the precomputed max radians makes this relatively straight forward. Pun not intended :D
            Vector3 forward = Vector3.RotateTowards(
                GetParentRotation(mount) * middleDirectionFromMountToPickup,
                projected,
                maxRadiansDelta: maxHorizontalRadians,
                maxMagnitudeDelta: 0f);
            return Quaternion.LookRotation(forward, mount.up);
        }

        // Copy paste, because that is slightly faster with Udon compared to calling a shared function with several parameters.
        private Quaternion GetCannonLookingAtPickupLimited()
        {
            Vector3 projected = GetProjectedVectorFromCannonToPickup().normalized;
            Vector3 forward = Vector3.RotateTowards(
                GetParentRotation(cannon) * middleDirectionFromCannonToPickup,
                projected,
                maxRadiansDelta: maxVerticalRadians,
                maxMagnitudeDelta: 0f);
            return Quaternion.LookRotation(forward, mount.right);
        }

        protected override void UpdateConstraint()
        {
#if CANNON_GRIP_DEBUG
            qd.ShowForOneFrame(this, "position", pickupTransform.position.ToString());
            qd.ShowForOneFrame(this, "vector", (pickupTransform.position - mount.position).ToString());
            qd.ShowForOneFrame(this, "projected", GetProjectedVectorFromMountToPickup().ToString());
            qd.ShowForOneFrame(this, "forward", GetProjectedVectorFromMountToPickup().normalized.ToString());
#endif
            mount.rotation = (limitMountRotation ? GetMountLookingAtPickupLimited() : GetMountLookingAtPickup()) * mountRotationOffset;
            cannon.rotation = (limitCannonRotation ? GetCannonLookingAtPickupLimited() : GetCannonLookingAtPickup()) * cannonRotationOffset;
        }
    }
}
