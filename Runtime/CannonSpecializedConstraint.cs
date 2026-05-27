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
        [Tooltip("The rotation of this object must be clean. "
            + "The up direction defines the axis the cannon will spin around,"
            + "The right direction defines the plane cannon will spin on.")]
        [SerializeField] private Transform mount;
        [Tooltip("Must be a child of Mount. Can be a nested child. The rotation of this object can be anything. "
            + "The position of this object defines the point it will spin around.")]
        [SerializeField] private Transform cannon;

        private Quaternion mountRotationOffset;
        private Quaternion cannonRotationOffset;

        protected override void Start()
        {
            pickupTransform = objSync.transform;

            // Calculate offsets to get from calculated rotations based on the pickup position to actual desired rotations.
            mountRotationOffset = Quaternion.Inverse(GetMountLookingAtPickup()) * mount.rotation;
            cannonRotationOffset = Quaternion.Inverse(GetCannonLookingAtPickup()) * cannon.rotation;

            base.Start();
        }

        // Calculate the vector which points from the mount to the pickup first, then project that onto the desired plane.
        // Projecting the pickup position would also be an option, however then the mount position would also have to
        // be projected onto the same plane before doing math with those two positions. Which would be a bit over complicated.
        private Vector3 GetProjectedVectorFromMountToPickup() => Vector3.ProjectOnPlane(pickupTransform.position - mount.position, mount.up);
        // Normalizing is redundant but more technically correct.
        private Quaternion GetMountLookingAtPickup() => Quaternion.LookRotation(GetProjectedVectorFromMountToPickup().normalized, mount.up);

        // Copy paste.
        private Vector3 GetProjectedVectorFromCannonToPickup() => Vector3.ProjectOnPlane(pickupTransform.position - cannon.position, mount.right);
        private Quaternion GetCannonLookingAtPickup() => Quaternion.LookRotation(GetProjectedVectorFromCannonToPickup().normalized, mount.right);

        protected override void UpdateConstraint()
        {
#if CANNON_GRIP_DEBUG
            qd.ShowForOneFrame(this, "position", pickupTransform.position.ToString());
            qd.ShowForOneFrame(this, "vector", (pickupTransform.position - mount.position).ToString());
            qd.ShowForOneFrame(this, "projected", GetProjectedVectorFromMountToPickup().ToString());
            qd.ShowForOneFrame(this, "forward", GetProjectedVectorFromMountToPickup().normalized.ToString());
#endif
            mount.rotation = GetMountLookingAtPickup() * mountRotationOffset;
            cannon.rotation = GetCannonLookingAtPickup() * cannonRotationOffset;
        }
    }
}
