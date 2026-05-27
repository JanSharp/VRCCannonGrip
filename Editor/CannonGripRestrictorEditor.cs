using System.Linq;
using MMMaellon;
using UdonSharpEditor;
using UnityEditor;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class CannonGripRestrictorOnBuild
    {
        static CannonGripRestrictorOnBuild()
        {
            OnBuildUtil.RegisterType<CannonGripRestrictor>(OnBuild);
        }

        public static bool OnBuild(CannonGripRestrictor cannonGrip)
        {
            Networking.SyncType syncTypeToUse;
            if (cannonGrip.TryGetComponent<SmartObjectSync>(out _))
                syncTypeToUse = Networking.SyncType.Manual;
            else if (cannonGrip.TryGetComponent<VRCObjectSync>(out _))
                syncTypeToUse = Networking.SyncType.Continuous;
            else if (cannonGrip.GetComponents<UdonBehaviour>().Length == 1)
                syncTypeToUse = Networking.SyncType.None;
            else
                return true; // There's other udon scripts on the object, leave it up to the user.

            UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(cannonGrip);
            if (backingBehaviour == null) // Not within our control, null check just in case.
                return true;
            SerializedObject so = new(backingBehaviour);
            so.FindProperty("_syncMethod").intValue = (int)syncTypeToUse;
            so.ApplyModifiedProperties();

            return true;
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(CannonGripRestrictor))]
    public class CannonGripRestrictorEditor : Editor
    {
        SerializedObject backingSo;
        SerializedProperty syncMethodProp;
        (bool mixed, Networking.SyncType value) prevSyncMethodState;

        private void OnEnable()
        {
            UpdateAllSyncTypes();

            UdonBehaviour[] backingBehaviours = targets
                .Cast<CannonGripRestrictor>()
                .Select(UdonSharpEditorUtility.GetBackingUdonBehaviour)
                .Where(b => b != null) // Not within our control, null check just in case.
                .ToArray();
            if (backingBehaviours.Length == 0)
                return;
            backingSo = new(backingBehaviours);
            syncMethodProp = backingSo.FindProperty("_syncMethod");
            prevSyncMethodState = GetSyncMethodPropState();
        }

        private void UpdateAllSyncTypes()
        {
            foreach (CannonGripRestrictor cannonGrip in targets.Cast<CannonGripRestrictor>())
                CannonGripRestrictorOnBuild.OnBuild(cannonGrip);
        }

        private (bool mixed, Networking.SyncType value) GetSyncMethodPropState()
        {
            backingSo?.Update();
            return syncMethodProp == null
                ? (false, Networking.SyncType.None)
                : (syncMethodProp.hasMultipleDifferentValues, (Networking.SyncType)syncMethodProp.intValue);
        }

        public override void OnInspectorGUI()
        {
            // This is something one would usually do inside of OnValidate defined in the MonoBehavior script itself.
            // For one I dislike putting editor scripting into runtime files.
            // Secondly and more importantly however, we don't get that event since it is not our script that
            // changes when the sync method changes, it's the UdonBehaviour that changes.
            //
            // Cannot check the value before and after calling DrawDefaultUdonSharpBehaviourHeader,
            // because the popup through which the sync method gets changed is actually its own editor window,
            // with its own OnGUI function, in which it changes the sync mode.
            // So doing it here instead, with the previous state saved in a variable rather than local.
            var syncMethodState = GetSyncMethodPropState();
            if (prevSyncMethodState != syncMethodState)
            {
                prevSyncMethodState = syncMethodState;
                UpdateAllSyncTypes();
            }

            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets))
                return;

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
