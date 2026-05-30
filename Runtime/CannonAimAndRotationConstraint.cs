using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CannonAimAndRotationConstraint : CannonSmartObjectSyncListenerBase
    {
        private Transform pickupTransform;
        [Tooltip("The up direction of this transform defines the axis it will spin around.")]
        [SerializeField] private Transform toRotate;
        [SerializeField] private Transform toAim;
        [Tooltip("Optional. When set this transform's up direction will be used to orient the To Aim transform. "
            + "It likely often makes sense for the cannon to be oriented based on the same up direction as "
            + "To Rotate is spinning around, therefore this could be set to the same transform as To Rotate.")]
        [SerializeField] private Transform toAimOverriddenUpDirection;

        /// <summary>
        /// <para>The rotation relative to <see cref="toAim"/>'s parent which defines how the object should be
        /// oriented when using <see cref="Quaternion.LookRotation(Vector3, Vector3)"/>, since that takes an
        /// up direction that it will try ot match as closely as possible.</para>
        /// </summary>
        private Quaternion aimNeutralLocalRotation;
        /// <summary>
        /// <para>The rotation to get from <see cref="GetLookingRotation"/> to <see cref="toAim"/>'s actual
        /// rotation, based on how <see cref="toAim"/> was rotated in the scene.</para>
        /// </summary>
        private Quaternion aimLookingOffsetRotation;

        /// <summary>
        /// <para>The rotation to get from <see cref="GetAimRotationInSameSpaceAsToRotate"/> to
        /// <see cref="toRotate"/>'s actual rotation, based on how <see cref="toAim"/> and
        /// <see cref="toRotate"/> were rotated in the scene.</para>
        /// </summary>
        private Quaternion aimToRotateOffsetRotation;

        protected override void Start()
        {
            pickupTransform = objSync.transform;

            aimNeutralLocalRotation = toAimOverriddenUpDirection == null
                ? toAim.localRotation
                // Get toAimOverriddenUpDirection's rotation as though it was local to toAim's parent,
                // which is the same space as toAim's own local rotation.
                : (Quaternion.Inverse(GetParentRotation(toAim)) * toAimOverriddenUpDirection.rotation);
            aimLookingOffsetRotation = Quaternion.Inverse(GetLookingRotation()) * toAim.localRotation;

            aimToRotateOffsetRotation = Quaternion.Inverse(GetAimRotationInSameSpaceAsToRotate()) * toRotate.localRotation;

            base.Start();
        }

        /// <summary>
        /// <para>Get a rotation relative to <see cref="toAim"/>'s parent where the forward direction of said
        /// rotation points at <see cref="pickupTransform"/>.</para>
        /// </summary>
        /// <returns></returns>
        private Quaternion GetLookingRotation()
        {
            Vector3 fromAimToPickup = pickupTransform.position - toAim.position;
            Quaternion toAimParentRotation = GetParentRotation(toAim);
            Quaternion neutralRotation = toAimParentRotation * aimNeutralLocalRotation;
            // LookRotation handles non normalized vectors, but it is more technically correct.
            Quaternion lookingRotation = Quaternion.LookRotation(fromAimToPickup.normalized, neutralRotation * Vector3.up);
            return Quaternion.Inverse(toAimParentRotation) * lookingRotation;
        }

        private Quaternion GetAimRotationInSameSpaceAsToRotate() => Quaternion.Inverse(GetParentRotation(toRotate)) * toAim.rotation;

        protected override void UpdateConstraint()
        {
            // Must update toAim first, toRotate's rotation is based on toAim's.
            toAim.localRotation = GetLookingRotation() * aimLookingOffsetRotation;

            Quaternion unconstrainedToRotateLocalRotation = GetAimRotationInSameSpaceAsToRotate() * aimToRotateOffsetRotation;
            Vector3 unconstrainedForward = unconstrainedToRotateLocalRotation * Vector3.forward;
            Vector3 up = toRotate.localRotation * Vector3.up; // Must also be in local space. Everything is in local space here.
            // LookRotation handles non normalized vectors, but it is more technically correct.
            toRotate.localRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(unconstrainedForward, up).normalized, up);
        }
    }
}
