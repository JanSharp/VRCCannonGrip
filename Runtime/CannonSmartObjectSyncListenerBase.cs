using MMMaellon;
using UnityEngine;
using VRC.SDKBase;

namespace JanSharp
{
    public abstract class CannonSmartObjectSyncListenerBase : SmartObjectSyncListener
    {
        [HideInInspector][SerializeField][SingletonReference] private UpdateManager updateManager;
        /// <summary>
        /// <para>Used by <see cref="UpdateManager"/>.</para>
        /// </summary>
        [System.NonSerialized] public int customUpdateInternalIndex;

        [SerializeField] protected SmartObjectSync objSync;

        private bool receivedFirstStateChange = false;
        private bool isMoving = false;

        protected virtual void Start()
        {
            // Doing it here rather than making the map creator do it manually in the inspector, for convenience.
            // Editor scripting to automate it at build time would technically be possible, but when the listener
            // script gets deleted, automatically cleaning that up would not be... clean.
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
            // This is the best method I found to tell the SmartObjectSync to force finish interpolation.
            objSync.interpolationStartTime = -1_000_000f;
            objSync.Interpolate();
            UpdateConstraint();
        }

        public override void OnChangeOwner(SmartObjectSync sync, VRCPlayerApi oldOwner, VRCPlayerApi newOwner)
        { }

        private void OnStartMoving()
        {
            UpdateConstraint();
            updateManager.Register(this);
        }

        private void OnStopMoving()
        {
            UpdateConstraint();
            updateManager.Deregister(this);
        }

        public void CustomUpdate()
        {
            UpdateConstraint();
        }

        protected Quaternion GetParentRotation(Transform t)
        {
            Transform parent = t.parent;
            // To handle root game objects. Their parent's rotation is effectively and functionally identity/"none".
            return parent == null ? Quaternion.identity : parent.rotation;
        }

        protected abstract void UpdateConstraint();
    }
}
