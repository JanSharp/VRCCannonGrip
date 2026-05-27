using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CannonAimAndRotationConstraint : CannonSmartObjectSyncListenerBase
    {
        private Transform pickupTransform;
        [SerializeField] private Transform toRotate;
        [SerializeField] private Transform toAim;

        private Quaternion aimNeutralLocalRotation;
        private Quaternion aimLookingOffsetRotation;

        private Quaternion aimToRotateOffsetRotation;

        protected override void Start()
        {
            pickupTransform = objSync.transform;

            aimNeutralLocalRotation = toAim.localRotation;
            aimLookingOffsetRotation = Quaternion.Inverse(GetLookingRotation()) * aimNeutralLocalRotation;

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
