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
        [Tooltip("Optional. When set this transform's up direction will be used to orient the To Aim transform.")]
        [SerializeField] private Transform toAimOverriddenUpDirection;

        private Quaternion aimNeutralLocalRotation;
        private Quaternion aimLookingOffsetRotation;

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

        private Quaternion GetLookingRotation()
        {
            Vector3 fromAimToPickup = pickupTransform.position - toAim.position;
            Quaternion toAimParentRotation = GetParentRotation(toAim);
            Quaternion neutralRotation = toAimParentRotation * aimNeutralLocalRotation;
            // Normalized is redundant but more technically correct.
            Quaternion lookingRotation = Quaternion.LookRotation(fromAimToPickup.normalized, neutralRotation * Vector3.up);
            return Quaternion.Inverse(toAimParentRotation) * lookingRotation;
        }

        private Quaternion GetAimRotationInSameSpaceAsToRotate() => Quaternion.Inverse(GetParentRotation(toRotate)) * toAim.rotation;

        protected override void UpdateConstraint()
        {
            Quaternion lookingRotation = GetLookingRotation();
            toAim.localRotation = lookingRotation * aimLookingOffsetRotation;

            Quaternion unconstrainedToRotateLocalRotation = GetAimRotationInSameSpaceAsToRotate() * aimToRotateOffsetRotation;
            Vector3 unconstrainedForward = unconstrainedToRotateLocalRotation * Vector3.forward;
            Vector3 up = toRotate.localRotation * Vector3.up; // Must also be in local space. Everything is in local space here.
            toRotate.localRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(unconstrainedForward, up).normalized, up);
        }
    }
}
