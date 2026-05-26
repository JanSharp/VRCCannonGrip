using MMMaellon;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class RotationGripWithSmartObjectSync : SmartObjectSyncListener
    {
        [HideInInspector][SerializeField][SingletonReference] private UpdateManager updateManager;
        /// <summary>
        /// <para>Used by <see cref="UpdateManager"/>.</para>
        /// </summary>
        [System.NonSerialized] public int customUpdateInternalIndex;

        private const float TimeBetweenDistanceChecks = 0.25f;
        private float nextDistanceCheckTime;

        [SerializeField][FindInParent] private SmartObjectSync objSync;
        private Transform pickupTransform;
        [SerializeField] private Transform toRotate;
        [SerializeField] private Transform toAim;
        [Tooltip("When the pickup gets moved too far away from its snap position, drop it.")]
        [Min(0.1f)]
        [SerializeField] private float maxAllowedDistanceAway = 1f;
        /// <summary>
        /// <para>In <see cref="toAim"/> local space, respecting its scaling too.</para>
        /// </summary>
        private Vector3 pickupOffsetVector;
        /// <summary>
        /// <para>In <see cref="toAim"/> local space.</para>
        /// </summary>
        private Quaternion pickupOffsetRotation;

        private Quaternion aimNeutralLocalRotation;
        private Quaternion aimLookingOffsetRotation;

        private Quaternion aimToRotateOffsetRotation;

        private bool isMoving = false;

        private void Start()
        {
            pickupTransform = objSync.transform;

            aimNeutralLocalRotation = toAim.localRotation;
            aimLookingOffsetRotation = Quaternion.Inverse(GetLookingRotation()) * aimNeutralLocalRotation;

            aimToRotateOffsetRotation = Quaternion.Inverse(GetAimRotationInSameSpaceAsToRotate()) * toRotate.localRotation;

            pickupOffsetVector = toAim.InverseTransformPoint(pickupTransform.position);
            pickupOffsetRotation = Quaternion.Inverse(toAim.rotation) * pickupTransform.rotation;

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

            bool nowMoving = sync.state != SmartObjectSync.STATE_SLEEPING && sync.state != SmartObjectSync.STATE_TELEPORTING;
            if (isMoving == nowMoving)
                return;
            isMoving = nowMoving;
            if (isMoving)
                OnStartMoving();
            else
                OnStopMoving();
        }

        public override void OnChangeOwner(SmartObjectSync sync, VRCPlayerApi oldOwner, VRCPlayerApi newOwner)
        { }

        private void OnStartMoving()
        {
            nextDistanceCheckTime = Time.time + TimeBetweenDistanceChecks;
            AimAndRotate();
            updateManager.Register(this);
        }

        private void OnStopMoving()
        {
            AimAndRotate();
            SnapBack();
            updateManager.Deregister(this);
        }

        private Vector3 GetSnappedPosition() => toAim.TransformPoint(pickupOffsetVector);
        private Quaternion GetSnappedRotation() => toAim.rotation * pickupOffsetRotation;

        private void SnapBack()
        {
            // The SmartObjectSync ends up syncing whatever the current position and rotation is.
            // It even changes its state 1 frame delayed after dropping, so this is definitely safe, there's
            // no way for it to fetch and sync the wrong values.
            pickupTransform.SetPositionAndRotation(GetSnappedPosition(), GetSnappedRotation());
        }

        public void CustomUpdate()
        {
            AimAndRotate();

            float time = Time.time;
            if (time >= nextDistanceCheckTime)
            {
                // Not doing nextDistanceCheckTime += DistanceCheckLoopUpdateInterval, because the amount of
                // checks per second on average does not matter, the interval is more like the minimum time
                // passed between checks.
                nextDistanceCheckTime = time + TimeBetweenDistanceChecks;
                DropIfTooFarAway();
            }
        }

        private void DropIfTooFarAway()
        {
            float distance = Vector3.Distance(GetSnappedPosition(), pickupTransform.position);
            // Also checking IsHeld since this script is using state chang events from
            // SmartObjectSync, not the pickup and drop events from VRChat.
            // The state change events run on all clients, not just the one holding the pickup.
            // And even then the IsHeld could be false on the holding client too, if it got
            // dropped already but we have not received a state change event from SmartObjectSync yet.
            if (distance > maxAllowedDistanceAway && objSync.pickup.IsHeld)
                objSync.pickup.Drop();
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

            // The fact that I got the aim constraint to work is already pretty, how to say, fulfilling. A big achievement.
            // The rotation constraint... I'll think about it.
            // It needs to only rotate around the Y axis
            // But doing that seems really hard once the up vectors of the 2 objects don't align
            // ok so I'm thinking we need to initially calculate offsets from the object toAim to the object toRotate
            // such that in that current state, we can take the toAim rotation and convert it to the toRotate rotation
            // local rotation. to be specific.
            // but intermediate steps must be in world space, since we have no control over the hierarchy.
            // now speaking of hierarchy
            // there is the argument made for this specific use case that it should have enforced hierarchy
            // specifically having the mount be the parent of the cannon
            // and then rotate the mount around the Y axis
            // and rotate the cannon around the X axis
            // Well I say axis, up direction and right direction
            // But I don't think that's good?
            // It's more restrictive in the setup
            // The math would probably be easier though, since it can be done with projections of vectors onto planes
            // which I don't know by heart, but it's something with the cross product I think
            // actually there is Vector3.ProjectOnPlane()
            // but I'm kind of curious if I can manage to implement a full on rotation constraint. well almost full on
            // alright, cool
            // mostly
            // it does not update for late joiners
            // and I don't know if there even is any event I could possibly listen to to fix that
            // again
            // the SmartObjectSyncs are severely lacking in API design
            // ok not severely, the custom states are pretty neat
            // thing is just that a full custom state implementation is overkill for what I'm trying to do here
            // and the events for the listeners is where the API design is lacking significantly
            // but alright
            // I'm good
            // I'm reasonably content
            // next step is fixing this late joiner issue, somehow, I truly don't know how yet
            // I think we might actually get a state change to sleeping because default state is teleporting
            // which does mean that if somebody wanted an event for deserialization but they use the teleport
            // functions, they're pretty screwed
            // actually no, it doesn't compare against the previous state...
            // but wait, VRChat's field change callback only gets raised for synced variables if the value
            // actually changed
            // so never mind
            // they are screwed
            // alright anyway since I'm not teleporting I should be fine to use the state change event,
            // the first one we get, to set the current aim and rotation transforms, even though the state is sleeping.
            // and then after fixing that bug, the next step is making what I described previously, that of
            // creating a variant which does require certain hierarchy, and works through projection onto planes nearly entirely
            // which is a bit more restrictive for the map creator, but way easier to implement, and most likely
            // also computationally less expensive
            // the constraints as I've implemented them now could probably also be optimized
            // but I am not good enough at math to do that
            // I just do what makes sense in my head, what I can visualize
            // and that's that
            // if there's a better way, I don't know about it
        }
    }
}
