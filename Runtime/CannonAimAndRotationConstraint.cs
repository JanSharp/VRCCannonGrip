using MMMaellon;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CannonAimAndRotationConstraint : SmartObjectSyncListener
    {
        [HideInInspector][SerializeField][SingletonReference] private UpdateManager updateManager;
        /// <summary>
        /// <para>Used by <see cref="UpdateManager"/>.</para>
        /// </summary>
        [System.NonSerialized] public int customUpdateInternalIndex;

        [SerializeField][FindInParent] private SmartObjectSync objSync;
        private Transform pickupTransform;
        [SerializeField] private Transform toRotate;
        [SerializeField] private Transform toAim;

        private Quaternion aimNeutralLocalRotation;
        private Quaternion aimLookingOffsetRotation;

        private Quaternion aimToRotateOffsetRotation;

        private bool receivedFirstStateChange = false;
        private bool isMoving = false;

        private void Start()
        {
            pickupTransform = objSync.transform;

            aimNeutralLocalRotation = toAim.localRotation;
            aimLookingOffsetRotation = Quaternion.Inverse(GetLookingRotation()) * aimNeutralLocalRotation;

            aimToRotateOffsetRotation = Quaternion.Inverse(GetAimRotationInSameSpaceAsToRotate()) * toRotate.localRotation;

            objSync.AddListener(this);
        }

        public override void OnChangeState(SmartObjectSync sync, int oldState, int newState)
        {
            // The state changes locally and remotely seem to match.
            // Though that is very very most likely not a guarantee, which is fine.
            // Somebody picking up and dropping the pickup instantly might skip the held state remotely,
            // it'll still go to STATE_INTERPOLATING and then STATE_SLEEPING.
            // Which is treated as a moving state and non moving state, so good enough.
            //
            // STATE_SLEEPING - Not moving... So long as no physics or other scripts cause it to move, which there are no events for, smile.
            // STATE_TELEPORTING - When it got teleported. Does not go to sleeping afterwards, but is stationary.
            // STATE_INTERPOLATING - Upon dropping.
            // STATE_FALLING - Probably for physics objects, moving.
            // STATE_LEFT_HAND_HELD - When held.
            // STATE_RIGHT_HAND_HELD - When held.
            // STATE_NO_HAND_HELD - Idk, but held, so moving.
            // STATE_ATTACHED_TO_PLAYSPACE - Idk, but probably moving so all I need to know.
            // STATE_WORLD_LOCK - Idk, but probably movable.
            // STATE_CUSTOM - Could be anything I'd assume, so probably moving.
            // Note that if one wanted to check for STATE_CUSTOM one should use >= not ==.

            int state = objSync.state;
            bool nowMoving = state != SmartObjectSync.STATE_SLEEPING && state != SmartObjectSync.STATE_TELEPORTING;

            if (!receivedFirstStateChange)
            {
                // The default state of SmartObjectSyncs is STATE_TELEPORTING.
                // This script never teleports the smart object sync (through its api, it does move the object).
                // If an object got moved before a client joined the instance, they will therefore receive
                // a state change event from STATE_TELEPORTING to STATE_SLEEPING, and that is our indication
                // to update the toAim and toRotate transforms for the late joiner (the local client).
                receivedFirstStateChange = true;
                if (!nowMoving) // Only if it actually changed to a non moving state, don't cancel interpolation when picking up the first time.
                {
                    // Delayed, however, because the state change event gets raised before SmartObjectSync moves the object.
                    SendCustomEventDelayedFrames(nameof(InitializeForLateJoiner), 1);
                }
            }

            if (isMoving == nowMoving)
                return;
            isMoving = nowMoving;
            if (isMoving)
                OnStartMoving();
            else
                OnStopMoving();
        }

        public void InitializeForLateJoiner()
        {
            // Preemptively force finish interpolation. There is no need for interpolation for late joiners,
            // for one, but also interpolation length is dynamic and there is no event for it finishing, so
            // forcing it to finish immediately simplifies this logic.
            objSync.interpolationStartTime = -1_000_000f;
            objSync.Interpolate();
            AimAndRotate();
        }

        public override void OnChangeOwner(SmartObjectSync sync, VRCPlayerApi oldOwner, VRCPlayerApi newOwner)
        { }

        private void OnStartMoving()
        {
            AimAndRotate();
            updateManager.Register(this);
        }

        private void OnStopMoving()
        {
            AimAndRotate();
            updateManager.Deregister(this);
        }

        public void CustomUpdate()
        {
            AimAndRotate();
        }

        private Quaternion GetParentRotation(Transform t)
        {
            Transform parent = t.parent;
            // To handle root game objects. Their parent's rotation is effectively and functionally identity/"none".
            return parent == null ? Quaternion.identity : parent.rotation;
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

        private void AimAndRotate()
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
